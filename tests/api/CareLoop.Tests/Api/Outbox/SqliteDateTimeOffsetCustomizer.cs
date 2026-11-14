using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLoop.Tests.Api.Outbox;

/// <summary>
/// Stores every <see cref="DateTimeOffset"/> as a sortable number, so SQLite can compare and order them in a query.
/// </summary>
/// <remarks>
/// PostgreSQL compares <c>timestamptz</c> natively, so production needs nothing. SQLite stores
/// <see cref="DateTimeOffset"/> as text and EF Core will not translate a comparison or an <c>ORDER BY</c> on it,
/// which the dispatcher's due-delivery query needs. <see cref="DateTimeOffsetToBinaryConverter"/> orders by the UTC
/// instant. Used only by the tests that run that query.
/// </remarks>
internal sealed class SqliteDateTimeOffsetCustomizer : RelationalModelCustomizer
{
    public SqliteDateTimeOffsetCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
