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

        services.AddSingleton<IFhirInboundMapper, DiagnosticReportMapper>();
        services.AddSingleton<IFhirInboundMapper, TaskMapper>();
        services.AddSingleton<IFhirInboundMapper, ServiceRequestMapper>();
        services.AddSingleton<IFhirInboundMapper, AppointmentMapper>();
        services.AddSingleton<IFhirInboundMapper, ProcedureMapper>();
        services.AddSingleton<IFhirInboundMapper, ProvenanceMapper>();

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
    /// All three notification providers are registered so the seam exists, but only those named in
    /// <see cref="NotificationOptions.EnabledProviders"/> are used. The dispatcher only runs as a
    /// hosted service when <c>Outbox:Enabled</c> is <see langword="true"/>, because its stub would otherwise stop the host.
    /// </remarks>
    public static IServiceCollection AddOutboxAndNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<SimulatorNotificationOptions>(configuration.GetSection(SimulatorNotificationOptions.SectionName));

        services.AddScoped<IDomainEventSubscriber, FhirPublisher>();
        services.AddScoped<IDomainEventSubscriber, NotificationRouter>();

        services.AddScoped<RecipientResolver>();
        services.AddScoped<NotificationSender>();
        services.AddSingleton<TemplateRenderer>();

        services.AddHttpClient<SimulatorNotificationProvider>();
        services.AddTransient<INotificationProvider>(sp => sp.GetRequiredService<SimulatorNotificationProvider>());
        services.AddTransient<INotificationProvider, SmsNotificationProvider>();
        services.AddTransient<INotificationProvider, EmailNotificationProvider>();

        // The inbound half of a provider. SMS and email get theirs when those providers are built.
        services.AddSingleton<IInboundNotificationHandler, SimulatorInboundNotificationHandler>();

        services.AddSingleton<OutboxDispatcher>();
        if (configuration.GetValue<bool>($"{OutboxOptions.SectionName}:{nameof(OutboxOptions.Enabled)}"))
        {
            // TODO(WS-4): set Outbox:Enabled to true in appsettings.json once OutboxDispatcher is implemented.
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
    /// <remarks>The worker only runs as a hosted service when <c>DeadlineWorker:Enabled</c> is <see langword="true"/>, because its stub would otherwise stop the host.</remarks>
    public static IServiceCollection AddDeadlines(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DeadlineWorkerOptions>(configuration.GetSection(DeadlineWorkerOptions.SectionName));

        services.AddSingleton<DeadlineWorker>();
        if (configuration.GetValue<bool>($"{DeadlineWorkerOptions.SectionName}:{nameof(DeadlineWorkerOptions.Enabled)}"))
        {
            // TODO(WS-5): set DeadlineWorker:Enabled to true in appsettings.json once DeadlineWorker is implemented.
            services.AddHostedService(sp => sp.GetRequiredService<DeadlineWorker>());
        }

        return services;
    }

    /// <summary>
    /// Registers the AI provider chosen by <c>Ai:Provider</c>; the no-op provider is the default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; supplies the <c>Ai</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSummaries(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        var provider = configuration.GetValue<string>($"{AiOptions.SectionName}:{nameof(AiOptions.Provider)}");
        if (string.Equals(provider, "AzureOpenAi", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiProvider, AzureOpenAiProvider>();
        }
        else
        {
            services.AddSingleton<IAiProvider, NoOpAiProvider>();
        }

        return services;
    }
}
