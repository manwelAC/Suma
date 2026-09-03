using System.Security.Cryptography;
using Suma.Application.Abstractions.Security;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Security;

public sealed class PinSecurityService(ISecuritySettingsStore settingsStore)
{
    public const int Iterations = 210000;
    public const string Algorithm = "PBKDF2-SHA256";

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => (await settingsStore.ReadAsync(cancellationToken)).Enabled;

    public async Task EnableAsync(string pin, string confirmation, CancellationToken cancellationToken = default)
    {
        ValidateNewPin(pin, confirmation);
        if ((await settingsStore.ReadAsync(cancellationToken)).Enabled) throw new ApplicationValidationException("Local PIN is already enabled.");
        await settingsStore.WriteAsync(CreateCredential(pin), cancellationToken);
    }

    public async Task<bool> VerifyAsync(string pin, CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.ReadAsync(cancellationToken);
        return !settings.Enabled || Verify(pin, settings);
    }

    public async Task ChangeAsync(string currentPin, string newPin, string confirmation, CancellationToken cancellationToken = default)
    {
        var current = await settingsStore.ReadAsync(cancellationToken);
        if (!current.Enabled || !Verify(currentPin, current)) throw new ApplicationValidationException("Current PIN is incorrect.");
        ValidateNewPin(newPin, confirmation);
        await settingsStore.WriteAsync(CreateCredential(newPin), cancellationToken);
    }

    public async Task DisableAsync(string currentPin, CancellationToken cancellationToken = default)
    {
        var current = await settingsStore.ReadAsync(cancellationToken);
        if (!current.Enabled || !Verify(currentPin, current)) throw new ApplicationValidationException("Current PIN is incorrect.");
        await settingsStore.WriteAsync(SecuritySettings.Disabled, cancellationToken);
    }

    private static SecuritySettings CreateCredential(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return new(1, true, Algorithm, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    private static bool Verify(string pin, SecuritySettings settings)
    {
        if (!IsValidPin(pin) || settings is not { Version: 1, Algorithm: Algorithm, Iterations: Iterations, Salt: not null, Hash: not null }) return false;
        try
        {
            var salt = Convert.FromBase64String(settings.Salt); var expected = Convert.FromBase64String(settings.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, settings.Iterations, HashAlgorithmName.SHA256, 32);
            return expected.Length == 32 && salt.Length == 16 && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    private static void ValidateNewPin(string pin, string confirmation)
    {
        if (!IsValidPin(pin)) throw new ApplicationValidationException("PIN must contain 4 to 6 numeric digits.");
        if (!string.Equals(pin, confirmation, StringComparison.Ordinal)) throw new ApplicationValidationException("PIN confirmation must match.");
    }

    private static bool IsValidPin(string? pin) => pin is { Length: >= 4 and <= 6 } && pin.All(character => character is >= '0' and <= '9');
}
