using Suma.Desktop.Operations.Accounts;

namespace Suma.Desktop.ViewModels;

public sealed class SavingsGoalEditorViewModel(IAccountOperations accounts)
{
    public IReadOnlyList<SavingsAccountOption> Accounts { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await accounts.GetAsync(false, cancellationToken);
            Accounts = new[] { new SavingsAccountOption(null, "No destination account", null) }
                .Concat(results.Select(item => new SavingsAccountOption(item.Id, item.Name, item.CurrencyCode))).ToArray();
            ErrorMessage = null; return true;
        }
        catch { ErrorMessage = "Suma could not load savings goal options. Try again."; return false; }
    }
}
