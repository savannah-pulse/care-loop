import asyncio
from typing import TypeVar

import httpx
import pytest
from pydantic import BaseModel, ValidationError

from careloop_ai_workflow.config import Settings
from careloop_ai_workflow.contracts import (
    AgentPurpose,
    ClosureReviewOutput,
    HandoffSummaryOutput,
    PatientMessageClass,
    PatientMessageOutput,
    PatientMessageRequest,
    ReplyCategory,
    ReplyTriageOutput,
    StageInsightsOutput,
    TranslationOutput,
)
from careloop_ai_workflow.main import create_app
from careloop_ai_workflow.providers.foundry import (
    DisabledStructuredOutputProvider,
    create_provider,
)
from careloop_ai_workflow.workflow import WorkflowStage

OutputModel = TypeVar("OutputModel", bound=BaseModel)


class FakeProvider:
    provider_name = "fake"
    deployment_name = "test-deployment"
    enabled = True

    def __init__(self, response: dict[str, object]) -> None:
        self.response = response

    async def generate(
        self,
        *,
        system_prompt: str,
        payload: dict[str, object],
        output_model: type[OutputModel],
    ) -> OutputModel:
        del system_prompt, payload
        return output_model.model_validate(self.response)

    def close(self) -> None:
        pass


def create_test_app(
    provider: FakeProvider | DisabledStructuredOutputProvider,
    *,
    max_input_bytes: int = 65536,
):
    settings = Settings(
        provider_mode="disabled",
        project_endpoint="",
        model_deployment="",
        azure_hosted=False,
        managed_identity_client_id="",
        prompt_shields_attested=False,
        max_input_bytes=max_input_bytes,
        timeout_seconds=1,
        prompt_version="test-prompt-v1",
    )
    return create_app(settings=settings, provider=provider)


async def post_json(app, path: str, payload: dict[str, object]) -> httpx.Response:
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=app),
        base_url="http://testserver",
    ) as client:
        return await client.post(path, json=payload)


def test_handoff_draft_returns_source_linked_summary() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "summary": "The recorded review is complete.",
                "source_fact_ids": ["fact-1"],
                "key_points": [
                    {
                        "text": "The review was recorded.",
                        "source_fact_ids": ["fact-1"],
                    }
                ],
            }
        )
    )
    payload = {
        "request_id": "request-1",
        "purpose": AgentPurpose.CLINICIAN_HANDOFF,
        "case_reference": "case-1",
        "stage": WorkflowStage.CONSULTATION_PENDING,
        "handoff_reason": "Consultation requested.",
        "facts": [
            {
                "fact_id": "fact-1",
                "value": "Review recorded",
                "source_reference": "event-1",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 200
    result = response.json()
    assert result["status"] == "draft"
    assert result["requires_human_review"] is True
    assert result["content"]["source_fact_ids"] == ["fact-1"]


def test_foundry_structured_output_schemas_avoid_unsupported_constraints() -> None:
    unsupported_keywords = {
        "minLength",
        "maxLength",
        "pattern",
        "format",
        "minimum",
        "maximum",
        "multipleOf",
        "minItems",
        "maxItems",
        "uniqueItems",
        "minProperties",
        "maxProperties",
        "patternProperties",
        "unevaluatedProperties",
        "unevaluatedItems",
        "contains",
        "minContains",
        "maxContains",
        "propertyNames",
    }
    output_models = (
        HandoffSummaryOutput,
        ClosureReviewOutput,
        PatientMessageOutput,
        TranslationOutput,
        ReplyTriageOutput,
        StageInsightsOutput,
    )

    def schema_keywords(value: object) -> set[str]:
        if isinstance(value, dict):
            return set(value).union(*(schema_keywords(item) for item in value.values()))
        if isinstance(value, list):
            return set().union(*(schema_keywords(item) for item in value))
        return set()

    for output_model in output_models:
        assert not schema_keywords(output_model.model_json_schema()) & unsupported_keywords


def test_handoff_rejects_unknown_source_fact_ids() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "summary": "A generated statement.",
                "source_fact_ids": ["invented-fact"],
                "key_points": [],
            }
        )
    )
    payload = {
        "request_id": "request-1",
        "purpose": "clinician_handoff",
        "case_reference": "case-1",
        "stage": "CONSULTATION_PENDING",
        "handoff_reason": "Consultation requested.",
        "facts": [
            {
                "fact_id": "fact-1",
                "value": "Review recorded",
                "source_reference": "event-1",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 502


def test_closure_review_preserves_api_owned_checklist_statuses() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "summary": "One item is present.",
                "evidence_notes": [
                    {
                        "evidence_reference": "evidence-1",
                        "status": "present",
                        "note": "The API records this item as present.",
                    }
                ],
            }
        )
    )
    payload = {
        "request_id": "request-2",
        "purpose": AgentPurpose.CLOSURE_REVIEW,
        "case_reference": "case-1",
        "closure_reason": "treated",
        "checklist": [
            {
                "evidence_reference": "evidence-1",
                "status": "present",
                "description": "Outcome record",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 200
    assert response.json()["content"]["evidence_notes"][0]["status"] == "present"


def test_result_bearing_sms_template_is_rejected() -> None:
    with pytest.raises(ValidationError):
        PatientMessageRequest.model_validate(
            {
                "request_id": "request-3",
                "purpose": "patient_message",
                "case_reference": "case-1",
                "stage": "PATIENT_NOTIFIED",
                "template": {
                    "template_id": "result-follow-up",
                    "version": "1",
                    "message_class": PatientMessageClass.RESULT_BEARING,
                    "channel": "sms",
                    "language": "en",
                    "physician_approved": True,
                    "segments": [{"segment_id": "text", "kind": "locked", "text": "Hello"}],
                    "slot_values": {},
                    "maximum_characters": 160,
                },
            }
        )


def test_patient_draft_preserves_locked_segments_and_slots() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "segments": [
                    {"segment_id": "greeting", "text": "Hello. "},
                    {"segment_id": "body", "text": "We are ready to help. "},
                    {"segment_id": "next", "text": "Please call the clinic."},
                ],
                "source_fact_ids": [],
            }
        )
    )
    payload = {
        "request_id": "request-4",
        "purpose": "patient_message",
        "case_reference": "case-1",
        "stage": "PATIENT_NOTIFIED",
        "template": {
            "template_id": "logistics",
            "version": "1",
            "message_class": "class_a",
            "channel": "sms",
            "language": "en",
            "physician_approved": False,
            "segments": [
                {"segment_id": "greeting", "kind": "locked", "text": "Hello. "},
                {"segment_id": "body", "kind": "adaptable", "text": "We can help."},
                {"segment_id": "next", "kind": "slot", "slot_name": "next_step"},
            ],
            "slot_values": {"next_step": "Please call the clinic."},
            "maximum_characters": 160,
        },
        "facts": [],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 200
    assert response.json()["content"] == (
        "Hello. We are ready to help. Please call the clinic."
    )


def test_translation_rejects_changed_numeric_content() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "translated_text": "الموعد في ٢٠٢٥-١١-١٣ الساعة ٠٨:٣٠",
                "source_fact_ids": [],
            }
        )
    )
    payload = {
        "request_id": "request-5",
        "purpose": "translation",
        "template_id": "appointment",
        "template_version": "1",
        "source_text_version": "text-v1",
        "source_text_approved": True,
        "approved_text": "Appointment on 2025-11-12 at 08:30",
        "source_fact_ids": [],
        "target_language": "ar-AE",
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 502


def test_translation_accepts_preserved_numeric_content_and_references() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "translated_text": "الموعد في ٢٠٢٥-١١-١٢ الساعة ٠٨:٣٠",
                "source_fact_ids": ["fact-1"],
            }
        )
    )
    payload = {
        "request_id": "request-5b",
        "purpose": AgentPurpose.TRANSLATION,
        "template_id": "appointment",
        "template_version": "1",
        "source_text_version": "text-v1",
        "source_text_approved": True,
        "approved_text": "Appointment on 2025-11-12 at 08:30",
        "source_fact_ids": ["fact-1"],
        "target_language": "ar-AE",
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 200
    assert response.json()["target_language"] == "ar-AE"
    assert response.json()["source_fact_ids"] == ["fact-1"]


def test_translation_rejects_output_without_arabic_script() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "translated_text": "The appointment is on 2025-11-12.",
                "source_fact_ids": [],
            }
        )
    )
    payload = {
        "request_id": "request-5c",
        "purpose": "translation",
        "template_id": "appointment",
        "template_version": "1",
        "source_text_version": "text-v1",
        "source_text_approved": True,
        "approved_text": "Appointment on 2025-11-12.",
        "source_fact_ids": [],
        "target_language": "ar-AE",
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 502


def test_reply_triage_keeps_deterministic_flags_and_limits_additions() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "category": ReplyCategory.LOGISTICS,
                "additional_flags": ["needs_callback"],
                "proposed_route": "nurse_queue",
            }
        )
    )
    payload = {
        "request_id": "request-6",
        "purpose": "reply_triage",
        "source_message_reference": "message-1",
        "reply_text": "Please call me.",
        "precheck_completed": True,
        "deterministic_flags": ["callback_requested"],
        "allowed_additional_flags": ["needs_callback"],
        "allowed_categories": ["logistics"],
        "allowed_routing_options": ["nurse_queue"],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 200
    assert response.json()["deterministic_flags"] == ["callback_requested"]
    assert response.json()["additional_flags"] == ["needs_callback"]
    assert response.json()["proposed_route"] == "nurse_queue"


def test_reply_triage_rejects_route_outside_api_allowlist() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "category": "logistics",
                "additional_flags": [],
                "proposed_route": "unapproved_route",
            }
        )
    )
    payload = {
        "request_id": "request-6b",
        "purpose": "reply_triage",
        "source_message_reference": "message-1",
        "reply_text": "Please call me.",
        "precheck_completed": True,
        "deterministic_flags": ["callback_requested"],
        "allowed_additional_flags": [],
        "allowed_categories": ["logistics"],
        "allowed_routing_options": ["nurse_queue"],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 502


def test_stage_insights_reject_numeric_claims_in_narrative() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "sections": [
                    {
                        "kind": "strength",
                        "title": "Strong follow-up",
                        "narrative": "The stage reached 75 percent completion.",
                        "metric_ids": ["metric-1"],
                    }
                ]
            }
        )
    )
    payload = {
        "request_id": "request-7",
        "purpose": AgentPurpose.STAGE_INSIGHTS,
        "report_reference": "report-1",
        "period_label": "current reporting period",
        "privacy_reviewed": True,
        "metrics": [
            {
                "metric_id": "metric-1",
                "stage": WorkflowStage.FOLLOW_UP_IN_PROGRESS,
                "label": "Follow-up completion",
                "definition": "Completed follow-up events divided by eligible cases.",
                "value": 0.75,
                "numerator": 75,
                "denominator": 100,
                "unit": "ratio",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/insights", payload))

    assert response.status_code == 502


def test_stage_insights_accepts_cited_non_numeric_narrative() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "sections": [
                    {
                        "kind": "strength",
                        "title": "Follow-up completion",
                        "narrative": "Recorded follow-up completion is stronger in this period.",
                        "metric_ids": ["metric-1"],
                    }
                ]
            }
        )
    )
    payload = {
        "request_id": "request-7b",
        "purpose": "stage_insights",
        "report_reference": "report-1",
        "period_label": "current reporting period",
        "privacy_reviewed": True,
        "metrics": [
            {
                "metric_id": "metric-1",
                "stage": WorkflowStage.FOLLOW_UP_IN_PROGRESS,
                "label": "Follow-up completion",
                "definition": "Completed follow-up events divided by eligible cases.",
                "value": 0.75,
                "numerator": 75,
                "denominator": 100,
                "unit": "ratio",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/insights", payload))

    assert response.status_code == 200
    assert response.json()["content"]["sections"][0]["metric_ids"] == ["metric-1"]


def test_disabled_provider_returns_unavailable_instead_of_a_fallback() -> None:
    app = create_test_app(DisabledStructuredOutputProvider())
    payload = {
        "request_id": "request-8",
        "purpose": "clinician_handoff",
        "case_reference": "case-1",
        "stage": "CONSULTATION_PENDING",
        "handoff_reason": "Consultation requested.",
        "facts": [
            {
                "fact_id": "fact-1",
                "value": "Review recorded",
                "source_reference": "event-1",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 503
    assert response.json()["detail"] == "AI drafting is disabled."


def test_foundry_provider_requires_prompt_shields_attestation() -> None:
    settings = Settings(
        provider_mode="foundry",
        project_endpoint="https://foundry.example",
        model_deployment="test-deployment",
        azure_hosted=False,
        managed_identity_client_id="",
        prompt_shields_attested=False,
        max_input_bytes=65536,
        timeout_seconds=1,
        prompt_version="test-prompt-v1",
    )

    with pytest.raises(ValueError, match="Prompt Shields"):
        create_provider(settings)


def test_oversized_ai_request_is_rejected_before_model_invocation() -> None:
    app = create_test_app(
        FakeProvider(
            {
                "summary": "Should not be returned.",
                "source_fact_ids": ["fact-1"],
                "key_points": [],
            }
        ),
        max_input_bytes=128,
    )
    payload = {
        "request_id": "request-9",
        "purpose": "clinician_handoff",
        "case_reference": "case-1",
        "stage": "CONSULTATION_PENDING",
        "handoff_reason": "x" * 1000,
        "facts": [
            {
                "fact_id": "fact-1",
                "value": "Review recorded",
                "source_reference": "event-1",
            }
        ],
    }

    response = asyncio.run(post_json(app, "/ai/v1/drafts", payload))

    assert response.status_code == 413
