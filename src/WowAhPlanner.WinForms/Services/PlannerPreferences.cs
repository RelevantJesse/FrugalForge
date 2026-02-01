using WowAhPlanner.Core.Domain;

namespace WowAhPlanner.WinForms.Services;

internal sealed class PlannerPreferences(JsonFileStateStore store)
{
    private const string StorageKey = "WowAhPlanner.PlannerPreferences.v1";
    private bool loaded;
    private Dictionary<string, HashSet<string>> excludedRecipeIdsByKey = new(StringComparer.OrdinalIgnoreCase);

    public async Task EnsureLoadedAsync()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;

        var stored = await store.GetAsync<PreferencesDto>(StorageKey);
        if (stored is null)
        {
            return;
        }

        excludedRecipeIdsByKey = stored.ExcludedRecipeIdsByKey?
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new HashSet<string>(kvp.Value ?? [], StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> GetExcludedRecipeIds(GameVersion version, int professionId)
    {
        var key = GetKey(version, professionId);
        return excludedRecipeIdsByKey.TryGetValue(key, out var set) ? set : [];
    }

    public async Task SetRecipeExcludedAsync(GameVersion version, int professionId, string recipeId, bool excluded)
    {
        await EnsureLoadedAsync();

        var key = GetKey(version, professionId);
        if (!excludedRecipeIdsByKey.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            excludedRecipeIdsByKey[key] = set;
        }

        if (excluded)
        {
            set.Add(recipeId);
        }
        else
        {
            set.Remove(recipeId);
            if (set.Count == 0)
            {
                excludedRecipeIdsByKey.Remove(key);
            }
        }

        await PersistAsync();
    }

    private Task PersistAsync()
        => store.SetAsync(
                StorageKey,
                new PreferencesDto(
                    excludedRecipeIdsByKey.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                        StringComparer.OrdinalIgnoreCase)))
            .AsTask();

    private static string GetKey(GameVersion version, int professionId) => $"{version}:{professionId}";

    private sealed record PreferencesDto(Dictionary<string, string[]>? ExcludedRecipeIdsByKey);
}

