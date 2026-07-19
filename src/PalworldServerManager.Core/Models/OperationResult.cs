namespace PalworldServerManager.Core.Models;

public sealed record OperationResult(
    bool Success,
    string Message,
    DateTimeOffset CompletedAtUtc)
{
    public static OperationResult Ok(string message) =>
        new(true, message, DateTimeOffset.UtcNow);

    public static OperationResult Fail(string message) =>
        new(false, message, DateTimeOffset.UtcNow);
}
