using CommunityToolkit.Mvvm.ComponentModel;
using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Settings;

namespace Suma.Desktop.ViewModels;

public sealed class SettingsViewModel(ISettingsOperations operations) : ObservableObject
{
    private bool pinEnabled; private bool securityBusy; private bool dataBusy; private bool restartRequired; private string? error; private string? success;
    public bool IsPinEnabled { get => pinEnabled; private set { if (SetProperty(ref pinEnabled, value)) Notify(); } }
    public bool IsSecurityBusy { get => securityBusy; private set { if (SetProperty(ref securityBusy, value)) Notify(); } }
    public bool IsDataBusy { get => dataBusy; private set { if (SetProperty(ref dataBusy, value)) Notify(); } }
    public bool IsRestartRequired { get => restartRequired; private set => SetProperty(ref restartRequired, value); }
    public string? ErrorMessage { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public string? SuccessMessage { get => success; private set { if (SetProperty(ref success, value)) OnPropertyChanged(nameof(HasSuccess)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage); public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool CanEnablePin => !IsPinEnabled && !IsSecurityBusy; public bool CanChangePin => IsPinEnabled && !IsSecurityBusy; public bool CanDisablePin => IsPinEnabled && !IsSecurityBusy; public bool CanManageData => !IsDataBusy;

    public async Task InitializeAsync(CancellationToken cancellationToken = default) { IsPinEnabled = await operations.IsPinEnabledAsync(cancellationToken); ClearMessages(); }
    public Task EnablePinAsync(string pin, string confirmation, CancellationToken cancellationToken = default) => MutatePinAsync(() => operations.EnablePinAsync(pin, confirmation, cancellationToken), true, "Local PIN enabled.");
    public Task ChangePinAsync(string currentPin, string newPin, string confirmation, CancellationToken cancellationToken = default) => MutatePinAsync(() => operations.ChangePinAsync(currentPin, newPin, confirmation, cancellationToken), true, "Local PIN changed.");
    public Task DisablePinAsync(string currentPin, CancellationToken cancellationToken = default) => MutatePinAsync(() => operations.DisablePinAsync(currentPin, cancellationToken), false, "Local PIN disabled.");

    public async Task RunBackupAsync(Func<string, Task<string?>> chooseDestinationAsync, DateTime localNow, CancellationToken cancellationToken = default)
    {
        if (IsDataBusy) return; IsDataBusy = true; ClearMessages();
        try { var name = $"suma-backup-{localNow:yyyyMMdd-HHmmss}.suma-backup"; var path = await chooseDestinationAsync(name); if (path is null) return; await operations.CreateBackupAsync(path, cancellationToken); SuccessMessage = "Backup created successfully."; }
        catch { ErrorMessage = "Suma could not create the backup. Your finance data was not changed."; }
        finally { IsDataBusy = false; }
    }

    public async Task RunRestoreAsync(Func<Task<string?>> chooseSourceAsync, Func<Task<bool>> confirmAsync, CancellationToken cancellationToken = default)
    {
        if (IsDataBusy) return; IsDataBusy = true; ClearMessages(); string? staged = null;
        try
        {
            var source = await chooseSourceAsync(); if (source is null) return; staged = await operations.ValidateAndStageRestoreAsync(source, cancellationToken);
            if (!await confirmAsync()) { await operations.DiscardStagedRestoreAsync(staged, cancellationToken); staged = null; return; }
            await operations.ConfirmRestoreAsync(staged, cancellationToken); staged = null; IsRestartRequired = true; SuccessMessage = "Restore is ready. Close Suma and open it again to finish.";
        }
        catch (ApplicationValidationException exception) { ErrorMessage = exception.Message; }
        catch { ErrorMessage = "Suma could not prepare the restore. Your current data was not changed."; }
        finally { if (staged is not null) { try { await operations.DiscardStagedRestoreAsync(staged, CancellationToken.None); } catch { } } IsDataBusy = false; }
    }

    private async Task MutatePinAsync(Func<Task> mutation, bool enabledAfter, string successMessage)
    {
        if (IsSecurityBusy) return; IsSecurityBusy = true; ClearMessages();
        try { await mutation(); IsPinEnabled = enabledAfter; SuccessMessage = successMessage; }
        catch (ApplicationValidationException exception) { ErrorMessage = exception.Message; }
        catch { ErrorMessage = "Suma could not update the local PIN. Your previous security setting was preserved."; }
        finally { IsSecurityBusy = false; }
    }
    private void ClearMessages() { ErrorMessage = null; SuccessMessage = null; }
    private void Notify() { OnPropertyChanged(nameof(CanEnablePin)); OnPropertyChanged(nameof(CanChangePin)); OnPropertyChanged(nameof(CanDisablePin)); OnPropertyChanged(nameof(CanManageData)); }
}
