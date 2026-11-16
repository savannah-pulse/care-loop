using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Routes the ClientApp uses to read and change how each simulated patient reacts to notifications.
/// </summary>
/// <remarks>
/// A scenario run sets the behaviours of its patients. These routes are for doing the same by
/// hand, and for changing the default that applies to every other patient. A change applies to
/// notifications received from then on; messages already in the inbox keep what happened to them.
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

        group.MapGet("/", (SimulatorStore store) => TypedResults.Ok(store.GetBehaviours()))
            .WithName("ListPatientBehaviours")
            .WithSummary("Lists the patients that have a behaviour of their own (SIMULATED).")
            .Produces<IReadOnlyList<PatientBehaviourAssignment>>();

        group.MapGet("/default", (SimulatorStore store) => TypedResults.Ok(store.GetDefaultBehaviour()))
            .WithName("GetDefaultPatientBehaviour")
            .WithSummary("Reads the behaviour used for patients that have none of their own (SIMULATED).")
            .Produces<PatientBehaviour>();

        group.MapPut("/default", (PatientBehaviour behaviour, SimulatorStore store) =>
            {
                if (PatientBehaviourRules.Check(behaviour) is { } problem)
                {
                    return Invalid(problem);
                }

                var stored = behaviour with { Simulated = true };
                store.SetDefaultBehaviour(stored);
                return Results.Ok(stored);
            })
            .WithName("SetDefaultPatientBehaviour")
            .WithSummary("Changes the behaviour used for patients that have none of their own (SIMULATED).")
            .Produces<PatientBehaviour>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{patientSyntheticId}", (string patientSyntheticId, PatientBehaviour behaviour, SimulatorStore store) =>
            {
                if (!PatientBehaviourRules.IsSynthetic(patientSyntheticId))
                {
                    return Invalid($"The patient identifier must be synthetic (prefixed {PatientBehaviourRules.SyntheticPrefix}). The simulator only handles simulated data.");
                }

                if (PatientBehaviourRules.Check(behaviour) is { } problem)
                {
                    return Invalid(problem);
                }

                var assignment = new PatientBehaviourAssignment(patientSyntheticId, behaviour with { Simulated = true }, ScenarioName: null);
                store.SetBehaviour(assignment);
                return Results.Ok(assignment);
            })
            .WithName("SetPatientBehaviour")
            .WithSummary("Sets how one patient reacts to notifications received from now on (SIMULATED).")
            .Produces<PatientBehaviourAssignment>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{patientSyntheticId}", (string patientSyntheticId, SimulatorStore store) =>
                store.RemoveBehaviour(patientSyntheticId) ? Results.NoContent() : Results.NotFound())
            .WithName("RemovePatientBehaviour")
            .WithSummary("Removes a patient's own behaviour so the default applies again (SIMULATED).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    private static IResult Invalid(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "The patient behaviour is not allowed.");
}
