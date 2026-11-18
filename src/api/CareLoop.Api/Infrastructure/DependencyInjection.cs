using Azure.Identity;
using CareLoop.Api.Features.Agents;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Features.Summaries;
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
using Microsoft.Extensions.Options;

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
        services.AddSingleton<HandoffDocumentReferenceMapper>();
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
    /// Registers the AI provider chosen by <c>Ai:Provider</c>, the drafting pipeline and, when it is
    /// switched on, the subscriber that drafts a summary as soon as a handoff is proposed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>Ai</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The no-op provider is the default, so the API runs with no AI service and no credential.
    /// <c>AiWorkflow</c> calls the Python service, which owns the prompt and the Foundry
    /// credentials; <c>FoundryDirect</c> calls a deployment from here instead. Both sit behind
    /// <see cref="IAiProvider"/>, so nothing outside this method knows which is in use.
    /// </remarks>
    public static IServiceCollection AddSummaries(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        var provider = configuration.GetValue<string>($"{AiOptions.SectionName}:{nameof(AiOptions.Provider)}");
        if (string.Equals(provider, "AiWorkflow", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiProvider, AiWorkflowProvider>();
        }
        else if (string.Equals(provider, "FoundryDirect", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiProvider, FoundryDirectProvider>();
        }
        else
        {
            services.AddSingleton<IAiProvider, NoOpAiProvider>();
        }

        services.AddScoped<HandoffFactCollector>();
        services.AddScoped<HandoffSummaryDrafter>();

        if (configuration.GetValue<bool>($"{AiOptions.SectionName}:{nameof(AiOptions.AutoDraftOnHandoff)}"))
        {
            // Off by default: an automatic draft spends a model call and sends facts out of CareLoop with nobody having asked.
            services.AddScoped<IDomainEventSubscriber, AutoDraftOnHandoff>();
        }

        return services;
    }

    /// <summary>
    /// Registers the client for the three Microsoft Foundry agents the dashboard asks for words (the role briefing, the
    /// impact summary and the patient message preview), on the route <c>Ai:Provider</c> chooses.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>Ai</c> and <c>Ai:Agents</c> sections.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// One set of AI settings: the route, the wait, the request-size limit and the addresses are the shared <c>Ai</c>
    /// settings <see cref="AddSummaries"/> binds, and <c>Ai:Agents</c> adds only whether the agents are on and which
    /// agent version writes each text. The agents do not draft the handoff summary and do not go through
    /// <see cref="IAiProvider"/>, but they follow the same <c>Ai:Provider</c>: <c>FoundryDirect</c> calls the Foundry
    /// project from the API (<see cref="FoundryAgentClient"/>), with a token from <c>DefaultAzureCredential</c>, the
    /// managed identity in Azure, which needs <c>Foundry User</c> (formerly <c>Azure AI User</c>) on the project;
    /// <c>AiWorkflow</c> hands the request to the Python service (<see cref="AiWorkflowAgentClient"/>, a proposed
    /// contract); <c>NoOp</c> keeps AI off (<see cref="NoOpAgentClient"/>).
    /// </para>
    /// <para>
    /// The endpoints answer 409 while <c>Ai:Agents:Enabled</c> is <see langword="false"/> or the route is <c>NoOp</c>.
    /// Once the agents are on, the settings are checked at start-up, so a missing address or a malformed agent version
    /// stops the host.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAiAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .Validate<IOptions<AiOptions>>((agents, ai) => AgentOptions.IsValid(agents, ai.Value),
                "Ai:Agents needs each agent as name:version (for example role-briefing:2), Ai:TimeoutSeconds from 1 to 300, a positive " +
                "Ai:MaxRequestBytes, and the route's address (an https Ai:FoundryDirect:ProjectEndpoint, or Ai:AiWorkflow:BaseUrl) when Enabled is true.")
            .ValidateOnStart();

        // A backstop only: each client cancels its call itself after Ai:TimeoutSeconds, token included.
        static TimeSpan Backstop(IServiceProvider sp) =>
            TimeSpan.FromSeconds(Math.Clamp(sp.GetRequiredService<IOptions<AiOptions>>().Value.TimeoutSeconds, 1, AiOptions.MaximumTimeoutSeconds) + 5);

        switch (AiOptions.RouteOf(configuration.GetValue<string>($"{AiOptions.SectionName}:{nameof(AiOptions.Provider)}")))
        {
            case AiRoute.FoundryDirect:
                services.AddSingleton(sp => new AgentAccessToken(new DefaultAzureCredential(),
                    sp.GetRequiredService<IOptions<AgentOptions>>(), sp.GetRequiredService<IHostEnvironment>(),
                    sp.GetRequiredService<IClock>(), sp.GetRequiredService<ILogger<AgentAccessToken>>()));
                services.AddHttpClient<IAgentClient, FoundryAgentClient>((sp, http) => http.Timeout = Backstop(sp));
                break;
            case AiRoute.AiWorkflow:
                services.AddHttpClient<IAgentClient, AiWorkflowAgentClient>((sp, http) => http.Timeout = Backstop(sp));
                break;
            default:
                services.AddSingleton<IAgentClient, NoOpAgentClient>();
                break;
        }

        return services;
    }
}
