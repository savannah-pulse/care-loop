using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Routes the ClientApp uses to read and change how each simulated patient reacts to notifications.
/// </summary>
/// <remarks>
/// A scenario run sets the behaviours of its patients. These routes are for doing the same by
/// hand, and for changing the default that applies to every other patient.
/// </remarks>
public static class PatientBehaviourEndpoints
{
    /// <summary>
    /// Maps the <c>/api/patient-behaviours</c> routes.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapPatientBehaviourEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/patient-behaviours").WithTags("Patient behaviours");

        group.MapGet("/", () =>
            {
                // TODO(WS-8): return SimulatorStore.GetBehaviours().
                return NotImplementedResponse.For("GET /api/patient-behaviours");
            })
            .WithName("ListPatientBehaviours")
            .WithSummary("Lists the patients that have a behaviour of their own (SIMULATED).")
            .Produces<IReadOnlyList<PatientBehaviourAssignment>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapGet("/default", () =>
            {
                // TODO(WS-8): return SimulatorStore.GetDefaultBehaviour().
                return NotImplementedResponse.For("GET /api/patient-behaviours/default");
            })
            .WithName("GetDefaultPatientBehaviour")
            .WithSummary("Reads the behaviour used for patients that have none of their own (SIMULATED).")
            .Produces<PatientBehaviour>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapPut("/default", (PatientBehaviour behaviour) =>
            {
                // TODO(WS-8): validate (see PUT /{patientSyntheticId}), call SimulatorStore.SetDefaultBehaviour
                // and return the stored behaviour.
                return NotImplementedResponse.For("PUT /api/patient-behaviours/default");
            })
            .WithName("SetDefaultPatientBehaviour")
            .WithSummary("Changes the behaviour used for patients that have none of their own (SIMULATED).")
            .Produces<PatientBehaviour>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapPut("/{patientSyntheticId}", (string patientSyntheticId, PatientBehaviour behaviour) =>
            {
                // TODO(WS-8): reject an identifier not prefixed SYN-, Fail without a reason, a response other
                // than None when delivery is not Deliver, a negative RespondAfter and RespondToNotification
                // below 1; then call SimulatorStore.SetBehaviour and return the stored assignment.
                return NotImplementedResponse.For("PUT /api/patient-behaviours/{patientSyntheticId}");
            })
            .WithName("SetPatientBehaviour")
            .WithSummary("Sets how one patient reacts to notifications received from now on (SIMULATED).")
            .Produces<PatientBehaviourAssignment>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        group.MapDelete("/{patientSyntheticId}", (string patientSyntheticId) =>
            {
                // TODO(WS-8): call SimulatorStore.RemoveBehaviour; 204 when removed, 404 when the patient had none.
                return NotImplementedResponse.For("DELETE /api/patient-behaviours/{patientSyntheticId}");
            })
            .WithName("RemovePatientBehaviour")
            .WithSummary("Removes a patient's own behaviour so the default applies again (SIMULATED).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);

        return routes;
    }
}
