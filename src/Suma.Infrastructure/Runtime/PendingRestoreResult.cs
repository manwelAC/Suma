namespace Suma.Infrastructure.Runtime;

public sealed record PendingRestoreResult(bool Attempted, bool Succeeded, bool PreviousDataRecovered, bool RollbackRetained, bool RecoveryRequired, string? UserMessage)
{
    public static PendingRestoreResult None { get; } = new(false, true, false, false, false, null);
}

public interface IPendingRestoreApplier
{
    Task<PendingRestoreResult> ApplyAsync(CancellationToken cancellationToken = default);
}
