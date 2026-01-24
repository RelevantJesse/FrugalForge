namespace WowAhPlanner.Core.Ports;

using WowAhPlanner.Core.Domain;

public interface IItemRepository
{
    Task<IReadOnlyDictionary<int, string>> GetItemsAsync(GameVersion gameVersion, CancellationToken cancellationToken);
    Task<string?> GetItemNameAsync(GameVersion gameVersion, int itemId, CancellationToken cancellationToken);
}

