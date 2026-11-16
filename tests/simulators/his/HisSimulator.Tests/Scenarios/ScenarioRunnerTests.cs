using System.Net;
using System.Diagnostics;
using HisSimulator.Scenarios;
using HisSimulator.State;
using HisSimulator.Time;

namespace HisSimulator.Tests.Scenarios;

public sealed class ScenarioRunnerTests
{
    internal static async Task<ScenarioRun> FinishedAsync(ScenarioRunner runner, Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (runner.GetRun(id)!.Status is ScenarioRunStatus.Pending or ScenarioRunStatus.Running)
            await Task.Delay(10, timeout.Token);
        return runner.GetRun(id)!;
    }
    [Fact] public async Task Run_returns_pending_snapshot_sends_only_HIS_resources_and_finishes_with_deterministic_keys()
    {
        using var folder = new ScenarioFolder(); await using var host = await SimulatorHost.StartAsync(folder.Path);
        var initial = await host.Runner.StartAsync("test", new(0, "stable"), default);
        var final = await FinishedAsync(host.Runner, initial.RunId);
        Assert.Equal(ScenarioRunStatus.Pending, initial.Status); Assert.All(initial.Steps, s => Assert.Equal(ScenarioStepStatus.Pending, s.Status));
        Assert.Equal(ScenarioRunStatus.Completed, final.Status); Assert.Equal(2, final.Steps.Count);
        Assert.Equal(["stable/s1", "stable/s2"], final.Steps.Select(s => s.SourceEventKey));
        Assert.Equal(["DiagnosticReport", "Task"], host.Handler.Resources.Select(r => r.TypeName));
        Assert.All(final.Steps, s => Assert.Equal(ScenarioStepStatus.Sent, s.Status));
        var original = host.Handler.Resources[0].Id;
        await Task.Delay(10);
        var replay = await host.Runner.StartAsync("test", new(0, "stable"), default);
        await FinishedAsync(host.Runner, replay.RunId); Assert.Equal(original, host.Handler.Resources[2].Id);
        Assert.Null(host.Runner.GetRun(Guid.NewGuid()));
    }
    [Fact] public async Task Paced_offsets_wait_in_real_time_and_resources_use_actual_send_time()
    {
        using var folder = new ScenarioFolder(); folder.Scenario["steps"]![1]!["at"] = "PT1H"; folder.Save();
        var clock = new SystemSimulatorClock(); await using var host = await SimulatorHost.StartAsync(folder.Path, clock: clock);
        var before = clock.UtcNow; var elapsed = Stopwatch.StartNew();
        var run = await host.Runner.StartAsync("test", new(0.08, "paced"), default);
        var final = await FinishedAsync(host.Runner, run.RunId);
        Assert.True(elapsed.Elapsed.TotalSeconds >= 0.08);
        Assert.Equal(run.StartedAt.AddSeconds(0.08), final.Steps[1].ScheduledAt);
        Assert.All(host.Handler.Resources, resource => Assert.InRange(resource.Meta!.LastUpdated!.Value, before, clock.UtcNow));
        Assert.True(host.Handler.Resources[1].Meta!.LastUpdated >= run.StartedAt.AddSeconds(0.08));
    }
    [Fact] public async Task Clinical_4xx_is_recorded_as_rejected_without_turning_finished_replay_into_clinical_success()
    {
        using var folder = new ScenarioFolder(); var handler = new CaptureHandler
        { Reply = r => new(r.RequestUri!.AbsolutePath.EndsWith("/Task", StringComparison.Ordinal) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.Created) };
        await using var host = await SimulatorHost.StartAsync(folder.Path, handler);
        var run = await host.Runner.StartAsync("test", new(0, null), default); var final = await FinishedAsync(host.Runner, run.RunId);
        Assert.Equal(ScenarioRunStatus.Completed, final.Status); Assert.Equal(ScenarioStepStatus.Rejected, final.Steps[1].Status);
        Assert.Equal(422, Assert.Single(final.Steps[1].HttpStatusCodes));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Transport_or_server_failure_stops_run(bool transport)
    {
        using var folder = new ScenarioFolder(); var handler = new CaptureHandler
        { Reply = _ => transport ? throw new HttpRequestException("Test transport failure") : new(HttpStatusCode.ServiceUnavailable) };
        await using var host = await SimulatorHost.StartAsync(folder.Path, handler);
        var run = await host.Runner.StartAsync("test", new(0, null), default); var final = await FinishedAsync(host.Runner, run.RunId);
        Assert.Equal(ScenarioRunStatus.Failed, final.Status); Assert.Equal(ScenarioStepStatus.Failed, final.Steps[0].Status);
        Assert.Equal(ScenarioStepStatus.Pending, final.Steps[1].Status); Assert.Single(handler.Requests);
    }
    [Fact] public async Task An_unexpected_exception_marks_the_step_and_run_failed_with_its_message()
    {
        using var folder = new ScenarioFolder(); var handler = new CaptureHandler { Reply = _ => throw new InvalidCastException("Test unexpected failure") };
        await using var host = await SimulatorHost.StartAsync(folder.Path, handler);
        var run = await host.Runner.StartAsync("test", new(0, null), default); var final = await FinishedAsync(host.Runner, run.RunId);
        Assert.Equal(ScenarioRunStatus.Failed, final.Status); Assert.Equal(ScenarioStepStatus.Failed, final.Steps[0].Status);
        Assert.Contains("Test unexpected failure", final.Steps[0].Error); Assert.NotNull(final.CompletedAt);
    }
    [Fact] public async Task Cohort_is_refused_before_background_work()
    {
        await using var host = await SimulatorHost.StartAsync();
        await Assert.ThrowsAsync<NotSupportedException>(() => host.Runner.StartAsync("cohort", new(0, null), default));
        Assert.Empty(host.Handler.Requests);
    }
    [Fact] public async Task Concurrent_same_key_is_refused_and_shutdown_records_failure()
    {
        using var folder = new ScenarioFolder(); folder.Scenario["steps"]![0]!["at"] = "PT1H"; folder.Scenario["steps"]![1]!["at"] = "PT1H"; folder.Save();
        await using var host = await SimulatorHost.StartAsync(folder.Path);
        var run = await host.Runner.StartAsync("test", new(60, "active"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Runner.StartAsync("test", new(60, "active"), default));
        await host.Runner.StopAsync(default);
        Assert.Equal(ScenarioRunStatus.Failed, host.Runner.GetRun(run.RunId)!.Status); Assert.Empty(host.Handler.Requests);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Runner.StartAsync("test", new(0, null), default));
    }
}
