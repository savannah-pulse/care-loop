export interface paths {
    "/health": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Reports that the service is running.
         * @description A liveness check only: it does not touch the database, so it stays green while a dependency is starting.
         */
        get: operations["GetHealth"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/notifications/inbound/{provider}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Receives one message from a notification provider. */
        post: operations["ReceiveInboundNotification"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Lists matching cases, updated newest first, with bounded paging. */
        get: operations["ListCases"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases/{caseId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Returns case state only after recording an authorized staff read. */
        get: operations["GetCase"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases/{caseId}/timeline": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Returns audit entries oldest received first; source timestamps are shown separately. */
        get: operations["GetCaseTimeline"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/exceptions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Returns unresolved exceptions, earliest due first, within an audited facility scope. */
        get: operations["GetExceptions"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/metrics": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Computes current counts and actual applied-event history within an audited scope.
         * @description A case whose history cannot support a measure (a source timestamp earlier than the screening result, or a
         *     treatment plan without its deadline) is left out of that measure's numerator and denominator and logged,
         *     so one inconsistent case never stops the Impact screen for every reader.
         */
        get: operations["GetMetrics"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/programmes": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Lists the configured programmes for an identified staff reader. */
        get: operations["ListProgrammes"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/programmes/{pathwayId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Returns a programme without assuming an unknown key exists. */
        get: operations["GetProgramme"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases/{caseId}/escalations/{escalationId}/acknowledgement": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Saves the acknowledgement through the transactional case write path. */
        post: operations["AcknowledgeEscalation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases/{caseId}/call-outcome": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Records the outcome of a call through the one intake pipeline. */
        post: operations["RecordCallOutcome"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/metrics/simulation": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Runs the escalation comparison on a simulated cohort. */
        post: operations["RunSimulation"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/ai/role-briefing": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetRoleBriefing"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/ai/impact-summary": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetImpactSummary"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/cases/{caseId}/patient-message-draft": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["GetPatientMessageDraft"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/fhir/metadata": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Describes what the CareLoop FHIR boundary supports.
         * @description Lists one entry per registered IFhirInboundMapper (create and update) plus read on
         *     `Task`, so the statement can never drift from what the endpoints accept.
         */
        get: operations["GetFhirCapabilityStatement"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/fhir/Task/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * Returns the current status of a case as a FHIR `Task`; also feeds the patient view.
         * @description Field mapping: handoff status to `Task.status`; stage to `Task.businessStatus`; owner to
         *     `Task.owner`; who handed over to `Task.requester`; the result or referral to `Task.focus`;
         *     patient to `Task.for`; deadline to `Task.restriction.period`; rejection or closure reason to
         *     `Task.statusReason`.
         */
        get: operations["GetFhirTask"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/fhir/{resourceType}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Accepts a new FHIR resource from an external system. */
        post: operations["CreateFhirResource"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/fhir/{resourceType}/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        /**
         * Accepts a new version of an existing FHIR resource from an external system.
         * @description An update is state, not an event: the mapper derives the transition and the resource version makes a resend idempotent.
         */
        put: operations["UpdateFhirResource"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        /** @description Text an AI model wrote for a member of staff to read: the role briefing, the impact summary or a patient message draft. */
        AiTextResponse: {
            /** @description The text as the model wrote it, trimmed. Plain text, never markup to render. */
            text: string;
            /** @description The provider and model that wrote it, for example `Foundry/gpt-6.1-sol`. */
            model: string;
            /**
             * Format: date-time
             * @description When the text was written. For a reused answer this is when it was first written, not now.
             */
            generatedAt: string;
            /** @description `true` when this is an earlier answer reused because the data has not changed; ask with `refresh=true` for a new one. */
            cached?: boolean;
        };
        /**
         * @description What a nurse records after speaking to the patient on the phone
         *     (`POST /cases/{caseId}/call-outcome`).
         */
        CallOutcomeRequest: {
            /** @description `true` when the patient said she will come: an acknowledgement. */
            acknowledged?: boolean;
            /** @description `true` when the patient said the next step back in her own words (teach-back). */
            understood?: boolean;
            /**
             * Format: int32
             * @description How confident the patient said she feels about what happens next, from 1 to 5; `null` when she was not asked or gave no rating.
             */
            confidence?: null | number | string;
        };
        /** @description The full coordination state of one case, returned by `GET /cases/{caseId}`. */
        CaseDetailResponse: {
            /**
             * Format: uuid
             * @description CareLoop's identifier of the case.
             */
            caseId: string;
            /** @description Identifier of the system that produced the result. */
            sourceSystem: string;
            /** @description The source system's identifier of the actionable result. `null` for the coordinator, who sees no one's identity. */
            sourceResultId?: null | string;
            /** @description Source-system reference of the patient. Never a name. `null` for the coordinator, who sees no one's identity. */
            patientReference?: null | string;
            /** @description Patient display name for staff; absent for a coordinator. */
            patientDisplayName?: null | string;
            /** @description Patient's second identifier for staff; absent for a coordinator. */
            patientSecondaryIdentifier?: null | string;
            /** @description The display step derived from stage and tracks by the Domain. */
            displayLabel: components["schemas"]["CaseDisplayLabel"];
            /** @description The escalation history, including acknowledgements and resolutions. */
            escalations: components["schemas"]["EscalationResponse"][];
            /** @description The latest outcome's evidence kind. */
            outcomeEvidenceKind?: null | string;
            /**
             * Format: int32
             * @description The patient's confidence rating, 1 to 5, when supplied.
             */
            confidence?: null | number | string;
            /** @description Identifier of the pathway the case follows. */
            pathwayId: string;
            /** @description The facility that produced the result. */
            facilityId: string;
            /** @description The pathway stage the case is in. */
            stage: components["schemas"]["CaseStage"];
            /** @description The clinician-reviewed urgency code; `null` until the review arrives. */
            urgency?: null | string;
            /** @description The one owner accountable for the next action. */
            owner: components["schemas"]["OwnerResponse"];
            proposedOwner?: null | components["schemas"]["OwnerResponse"];
            /** @description Key of the single next action. */
            nextAction?: null | string;
            /**
             * Format: date-time
             * @description When the next action is due.
             */
            dueAt?: null | string;
            /** @description `true` while the case has an open escalation. */
            isEscalated: boolean;
            /** @description Names of the unresolved exceptions on the case. */
            exceptions: string[];
            /** @description The state of each parallel track. */
            tracks: components["schemas"]["TrackStatusResponse"][];
            /** @description Reference to the recorded outcome evidence, when there is any. `null` for the coordinator, who sees no one's identity. */
            outcomeEvidenceReference?: null | string;
            closureReason?: null | components["schemas"]["ClosureReason"];
            /**
             * Format: date-time
             * @description When the case was opened.
             */
            createdAt: string;
            /**
             * Format: date-time
             * @description When the case last changed.
             */
            updatedAt: string;
        };
        /**
         * @description The ten labels the screen designs show for where a case is, computed from the five
         *     CaseStage values plus the track statuses (decisions 1 and 19).
         * @enum {unknown}
         */
        CaseDisplayLabel: "ResultReceived" | "CaseOpened" | "OwnerAssigned" | "PatientNotified" | "PatientAcknowledged" | "Consultation" | "FollowUp" | "OutcomeRecorded" | "ClosureReview" | "Closed";
        /**
         * @description The kinds of event that can change a case.
         * @enum {unknown}
         */
        CaseEventType: "ScreeningResultReceived" | "ClinicalReviewCompleted" | "NotificationUpdated" | "ConsultationUpdated" | "ReferralUpdated" | "AppointmentUpdated" | "OutcomeRecorded" | "ClosureDecided" | "ConsentRecorded" | "DeadlineElapsed" | "EscalationAcknowledged";
        /**
         * @description The pathway stage a case is in.
         * @enum {unknown}
         */
        CaseStage: "Received" | "ReviewPending" | "FollowUpActive" | "ClosureReview" | "Closed";
        /** @description One row in the case list returned by `GET /cases`. */
        CaseSummaryResponse: {
            /**
             * Format: uuid
             * @description CareLoop's identifier of the case.
             */
            caseId: string;
            /** @description Source-system reference of the patient. Never a name. `null` for the coordinator, who sees no one's identity. */
            patientReference?: null | string;
            /** @description Patient display name for staff; absent for a coordinator. */
            patientDisplayName?: null | string;
            /** @description Patient's second identifier for staff; absent for a coordinator. */
            patientSecondaryIdentifier?: null | string;
            /** @description The case's facility, for audited team-scoped reads. */
            facilityId: string;
            /** @description The display step derived by the Domain, never recomputed by the dashboard. */
            displayLabel: components["schemas"]["CaseDisplayLabel"];
            closureReason?: null | components["schemas"]["ClosureReason"];
            /**
             * Format: date-time
             * @description When the result opened this case.
             */
            createdAt: string;
            /** @description Identifier of the pathway the case follows. */
            pathwayId: string;
            /** @description The pathway stage the case is in. */
            stage: components["schemas"]["CaseStage"];
            /** @description The clinician-reviewed urgency code; `null` until the review arrives. */
            urgency?: null | string;
            /** @description The one owner accountable for the next action. */
            owner: components["schemas"]["OwnerResponse"];
            /** @description Key of the single next action. */
            nextAction?: null | string;
            /**
             * Format: date-time
             * @description When the next action is due.
             */
            dueAt?: null | string;
            /** @description `true` while the case has an open escalation. */
            isEscalated: boolean;
            /** @description Names of the unresolved exceptions on the case; empty when there are none. */
            exceptions: string[];
            /**
             * Format: date-time
             * @description When the case last changed.
             */
            updatedAt: string;
        };
        /** @description What a pathway requires before a case may be closed for a given reason. */
        ClosureEvidenceRule: {
            /** @description The closure reason the rule applies to. */
            reason: components["schemas"]["ClosureReason"];
            /** @description The role keys allowed to authorize closure for this reason. */
            authorizedRoles: string[];
            /** @description `true` when the reason is a documented exception rather than completed care. */
            isException: boolean;
            /** @description `true` when an outcome evidence reference must be present. */
            requiresEvidenceReference: boolean;
            /** @description `true` when a written exception note must be present. */
            requiresExceptionNote: boolean;
            /** @description `true` when a next surveillance date must be present. */
            requiresNextSurveillanceDate: boolean;
            /**
             * Format: int32
             * @description The minimum number of documented contact attempts required; zero when none are needed.
             */
            minimumDocumentedAttempts: number | string;
            /** @description The clinical or policy source the evidence requirement is taken from, or `null` while it is a placeholder with no source. */
            citation?: null | string;
        };
        /** @enum {unknown} */
        ClosureReason: "Treated" | "OnSurveillance" | "NegativeOnConfirmation" | "Declined" | "TransferredAndAccepted" | "Untraceable" | null;
        /** @description How long one kind of next action may take, and who is expected to hold the case while it is open. */
        DeadlineDefinition: {
            /** @description Key of the next action the deadline applies to, for example `acknowledge-notification`. */
            nextAction: string;
            /** @description The urgency code the deadline applies to, or `null` when it applies to every urgency. */
            urgencyCode?: null | string;
            /** @description The time allowed from when the next action is set. */
            duration: string;
            /** @description How long before the deadline the owner is warned. */
            warnBefore: string;
            /** @description `true` once a clinical adviser has approved the value; `false` marks a placeholder. */
            clinicallyApproved: boolean;
            /**
             * @description Role key of the person expected to own the case while this next action is open, for example
             *     `nurse-navigator` for acknowledging a notification, or `null` when the
             *     pathway does not say.
             */
            ownerRole?: null | string;
            /** @description The clinical or policy source the deadline is taken from, or `null` while it is a placeholder with no source. */
            citation?: null | string;
        };
        /** @description Median elapsed clock time and the number of cases that reached that step. */
        DurationMeasureResponse: {
            /**
             * Format: int32
             * @description Number of eligible cases that reached the measured step.
             */
            denominator: number | string;
            /** @description Median duration, or null when the step has not been reached. */
            median?: null | string;
        };
        /** @description An oversight escalation; acknowledgement records seeing it, not resolving it. */
        EscalationResponse: {
            /**
             * Format: uuid
             * @description The specific escalation a person may acknowledge.
             */
            escalationId: string;
            /**
             * Format: int32
             * @description The pathway ladder step.
             */
            level: number | string;
            /** @description The oversight role and facility queue addressed. */
            escalatedTo: components["schemas"]["OwnerResponse"];
            /**
             * Format: date-time
             * @description When oversight was requested.
             */
            raisedAt: string;
            /** @description The overdue action that prompted oversight. */
            reason: string;
            /** @description Who acknowledged it, when anyone has. */
            acknowledgedBy?: null | string;
            /** @description The role the acknowledger acted in. */
            acknowledgedByRole?: null | string;
            /**
             * Format: date-time
             * @description When the oversight role saw it.
             */
            acknowledgedAt?: null | string;
            /**
             * Format: date-time
             * @description When the overdue work was resolved, independently of acknowledgement.
             */
            resolvedAt?: null | string;
        };
        /** @description One step on a pathway's escalation ladder. */
        EscalationStep: {
            /**
             * Format: int32
             * @description Position on the ladder; 1 is the first step.
             */
            level: number | string;
            /** @description Role key of who is told at this step, for example the facility lead and then the coordinator. */
            escalateToRole: string;
            /** @description How long after the deadline passed this step is reached. */
            afterOverdue: string;
            /** @description `true` once a clinical adviser has approved the value; `false` marks a placeholder. */
            clinicallyApproved: boolean;
        };
        /** @description One kind of outcome evidence the pathway recognises, and what recording it means for the case. */
        EvidenceKindDefinition: {
            /** @description The code the outcome event carries, for example `treatment-plan`. Compared ignoring case. */
            kind: string;
            /** @description What the kind means, for the programme screen. */
            description?: null | string;
            /** @description `true` once a clinical adviser has approved the kind; `false` marks a placeholder. */
            clinicallyApproved: boolean;
            /**
             * @description Key of the next action to set when an outcome of this kind is recorded, or `null`
             *     to keep whatever next action the stage rules give the case.
             */
            nextAction?: null | string;
        };
        /** @description One row in the exception queue returned by `GET /exceptions`. */
        ExceptionItemResponse: {
            /**
             * Format: uuid
             * @description CareLoop's identifier of the case.
             */
            caseId: string;
            /** @description Source-system reference of the patient. Never a name. `null` for the coordinator, who sees no one's identity. */
            patientReference?: null | string;
            /** @description Patient display name for staff; absent for a coordinator. */
            patientDisplayName?: null | string;
            /** @description Patient's second identifier for staff; absent for a coordinator. */
            patientSecondaryIdentifier?: null | string;
            /** @description The case's facility. */
            facilityId: string;
            /** @description The pathway the case follows. */
            pathwayId: string;
            /** @description The display step derived by the Domain. */
            displayLabel: components["schemas"]["CaseDisplayLabel"];
            /** @description Names of the unresolved exceptions on the case. */
            exceptions: string[];
            /** @description The pathway stage the case is in. */
            stage: components["schemas"]["CaseStage"];
            /** @description The clinician-reviewed urgency code; `null` until the review arrives. */
            urgency?: null | string;
            /** @description Key of the single next action; `null` when the case has none, which should not happen on an open exception and is shown rather than hidden. */
            nextAction?: null | string;
            /** @description The owner accountable for the next action. */
            owner: components["schemas"]["OwnerResponse"];
            /**
             * Format: date-time
             * @description When the next action is due; `null` when the case has no deadline.
             */
            dueAt?: null | string;
            /** @description How long the next action is overdue; `null` when it is not overdue. */
            overdueBy?: null | string;
            /** @description `true` while the case has an open escalation. */
            isEscalated: boolean;
            /**
             * Format: int32
             * @description The highest escalation level reached; zero when never escalated.
             */
            escalationLevel: number | string;
        };
        /** @description Body of `GET /health`. */
        HealthResponse: {
            /** @description Always `ok` when the service is able to answer. */
            status: string;
            /** @description Name of the service that answered. */
            service: string;
        };
        JsonElement: unknown;
        /** @description Loop-closure metrics returned by `GET /metrics`. */
        MetricsResponse: {
            /**
             * Format: date-time
             * @description When the metrics were computed.
             */
            generatedAt: string;
            /**
             * Format: int32
             * @description Total number of cases.
             */
            totalCases: number | string;
            /**
             * Format: int32
             * @description Number of cases not yet closed.
             */
            openCases: number | string;
            /**
             * Format: int32
             * @description Number of cases closed with evidence or a documented exception.
             */
            closedCases: number | string;
            /** @description Number of open cases per stage name. */
            casesByStage: {
                [key: string]: number | string;
            };
            /** @description Number of open cases per unresolved exception name. */
            casesByException: {
                [key: string]: number | string;
            };
            /**
             * Format: int32
             * @description Number of open cases past their deadline.
             */
            overdueCases: number | string;
            /**
             * Format: int32
             * @description Number of open cases with an open escalation.
             */
            escalatedCases: number | string;
            /**
             * Format: int32
             * @description Number of open cases in the facility lead queue with no named owner.
             */
            unassignedCases: number | string;
            /**
             * Format: double
             * @description Share of notifications the patient acknowledged, from 0 to 1; `null` when none were sent.
             */
            acknowledgementRate?: null | number | string;
            /**
             * Format: double
             * @description Share of cases closed, from 0 to 1; `null` when there are no cases.
             */
            closureRate?: null | number | string;
            /** @description Median time from result received to closure; `null` when no case is closed. */
            medianTimeToClosure?: null | string;
            /** @description Rates keyed by measure: acknowledgement, closure, understanding, nextStepOnTime, referralAcceptedAndAttended, appointmentsAttended, treatmentStartedOnTime and signedEvidence. */
            measures: {
                [key: string]: components["schemas"]["RateMeasureResponse"];
            };
            /** @description Elapsed clock-time medians keyed by acknowledgement, consultation, appointment, careStarted and closure. */
            timeToStep: {
                [key: string]: components["schemas"]["DurationMeasureResponse"];
            };
            /** @description Closed cases per authorized typed closure reason. */
            closuresByReason: {
                [key: string]: number | string;
            };
            /**
             * @description Cases that reached each display label. A label counts once the event that puts a case there was applied, even when
             *     the case moved on at once, as it does from result received to owner assigned; an optional step (patient acknowledged,
             *     consultation, follow-up or closure review) counts only for cases where it happened.
             */
            casesReachingDisplayLabel: {
                [key: string]: components["schemas"]["RateMeasureResponse"];
            };
            /** @description Current cases per Domain display label. */
            casesByDisplayLabel: {
                [key: string]: number | string;
            };
            /**
             * Format: double
             * @description Share of notified cases that supplied a confidence rating; null when none were notified.
             */
            confidenceRate?: null | number | string;
            /**
             * Format: int32
             * @description Number of notified cases eligible to supply a confidence rating.
             */
            confidenceDenominator: number | string;
            /**
             * Format: int32
             * @description Number of cases supplying a rating.
             */
            confidenceResponses: number | string;
            /**
             * Format: double
             * @description Mean latest rating, 1 to 5; null when no rating was supplied.
             */
            averageConfidence?: null | number | string;
        };
        /** @description Body returned with HTTP 501 by endpoints that are scaffolded but not yet implemented. */
        NotImplementedResponse: {
            /** @description The workstream that will implement the endpoint, for example `WS6`. */
            workstream: string;
            /** @description The slice that is not implemented yet. */
            feature: string;
            /** @description A short explanation for whoever called the endpoint. */
            message: string;
        };
        /**
         * @description A channel over which CareLoop can send a notification.
         * @enum {unknown}
         */
        NotificationChannel: "Sms" | "Email";
        /** @description The person or queue accountable for a case, as shown in the dashboard. */
        OwnerResponse: {
            /** @description Source-system reference of the person, or the queue identifier. */
            reference: string;
            /** @description The role the owner holds. */
            role: string;
            /** @description The facility the owner belongs to. */
            facilityId: string;
            /** @description `true` when the owner is the facility lead queue rather than a named person. */
            isQueue: boolean;
        };
        /** @description One entry in a pathway definition's change history: who approved which version, when, and what changed. */
        PathwayChangeLogEntry: {
            /** @description The definition version the entry describes; matches string PathwayDefinition.Version for the current entry. */
            version: string;
            /**
             * Format: date
             * @description The date the version was approved or, for a placeholder, drafted.
             */
            date: string;
            /** @description Who approved the version. A placeholder entry names no clinical adviser and says it is unapproved. */
            approver: string;
            /** @description What changed in this version, in one or two sentences. */
            summary: string;
        };
        ProblemDetails: {
            type?: null | string;
            title?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            detail?: null | string;
            instance?: null | string;
        };
        /** @description The read-only, versioned pathway configuration; approvals happen outside CareLoop. */
        ProgrammeResponse: {
            /** @description The pathway key. */
            pathwayId: string;
            /** @description The current definition's version, not a historical case-version claim. */
            version: string;
            /** @description The configured programme name. */
            displayName: string;
            /** @description Whether the definition still contains clinically unapproved placeholders. */
            isPlaceholder: boolean;
            /** @description The reviewed urgency codes the pathway accepts. */
            urgencyCodes: string[];
            /** @description The role accountable while a case has no named owner. */
            unassignedQueueRole: string;
            /** @description Configured stages and permitted roles. */
            stages: components["schemas"]["StageDefinition"][];
            /** @description Tracks required for closure and their exception next actions. */
            tracks: components["schemas"]["TrackDefinition"][];
            /** @description Deadlines with owners, citations and clinical approval status. */
            deadlines: components["schemas"]["DeadlineDefinition"][];
            /** @description Oversight steps and elapsed overdue thresholds. */
            escalationLadder: components["schemas"]["EscalationStep"][];
            /** @description Authorized closure reasons and evidence requirements. */
            closureRules: components["schemas"]["ClosureEvidenceRule"][];
            /** @description Recognized outcome kinds and their next actions. */
            evidenceKinds: components["schemas"]["EvidenceKindDefinition"][];
            /** @description The configuration's documented change history. */
            changeLog: components["schemas"]["PathwayChangeLogEntry"][];
        };
        /** @description A coordination rate with its explicit population, not a clinical effectiveness claim. */
        RateMeasureResponse: {
            /**
             * Format: int32
             * @description Cases satisfying the measure.
             */
            numerator: number | string;
            /**
             * Format: int32
             * @description Cases eligible for the measure.
             */
            denominator: number | string;
            /**
             * Format: double
             * @description Numerator divided by denominator, or null when no cases are eligible.
             */
            rate?: null | number | string;
        };
        /** @description The result of replaying the cohort under one condition (with or without escalation). */
        SimulationArmResponse: {
            /** @description `true` when deadlines and escalation were active in this arm. */
            escalationEnabled: boolean;
            /**
             * Format: int32
             * @description Number of simulated cases replayed.
             */
            totalCases: number | string;
            /**
             * Format: int32
             * @description Number of cases closed with evidence or a documented exception by the end of the replay.
             */
            closedCases: number | string;
            /**
             * Format: int32
             * @description Number of cases left without action past their deadline and never picked up.
             */
            stalledCases: number | string;
            /**
             * Format: int32
             * @description Number of escalations raised.
             */
            escalationsRaised: number | string;
            /** @description Median time from result received to closure; `null` when no case closed. */
            medianTimeToClosure?: null | string;
        };
        /** @description Parameters for the with-and-without-escalation comparison started by `POST /metrics/simulation`. */
        SimulationRequest: {
            /** @description Name of the cohort scenario to replay, for example `cohort`. */
            cohortScenario: string;
            /**
             * Format: int32
             * @description Random seed overriding the scenario's own, so a run can be repeated exactly; `null` uses the scenario's seed.
             */
            seed?: null | number | string;
        };
        /** @description The with-and-without-escalation comparison returned by `POST /metrics/simulation`. */
        SimulationResponse: {
            /** @description Name of the cohort scenario that was replayed. */
            cohortScenario: string;
            /**
             * Format: int32
             * @description The seed used, so the run can be repeated.
             */
            seed: number | string;
            /** @description The arm with deadlines and escalation active. */
            withEscalation: components["schemas"]["SimulationArmResponse"];
            /** @description The arm with deadlines and escalation switched off. */
            withoutEscalation: components["schemas"]["SimulationArmResponse"];
        };
        /**
         * @description What CareLoop did with a source event.
         * @enum {unknown}
         */
        SourceEventDisposition: "Applied" | "Rejected";
        /** @description Entry and exit rules for one pathway stage. */
        StageDefinition: {
            /** @description The stage being defined. */
            stage: components["schemas"]["CaseStage"];
            /** @description The event types that move a case out of this stage. */
            leavesOn: components["schemas"]["CaseEventType"][];
            next?: null | components["schemas"]["CaseStage"];
            /** @description The role keys allowed to perform the event that leaves this stage. */
            allowedRoles: string[];
            /** @description Key of the next action set when the stage is entered; matches a string DeadlineDefinition.NextAction. */
            nextActionOnEntry?: null | string;
        };
        /** @description One audit entry in a case timeline, returned by `GET /cases/{caseId}/timeline`. */
        TimelineEntryResponse: {
            /**
             * Format: uuid
             * @description Identifier of the audit entry.
             */
            auditEventId: string;
            /** @description The source system joined from the stored event; internal events use careloop. */
            sourceSystem: string;
            /** @description The kind of event. */
            eventType: components["schemas"]["CaseEventType"];
            /** @description Whether the event was applied or refused (for example a closure attempt without evidence). */
            disposition: components["schemas"]["SourceEventDisposition"];
            /** @description The source system or actor responsible. For the coordinator, `(hidden)` where it would name the patient. */
            actorReference: string;
            /** @description The role the actor acted in. */
            actorRole: string;
            /**
             * Format: date-time
             * @description When the action happened according to the source.
             */
            sourceTimestamp: string;
            /**
             * Format: date-time
             * @description When CareLoop received it.
             */
            receivedTimestamp: string;
            priorStage?: null | components["schemas"]["CaseStage"];
            /** @description The stage after the event. */
            newStage: components["schemas"]["CaseStage"];
            /**
             * @description Why the change was made or refused. For the coordinator, any identifier of the patient or of a hospital record
             *     that the wording quotes reads `(hidden)`.
             */
            reason: string;
            /** @description Reference to the supporting evidence, when there is any. `null` for the coordinator, who sees no one's identity. */
            evidenceReference?: null | string;
            /**
             * @description Where the entry came from: `hospital` (the case's source system), `careloop` (CareLoop itself, including
             *     a call a nurse recorded), `sms` or `email` (a delivery report or the patient's reply on that channel).
             */
            origin: string;
        };
        /** @description How a pathway uses one of the parallel tracks. */
        TrackDefinition: {
            /** @description The track being configured. */
            kind: components["schemas"]["TrackKind"];
            /** @description `true` when the track must be complete before closure on a non-exception reason. */
            requiredForClosure: boolean;
            /** @description Key of the next action created when the track hits an exception (failed, rejected, missed). */
            nextActionOnException: string;
        };
        /**
         * @description Identifies one of the parallel follow-up tracks of a case.
         * @enum {unknown}
         */
        TrackKind: "Notification" | "Consultation" | "Referral" | "Appointment";
        /** @description The current state of one parallel track of a case. */
        TrackStatusResponse: {
            /** @description Which track this is. */
            kind: components["schemas"]["TrackKind"];
            /** @description The track's status name, for example `Delivered` or `Accepted`; `null` when the track has not started. */
            status?: null | string;
            /** @description `true` when the track has reached its completing status. Delivery alone does not complete notification; an accepted referral is still not completed care. */
            isComplete: boolean;
            /**
             * Format: date-time
             * @description When the track last changed; `null` when it has not started.
             */
            updatedAt?: null | string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    GetHealth: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description 200 with the service name. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["HealthResponse"];
                };
            };
        };
    };
    ReceiveInboundNotification: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description Name of the provider calling back, as used in List&lt;string&gt; NotificationOptions.EnabledProviders (case-insensitive). */
                provider: string;
            };
            cookie?: never;
        };
        /** @description Cancels the request. */
        requestBody: {
            content: {
                "application/json": unknown;
                "application/fhir+json": unknown;
                "application/x-www-form-urlencoded": unknown;
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListCases: {
        parameters: {
            query?: {
                /** @description Optional stage. */
                stage?: components["schemas"]["CaseStage"];
                /** @description Optional accountable owner. */
                ownerReference?: string;
                /** @description Optional open-escalation filter. */
                escalated?: boolean;
                /** @description Nonnegative offset; default zero. */
                skip?: number | string;
                /** @description Page size from 1 to 200; default 50. */
                take?: number | string;
                /** @description Facility filter; defaults to the staff reader's facility. */
                facilityId?: string;
                /** @description Patient search, restricted to clinician and receiving-clinician. */
                patientReference?: string;
                /** @description Reason required for a cross-facility read. */
                reason?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CaseSummaryResponse"][];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetCase: {
        parameters: {
            query?: {
                /** @description Reason required outside the caller's facility. */
                reason?: string;
            };
            header?: never;
            path: {
                /** @description The case identifier. */
                caseId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CaseDetailResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
        };
    };
    GetCaseTimeline: {
        parameters: {
            query?: {
                /** @description Reason required outside the caller's facility. */
                reason?: string;
            };
            header?: never;
            path: {
                /** @description The case identifier. */
                caseId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["TimelineEntryResponse"][];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
        };
    };
    GetExceptions: {
        parameters: {
            query?: {
                /** @description Facility filter; defaults to the staff reader's facility. */
                facilityId?: string;
                /** @description Optional accountable owner. */
                ownerReference?: string;
                /** @description Reason required outside the reader's facility. */
                reason?: string;
                /** @description Nonnegative offset; default zero. */
                skip?: number | string;
                /** @description Page size from 1 to 200; default 200. */
                take?: number | string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ExceptionItemResponse"][];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetMetrics: {
        parameters: {
            query?: {
                /** @description Optional facility; coordinators otherwise receive global aggregates. */
                facilityId?: string;
                /** @description Reason for staff reading aggregates outside their facility. */
                reason?: string;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["MetricsResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ListProgrammes: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ProgrammeResponse"][];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetProgramme: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description The programme key. */
                pathwayId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ProgrammeResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
        };
    };
    AcknowledgeEscalation: {
        parameters: {
            query?: {
                /** @description Access reason required outside the reader's facility. */
                reason?: string;
            };
            header?: never;
            path: {
                /** @description The case. */
                caseId: string;
                /** @description The exact escalation the person saw. */
                escalationId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CaseDetailResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    RecordCallOutcome: {
        parameters: {
            query?: {
                /** @description Access reason required outside the nurse's facility. */
                reason?: string;
            };
            header?: never;
            path: {
                /** @description The case. */
                caseId: string;
            };
            cookie?: never;
        };
        /** @description Cancels the request. */
        requestBody: {
            content: {
                "application/json": components["schemas"]["CallOutcomeRequest"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["CaseDetailResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    RunSimulation: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** @description Cancels the request. */
        requestBody: {
            content: {
                "application/json": components["schemas"]["SimulationRequest"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["SimulationResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Implemented */
            501: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["NotImplementedResponse"];
                };
            };
        };
    };
    GetRoleBriefing: {
        parameters: {
            query?: {
                refresh?: boolean;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AiTextResponse"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetImpactSummary: {
        parameters: {
            query?: {
                facilityId?: string;
                reason?: string;
                refresh?: boolean;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AiTextResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetPatientMessageDraft: {
        parameters: {
            query?: {
                channel?: components["schemas"]["NotificationChannel"];
                reason?: string;
            };
            header?: never;
            path: {
                caseId: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["AiTextResponse"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Conflict */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetFhirCapabilityStatement: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description 200 with a FHIR R4 `CapabilityStatement` listing the supported resource types and interactions. */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
        };
    };
    GetFhirTask: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description CareLoop's case identifier, used as the logical identifier of the case `Task`. */
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
        };
    };
    CreateFhirResource: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description The FHIR resource type in the URL, for example `DiagnosticReport`; must match the body. */
                resourceType: string;
            };
            cookie?: never;
        };
        /** @description Cancels the request. */
        requestBody: {
            content: {
                "application/fhir+json": components["schemas"]["JsonElement"];
                "application/json": components["schemas"]["JsonElement"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Created */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
        };
    };
    UpdateFhirResource: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description The FHIR resource type in the URL; must match the body. */
                resourceType: string;
                /** @description The logical identifier of the resource; must match the body. */
                id: string;
            };
            cookie?: never;
        };
        /** @description Cancels the request. */
        requestBody: {
            content: {
                "application/fhir+json": components["schemas"]["JsonElement"];
                "application/json": components["schemas"]["JsonElement"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Unprocessable Entity */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/fhir+json": components["schemas"]["JsonElement"];
                };
            };
        };
    };
}
