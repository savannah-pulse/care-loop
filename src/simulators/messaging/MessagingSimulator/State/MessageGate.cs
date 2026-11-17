using System.Collections.Concurrent;

namespace MessagingSimulator.State;

/// <summary>
/// Lets only one piece of work at a time decide and report something about an inbox message.
/// </summary>
/// <remarks>
/// Reporting to CareLoop is "read the message, send, then record the result", and the send is a
/// network call. Without a gate two requests about the same message can both read the same
/// state and both send: a resend arriving while the first delivery is still being reported would
/// report and respond twice, and a double-click would count two call requests where CareLoop
/// accepted one. The gate is per message, so different messages never wait for each other. It is
/// not re-entrant: never enter it for a message while already inside it for that message.
/// </remarks>
public sealed class MessageGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <summary>
    /// Waits for the message's gate and enters it.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the inbox message.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>A handle that leaves the gate when disposed.</returns>
    public async Task<IDisposable> EnterAsync(string messageId, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(messageId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Exit(gate);
    }

    private sealed class Exit(SemaphoreSlim gate) : IDisposable
    {
        private int _left;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _left, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
