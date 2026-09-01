using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Suma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinancialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    include_in_available_to_spend = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    opening_balance_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    period_start = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    period_end = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    expected_income_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budgets", x => x.id);
                    table.CheckConstraint("ck_budgets_expected_income", "expected_income_minor >= 0");
                    table.CheckConstraint("ck_budgets_period", "period_start <= period_end");
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    transaction_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    icon_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_system = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.CheckConstraint("ck_categories_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "savings_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    target_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    destination_account_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    is_archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    target_amount_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_savings_goals", x => x.id);
                    table.CheckConstraint("ck_savings_goals_target_positive", "target_amount_minor > 0");
                    table.ForeignKey(
                        name: "FK_savings_goals_accounts_destination_account_id",
                        column: x => x.destination_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    budget_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    category_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reserve_from_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    amount_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_allocations", x => x.id);
                    table.CheckConstraint("ck_budget_allocations_amount_positive", "amount_minor > 0");
                    table.ForeignKey(
                        name: "FK_budget_allocations_budgets_budget_id",
                        column: x => x.budget_id,
                        principalTable: "budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_allocations_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    source_account_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    destination_account_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    category_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    frequency_unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    interval_count = table.Column<int>(type: "INTEGER", nullable: false),
                    day_of_week = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    day_of_month = table.Column<int>(type: "INTEGER", nullable: true),
                    month_of_year = table.Column<int>(type: "INTEGER", nullable: true),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    end_date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    amount_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_transactions", x => x.id);
                    table.CheckConstraint("ck_recurring_transactions_amount_positive", "amount_minor > 0");
                    table.CheckConstraint("ck_recurring_transactions_dates", "end_date IS NULL OR start_date <= end_date");
                    table.CheckConstraint("ck_recurring_transactions_day_of_month", "day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_recurring_transactions_distinct_accounts", "source_account_id IS NULL OR destination_account_id IS NULL OR source_account_id <> destination_account_id");
                    table.CheckConstraint("ck_recurring_transactions_interval", "interval_count > 0");
                    table.CheckConstraint("ck_recurring_transactions_month_of_year", "month_of_year IS NULL OR month_of_year BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_recurring_transactions_accounts_destination_account_id",
                        column: x => x.destination_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_accounts_source_account_id",
                        column: x => x.source_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    source_account_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    destination_account_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    category_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    original_transaction_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    transaction_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    amount_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.CheckConstraint("ck_transactions_amount_positive", "amount_minor > 0");
                    table.CheckConstraint("ck_transactions_distinct_accounts", "source_account_id IS NULL OR destination_account_id IS NULL OR source_account_id <> destination_account_id");
                    table.ForeignKey(
                        name: "FK_transactions_accounts_destination_account_id",
                        column: x => x.destination_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_source_account_id",
                        column: x => x.source_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_transactions_original_transaction_id",
                        column: x => x.original_transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goal_contributions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    savings_goal_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    transaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    amount_minor = table.Column<long>(type: "INTEGER", nullable: false),
                    currency_code = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_contributions", x => x.id);
                    table.CheckConstraint("ck_goal_contributions_amount_positive", "amount_minor > 0");
                    table.ForeignKey(
                        name: "FK_goal_contributions_savings_goals_savings_goal_id",
                        column: x => x.savings_goal_id,
                        principalTable: "savings_goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goal_contributions_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    recurring_transaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    transaction_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_recurring_transactions_recurring_transaction_id",
                        column: x => x.recurring_transaction_id,
                        principalTable: "recurring_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_budget_id",
                table: "budget_allocations",
                column: "budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_budget_id_category_id",
                table: "budget_allocations",
                columns: new[] { "budget_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_allocations_category_id",
                table: "budget_allocations",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_budgets_period_end",
                table: "budgets",
                column: "period_end");

            migrationBuilder.CreateIndex(
                name: "IX_budgets_period_start",
                table: "budgets",
                column: "period_start");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_category_id",
                table: "categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_contributions_savings_goal_id",
                table: "goal_contributions",
                column: "savings_goal_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_contributions_transaction_id",
                table: "goal_contributions",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_due_date",
                table: "recurring_occurrences",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_recurring_transaction_id_due_date",
                table: "recurring_occurrences",
                columns: new[] { "recurring_transaction_id", "due_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_status",
                table: "recurring_occurrences",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_transaction_id",
                table: "recurring_occurrences",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_category_id",
                table: "recurring_transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_destination_account_id",
                table: "recurring_transactions",
                column: "destination_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_end_date",
                table: "recurring_transactions",
                column: "end_date");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_is_active",
                table: "recurring_transactions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_source_account_id",
                table: "recurring_transactions",
                column: "source_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_start_date",
                table: "recurring_transactions",
                column: "start_date");

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_destination_account_id",
                table: "savings_goals",
                column: "destination_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_is_archived",
                table: "savings_goals",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "IX_savings_goals_target_date",
                table: "savings_goals",
                column: "target_date");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_destination_account_id",
                table: "transactions",
                column: "destination_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_original_transaction_id",
                table: "transactions",
                column: "original_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_source_account_id",
                table: "transactions",
                column: "source_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_transaction_date",
                table: "transactions",
                column: "transaction_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_allocations");

            migrationBuilder.DropTable(
                name: "goal_contributions");

            migrationBuilder.DropTable(
                name: "recurring_occurrences");

            migrationBuilder.DropTable(
                name: "budgets");

            migrationBuilder.DropTable(
                name: "savings_goals");

            migrationBuilder.DropTable(
                name: "recurring_transactions");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
