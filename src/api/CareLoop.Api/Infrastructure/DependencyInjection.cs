using Azure.Identity;
using CareLoop.Api.Features.Ai;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Api.Infrastructure.Pathways;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Api.Infrastructure.Time;
using CareLoop.Api.Worker;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Infrastructure;

/// <summary>
/// Registers every CareLoop service, one method per workstream area so that people working in
/// parallel edit different methods.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the clock, the pathway provider, the database and the single write path.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>CareLoop</c> connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCoreAndPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<PathwayOptions>(configuration.GetSection(PathwayOptions.SectionName));
        services.AddSingleton<IPathwayProvider, JsonPathwayProvider>();

        services.AddDbContext<CareLoopDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CareLoop")));
        services.AddScoped(OutboxSubscriberRegistry.FromServices);
        services.AddScoped<ICaseRepository, EfCaseRepository>();

        return services;
    }

    /// <summary>
    /// Registers the intake pipeline and its nine event handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddIntake(this IServiceCollection services)
    {
        services.AddScoped<EventIngestor>();
        services.AddScoped<IEventHandler, ScreeningResultHandler>();
        services.AddScoped<IEventHandler, ClinicalReviewHandler>();
        services.AddScoped<IEventHandler, NotificationUpdateHandler>();
        services.AddScoped<IEventHandler, ConsultationUpdateHandler>();
        services.AddScoped<IEventHandler, ReferralUpdateHandler>();
        services.AddScoped<IEventHandler, AppointmentUpdateHandler>();
        services.AddScoped<IEventHandler, OutcomeHandler>();
        services.AddScoped<IEventHandler, ClosureHandler>();
        services.AddScoped<IEventHandler, ConsentHandler>();

        return services;
    }

    /// <summary>
    /// Registers the FHIR boundary: inbound mappers, outbound mappers and the FHIR HTTP client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the configured external systems.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFhirBoundary(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExternalSystemsOptions>(configuration.GetSection(ExternalSystemsOptions.SectionName));

        // Scoped: DiagnosticReportMapper reads the referenced Patient through the typed FhirHttpClient,
        // which must not be captured by a singleton.
        services.AddScoped<IFhirInboundMapper, DiagnosticReportMapper>();
        services.AddScoped<IFhirInboundMapper, TaskMapper>();
        services.AddScoped<IFhirInboundMapper, ServiceRequestMapper>();
        services.AddScoped<IFhirInboundMapper, AppointmentMapper>();
        services.AddScoped<IFhirInboundMapper, ProcedureMapper>();
        services.AddScoped<IFhirInboundMapper, ProvenanceMapper>();
        services.AddScoped<IFhirInboundMapper, ConsentMapper>();

        services.AddSingleton<EscalationTaskMapper>();
        services.AddSingleton<CaseTaskMapper>();

        services.AddHttpClient<FhirHttpClient>();

        return services;
    }

    /// <summary>
    /// Registers the outbox dispatcher, its two subscribers and the notification pipeline.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>Outbox</c> and <c>Notifications</c> sections.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The text-message and email providers are both registered, each with its inbound handler, but only those
    /// named in <see cref="NotificationOptions.EnabledProviders"/> are used. A provider name that is not a
    /// notification channel stops the host at start-up, rather than being ignored. The dispatcher only runs as a
    /// hosted service when <c>Outbox:Enabled</c> is <see langword="true"/>, so a test host or a tool can run without it.
    /// </remarks>
    public static IServiceCollection AddOutboxAndNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .Validate(NotificationOptions.NamesOnlyKnownProviders,
                "Notifications:EnabledProviders and Notifications:PerEventOverrides may only name Sms and Email.")
            .Validate(NotificationOptions.HasKnownTimeZone, "Notifications:TimeZone must name a time zone this host knows, such as Asia/Dubai.")
            .ValidateOnStart();
        services.AddSingleton(sp => new MessageFormat(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>().Value));
        foreach (var channel in Enum.GetValues<NotificationChannel>())
        {
            services.Configure<SimulatorNotificationOptions>(channel.ToString(), configuration.GetSection(SimulatorNotificationOptions.SectionFor(channel)));
        }

        services.Configure<StaffContactOptions>(o => configuration.GetSection(StaffContactOptions.SectionName).Bind(o.ByRole));

        services.AddScoped<IDomainEventSubscriber, FhirPublisher>();
        services.AddScoped<IDomainEventSubscriber, NotificationRouter>();

        services.AddScoped<RecipientResolver>();
        services.AddScoped<NotificationSender>();
        services.AddSingleton<TemplateRenderer>();
        services.AddSingleton<NotificationSendLog>();

        services.AddHttpClient<SmsNotificationProvider>();
        services.AddHttpClient<EmailNotificationProvider>();
        services.AddTransient<INotificationProvider>(sp => sp.GetRequiredService<SmsNotificationProvider>());
        services.AddTransient<INotificationProvider>(sp => sp.GetRequiredService<EmailNotificationProvider>());

        // The inbound half of each provider. Scoped: a handler reads the case a callback is about.
        services.AddScoped<IInboundNotificationHandler, SmsInboundNotificationHandler>();
        services.AddScoped<IInboundNotificationHandler, EmailInboundNotificationHandler>();

        services.AddSingleton<OutboxDispatcher>();
        if (configuration.GetValue<bool>($"{OutboxOptions.SectionName}:{nameof(OutboxOptions.Enabled)}"))
        {
            services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher>());
        }

        return services;
    }

    /// <summary>
    /// Registers the deadline worker.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>DeadlineWorker</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>The worker only runs as a hosted service when <c>DeadlineWorker:Enabled</c> is <see langword="true"/>. Explicit sweeps remain available to tests.</remarks>
    public static IServiceCollection AddDeadlines(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DeadlineWorkerOptions>()
            .Bind(configuration.GetSection(DeadlineWorkerOptions.SectionName))
            .Validate(o => o.Interval > TimeSpan.Zero && o.Interval.TotalMilliseconds <= uint.MaxValue - 1,
                "DeadlineWorker:Interval must be positive and supported by Task.Delay.")
            .ValidateOnStart();

        services.AddSingleton<DeadlineWorker>();
        if (configuration.GetValue<bool>($"{DeadlineWorkerOptions.SectionName}:{nameof(DeadlineWorkerOptions.Enabled)}"))
        {
            services.AddHostedService(sp => sp.GetRequiredService<DeadlineWorker>());
        }

        return services;
    }

    /// <summary>
    /// Registers the three AI texts (the role briefing, the impact summary and the patient message draft): the settings,
    /// the prompt files and the AI client <c>Ai:Provider</c> chooses.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>Ai</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>Ai:Provider</c> names no known provider, or <c>Foundry</c> is chosen without its settings.</exception>
    /// <remarks>
    /// <c>NoOp</c> is the default, so the API runs with no AI service and no credential, and the AI endpoints answer 409.
    /// To add a provider, write an <see cref="IAiClient"/> and add a case here; nothing else changes.
    /// </remarks>
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.SectionName);
        services.Configure<AiOptions>(section);
        var options = section.Get<AiOptions>() ?? new AiOptions();

        services.AddMemoryCache();
        services.AddScoped<AiText>();
        services.AddSingleton(sp => new AiPrompts(Path.IsPathRooted(options.PromptsDirectory)
            ? options.PromptsDirectory
            : Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, options.PromptsDirectory)));

        switch ((string.IsNullOrWhiteSpace(options.Provider) ? "NoOp" : options.Provider).Trim().ToUpperInvariant())
        {
            case "NOOP":
                services.AddSingleton<IAiClient, NoOpAiClient>();
                break;
            case "FOUNDRY":
                if (!Uri.TryCreate(options.Foundry.ProjectEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
                    || string.IsNullOrWhiteSpace(options.Foundry.Model))
                {
                    throw new InvalidOperationException("Ai:Provider is Foundry, which needs an https Ai:Foundry:ProjectEndpoint and an Ai:Foundry:Model.");
                }

                // DefaultAzureCredential: the managed identity in Azure, the developer's own sign-in locally.
                services.AddSingleton<Azure.Core.TokenCredential>(new DefaultAzureCredential());
                services.AddHttpClient<FoundryAiClient>();
                services.AddSingleton<IAiClient>(sp => sp.GetRequiredService<FoundryAiClient>());
                break;
            default:
                throw new InvalidOperationException($"Unsupported AI provider '{options.Provider}'. Configure NoOp or Foundry.");
        }

        return services;
    }
}
