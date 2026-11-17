namespace MessagingSimulator.Inbound;

/// <summary>
/// Background loop that sends what is due: delayed patient responses and delivery reports that
/// are being tried again.
/// </summary>
/// <remarks>
/// A behaviour may say the patient responds some time after delivery, and a delivery report that
/// failed is retried later. Both wait in the store; this service asks
/// <see cref="DeliveryReportRetrier.RetryDueAsync"/> and <see cref="AutomaticResponder.SendDueAsync"/>
/// for the due ones a few times a second. It holds no state of its own, so a failure in one pass
/// cannot lose anything that is not yet due.
/// </remarks>
public sealed class ResponseDispatcher : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ResponseDispatcher> _logger;

    /// <summary>
    /// Creates the dispatcher.
    /// </summary>
    /// <param name="scopes">Creates a scope per pass, because the responder uses a typed HTTP client.</param>
    /// <param name="logger">Receives a warning when a pass fails.</param>
    public ResponseDispatcher(IServiceScopeFactory scopes, ILogger<ResponseDispatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<DeliveryReportRetrier>().RetryDueAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<AutomaticResponder>().SendDueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Sending the due patient responses failed; the next pass will carry on.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }
}
