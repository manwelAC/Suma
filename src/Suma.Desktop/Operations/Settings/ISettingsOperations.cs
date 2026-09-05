namespace Suma.Desktop.Operations.Settings;

public interface ISettingsOperations
{
    Task<bool> IsPinEnabledAsync(CancellationToken cancellationToken = default);
    Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default);
    Task EnablePinAsync(string pin, string confirmation, CancellationToken cancellationToken = default);
    Task ChangePinAsync(string currentPin, string newPin, string confirmation, CancellationToken cancellationToken = default);
    Task DisablePinAsync(string currentPin, CancellationToken cancellationToken = default);
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
    Task<string> ValidateAndStageRestoreAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task ConfirmRestoreAsync(string stagedPath, CancellationToken cancellationToken = default);
    Task DiscardStagedRestoreAsync(string stagedPath, CancellationToken cancellationToken = default);
    Task ResetAllDataAsync(CancellationToken cancellationToken = default);
}
