namespace Suma.Application.Abstractions.Security;

public sealed record SecuritySettings(int Version, bool Enabled, string Algorithm, int Iterations, string? Salt, string? Hash)
{
    public static SecuritySettings Disabled { get; } = new(1, false, "PBKDF2-SHA256", 210000, null, null);
}

public interface ISecuritySettingsStore
{
    Task<SecuritySettings> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(SecuritySettings settings, CancellationToken cancellationToken = default);
}
