using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Events;

/// <summary>Reads the latest applied screening payloads without duplicating patient data on the case projection.</summary>
internal static class ScreeningResultReader
{
    /// <summary>Loads screening payloads for a bounded set of cases in one no-tracking query.</summary>
    /// <param name="db">The source-event read context.</param>
    /// <param name="caseIds">The cases the caller is authorized to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>Latest applied payloads by case; cases without a screening event are absent.</returns>
    public static async Task<IReadOnlyDictionary<Guid, string>> ReadPayloadsAsync(
        CareLoopDbContext db, Guid[] caseIds, CancellationToken ct)
    {
        if (caseIds.Length == 0) return new Dictionary<Guid, string>();
        var stored = await db.SourceEvents.AsNoTracking()
            .Where(e => caseIds.Contains(e.CaseId)
                && e.Type == CaseEventType.ScreeningResultReceived
                && e.Disposition == SourceEventDisposition.Applied)
            .Select(e => new { e.CaseId, e.Id, e.PayloadJson, e.ReceivedAt })
            .ToListAsync(ct);
        return stored.GroupBy(e => e.CaseId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.Id).First().PayloadJson);
    }
}
