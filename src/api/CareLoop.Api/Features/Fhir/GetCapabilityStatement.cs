using CareLoop.Domain.Time;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// Slice for <c>GET /fhir/metadata</c>.
/// </summary>
public static class GetCapabilityStatement
{
    /// <summary>
    /// Describes what the CareLoop FHIR boundary supports.
    /// </summary>
    /// <param name="mappers">The registered inbound mappers; each one is a resource type CareLoop accepts.</param>
    /// <param name="clock">Supplies the statement's date.</param>
    /// <returns>200 with a FHIR R4 <c>CapabilityStatement</c> listing the supported resource types and interactions.</returns>
    /// <remarks>
    /// Lists one entry per registered <see cref="IFhirInboundMapper"/> (create and update) plus read on
    /// <c>Task</c>, so the statement can never drift from what the endpoints accept.
    /// </remarks>
    public static IResult Handle(IEnumerable<IFhirInboundMapper> mappers, IClock clock)
    {
        var accepted = mappers.Select(m => m.ResourceType).ToHashSet(StringComparer.Ordinal);
        var types = accepted.Append("Task").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        var rest = new CapabilityStatement.RestComponent { Mode = CapabilityStatement.RestfulCapabilityMode.Server };
        foreach (var type in types)
        {
            var resource = new CapabilityStatement.ResourceComponent { TypeElement = new Code<ResourceType>(Enum.Parse<ResourceType>(type)) };
            if (accepted.Contains(type))
            {
                resource.Interaction.Add(new CapabilityStatement.ResourceInteractionComponent { Code = CapabilityStatement.TypeRestfulInteraction.Create });
                resource.Interaction.Add(new CapabilityStatement.ResourceInteractionComponent { Code = CapabilityStatement.TypeRestfulInteraction.Update });
            }

            if (type == "Task")
            {
                resource.Interaction.Add(new CapabilityStatement.ResourceInteractionComponent { Code = CapabilityStatement.TypeRestfulInteraction.Read });
            }

            rest.Resource.Add(resource);
        }

        var statement = new CapabilityStatement
        {
            Status = PublicationStatus.Active,
            Date = clock.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture),
            Kind = CapabilityStatementKind.Instance,
            Software = new CapabilityStatement.SoftwareComponent { Name = "CareLoop" },
            Implementation = new CapabilityStatement.ImplementationComponent { Description = "CareLoop FHIR boundary" },
            FhirVersion = FHIRVersion.N4_0_1,
            Format = [FhirEndpoints.FhirJson],
            Rest = [rest],
        };

        return new FhirResourceResult(StatusCodes.Status200OK, statement);
    }
}
