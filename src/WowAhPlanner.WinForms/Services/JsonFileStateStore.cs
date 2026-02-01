using System.Text.Json;

namespace WowAhPlanner.WinForms.Services;

internal sealed class JsonFileStateStore(AppPaths paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async ValueTask<T?> GetAsync<T>(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    public async ValueTask SetAsync<T>(string key, T value)
    {
        var path = GetPath(key);

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private string GetPath(string key)
    {
        var safe = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        if (safe.Length == 0)
        {
            safe = "state";
        }

        return Path.Combine(paths.StateRoot, $"{safe}.json");
    }
}

