namespace PatientChannelSimulator.Tests;

/// <summary>Planned WS8 behaviours: inbound CommunicationRequest handling.</summary>
public class InboundCommunicationRequestTests
{
    [Fact(Skip = "WS8: not implemented")]
    public void CommunicationRequest_lands_in_the_patient_inbox_marked_simulated() { }

    [Fact(Skip = "WS8: not implemented")]
    public void CommunicationRequest_medium_selects_the_inbox_channel() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Message_body_is_shown_exactly_as_CareLoop_rendered_it() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Resent_CommunicationRequest_does_not_create_a_second_inbox_message() { }

    [Fact(Skip = "WS8: not implemented")]
    public void CommunicationRequest_with_non_synthetic_patient_identifier_is_rejected() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Unsupported_resource_type_returns_400_OperationOutcome() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Unparseable_body_returns_400_OperationOutcome() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Inbound_request_is_recorded_in_the_fhir_message_log() { }
}
