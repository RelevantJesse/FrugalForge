using WowAhPlanner.Core.Domain;
using Region = WowAhPlanner.Core.Domain.Region;

namespace WowAhPlanner.WinForms.Services;

internal sealed class SelectionState(JsonFileStateStore store)
{
    private const string StorageKey = "WowAhPlanner.Selection.v1";
    private bool loaded;

    public Region Region { get; set; } = Region.US;
    public GameVersion GameVersion { get; set; } = GameVersion.Era;
    public string RealmSlug { get; set; } = "whitemane";
    public int ProfessionId { get; set; } = 185;

    public async Task EnsureLoadedAsync()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;

        var stored = await store.GetAsync<SelectionDto>(StorageKey);
        if (stored is null)
        {
            return;
        }

        Region = stored.Region;
        GameVersion = stored.GameVersion;
        RealmSlug = stored.RealmSlug ?? RealmSlug;
        if (stored.ProfessionId > 0)
        {
            ProfessionId = stored.ProfessionId;
        }
    }

    public Task PersistAsync()
        => store.SetAsync(StorageKey, new SelectionDto(Region, GameVersion, NormalizeRealmSlug(RealmSlug), ProfessionId)).AsTask();

    public RealmKey ToRealmKey() => new(Region, GameVersion, NormalizeRealmSlug(RealmSlug));

    private static string NormalizeRealmSlug(string value)
        => (value ?? "").Trim().ToLowerInvariant();

    private sealed record SelectionDto(Region Region, GameVersion GameVersion, string? RealmSlug, int ProfessionId);
}
