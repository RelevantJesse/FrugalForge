namespace WowAhPlanner.Infrastructure.Pricing;

using Microsoft.EntityFrameworkCore;
using WowAhPlanner.Core.Domain;
using WowAhPlanner.Core.Ports;
using WowAhPlanner.Infrastructure.Persistence;

public sealed class UploadedSnapshotPriceProvider(IDbContextFactory<AppDbContext> dbContextFactory) : IPriceProvider
{
    public string Name => "UploadedSnapshot";

    public async Task<PriceProviderResult> GetPricesAsync(
        RealmKey realmKey,
        IReadOnlyCollection<int> itemIds,
        PriceMode priceMode,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var key = realmKey.ToString();
        var entities = await db.ItemPriceSummaries
            .AsNoTracking()
            .Where(x => x.RealmKey == key && x.ProviderName == Name && itemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return new PriceProviderResult(
                Success: false,
                ProviderName: Name,
                SnapshotTimestampUtc: DateTime.UtcNow,
                Prices: new Dictionary<int, PriceSummary>(),
                ErrorCode: "no_snapshot",
                ErrorMessage: "No uploaded snapshot found for this realm.");
        }

        var snapshotTs = entities.Max(x => x.SnapshotTimestampUtc);
        var dict = entities.ToDictionary(
            x => x.ItemId,
            x => new PriceSummary(
                ItemId: x.ItemId,
                MinBuyoutCopper: x.MinBuyoutCopper,
                MedianCopper: x.MedianCopper,
                SnapshotTimestampUtc: x.SnapshotTimestampUtc,
                SourceProvider: Name));

        return new PriceProviderResult(
            Success: true,
            ProviderName: Name,
            SnapshotTimestampUtc: snapshotTs,
            Prices: dict,
            ErrorCode: null,
            ErrorMessage: null);
    }
}

