"""Typed requests and responses for CareLoop's purpose-specific AI agents."""

from enum import StrEnum
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator

from careloop_ai_workflow.workflow import WorkflowStage


class StrictModel(BaseModel):
    """Base for immutable, closed API contracts."""

    model_config = ConfigDict(extra="forbid", frozen=True)


class AgentPurpose(StrEnum):
    """Supported AI-only drafting purposes."""

    CLINICIAN_HANDOFF = "clinician_handoff"
    CLOSURE_REVIEW = "closure_review"
    PATIENT_MESSAGE = "patient_message"
    TRANSLATION = "translation"
    REPLY_TRIAGE = "reply_triage"
    STAGE_INSIGHTS = "stage_insights"


class PatientMessageClass(StrEnum):
    """Approved content class supplied by the coordinating API."""

    LOGISTICS = "class_a"
    RESULT_BEARING = "class_b"


class MessageChannel(StrEnum):
    """Patient communication channel selected by the coordinating API."""

    SMS = "sms"
    EMAIL = "email"
    PORTAL = "portal"
    SIMULATOR = "simulator"


class ReplyCategory(StrEnum):
    """Non-clinical reply categories permitted by the approved triage contract."""

    ACKNOWLEDGEMENT = "acknowledgement"
    LOGISTICS = "logistics"
    CLINICAL_QUESTION = "clinical_question"
    DISTRESS = "distress"
    RED_FLAG = "red_flag"
    UNCLEAR = "unclear"


class EvidenceStatus(StrEnum):
    """Checklist status computed by the API, not the AI service."""

    PRESENT = "present"
    MISSING = "missing"
    STALE = "stale"


class InsightKind(StrEnum):
    """Descriptive grouping for an aggregate insight."""

    STRENGTH = "strength"
    GAP = "gap"
    DATA_QUALITY = "data_quality"
    COMPARISON = "comparison"
    REVIEW_QUESTION = "review_question"


class SourceFact(StrictModel):
    """A finalized, source-linked fact selected by the coordinating API."""

    fact_id: str = Field(min_length=1, max_length=128)
    value: str = Field(min_length=1, max_length=4000)
    source_reference: str = Field(min_length=1, max_length=256)
    approved_for_patient: bool = False


class CitedStatement(StrictModel):
    """A factual statement and the source facts supporting it."""

    text: str
    source_fact_ids: tuple[str, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "CitedStatement":
        if (
            not self.text.strip()
            or len(self.text) > 2000
            or not 1 <= len(self.source_fact_ids) <= 50
        ):
            raise ValueError("A cited statement exceeds its output bounds.")
        return self


class HandoffSummaryRequest(StrictModel):
    """Minimum-necessary, finalized facts for a clinician handoff draft."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.CLINICIAN_HANDOFF]
    case_reference: str = Field(min_length=1, max_length=128)
    stage: WorkflowStage
    handoff_reason: str = Field(min_length=1, max_length=1000)
    facts: tuple[SourceFact, ...] = Field(min_length=1, max_length=100)


class HandoffSummaryOutput(StrictModel):
    """Structured handoff summary with source-linked statements."""

    summary: str
    source_fact_ids: tuple[str, ...]
    key_points: tuple[CitedStatement, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "HandoffSummaryOutput":
        if (
            not self.summary.strip()
            or len(self.summary) > 4000
            or not 1 <= len(self.source_fact_ids) <= 100
            or len(self.key_points) > 30
        ):
            raise ValueError("The handoff summary exceeds its output bounds.")
        return self


class ClosureEvidence(StrictModel):
    """One API-computed closure evidence checklist item."""

    evidence_reference: str = Field(min_length=1, max_length=256)
    status: EvidenceStatus
    description: str = Field(min_length=1, max_length=1000)


class ClosureReviewRequest(StrictModel):
    """API-computed evidence checklist for a human closure reviewer."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.CLOSURE_REVIEW]
    case_reference: str = Field(min_length=1, max_length=128)
    closure_reason: str = Field(min_length=1, max_length=128)
    checklist: tuple[ClosureEvidence, ...] = Field(min_length=1, max_length=100)


class ClosureEvidenceNote(StrictModel):
    """AI explanation of a checklist item without changing its API-owned status."""

    evidence_reference: str
    status: EvidenceStatus
    note: str

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "ClosureEvidenceNote":
        if (
            not 1 <= len(self.evidence_reference) <= 256
            or not self.note.strip()
            or len(self.note) > 1000
        ):
            raise ValueError("A closure evidence note exceeds its output bounds.")
        return self


class ClosureReviewOutput(StrictModel):
    """Human-facing summary of the API's closure evidence checklist."""

    summary: str
    evidence_notes: tuple[ClosureEvidenceNote, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "ClosureReviewOutput":
        if (
            not self.summary.strip()
            or len(self.summary) > 3000
            or not 1 <= len(self.evidence_notes) <= 100
        ):
            raise ValueError("The closure review exceeds its output bounds.")
        return self


class TemplateSegment(StrictModel):
    """One locked, adaptable, or API-filled segment of an approved template."""

    segment_id: str = Field(min_length=1, max_length=128)
    kind: Literal["locked", "adaptable", "slot"]
    text: str = Field(default="", max_length=2000)
    slot_name: str | None = Field(default=None, max_length=128)

    @model_validator(mode="after")
    def validate_segment_kind(self) -> "TemplateSegment":
        if self.kind == "slot" and (not self.slot_name or self.text):
            raise ValueError("A slot segment requires a slot name and no template text.")
        if self.kind != "slot" and self.slot_name is not None:
            raise ValueError("Only slot segments may specify a slot name.")
        if self.kind != "slot" and not self.text:
            raise ValueError("Locked and adaptable segments require template text.")
        return self


class ApprovedTemplate(StrictModel):
    """Versioned template and content controls selected by the API."""

    template_id: str = Field(min_length=1, max_length=128)
    version: str = Field(min_length=1, max_length=64)
    message_class: PatientMessageClass
    channel: MessageChannel
    language: Literal["en"]
    physician_approved: bool
    segments: tuple[TemplateSegment, ...] = Field(min_length=1, max_length=50)
    slot_values: dict[str, str] = Field(max_length=50)
    maximum_characters: int = Field(gt=0, le=10000)

    @model_validator(mode="after")
    def validate_message_controls(self) -> "ApprovedTemplate":
        slot_names = [segment.slot_name for segment in self.segments if segment.kind == "slot"]
        if len(slot_names) != len(set(slot_names)):
            raise ValueError("Each template slot may appear only once.")
        if set(slot_names) != set(self.slot_values):
            raise ValueError("Template slot values must exactly match the declared slots.")
        if self.message_class is PatientMessageClass.RESULT_BEARING and not self.physician_approved:
            raise ValueError("Result-bearing content requires physician approval.")
        if (
            self.message_class is PatientMessageClass.RESULT_BEARING
            and self.channel is MessageChannel.SMS
        ):
            raise ValueError("Result-bearing content cannot be sent by SMS.")
        return self


class PatientMessageRequest(StrictModel):
    """Approved template, patient-approved facts, and source references for a draft."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.PATIENT_MESSAGE]
    case_reference: str = Field(min_length=1, max_length=128)
    stage: WorkflowStage
    template: ApprovedTemplate
    facts: tuple[SourceFact, ...] = Field(default=(), max_length=100)

    @model_validator(mode="after")
    def require_patient_approved_facts(self) -> "PatientMessageRequest":
        if any(not fact.approved_for_patient for fact in self.facts):
            raise ValueError("Only API-approved facts may be included in a patient draft.")
        return self


class DraftSegment(StrictModel):
    """Generated text for one template segment."""

    segment_id: str
    text: str

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "DraftSegment":
        if not 1 <= len(self.segment_id) <= 128 or len(self.text) > 2000:
            raise ValueError("A patient message segment exceeds its output bounds.")
        return self


class PatientMessageOutput(StrictModel):
    """Generated adaptable text and source facts for a patient message draft."""

    segments: tuple[DraftSegment, ...]
    source_fact_ids: tuple[str, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "PatientMessageOutput":
        if not 1 <= len(self.segments) <= 50 or len(self.source_fact_ids) > 100:
            raise ValueError("The patient message exceeds its output bounds.")
        return self


class TranslationRequest(StrictModel):
    """Clinician-approved English text to translate into UAE Arabic."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.TRANSLATION]
    template_id: str = Field(min_length=1, max_length=128)
    template_version: str = Field(min_length=1, max_length=64)
    source_text_version: str = Field(min_length=1, max_length=128)
    source_text_approved: Literal[True]
    approved_text: str = Field(min_length=1, max_length=10000)
    source_fact_ids: tuple[str, ...] = Field(max_length=100)
    target_language: Literal["ar-AE"]


class TranslationOutput(StrictModel):
    """Arabic translation and unchanged source fact references."""

    translated_text: str
    source_fact_ids: tuple[str, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "TranslationOutput":
        if (
            not self.translated_text.strip()
            or len(self.translated_text) > 10000
            or len(self.source_fact_ids) > 100
        ):
            raise ValueError("The translation exceeds its output bounds.")
        return self


class ReplyTriageRequest(StrictModel):
    """Patient reply plus completed deterministic checks and allowed triage choices."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.REPLY_TRIAGE]
    source_message_reference: str = Field(min_length=1, max_length=256)
    reply_text: str = Field(min_length=1, max_length=4000)
    precheck_completed: Literal[True]
    deterministic_flags: tuple[str, ...] = Field(max_length=50)
    allowed_additional_flags: tuple[str, ...] = Field(max_length=50)
    allowed_categories: tuple[ReplyCategory, ...] = Field(min_length=1)
    allowed_routing_options: tuple[str, ...] = Field(max_length=50)


class ReplyTriageOutput(StrictModel):
    """AI additions to deterministic triage; contains no patient-facing reply."""

    category: ReplyCategory
    additional_flags: tuple[str, ...]
    proposed_route: str

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "ReplyTriageOutput":
        if len(self.additional_flags) > 50 or len(self.proposed_route) > 128:
            raise ValueError("The reply triage result exceeds its output bounds.")
        return self


class MetricInput(StrictModel):
    """One precomputed, privacy-reviewed aggregate supplied by the API."""

    metric_id: str = Field(min_length=1, max_length=128)
    stage: WorkflowStage
    label: str = Field(min_length=1, max_length=256)
    definition: str = Field(min_length=1, max_length=1000)
    value: float | None
    numerator: float | None
    denominator: float | None
    unit: str = Field(min_length=1, max_length=64)
    comparison_label: str = Field(default="", max_length=128)
    comparison_value: float | None = None
    data_quality_notes: tuple[str, ...] = Field(default=(), max_length=20)

    @model_validator(mode="after")
    def validate_denominator(self) -> "MetricInput":
        if self.denominator == 0 and self.value is not None:
            raise ValueError("A metric with a zero denominator must not include a value.")
        return self


class StageInsightsRequest(StrictModel):
    """Privacy-reviewed precomputed aggregate measures for descriptive analysis."""

    request_id: str = Field(min_length=1, max_length=128)
    purpose: Literal[AgentPurpose.STAGE_INSIGHTS]
    report_reference: str = Field(min_length=1, max_length=128)
    period_label: str = Field(min_length=1, max_length=128)
    privacy_reviewed: Literal[True]
    metrics: tuple[MetricInput, ...] = Field(min_length=1, max_length=500)


class InsightSection(StrictModel):
    """Descriptive narrative tied to one or more supplied aggregate metrics."""

    kind: InsightKind
    title: str
    narrative: str
    metric_ids: tuple[str, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "InsightSection":
        if (
            not self.title.strip()
            or len(self.title) > 256
            or not self.narrative.strip()
            or len(self.narrative) > 2000
            or not 1 <= len(self.metric_ids) <= 100
        ):
            raise ValueError("An insight section exceeds its output bounds.")
        return self


class StageInsightsOutput(StrictModel):
    """Stage-level observations for human review, never a clinical recommendation."""

    sections: tuple[InsightSection, ...]

    @model_validator(mode="after")
    def validate_output_bounds(self) -> "StageInsightsOutput":
        if not 1 <= len(self.sections) <= 50:
            raise ValueError("The stage insights exceed their output bounds.")
        return self


DraftRequest = Annotated[
    HandoffSummaryRequest
    | ClosureReviewRequest
    | PatientMessageRequest
    | TranslationRequest
    | ReplyTriageRequest,
    Field(discriminator="purpose"),
]


class DraftEnvelope(StrictModel):
    """Base metadata returned with each human-reviewable draft."""

    request_id: str
    status: Literal["draft"] = "draft"
    requires_human_review: Literal[True] = True
    provider: str
    deployment: str
    prompt_version: str


class HandoffSummaryResponse(DraftEnvelope):
    """Response contract for AG1 clinician handoff summaries."""

    purpose: Literal[AgentPurpose.CLINICIAN_HANDOFF]
    content: HandoffSummaryOutput


class ClosureReviewResponse(DraftEnvelope):
    """Response contract for AG2 closure evidence summaries."""

    purpose: Literal[AgentPurpose.CLOSURE_REVIEW]
    content: ClosureReviewOutput


class PatientMessageResponse(DraftEnvelope):
    """Response contract for AG3 patient message drafts."""

    purpose: Literal[AgentPurpose.PATIENT_MESSAGE]
    template_id: str
    template_version: str
    content: str
    source_fact_ids: tuple[str, ...]


class TranslationResponse(DraftEnvelope):
    """Response contract for AG4 UAE Arabic translations."""

    purpose: Literal[AgentPurpose.TRANSLATION]
    template_id: str
    template_version: str
    source_text_version: str
    target_language: Literal["ar-AE"]
    content: str
    source_fact_ids: tuple[str, ...]


class ReplyTriageResponse(DraftEnvelope):
    """Response contract for AG5 additive reply classification."""

    purpose: Literal[AgentPurpose.REPLY_TRIAGE]
    source_message_reference: str
    category: ReplyCategory
    deterministic_flags: tuple[str, ...]
    additional_flags: tuple[str, ...]
    proposed_route: str


class StageInsightsResponse(DraftEnvelope):
    """Response contract for AG6 stage insight narratives."""

    purpose: Literal[AgentPurpose.STAGE_INSIGHTS]
    report_reference: str
    period_label: str
    content: StageInsightsOutput


DraftResponse = Annotated[
    HandoffSummaryResponse
    | ClosureReviewResponse
    | PatientMessageResponse
    | TranslationResponse
    | ReplyTriageResponse,
    Field(discriminator="purpose"),
]
