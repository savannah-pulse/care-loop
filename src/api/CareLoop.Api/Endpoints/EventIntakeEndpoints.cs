using CareLoop.Application.Intake;
using CareLoop.Application.Queries;

namespace CareLoop.Api.Endpoints;

public sealed class EventIntakeEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapPost("/api/events", IngestAsync)
            .WithName("IngestEvent")
            .WithTags("Events")
            .WithSummary("Submit a coordination event. Safe to resend: (sourceSystem, sourceEventId) is idempotent.")
            .Produces<IntakeResponse>(StatusCodes.Status201Created)
            .Produces<IntakeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> IngestAsync(
        EventEnvelope envelope, IEventIntakeService intake, CancellationToken cancellationToken)
    {
        var result = await intake.IngestAsync(envelope, cancellationToken);
        var body = new IntakeResponse(
            result.Status, result.EventId, result.CaseId,
            result.Stage is { } stage ? new StageDto((int)stage, CareLoop.Domain.Stages.StageCatalog.Get(stage).Code) : null);

        return result.Status switch
        {
            IntakeStatus.Accepted => Results.Created($"/api/cases/{result.CaseId}", body),
            IntakeStatus.Duplicate => Results.Ok(body),
            IntakeStatus.Invalid => Results.ValidationProblem(
                result.Errors!, detail: result.Message, extensions: Extensions(result)),
            _ => Results.Problem(
                statusCode: StatusCodeFor(result.Status),
                title: result.Status.ToString(),
                detail: result.Message,
                extensions: Extensions(result)),
        };
    }

    private static Dictionary<string, object?> Extensions(IntakeResult result) => new()
    {
        ["code"] = result.Code,
        ["eventId"] = result.EventId,
        ["caseId"] = result.CaseId,
    };

    private static int StatusCodeFor(IntakeStatus status) => status switch
    {
        IntakeStatus.Forbidden => StatusCodes.Status403Forbidden,
        IntakeStatus.NotFound => StatusCodes.Status404NotFound,
        IntakeStatus.UnsupportedEventType => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status409Conflict,
    };

    public sealed record IntakeResponse(IntakeStatus Status, Guid? EventId, Guid? CaseId, StageDto? Stage);
}
