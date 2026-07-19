namespace PalworldServerManager.Core.Options;

public sealed class PalworldServerOptions
{
    public const string SectionName = "Palworld";

    public string InstallDirectory { get; set; } = @"C:\Servers\Palworld";

    public string ExecutableRelativePath { get; set; } = "PalServer.exe";

    public string StartupArguments { get; set; } = "-useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS";

    public string[] ProcessNames { get; set; } =
    [
        "PalServer",
        "PalServer-Win64-Test-Cmd"
    ];

    public int MaximumBufferedLogLines { get; set; } = 2_000;
}
