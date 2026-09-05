using Microsoft.Extensions.DependencyInjection;
using Suma.Application.DataManagement;
using Suma.Application.Security;

namespace Suma.Desktop.Operations.Settings;

public sealed class SettingsOperations(IServiceScopeFactory scopeFactory) : ISettingsOperations
{
    public Task<bool> IsPinEnabledAsync(CancellationToken cancellationToken = default) => Security(service => service.IsEnabledAsync(cancellationToken));
    public Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default) => Security(service => service.VerifyAsync(pin, cancellationToken));
    public Task EnablePinAsync(string pin, string confirmation, CancellationToken cancellationToken = default) => Security(service => service.EnableAsync(pin, confirmation, cancellationToken));
    public Task ChangePinAsync(string currentPin, string newPin, string confirmation, CancellationToken cancellationToken = default) => Security(service => service.ChangeAsync(currentPin, newPin, confirmation, cancellationToken));
    public Task DisablePinAsync(string currentPin, CancellationToken cancellationToken = default) => Security(service => service.DisableAsync(currentPin, cancellationToken));
    public Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default) => Backup(service => service.CreateAsync(destinationPath, cancellationToken));
    public Task<string> ValidateAndStageRestoreAsync(string sourcePath, CancellationToken cancellationToken = default) => Backup(service => service.ValidateAndStageAsync(sourcePath, cancellationToken));
    public Task ConfirmRestoreAsync(string stagedPath, CancellationToken cancellationToken = default) => Backup(service => service.MarkPendingAsync(stagedPath, cancellationToken));
    public Task DiscardStagedRestoreAsync(string stagedPath, CancellationToken cancellationToken = default) => Backup(service => service.DiscardStagedAsync(stagedPath, cancellationToken));
    public Task ResetAllDataAsync(CancellationToken cancellationToken = default) => Backup(service => service.ResetDataAsync(cancellationToken));
    private async Task Security(Func<PinSecurityService, Task> action) { await using var scope = scopeFactory.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<PinSecurityService>()); }
    private async Task<T> Security<T>(Func<PinSecurityService, Task<T>> action) { await using var scope = scopeFactory.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<PinSecurityService>()); }
    private async Task Backup(Func<FinanceBackupService, Task> action) { await using var scope = scopeFactory.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<FinanceBackupService>()); }
    private async Task<T> Backup<T>(Func<FinanceBackupService, Task<T>> action) { await using var scope = scopeFactory.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<FinanceBackupService>()); }
}
