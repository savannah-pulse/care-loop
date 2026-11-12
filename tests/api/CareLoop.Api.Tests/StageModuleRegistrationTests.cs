using CareLoop.Application.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace CareLoop.Api.Tests;

/// <summary>Guards the extension points many engineers will add to, so collisions fail fast in CI.</summary>
public sealed class StageModuleRegistrationTests(CareLoopApiFactory factory) : IClassFixture<CareLoopApiFactory>
{
    [Fact]
    public void Every_event_type_has_at_most_one_handler()
    {
        using var scope = factory.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<ICaseEventHandler>().ToList();

        Assert.NotEmpty(handlers);
        Assert.Equal(handlers.Count, handlers.Select(h => h.EventType).Distinct().Count());
    }

    [Fact]
    public void Every_automatic_step_targets_a_distinct_stage()
    {
        using var scope = factory.Services.CreateScope();

        // The runner throws on construction if two steps target the same stage.
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AutomaticStageRunner>());
    }
}
