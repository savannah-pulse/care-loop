using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Recipients;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// A SQLite coordination store with the real intake pipeline, for the notification tests.
/// </summary>
internal sealed class NotificationStore : IDisposable
{
    private static readonly JsonSerializerOptions OutboxSerializerOptions = CreateOptions();

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IReadOnlyList<IEventHandler> _handlers;

    public NotificationStore()
    {
        _connection.Open();
        using (var db = NewContext())
        {
            db.Database.EnsureCreated();
        }

        IPathwayProvider pathways = DomainTestData.Provider();
        _handlers =
        [
            new ScreeningResultHandler(pathways, Clock),
            new ClinicalReviewHandler(pathways, Clock),
            new NotificationUpdateHandler(pathways, Clock),
            new ConsultationUpdateHandler(pathways, Clock),
            new ReferralUpdateHandler(pathways, Clock),
            new AppointmentUpdateHandler(pathways, Clock),
            new OutcomeHandler(pathways, Clock),
            new ClosureHandler(pathways, Clock),
            new ConsentHandler(),
        ];
    }

    public FakeClock Clock { get; } = new();

    public CareLoopDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CareLoopDbContext>().UseSqlite(_connection).Options);

    /// <summary>An ingestor writing through <paramref name="db"/>.</summary>
    public EventIngestor Ingestor(CareLoopDbContext db) =>
        new(new EfCaseRepository(db, Clock, new OutboxSubscriberRegistry(() => ["NotificationRouter"])), _handlers, Clock, NullLogger<EventIngestor>.Instance);

    public async Task<IngestResult> IngestAsync(EventEnvelope envelope)
    {
        await using var db = NewContext();
        return await Ingestor(db).IngestAsync(envelope, CancellationToken.None);
    }

    /// <summary>Opens a case and reviews it, which requests the patient notification.</summary>
    public async Task<NotificationRequested> ReviewedCaseAsync()
    {
        await IngestAsync(Envelopes.ScreeningResult(Clock.UtcNow, namedOwner: Envelopes.Clinician()));
        Clock.Advance(TimeSpan.FromHours(1));
        await IngestAsync(Envelopes.ClinicalReview(Clock.UtcNow));
        Clock.Advance(TimeSpan.FromMinutes(5));
        return Assert.Single(await RaisedAsync<NotificationRequested>());
    }

    /// <summary>Opens a case without reviewing it.</summary>
    public async Task<Case> OpenCaseAsync()
    {
        await IngestAsync(Envelopes.ScreeningResult(Clock.UtcNow, namedOwner: Envelopes.Clinician()));
        Clock.Advance(TimeSpan.FromHours(1));
        return await TheCaseAsync();
    }

    public async Task<Case> TheCaseAsync()
    {
        await using var db = NewContext();
        return await db.Cases.AsNoTracking().SingleAsync();
    }

    /// <summary>Every domain event of type <typeparamref name="T"/> in the outbox.</summary>
    public async Task<IReadOnlyList<T>> RaisedAsync<T>()
        where T : IDomainEvent
    {
        await using var db = NewContext();
        var payloads = await db.OutboxMessages.AsNoTracking().Where(m => m.EventType == typeof(T).Name).Select(m => m.PayloadJson).ToListAsync();
        return payloads.Select(p => JsonSerializer.Deserialize<T>(p, OutboxSerializerOptions)!).ToList();
    }

    public void Dispose() => _connection.Dispose();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// A notification provider that records what it was asked to send.
/// </summary>
internal sealed class FakeProvider : INotificationProvider
{
    public FakeProvider(NotificationChannel channel, bool allowsSensitiveContent = true)
    {
        Channel = channel;
        AllowsSensitiveContent = allowsSensitiveContent;
    }

    public NotificationChannel Channel { get; }

    public bool AllowsSensitiveContent { get; }

    /// <summary>Whether the provider can reach a recipient; by default when it has a contact point on <see cref="Channel"/>.</summary>
    public Func<Recipient, bool>? Reach { get; set; }

    /// <summary>The answer to every send; accepted by default.</summary>
    public Func<NotificationMessage, SendResult> Answer { get; set; } = m => new SendResult { Accepted = true, ProviderMessageId = $"fake-{m.AttemptId:N}" };

    public List<NotificationMessage> Sent { get; } = [];

    public bool CanReach(Recipient recipient) =>
        Reach?.Invoke(recipient) ?? recipient.ContactPoints.Any(c => c.Channel == Channel);

    public Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        return Task.FromResult(Answer(message));
    }
}

/// <summary>
/// Builders shared by the notification tests.
/// </summary>
internal static class NotificationTestSupport
{
    /// <summary>A renderer reading the templates that ship with the API.</summary>
    public static TemplateRenderer ShippedTemplates() => new(new TestHostEnvironment(AppContext.BaseDirectory));

    /// <summary>A renderer over a fresh folder, with <paramref name="files"/> written under <c>Infrastructure/Templates</c>.</summary>
    public static TemplateRenderer TemplatesIn(out string root, params (string RelativePath, string Text)[] files)
    {
        root = Path.Combine(Path.GetTempPath(), "careloop-templates-" + Guid.NewGuid().ToString("N"));
        foreach (var (relativePath, text) in files)
        {
            var path = Path.Combine(root, "Infrastructure", "Templates", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        Directory.CreateDirectory(Path.Combine(root, "Infrastructure", "Templates"));
        return new TemplateRenderer(new TestHostEnvironment(root));
    }

    public static NotificationSender Sender(FakeClock clock, NotificationOptions options, params INotificationProvider[] providers) =>
        new(providers, ShippedTemplates(), Options.Create(options), clock);

    public static NotificationOptions Enabled(params string[] providers) => new() { EnabledProviders = [.. providers] };

    public static Recipient Patient(params NotificationChannel[] channels) => new()
    {
        Reference = "SYN-PT-0001",
        Kind = RecipientKind.Patient,
        Language = "en",
        ContactPoints = channels.Select(c => new ContactPoint { Channel = c, Address = $"SYN-{c}-0001" }).ToList(),
    };

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "CareLoop.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
