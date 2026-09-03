using System.Text.Json;
using Suma.Application.Abstractions.Security;
using Suma.Infrastructure.Runtime;

namespace Suma.Infrastructure.Security;

public sealed class JsonSecuritySettingsStore(SumaRuntimePaths paths) : ISecuritySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<SecuritySettings> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SecurityPath)) return SecuritySettings.Disabled;
        await using var stream = new FileStream(paths.SecurityPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<SecuritySettings>(stream, JsonOptions, cancellationToken) ?? SecuritySettings.Disabled;
    }

    public async Task WriteAsync(SecuritySettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ApplicationDirectory);
        var temporaryPath = paths.SecurityPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken); stream.Flush(true);
            }
            if (File.Exists(paths.SecurityPath)) File.Replace(temporaryPath, paths.SecurityPath, null); else File.Move(temporaryPath, paths.SecurityPath);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }
}
