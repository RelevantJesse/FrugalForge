namespace WowAhPlanner.Infrastructure.Pricing;

using Microsoft.EntityFrameworkCore;
using WowAhPlanner.Infrastructure.Persistence;

public sealed class UploadedSnapshotIngestService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    CommunityUploadOptions options)
{
    public const string ProviderName = "UploadedSnapshot";

    public async Task<IngestResult> IngestAsync(
        string realmKey,
        DateTime snapshotTimestampUtc,
        string? uploaderUserId,
        IReadOnlyCollection<UploadPriceRow> prices,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(realmKey))
        {
            throw new ArgumentException("Realm key is required.", nameof(realmKey));
        }

        if (prices.Count == 0)
        {
            return new IngestResult(UploadId: null, StoredItemCount: 0, AggregatedItemCount: 0);
        }

        var grouped = prices
            .Where(p => p.ItemId > 0 && p.MinUnitBuyoutCopper is not null && p.MinUnitBuyoutCopper.Value >= 0)
            .GroupBy(p => p.ItemId)
            .Select(g =>
            {
                var min = g.Min(x => x.MinUnitBuyoutCopper!.Value);
                var qty = g.Sum(x => x.TotalQuantity ?? 0);
                return new UploadPriceRow(g.Key, min, qty == 0 ? null : qty);
            })
            .ToArray();

        if (grouped.Length == 0)
        {
            return new IngestResult(UploadId: null, StoredItemCount: 0, AggregatedItemCount: 0);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await SqliteSchemaBootstrapper.EnsureAppSchemaAsync(db, cancellationToken);

        var uploadId = Guid.NewGuid();
        var upload = new PriceSnapshotUploadEntity
        {
            Id = uploadId,
            RealmKey = realmKey,
            UploadedAtUtc = DateTime.UtcNow,
            SnapshotTimestampUtc = snapshotTimestampUtc,
            UploaderUserId = uploaderUserId,
            ItemCount = grouped.Length,
        };

        foreach (var row in grouped)
        {
            upload.Items.Add(new PriceSnapshotItemEntity
            {
                UploadId = uploadId,
                ItemId = row.ItemId,
                MinUnitBuyoutCopper = row.MinUnitBuyoutCopper!.Value,
                TotalQuantity = row.TotalQuantity,
            });
        }

        db.PriceSnapshotUploads.Add(upload);
        await db.SaveChangesAsync(cancellationToken);

        var aggregatedCount = await RecomputeAggregatesAsync(db, realmKey, grouped.Select(x => x.ItemId).ToArray(), cancellationToken);
        return new IngestResult(UploadId: uploadId, StoredItemCount: grouped.Length, AggregatedItemCount: aggregatedCount);
    }

    private async Task<int> RecomputeAggregatesAsync(
        AppDbContext db,
        string realmKey,
        IReadOnlyCollection<int> affectedItemIds,
        CancellationToken cancellationToken)
    {
        var lastN = Math.Max(1, options.AggregateLastNUploads);

        var uploadIds = await db.PriceSnapshotUploads
            .AsNoTracking()
            .Where(x => x.RealmKey == realmKey)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Take(lastN)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (uploadIds.Count == 0)
        {
            return 0;
        }

        var uploadTsById = await db.PriceSnapshotUploads
            .AsNoTracking()
            .Where(x => uploadIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.SnapshotTimestampUtc, cancellationToken);

        var rows = await db.PriceSnapshotItems
            .AsNoTracking()
            .Where(x => uploadIds.Contains(x.UploadId) && affectedItemIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.UploadId, x.MinUnitBuyoutCopper })
            .ToListAsync(cancellationToken);

        var aggregated = rows
            .GroupBy(x => x.ItemId)
            .Select(g =>
            {
                var values = g.Select(x => x.MinUnitBuyoutCopper).ToList();
                var median = ComputeMedian(values);
                var snapshotTs = g.Max(x => uploadTsById[x.UploadId]);
                return new { ItemId = g.Key, Median = median, SnapshotTs = snapshotTs };
            })
            .ToArray();

        var updated = 0;
        foreach (var a in aggregated)
        {
            var existing = await db.ItemPriceSummaries.FindAsync([realmKey, ProviderName, a.ItemId], cancellationToken);
            if (existing is null)
            {
                db.ItemPriceSummaries.Add(new ItemPriceSummaryEntity
                {
                    RealmKey = realmKey,
                    ProviderName = ProviderName,
                    ItemId = a.ItemId,
                    MinBuyoutCopper = a.Median,
                    MedianCopper = a.Median,
                    SnapshotTimestampUtc = a.SnapshotTs,
                    CachedAtUtc = DateTime.UtcNow,
                });
            }
            else
            {
                existing.MinBuyoutCopper = a.Median;
                existing.MedianCopper = a.Median;
                existing.SnapshotTimestampUtc = a.SnapshotTs;
                existing.CachedAtUtc = DateTime.UtcNow;
            }

            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private static long ComputeMedian(List<long> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        if (values.Count % 2 == 1)
        {
            return values[mid];
        }

        return (values[mid - 1] + values[mid]) / 2;
    }

    public sealed record UploadPriceRow(int ItemId, long? MinUnitBuyoutCopper, long? TotalQuantity);

    public sealed record IngestResult(Guid? UploadId, int StoredItemCount, int AggregatedItemCount);
}
