namespace WowAhPlanner.WinForms.Services;

internal sealed class AppPaths
{
    public string AppDataRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WowAhPlanner");

    public string StateRoot => Path.Combine(AppDataRoot, "state");

    public AppPaths()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(StateRoot);
    }
}

