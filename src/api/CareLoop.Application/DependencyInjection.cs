using System.Reflection;
using CareLoop.Application.Intake;
using CareLoop.Application.Stages.S03_OwnerAssigned;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CareLoop.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers intake plus every <see cref="ICaseEventHandler"/> and <see cref="IAutomaticStageStep"/> in this
    /// assembly. New stage work needs no edits here: add a class and it is discovered.
    /// </summary>
    public static IServiceCollection AddCareLoopApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OwnerAssignmentOptions>().BindConfiguration(OwnerAssignmentOptions.SectionName);
        services.AddScoped<IOwnerAssignmentPolicy, ConfiguredQueueOwnerAssignmentPolicy>();
        services.AddScoped<AutomaticStageRunner>();
        services.AddScoped<IEventIntakeService, EventIntakeService>();

        var assembly = Assembly.GetExecutingAssembly();
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            if (typeof(ICaseEventHandler).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(ICaseEventHandler), type);
            }

            if (typeof(IAutomaticStageStep).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IAutomaticStageStep), type);
            }
        }

        return services;
    }
}
