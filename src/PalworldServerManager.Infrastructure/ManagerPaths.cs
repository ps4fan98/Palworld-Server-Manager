namespace PalworldServerManager.Infrastructure;

public sealed record ManagerPaths(string DataRoot)
{
    public string DatabasePath => Path.Combine(DataRoot, "manager.db");

    public string LogDirectory => Path.Combine(DataRoot, "logs");

    public string ExportDirectory => Path.Combine(DataRoot, "exports");
}
