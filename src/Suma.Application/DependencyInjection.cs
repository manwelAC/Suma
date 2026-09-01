using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Accounts.GetAccountBalance;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Recurring.MarkOccurrencePaid;
using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.GetTransactions;

namespace Suma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetAccountBalanceUseCase>();
        services.AddScoped<GetAccountsUseCase>();
        services.AddScoped<CreateExpenseUseCase>();
        services.AddScoped<CreateIncomeUseCase>();
        services.AddScoped<CreateTransferUseCase>();
        services.AddScoped<CreateRefundUseCase>();
        services.AddScoped<GetTransactionsUseCase>();
        services.AddScoped<CreateBudgetUseCase>();
        services.AddScoped<AddBudgetAllocationUseCase>();
        services.AddScoped<MarkOccurrencePaidUseCase>();
        services.AddScoped<CreateSavingsGoalUseCase>();
        services.AddScoped<AddGoalContributionUseCase>();

        return services;
    }
}
