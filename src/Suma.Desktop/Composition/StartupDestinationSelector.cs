using Suma.Infrastructure.Runtime;

namespace Suma.Desktop.Composition;

public enum StartupDestination { Shell, Lock, Recovery }

public static class StartupDestinationSelector
{
    public static StartupDestination Select(PendingRestoreResult restore, bool requiresPin) => restore.RecoveryRequired ? StartupDestination.Recovery : requiresPin ? StartupDestination.Lock : StartupDestination.Shell;
}
