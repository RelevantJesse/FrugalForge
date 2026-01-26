namespace WowAhPlanner.Tests;

using System.Text.Json;

public sealed class AnniversaryDataSanityTests
{
    [Fact]
    public void All_anniversary_recipes_have_minSkill_at_least_1()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "data", "Anniversary", "professions");

        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            var recipes = doc.RootElement.GetProperty("recipes").EnumerateArray();
            foreach (var recipe in recipes)
            {
                var recipeId = recipe.GetProperty("recipeId").GetString() ?? "(missing recipeId)";
                var minSkill = recipe.GetProperty("minSkill").GetInt32();
                Assert.True(minSkill >= 1, $"{Path.GetFileName(path)} recipeId={recipeId} has minSkill={minSkill} (expected >= 1).");
            }
        }
    }

    [Fact]
    public void Anniversary_alchemy_transmutes_are_marked_with_cooldowns()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "data", "Anniversary", "professions", "alchemy.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var transmutes = doc.RootElement
            .GetProperty("recipes")
            .EnumerateArray()
            .Where(r => (r.GetProperty("recipeId").GetString() ?? "").StartsWith("transmute-", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(transmutes);

        foreach (var recipe in transmutes)
        {
            var recipeId = recipe.GetProperty("recipeId").GetString() ?? "(missing recipeId)";
            Assert.True(recipe.TryGetProperty("cooldownSeconds", out var cd), $"alchemy recipeId={recipeId} is missing cooldownSeconds.");
            Assert.True(cd.GetInt32() > 0, $"alchemy recipeId={recipeId} has cooldownSeconds <= 0.");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WowAhPlanner.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find repo root (WowAhPlanner.slnx).");
    }
}

