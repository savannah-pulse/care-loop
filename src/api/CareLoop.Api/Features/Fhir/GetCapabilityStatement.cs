using CareLoop.Api.Features.Common;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// Slice for <c>GET /fhir/metadata</c>.
/// </summary>
public static class GetCapabilityStatement
{
    /// <summary>
    /// Describes what the CareLoop FHIR boundary supports.
    /// </summary>
    /// <returns>200 with a FHIR R4 <c>CapabilityStatement</c> listing the supported resource types and interactions.</returns>
    /// <remarks>
    /// Will list one entry per registered <see cref="IFhirInboundMapper"/> (create and update) plus read on
    /// <c>Task</c>.
    /// </remarks>
    public static IResult Handle()
    {
        // TODO(WS-3): build the CapabilityStatement from the registered inbound mappers.
        return NotImplementedResults.For("WS3", "GetCapabilityStatement");
    }
}
