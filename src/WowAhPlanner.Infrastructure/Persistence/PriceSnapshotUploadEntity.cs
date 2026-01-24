namespace WowAhPlanner.Infrastructure.Persistence;

public sealed class PriceSnapshotUploadEntity
{
    public required Guid Id { get; init; }
    public required string RealmKey { get; init; }
    public required DateTime UploadedAtUtc { get; init; }
    public required DateTime SnapshotTimestampUtc { get; init; }
    public string? UploaderUserId { get; init; }
    public required int ItemCount { get; init; }

    public List<PriceSnapshotItemEntity> Items { get; } = [];
}

