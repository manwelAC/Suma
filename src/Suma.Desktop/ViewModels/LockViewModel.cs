using CommunityToolkit.Mvvm.ComponentModel;
using Suma.Desktop.Operations.Settings;

namespace Suma.Desktop.ViewModels;

public sealed class LockViewModel(ISettingsOperations operations) : ObservableObject
{
    private bool busy; private string? error;
    public bool IsBusy { get => busy; private set => SetProperty(ref busy, value); }
    public string? ErrorMessage { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public async Task<bool> UnlockAsync(string pin, CancellationToken cancellationToken = default) { if (IsBusy) return false; IsBusy = true; ErrorMessage = null; try { var valid = await operations.VerifyPinAsync(pin, cancellationToken); if (!valid) ErrorMessage = "That PIN is incorrect."; return valid; } catch { ErrorMessage = "Suma could not verify the PIN. Try again."; return false; } finally { IsBusy = false; } }
}
