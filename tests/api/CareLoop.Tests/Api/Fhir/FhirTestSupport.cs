using System.Net;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Loads the sample resources and builds the FHIR client the mappers and the publisher need.
/// </summary>
internal static class FhirTestSupport
{
    /// <summary>The source system identifier the tests send as.</summary>
    public const string SourceSystemId = "SYN-HIS";

    private static string SamplesDirectory => Path.Combine(AppContext.BaseDirectory, "Api", "Fhir", "Samples");

    /// <summary>Reads a sample file as JSON text.</summary>
    public static string SampleJson(string name) => File.ReadAllText(Path.Combine(SamplesDirectory, name + ".json"));

    /// <summary>Parses a sample file into its resource type.</summary>
    public static T Sample<T>(string name)
        where T : Resource => (T)FhirJsonCodec.Deserialize(SampleJson(name));

    /// <summary>Runs a mapper synchronously.</summary>
    public static IReadOnlyList<EventEnvelope> Map(IFhirInboundMapper mapper, Resource resource) =>
        mapper.MapAsync(resource, SourceSystemId, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Adds the CareLoop role and facility extensions to a reference.</summary>
    public static ResourceReference WithRole(this ResourceReference reference, string role, string facility = "SYN-FAC-A")
    {
        reference.SetExtension(StatusMap.ActorRoleExtension, new Code(role));
        reference.SetExtension(StatusMap.ActorFacilityExtension, new FhirString(facility));
        return reference;
    }

    /// <summary>A FHIR client with no external system configured.</summary>
    public static FhirHttpClient UnconfiguredClient() => Client(new RecordingHandler(), configured: false);

    /// <summary>A FHIR client whose <see cref="SourceSystemId"/> points at the given handler.</summary>
    public static FhirHttpClient Client(RecordingHandler handler, bool configured = true)
    {
        var options = new ExternalSystemsOptions();
        if (configured)
        {
            options.Systems[SourceSystemId] = new ExternalSystemEndpoint { BaseUrl = "http://his.invalid" };
        }

        return new FhirHttpClient(new HttpClient(handler), Options.Create(options));
    }

    /// <summary>
    /// Records every request and answers with a fixed response.
    /// </summary>
    public sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        /// <summary>Creates a handler that answers 200 with an empty body, or with <paramref name="respond"/>.</summary>
        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
        {
            _respond = respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK));
        }

        /// <summary>Each request: method, URI and body.</summary>
        public List<(HttpMethod Method, Uri Uri, string Body, string? ContentType)> Requests { get; } = [];

        /// <summary>The resources sent with PUT, parsed back.</summary>
        public IEnumerable<Resource> Sent =>
            Requests.Where(r => r.Method == HttpMethod.Put).Select(r => FhirJsonCodec.Deserialize(r.Body));

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!, body, request.Content?.Headers.ContentType?.MediaType));
            return _respond(request);
        }
    }
}
