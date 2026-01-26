namespace WowAhPlanner.Infrastructure.Pricing;

using Microsoft.EntityFrameworkCore;
using WowAhPlanner.Core.Domain;
using WowAhPlanner.Infrastructure.Persistence;

public sealed class PriceBrowseService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public sealed record CachedPriceRow(
        int ItemId,
        string ProviderName,
        long MinBuyoutCopper,
        long? MedianCopper,
        DateTime SnapshotTimestampUtc,
        DateTime CachedAtUtc);

    public async Task<IReadOnlyList<string>> GetProvidersAsync(RealmKey realmKey, CancellationToken cancellationToken)
    {
        var key = realmKey.ToString();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.ItemPriceSummaries
            .AsNoTracking()
            .Where(x => x.RealmKey == key)
            .Select(x => x.ProviderName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CachedPriceRow>> GetCachedPricesAsync(
        RealmKey realmKey,
        string? providerName,
        CancellationToken cancellationToken)
    {
        var key = realmKey.ToString();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.ItemPriceSummaries
            .AsNoTracking()
            .Where(x => x.RealmKey == key);

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            query = query.Where(x => x.ProviderName == providerName);
        }

        return await query
            .OrderBy(x => x.ItemId)
            .Select(x => new CachedPriceRow(
                x.ItemId,
                x.ProviderName,
                x.MinBuyoutCopper,
                x.MedianCopper,
                x.SnapshotTimestampUtc,
                x.CachedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

