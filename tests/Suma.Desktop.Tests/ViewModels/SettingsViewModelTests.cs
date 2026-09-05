using Suma.Application.Common.Exceptions;
using Suma.Desktop.Operations.Settings;
using Suma.Desktop.ViewModels;
using Xunit;

namespace Suma.Desktop.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Initial_status_and_pin_mutations_surface_success_and_failure()
    {
        var operations = new FakeOperations(); var viewModel = new SettingsViewModel(operations); await viewModel.InitializeAsync(Token); Assert.False(viewModel.IsPinEnabled);
        await viewModel.EnablePinAsync("1234", "1234", Token); Assert.True(viewModel.IsPinEnabled); Assert.True(viewModel.HasSuccess);
        operations.PinFailure = new ApplicationValidationException("Current PIN is incorrect."); await viewModel.ChangePinAsync("0000", "5678", "5678", Token); Assert.True(viewModel.IsPinEnabled); Assert.Equal("Current PIN is incorrect.", viewModel.ErrorMessage);
        operations.PinFailure = null; await viewModel.DisablePinAsync("1234", Token); Assert.False(viewModel.IsPinEnabled);
    }

    [Fact]
    public async Task Duplicate_pin_mutation_is_ignored()
    {
        var operations = new FakeOperations { DelayPin = true }; var viewModel = new SettingsViewModel(operations); var first = viewModel.EnablePinAsync("1234", "1234", Token); await operations.Started.Task;
        await viewModel.EnablePinAsync("5678", "5678", Token); Assert.Equal(1, operations.PinMutations); operations.Release.SetResult(); await first; Assert.False(viewModel.IsSecurityBusy);
    }

    [Fact]
    public async Task Backup_busy_duplicate_cancel_failure_and_success_are_deterministic()
    {
        var operations = new FakeOperations { DelayBackup = true }; var viewModel = new SettingsViewModel(operations); var first = viewModel.RunBackupAsync(_ => Task.FromResult<string?>("backup.suma-backup"), new(2026, 9, 3, 12, 13, 14), Token); await operations.Started.Task;
        Assert.True(viewModel.IsDataBusy); await viewModel.RunBackupAsync(_ => Task.FromResult<string?>("duplicate"), DateTime.Now, Token); Assert.Equal(1, operations.Backups); operations.Release.SetResult(); await first; Assert.True(viewModel.HasSuccess); Assert.False(viewModel.IsDataBusy);
        await viewModel.RunBackupAsync(_ => Task.FromResult<string?>(null), DateTime.Now, Token); Assert.False(viewModel.HasError);
        operations.DataFailure = true; await viewModel.RunBackupAsync(_ => Task.FromResult<string?>("failed"), DateTime.Now, Token); Assert.True(viewModel.HasError); Assert.False(viewModel.IsDataBusy);
    }

    [Fact]
    public async Task Restore_cancel_invalid_confirmation_and_success_preserve_state_contract()
    {
        var operations = new FakeOperations(); var viewModel = new SettingsViewModel(operations);
        await viewModel.RunRestoreAsync(() => Task.FromResult<string?>(null), () => Task.FromResult(true), Token); Assert.Equal(0, operations.Stages);
        await viewModel.RunRestoreAsync(() => Task.FromResult<string?>("valid"), () => Task.FromResult(false), Token); Assert.Equal(1, operations.Discards); Assert.False(viewModel.IsRestartRequired);
        await viewModel.RunRestoreAsync(() => Task.FromResult<string?>("valid"), () => Task.FromResult(true), Token); Assert.True(viewModel.IsRestartRequired); Assert.Equal(1, operations.Confirms); Assert.False(viewModel.IsDataBusy);
    }

    [Fact]
    public async Task Backup_and_restore_are_mutually_exclusive()
    {
        var operations = new FakeOperations { DelayBackup = true }; var viewModel = new SettingsViewModel(operations); var backup = viewModel.RunBackupAsync(_ => Task.FromResult<string?>("backup"), DateTime.Now, Token); await operations.Started.Task;
        await viewModel.RunRestoreAsync(() => Task.FromResult<string?>("candidate"), () => Task.FromResult(true), Token); Assert.Equal(0, operations.Stages);
        operations.Release.SetResult(); await backup; Assert.False(viewModel.IsDataBusy);
    }

    [Fact]
    public async Task Wrong_unlock_stays_locked_and_correct_unlock_proceeds()
    {
        var operations = new FakeOperations { ValidPin = false }; var viewModel = new LockViewModel(operations); Assert.False(await viewModel.UnlockAsync("0000", Token)); Assert.NotNull(viewModel.ErrorMessage);
        operations.ValidPin = true; Assert.True(await viewModel.UnlockAsync("1234", Token)); Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Reset_data_erases_all_data_when_confirmed_and_aborts_when_cancelled()
    {
        var operations = new FakeOperations();
        var viewModel = new SettingsViewModel(operations);

        // Cancelled
        await viewModel.RunResetDataAsync(() => Task.FromResult(false), Token);
        Assert.Equal(0, operations.Resets);
        Assert.Null(viewModel.SuccessMessage);

        // Confirmed
        await viewModel.RunResetDataAsync(() => Task.FromResult(true), Token);
        Assert.Equal(1, operations.Resets);
        Assert.NotNull(viewModel.SuccessMessage);
        Assert.False(viewModel.IsDataBusy);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class FakeOperations : ISettingsOperations
    {
        public bool Enabled; public bool ValidPin = true; public bool DelayPin; public bool DelayBackup; public bool DataFailure; public Exception? PinFailure; public int PinMutations; public int Backups; public int Stages; public int Confirms; public int Discards; public int Resets;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> IsPinEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enabled);
        public Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default) => Task.FromResult(ValidPin);
        public Task EnablePinAsync(string pin, string confirmation, CancellationToken cancellationToken = default) => PinMutation(true);
        public Task ChangePinAsync(string currentPin, string newPin, string confirmation, CancellationToken cancellationToken = default) => PinMutation(true);
        public Task DisablePinAsync(string currentPin, CancellationToken cancellationToken = default) => PinMutation(false);
        private async Task PinMutation(bool enabled) { PinMutations++; if (PinFailure is not null) throw PinFailure; if (DelayPin) { DelayPin = false; Started.SetResult(); await Release.Task; } Enabled = enabled; }
        public async Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default) { Backups++; if (DelayBackup) { DelayBackup = false; Started.SetResult(); await Release.Task; } if (DataFailure) throw new IOException(); }
        public Task<string> ValidateAndStageRestoreAsync(string sourcePath, CancellationToken cancellationToken = default) { Stages++; if (DataFailure) throw new ApplicationValidationException("That file is not a valid Suma backup."); return Task.FromResult("staged"); }
        public Task ConfirmRestoreAsync(string stagedPath, CancellationToken cancellationToken = default) { Confirms++; return Task.CompletedTask; }
        public Task DiscardStagedRestoreAsync(string stagedPath, CancellationToken cancellationToken = default) { Discards++; return Task.CompletedTask; }
        public Task ResetAllDataAsync(CancellationToken cancellationToken = default) { Resets++; if (DataFailure) throw new IOException(); return Task.CompletedTask; }
    }
}
