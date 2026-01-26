namespace WowAhPlanner.Tests;

using System.Text.Json;

public sealed class AnniversaryAlchemyDataTests
{
    [Fact]
    public void Alchemy_does_not_mark_high_level_recipes_as_minSkill_0()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "data", "Anniversary", "professions", "alchemy.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var recipes = doc.RootElement.GetProperty("recipes").EnumerateArray().ToArray();

        Assert.DoesNotContain(recipes, r => r.GetProperty("minSkill").GetInt32() == 0);

        var cauldron = FindRecipe(recipes, "cauldron-of-major-arcane-protection");
        Assert.True(cauldron.GetProperty("minSkill").GetInt32() >= 300);
    }

    private static JsonElement FindRecipe(JsonElement[] recipes, string recipeId)
    {
        foreach (var r in recipes)
        {
            if (string.Equals(r.GetProperty("recipeId").GetString(), recipeId, StringComparison.OrdinalIgnoreCase))
            {
                return r;
            }
        }

        throw new InvalidOperationException($"Recipe not found: {recipeId}");
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

