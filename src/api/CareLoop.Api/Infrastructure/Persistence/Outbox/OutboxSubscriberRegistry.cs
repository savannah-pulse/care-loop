using CareLoop.Api.Features.Outbox;

namespace CareLoop.Api.Infrastructure.Persistence.Outbox;

/// <summary>
/// Names the subscribers every outbox message must be delivered to.
/// </summary>
/// <remarks>
/// The repository writes one <see cref="OutboxDelivery"/> row per subscriber in the same transaction
/// as the case change, so it has to know the subscribers' names at save time. The names are
/// resolved lazily, on first use, because the subscribers themselves carry heavy dependencies
/// (FHIR client, notification sender) that the write path must not construct or depend on.
/// </remarks>
public sealed class OutboxSubscriberRegistry
{
    private readonly Lazy<IReadOnlyList<string>> _names;

    /// <summary>
    /// Creates a registry that resolves the names on first use.
    /// </summary>
    /// <param name="resolveNames">Supplies the subscriber names; called once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolveNames"/> is <see langword="null"/>.</exception>
    public OutboxSubscriberRegistry(Func<IEnumerable<string>> resolveNames)
    {
        ArgumentNullException.ThrowIfNull(resolveNames);

        _names = new Lazy<IReadOnlyList<string>>(() =>
            resolveNames().Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Creates the registry used in the application: the names of every registered <see cref="IDomainEventSubscriber"/>.
    /// </summary>
    /// <param name="services">The scoped service provider the subscribers are resolved from.</param>
    /// <returns>The registry.</returns>
    public static OutboxSubscriberRegistry FromServices(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new OutboxSubscriberRegistry(() => services.GetServices<IDomainEventSubscriber>().Select(s => s.Name));
    }

    /// <summary>The stable names of every subscriber, each stored on its own delivery row.</summary>
    public IReadOnlyList<string> SubscriberNames => _names.Value;
}
