using System.Text.Json;

namespace WowAhPlanner.WinForms.Services;

internal sealed class OwnedBreakdownService(JsonFileStateStore store)
{
    public async Task SaveAsync(string realmKey, IReadOnlyDictionary<int, IReadOnlyList<(string CharacterName, long Qty)>> byItemId)
    {
        var dto = byItemId.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value.Select(x => new OwnedByCharacterDto(x.CharacterName, x.Qty)).ToArray(),
            StringComparer.Ordinal);

        await store.SetAsync(GetKey(realmKey), dto);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<(string CharacterName, long Qty)>>> LoadAsync(string realmKey)
    {
        var stored = await store.GetAsync<Dictionary<string, OwnedByCharacterDto[]>>(GetKey(realmKey));
        if (stored is null || stored.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<(string, long)>>();
        }

        var result = new Dictionary<int, IReadOnlyList<(string CharacterName, long Qty)>>();
        foreach (var (itemIdText, list) in stored)
        {
            if (!int.TryParse(itemIdText, out var itemId) || itemId <= 0)
            {
                continue;
            }

            var mapped = (list ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x.CharacterName) && x.Qty > 0)
                .Select(x => (x.CharacterName, x.Qty))
                .ToArray();

            if (mapped.Length > 0)
            {
                result[itemId] = mapped;
            }
        }

        return result;
    }

    private static string GetKey(string realmKey) => $"WowAhPlanner.OwnedBreakdown.{realmKey}.v1";

    private sealed record OwnedByCharacterDto(string CharacterName, long Qty);
}

