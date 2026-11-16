using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CareLoop.Api.Infrastructure;

/// <summary>
/// Normalizes line endings in the generated OpenAPI document to <c>\n</c>.
/// </summary>
/// <remarks>
/// Descriptions come from XML documentation comments, which carry the line endings of the checked-out
/// source files. Without normalization the committed <c>contracts/openapi/careloop.json</c> differs
/// between a Windows checkout (CRLF) and a Linux build (LF), so the contract-drift check in CI fails
/// although nothing in the contract changed.
/// </remarks>
internal sealed class OpenApiLineEndingTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance);

        foreach (var schema in document.Components?.Schemas?.Values ?? [])
        {
            Normalize(schema, visited);
        }

        foreach (var path in document.Paths.Values)
        {
            if (path.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Operations.Values)
            {
                operation.Summary = Normalize(operation.Summary);
                operation.Description = Normalize(operation.Description);

                foreach (var parameter in operation.Parameters ?? [])
                {
                    if (parameter is OpenApiParameter concrete)
                    {
                        concrete.Description = Normalize(concrete.Description);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void Normalize(IOpenApiSchema? schema, HashSet<IOpenApiSchema> visited)
    {
        if (schema is not OpenApiSchema concrete || !visited.Add(concrete))
        {
            return;
        }

        concrete.Description = Normalize(concrete.Description);

        foreach (var property in concrete.Properties?.Values ?? [])
        {
            Normalize(property, visited);
        }

        Normalize(concrete.Items, visited);

        foreach (var member in concrete.AllOf ?? [])
        {
            Normalize(member, visited);
        }

        foreach (var member in concrete.AnyOf ?? [])
        {
            Normalize(member, visited);
        }

        foreach (var member in concrete.OneOf ?? [])
        {
            Normalize(member, visited);
        }
    }

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n");
}
