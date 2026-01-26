namespace WowAhPlanner.Tests;

using System.Text.Json;

public sealed class AnniversaryEnchantingDataTests
{
    [Fact]
    public void Enchanting_does_not_mark_tbc_recipes_as_minSkill_0()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "data", "Anniversary", "professions", "enchanting.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var recipes = doc.RootElement.GetProperty("recipes").EnumerateArray().ToArray();

        Assert.DoesNotContain(recipes, r => r.GetProperty("minSkill").GetInt32() == 0);

        AssertMinSkill(recipes, "runed-eternium-rod", expectedMinSkill: 350);

        AssertAlwaysGrayAtMinSkill(recipes, "nexus-transformation", expectedMinSkill: 300);
        AssertAlwaysGrayAtMinSkill(recipes, "small-prismatic-shard", expectedMinSkill: 300);
        AssertAlwaysGrayAtMinSkill(recipes, "large-prismatic-shard", expectedMinSkill: 300);
        AssertAlwaysGrayAtMinSkill(recipes, "void-shatter", expectedMinSkill: 375);
    }

    private static void AssertMinSkill(JsonElement[] recipes, string recipeId, int expectedMinSkill)
    {
        var r = FindRecipe(recipes, recipeId);
        Assert.Equal(expectedMinSkill, r.GetProperty("minSkill").GetInt32());
    }

    private static void AssertAlwaysGrayAtMinSkill(JsonElement[] recipes, string recipeId, int expectedMinSkill)
    {
        var r = FindRecipe(recipes, recipeId);
        var minSkill = r.GetProperty("minSkill").GetInt32();
        Assert.Equal(expectedMinSkill, minSkill);

        var orangeUntil = r.GetProperty("orangeUntil").GetInt32();
        var yellowUntil = r.GetProperty("yellowUntil").GetInt32();
        var greenUntil = r.GetProperty("greenUntil").GetInt32();
        var grayAt = r.GetProperty("grayAt").GetInt32();

        Assert.Equal(minSkill - 1, orangeUntil);
        Assert.Equal(minSkill - 1, yellowUntil);
        Assert.Equal(minSkill - 1, greenUntil);
        Assert.Equal(minSkill, grayAt);
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

