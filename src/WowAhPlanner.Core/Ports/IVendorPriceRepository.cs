namespace WowAhPlanner.Core.Ports;

using WowAhPlanner.Core.Domain;

public interface IVendorPriceRepository
{
    Task<IReadOnlyDictionary<int, long>> GetVendorPricesAsync(GameVersion gameVersion, CancellationToken cancellationToken);
    Task<long?> GetVendorPriceCopperAsync(GameVersion gameVersion, int itemId, CancellationToken cancellationToken);
}

