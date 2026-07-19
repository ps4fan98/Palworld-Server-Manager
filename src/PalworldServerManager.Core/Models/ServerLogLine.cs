namespace PalworldServerManager.Core.Models;

public sealed record ServerLogLine(
    DateTimeOffset TimestampUtc,
    string Stream,
    string Message);
