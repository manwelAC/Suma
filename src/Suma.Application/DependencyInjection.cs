using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Accounts.GetAccountBalance;
using Suma.Application.Accounts.GetAccounts;
using Suma.Application.Accounts.ArchiveAccount;
using Suma.Application.Accounts.CreateAccount;
using Suma.Application.Accounts.RestoreAccount;
using Suma.Application.Accounts.UpdateAccount;
using Suma.Application.Budgets.AddBudgetAllocation;
using Suma.Application.Budgets.ArchiveBudget;
using Suma.Application.Budgets.CreateBudget;
using Suma.Application.Budgets.GetBudgetDetails;
using Suma.Application.Budgets.GetBudgets;
using Suma.Application.Budgets.RestoreBudget;
using Suma.Application.Categories.ArchiveCategory;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.RestoreCategory;
using Suma.Application.Categories.UpdateCategory;
using Suma.Application.Recurring.MarkOccurrencePaid;
using Suma.Application.Savings.AddGoalContribution;
using Suma.Application.Savings.CreateSavingsGoal;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Transactions.GetTransactions;
using Suma.Application.Transactions.GetRefundableExpenses;

namespace Suma.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetAccountBalanceUseCase>();
        services.AddScoped<GetAccountsUseCase>();
        services.AddScoped<CreateAccountUseCase>();
        services.AddScoped<UpdateAccountUseCase>();
        services.AddScoped<ArchiveAccountUseCase>();
        services.AddScoped<RestoreAccountUseCase>();
        services.AddScoped<GetCategoriesUseCase>();
        services.AddScoped<CreateCategoryUseCase>();
        services.AddScoped<UpdateCategoryUseCase>();
        services.AddScoped<ArchiveCategoryUseCase>();
        services.AddScoped<RestoreCategoryUseCase>();
        services.AddScoped<CreateExpenseUseCase>();
        services.AddScoped<CreateIncomeUseCase>();
        services.AddScoped<CreateTransferUseCase>();
        services.AddScoped<CreateRefundUseCase>();
        services.AddScoped<GetTransactionsUseCase>();
        services.AddScoped<GetRefundableExpensesUseCase>();
        services.AddScoped<CreateBudgetUseCase>();
        services.AddScoped<AddBudgetAllocationUseCase>();
        services.AddScoped<GetBudgetsUseCase>();
        services.AddScoped<GetBudgetDetailsUseCase>();
        services.AddScoped<ArchiveBudgetUseCase>();
        services.AddScoped<RestoreBudgetUseCase>();
        services.AddScoped<MarkOccurrencePaidUseCase>();
        services.AddScoped<CreateSavingsGoalUseCase>();
        services.AddScoped<AddGoalContributionUseCase>();

        return services;
    }
}
