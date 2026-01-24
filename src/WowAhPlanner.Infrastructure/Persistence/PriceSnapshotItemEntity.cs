namespace WowAhPlanner.Infrastructure.Persistence;

public sealed class PriceSnapshotItemEntity
{
    public required Guid UploadId { get; init; }
    public required int ItemId { get; init; }
    public required long MinUnitBuyoutCopper { get; init; }
    public long? TotalQuantity { get; init; }

    public PriceSnapshotUploadEntity? Upload { get; init; }
}

