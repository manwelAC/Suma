using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Abstractions.Persistence;
using Suma.Application;
using Suma.Application.Accounts.GetAccountBalance;
using Suma.Application.Transactions.CreateExpense;
using Suma.Application.Transactions.CreateIncome;
using Suma.Application.Transactions.CreateRefund;
using Suma.Application.Transactions.CreateTransfer;
using Suma.Application.Recurring.MarkOccurrencePaid;
using Suma.Domain.Accounts;
using Suma.Domain.Categories;
using Suma.Domain.Transactions;
using Suma.Domain.ValueObjects;
using Suma.Infrastructure.Persistence;
using Suma.Infrastructure;
using Suma.Infrastructure.Runtime;
using Suma.Infrastructure.Time;
using Xunit;

namespace Suma.Infrastructure.Tests.Runtime;

public sealed class RuntimeCompositionTests
{
    [Fact]
    public async Task Required_application_workflows_resolve_from_a_scope()
    {
        await using var runtime = RuntimeServices.Create();
        await using var scope = runtime.Provider.CreateAsyncScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateExpenseUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateIncomeUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateTransferUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateRefundUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetAccountBalanceUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MarkOccurrencePaidUseCase>());
    }

    [Fact]
    public async Task Stores_and_unit_of_work_share_the_scoped_context()
    {
        await using var runtime = RuntimeServices.Create();
        await runtime.InitializeAsync();
        var account = NewAccount("Scoped account");

        await using (var scope = runtime.Provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SumaDbContext>();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountStore>();
            var transactions = scope.ServiceProvider.GetRequiredService<ITransactionStore>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            context.Accounts.Add(account);
            Assert.Same(context, scope.ServiceProvider.GetRequiredService<SumaDbContext>());
            await unitOfWork.SaveChangesAsync(Token);
            Assert.Same(account, await accounts.GetByIdAsync(account.Id, Token));

            var category = new Category("Food", CategoryTransactionKind.Expense);
            var transaction = Transaction.CreateExpense(account.Id, category.Id, new Money(100, "PHP"), new(2026, 9, 1));
            context.Categories.Add(category);
            await transactions.AddAsync(transaction, Token);
            Assert.Equal(EntityState.Added, context.Entry(transaction).State);
            await unitOfWork.SaveChangesAsync(Token);
        }

        await using var verificationScope = runtime.Provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<SumaDbContext>();
        Assert.True(await verificationContext.Accounts.AnyAsync(item => item.Id == account.Id, Token));
    }

    [Fact]
    public async Task Separate_scopes_receive_different_contexts()
    {
        await using var runtime = RuntimeServices.Create();
        await using var scopeA = runtime.Provider.CreateAsyncScope();
        await using var scopeB = runtime.Provider.CreateAsyncScope();

        Assert.NotSame(
            scopeA.ServiceProvider.GetRequiredService<SumaDbContext>(),
            scopeB.ServiceProvider.GetRequiredService<SumaDbContext>());
    }

    [Fact]
    public void Database_path_uses_supplied_root_and_expected_suffix()
    {
        var root = Path.Combine("C:", "Users", "Example", "AppData", "Local");
        var path = LocalDataPaths.BuildDatabasePath(root);

        Assert.Equal(Path.Combine(root, "Suma", "suma.db"), path);
        Assert.EndsWith(Path.Combine("Suma", "suma.db"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_connection_targets_file_with_foreign_keys_and_no_pooling()
    {
        var path = Path.Combine(Path.GetTempPath(), "Suma.Tests", "connection.db");
        var builder = new SqliteConnectionStringBuilder(SqliteRuntimeConnection.Build(path));

        Assert.Equal(path, builder.DataSource);
        Assert.True(builder.ForeignKeys);
        Assert.False(builder.Pooling);
    }

    [Fact]
    public async Task Runtime_initializer_applies_initial_migration_to_fresh_database()
    {
        await using var runtime = RuntimeServices.Create();
        await runtime.InitializeAsync();

        await using var scope = runtime.Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SumaDbContext>();
        var applied = await context.Database.GetAppliedMigrationsAsync(Token);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialFinancialSchema", StringComparison.Ordinal));
        Assert.True(await context.Database.CanConnectAsync(Token));
        Assert.True(File.Exists(runtime.DatabasePath));
    }

    [Fact]
    public async Task Repeated_initialization_is_idempotent_and_preserves_data()
    {
        await using var runtime = RuntimeServices.Create();
        await runtime.InitializeAsync();
        var account = NewAccount("Preserved account");

        await using (var scope = runtime.Provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SumaDbContext>();
            context.Accounts.Add(account);
            await context.SaveChangesAsync(Token);
        }

        await runtime.InitializeAsync();

        await using var verificationScope = runtime.Provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<SumaDbContext>();
        Assert.True(await verificationContext.Accounts.AnyAsync(item => item.Id == account.Id, Token));
    }

    [Fact]
    public void System_date_provider_returns_current_local_calendar_date()
    {
        var before = DateOnly.FromDateTime(DateTime.Now);
        var actual = new SystemDateProvider().Today;
        var after = DateOnly.FromDateTime(DateTime.Now);

        Assert.Contains(actual, new[] { before, after });
    }

    private static Account NewAccount(string name) =>
        new(name, AccountType.Bank, Money.Zero("PHP"), "PHP", true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class RuntimeServices : IAsyncDisposable
    {
        private readonly string directory;

        private RuntimeServices(string directory, string databasePath, ServiceProvider provider)
        {
            this.directory = directory;
            DatabasePath = databasePath;
            Provider = provider;
        }

        public string DatabasePath { get; }

        public ServiceProvider Provider { get; }

        public static RuntimeServices Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "Suma.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var databasePath = Path.Combine(directory, "runtime.db");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(databasePath);
            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            return new RuntimeServices(directory, databasePath, provider);
        }

        public async Task InitializeAsync() =>
            await Provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(Token);

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
