using System.Text.Json;

namespace Suma.Infrastructure.Runtime;

internal enum RestorePhase { RollbackAuthoritative, CandidateApplied, NoPreviousDatabaseApplying }
internal sealed record RestoreTransactionState(int Version, RestorePhase Phase);

internal sealed class RestoreStateStore(SumaRuntimePaths paths)
{
    public RestoreTransactionState? Read()
    {
        if (!File.Exists(paths.RestoreStatePath)) return null;
        using var stream = new FileStream(paths.RestoreStatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var state = JsonSerializer.Deserialize<RestoreTransactionState>(stream) ?? throw new InvalidDataException("Restore state is invalid.");
        if (state.Version != 1) throw new InvalidDataException($"Unsupported restore state version '{state.Version}'.");
        if (!Enum.IsDefined(state.Phase)) throw new InvalidDataException($"Undefined restore phase '{(int)state.Phase}'.");
        return state;
    }

    public void Write(RestorePhase phase)
    {
        Directory.CreateDirectory(paths.RestoreDirectory); var temporary = paths.RestoreStatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) { JsonSerializer.Serialize(stream, new RestoreTransactionState(1, phase)); stream.Flush(true); }
            if (File.Exists(paths.RestoreStatePath)) File.Replace(temporary, paths.RestoreStatePath, null); else File.Move(temporary, paths.RestoreStatePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Delete() { if (File.Exists(paths.RestoreStatePath)) File.Delete(paths.RestoreStatePath); }
}
