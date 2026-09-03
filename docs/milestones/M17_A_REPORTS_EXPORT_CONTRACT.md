# Suma M17-A — Reports and CSV Export Contract

## Status and Scope

This document is the authoritative contract for M17-B. It was produced on branch `milestone/17-reports-export` from M16 commit `a38ac8f Add overview and available to spend` after a clean-tree repository inspection.

M17 is read-only reporting over Suma's existing authoritative data. It does not create a second ledger, persist report totals, mutate financial state, introduce FX, or reinterpret M01–M16 semantics. CSV is the only export format in M17. M18 Settings, PIN, and Backup/Restore and M19 Widgets/Packaging are excluded.

## Repository Findings

### Transactions and historical identity

`Transaction` is the immutable ledger entity. Amounts are positive signed-`long` minor-unit values paired with normalized three-letter currency codes; transaction type supplies direction.

| Type | Source effect | Destination effect | Category | Link |
|---|---:|---:|---|---|
| Income | — | `+Amount` | Income | none |
| Expense | `-Amount` | — | Expense | none |
| Transfer | `-Amount` | `+Amount` | none | none |
| Refund | — | `+Amount` | Expense | original Expense |

`TransactionDate` is a `DateOnly` and is authoritative for an actual ledger event. Creation rejects future dates. A Refund must reference an Expense, match its currency, and cannot make aggregate refunds exceed the original amount. The current use case requires an Expense category but does not independently require the Refund's submitted `CategoryId` to equal the original Expense category; report attribution must therefore follow the original Expense link.

Transfers require distinct active Accounts whose currencies match each other and the Transfer amount. Cross-currency Transfers are impossible through approved Application workflows. M17 must not add FX or synthesize cross-currency movement.

Accounts and Categories cannot be used for new transactions after archival, but archival does not delete them or their historical ledger references. Historical report rows must retain and display their names and an archived indicator where useful.

### Authoritative Account balance

For Account `a`:

```text
CurrentBalance(a) = OpeningBalance(a)
                  + Income deposited to a
                  + Refunds deposited to a
                  + Transfers into a
                  - Expenses paid from a
                  - Transfers out of a
```

`AccountBalanceCalculator` and `GetAccountBalanceUseCase` implement this with checked arithmetic. `OverviewStore.GetAccountBalanceFactsAsync` is the existing set-based all-history fact read by currency. It is safe for a current-balance summary but is not date-range activity. M17 will not persist balances or derive report totals from bounded activity.

### Existing query safety

| API | M17 use | Finding |
|---|---|---|
| `ITransactionStore.GetHistoryAsync` | Display precedent only | Joins names, but is bounded to 500 by `GetTransactionsUseCase`; unsafe for totals/export. |
| `ITransactionStore.GetRecentAsync` | Do not use | Arbitrary recent-row limit. |
| `ITransactionStore.GetForAccountAsync` | Existing one-Account balance only | Accurate but creates Account N+1 if used for a report. |
| `GetRefundedAmountMinorAsync` | Write validation | One original Expense at a time; unsuitable for grouped reports. |
| `GetNetExpenseAmountsByCategoryAsync` | Reuse for Budget Performance | Exact M13 semantics and set-based by requested category IDs. |
| `OverviewStore.GetAccountBalanceFactsAsync` | Optional current balance fact source | Set-based, all history, per currency; not a selected-range report. |
| `OverviewStore.GetAccountCurrencyFactsAsync` | Reuse currency policy or equivalent | Persisted Account currencies, including archived Accounts, with deterministic active/included preference. |
| `GetBudgetDetailsUseCase` | Reuse Budget Performance policy | Produces Allocation, Spent, Remaining, utilization, archive/name facts. |

M17 needs a focused report persistence contract because no existing unbounded, currency-and-range-filtered query returns the complete report facts.

### Budget semantics

A Budget has an inclusive period, one currency through Expected Income, and an archive state. Active Budgets cannot overlap. An allocation belongs to one exact Category and has Amount and `ReserveFromAvailable`.

M13 Budget Spent is:

```text
AllocationSpent = Expenses where
                    original Expense date is inside Budget period,
                    Expense currency equals Budget currency, and
                    Expense CategoryId exactly equals allocation CategoryId
                  - every Refund linked to those qualifying Expenses,
                    regardless of Refund date
```

There is no parent/child roll-up. Archived Categories remain named. `Remaining = checked(Allocation - Spent)` and may be negative. Utilization is decimal and may exceed 100%. `ReserveFromAvailable` affects M16 ATS protection but does not change Budget Spent.

Archived Budgets remain valid historical reports. Budget Performance must reuse `GetBudgetDetailsUseCase` / `GetNetExpenseAmountsByCategoryAsync`, not approximate this formula with general report-range Refund rules.

### Categories

Categories have stable identity, Expense or Income kind, optional parent identity, name, ordering metadata, and archive state. Reports group by exact `CategoryId`. A child is not rolled into its parent. Historical active and archived Categories with report activity are displayed. Categories with no activity in the selected range are omitted from cash-flow/category reports. Names sort ordinal-ignore-case for presentation, with `CategoryId` as a deterministic internal tie-breaker; IDs are not shown or exported.

Refund reporting attributes the reversal to the original Expense's exact Category, not an independently submitted Refund category. This preserves the financial relationship even if data was created through a non-UI caller.

### Savings and Recurring

Savings Goal target, progress, remaining, destination, and contribution attribution are planning metadata, not another ledger. They are excluded from M17 financial totals and the initial report set.

Pending and Skipped recurring occurrences are not actual transactions and are excluded. A Paid occurrence creates exactly one ordinary Transaction; that Transaction participates normally by type, date, currency, Account, and Category. Reports do not separately count the occurrence.

## M17 Report Set

The smallest coherent M17 report set is:

1. **Cash Flow** — actual Income, gross Expenses, Refunds, net Expenses, and net cash flow for one currency and inclusive date range.
2. **Expense by Category** — exact-category gross Expense, Refund, and net Expense values for the same selection.
3. **Income by Category** — actual Income grouped by exact Income Category.
4. **Account Movement** — per-Account inflow, outflow, and net movement for the range, with an optional detail list/export that explains transfers and other ledger events.
5. **Budget Performance** — a selected active or archived Budget using exact M13 results.

A separate current Account balance summary is excluded from M17-B because Accounts and Overview already provide it and it is not a date-range report. Account Movement adds reporting value without duplicating Activity. Account Movement detail is required, but it is loaded on demand rather than included in every base report snapshot.

Savings progress and recurring forecasts are excluded from M17-B. They can be considered later only as clearly separated planning reports.

## Initial Reports State and Date Presets

When Reports first opens, the general report state is exactly:

- Section: `Cash Flow`.
- Currency: the approved M16 persisted-Account policy—first ordinal currency represented by an active ATS-included Account, otherwise the first persisted Account currency ordinally. No currency is manufactured. No Accounts produces a no-currency state.
- Start date: the first calendar day of the month containing `IDateProvider.Today`.
- End date: `IDateProvider.Today`.

Application policy obtains Today through `IDateProvider`; it does not read the system clock directly.

Desktop/ViewModel exposes these convenience presets:

| Preset | Explicit inclusive dates resolved before the Application call |
|---|---|
| Month to date | First day of Today's month through Today |
| Last month | First through last calendar day of the month immediately before Today |
| Last 30 days | Today minus 29 days through Today—exactly 30 calendar dates |
| Year to date | January 1 of Today's year through Today |
| Custom | User-supplied StartDate and EndDate |

Presets are presentation conveniences only. `GetFinancialReportUseCase` always receives explicit `StartDate` and `EndDate`; Application financial calculation does not depend on a UI preset enum.

## General Base-Report Loading Contract

`GetFinancialReportUseCase` receives one normalized persisted Account currency, `StartDate`, and `EndDate`, and returns one complete base snapshot containing Cash Flow summary, Expense by Category, Income by Category, and Account Movement summaries.

The base-load identity is exactly `(Currency, StartDate, EndDate)`. Switching among Cash Flow, Categories, and Accounts is presentation state and must not re-query when that key has not changed. The serialized latest-wins pump therefore coordinates currency/date requests, not general report type. Only one base load runs at once; queued selection changes are preserved; the latest currency/date wins; stale results/errors cannot apply; and every caller completes without a lock crossing an `await`.

## Date-Range Contract

General reports use inclusive event-date filtering:

```text
StartDate <= Transaction.TransactionDate <= EndDate
```

- `StartDate > EndDate`: reject with an Application validation error; do not swap silently.
- Same-day range: valid and includes both boundaries because they are the same date.
- Empty range result: valid, with zero summary totals and empty grouped/detail collections.
- Historical ranges: valid without a lower repository-imposed limit.
- Future ranges: valid read-only selections and normally empty because actual future Transactions cannot be created.
- Determinism: the selected dates and currency fully determine the result for a fixed database snapshot.

Budget Performance is selected by Budget identity and always uses that Budget's stored inclusive period. The general date controls do not override a Budget's financial period.

## Cash Flow Definition

For selected currency `c` and inclusive range `r`:

```text
GrossIncome(c,r) = checked sum Amount of Income events in r, currency c

GrossExpense(c,r) = checked sum Amount of Expense events in r, currency c

Refunds(c,r) = checked sum Amount of Refund events in r, currency c

NetExpense(c,r) = checked(GrossExpense(c,r) - Refunds(c,r))

NetCashFlow(c,r) = checked(GrossIncome(c,r) - NetExpense(c,r))
                 = checked(GrossIncome - GrossExpense + Refunds)
```

Transfers are excluded from every Cash Flow component. `NetExpense` and a category net may be negative when the range contains Refund cash for Expenses outside the range. That is truthful event-date cash flow and must not be clamped. The UI shows Refunds separately so the result is explainable.

Opening balances are not Income and do not enter range cash flow. Expected Income is not actual Income. Savings attribution and Pending/Skipped recurring occurrences have no effect.

## Refund Semantics by Report

Refunds are never Income.

| Report | Date attribution | Category attribution | Effect |
|---|---|---|---|
| Cash Flow | Refund's own `TransactionDate` | Not needed for total | Adds cash and reduces Net Expense in that range. |
| Expense by Category | Refund's own `TransactionDate` | Original Expense's exact Category | Reduces that category's Net Expense in the Refund event's range. |
| Account Movement | Refund's own `TransactionDate` | Original Expense category for context | Inflow to Refund destination Account. |
| Budget Performance | Original Expense date determines Budget membership; Refund date unrestricted | Original Expense exact Category | Reuses M13 and reverses qualifying historical Budget Spent. |

The distinction is intentional: Cash Flow and Account Movement answer “what cash moved during this selected range?”, while Budget Performance answers “what is the final net fulfillment of this Budget-period Expense cohort?” The UI must label Cash Flow Refunds separately and explain the Budget behavior in supporting text.

A partial Refund applies only its amount. Multiple Refunds are checked-summed. A Refund inside the selected report range reverses Expense cash flow even if its original Expense is outside the range. A Refund outside the selected report range does not affect general Cash Flow/category/account movement for that range, but it does affect Budget Performance when linked to an Expense qualifying for that Budget.

## Transfer Semantics

- Cash Flow, Income by Category, and Expense by Category exclude Transfers entirely.
- A same-currency Transfer between two Accounts has zero global net movement.
- Account Movement records `TransferOut` on the source and `TransferIn` on the destination. The per-Account net effects are negative and positive respectively.
- When both Accounts are included in a report, total Account Movement nets the Transfer to zero; it must not be labeled Income or Expense.
- Cross-currency Transfers cannot be created by current Application rules and M17 must not add them, convert them, or infer exchange rates.

## Category Reporting

### Expense by Category

For each exact original Expense Category:

```text
GrossExpense = checked sum of Expense amounts whose own dates are in range
Refunds = checked sum of Refund amounts whose own dates are in range
          attributed through OriginalTransactionId to the original Expense Category
NetExpense = checked(GrossExpense - Refunds)
```

Include a category when either Gross Expense or Refunds is nonzero. Include archived Categories and historical names. Do not include zero-activity categories. Do not roll child values into parents. Order by Net Expense descending, then category name ordinal-ignore-case, then CategoryId internally.

### Income by Category

Group only actual Income transactions by exact Income `CategoryId` and selected currency/date range. Transfers, Refunds, opening balances, Expected Income, and recurring plans are excluded. Include historical archived Categories. Order by Income descending, then category name ordinal-ignore-case, then CategoryId internally.

## Account Movement Contract

For each Account participating in selected-currency events in the inclusive range, return:

- Income In
- Refund In
- Transfer In
- Expense Out
- Transfer Out
- Total Inflow = checked sum of the three inflows
- Total Outflow = checked sum of the two outflows
- Net Movement = checked(TotalInflow - TotalOutflow)

The Account's opening balance is excluded because it is not range activity. Archived Accounts remain reportable and named. Accounts with no selected-range movement are omitted.

Account detail rows use each transaction's own date and show the Account-relative direction. Income, Refund, and Transfer In are inflows; Expense and Transfer Out are outflows. A Transfer detail names the counterparty. Refund detail names the original Expense category. Deterministic ordering is date descending, transaction ID descending, then direction when one Transfer produces two all-account detail projections.

Account Movement summary rows are ordered by Account name ordinal-ignore-case ascending, then `AccountId` internally as the deterministic tie-breaker. `AccountId` remains hidden from UI and CSV.

Account Movement detail is mandatory for the Accounts UI and its detail CSV, but is not part of every base snapshot. A focused `GetAccountMovementDetailUseCase` (or equivalently focused query) receives the successfully applied base snapshot's normalized currency and inclusive dates, plus optional `AccountId` only if the UI supports one-Account filtering. Load detail only when the Accounts report requires it or Account Movement CSV is requested. The query is set-based and range-filtered; it must not use bounded Activity, issue one query per Account, or hydrate all-history ledger graphs.

## Budget Performance Decision

Budget Performance is included because the existing `GetBudgetDetailsUseCase` already supplies an authoritative, useful read model without new semantics.

- User selects one active or archived Budget.
- The Budget's stored period and currency are displayed and control the calculation.
- Rows show exact Category, archived Category status, Allocation, Spent, Remaining, utilization, and `ReserveFromAvailable` context.
- Totals show Allocated, Spent, and Remaining.
- Expected Income may be displayed as stored planning context but is not Cash Flow Income.
- Rows follow the existing category-name ordering from `BudgetAllocationStore`.
- No cross-Budget or cross-currency aggregation is introduced.
- General report Refund/date rules do not replace M13 Budget rules.

Budget Performance has its own selector. On first entry:

1. Prefer the non-archived Budget whose inclusive stored period contains `IDateProvider.Today`.
2. Otherwise select the Budget with the most recent `PeriodStart`.
3. Tie-break by `PeriodEnd` descending, then Name ordinal-ignore-case, then `BudgetId` internally.

Active and archived Budgets remain selectable. With no Budgets, show a Budget no-data state and disable Budget CSV export. A selected Budget always uses its own currency and stored period. Budget selection must not overwrite the user's general currency/date state; returning to a general report restores that preserved selection.

## Currency Contract

Every general report requires exactly one normalized persisted Account currency. Currency discovery follows the approved M16 policy:

1. derive currencies from persisted Accounts, including archived Accounts;
2. prefer the first ordinal currency represented by an active ATS-included Account;
3. otherwise choose the first persisted currency ordinally;
4. never manufacture PHP or another unavailable currency;
5. if there are no Accounts, show a no-currency/no-data state and disable export.

All persistence filters include the selected currency. PHP and USD results are independent. There is no combined monetary total and no FX. Budget Performance uses the selected Budget's currency rather than combining it with the general report currency.

## CSV Export Specification

### Common rules

- Exportable reports: Cash Flow, Expense by Category, Income by Category, Account Movement detail, and Budget Performance.
- Encoding: UTF-8 with BOM for reliable Windows spreadsheet interoperability.
- Line endings: CRLF.
- Header: always present, including empty reports.
- CSV quoting: enclose a field in double quotes when it contains comma, quote, CR, or LF; double every embedded quote. Preserve Unicode.
- Dates: invariant ISO `yyyy-MM-dd`.
- Currency: uppercase three-letter code in an explicit `Currency` column.
- Money decimal: invariant, no group separators, exactly two fractional digits, derived with decimal/integer arithmetic as `AmountMinor / 100m`; never `double`/`float`.
- Minor units: retain explicit paired `*Minor` columns for lossless auditing and round-trip verification.
- IDs: database IDs are excluded from CSV.
- Null text: empty field, not a sentinel string.
- Ordering: use each report's specified deterministic order; repeated export of the same snapshot is byte-identical except filename.
- Filename: `suma-{report-slug}-{currency}-{start:yyyyMMdd}-{end:yyyyMMdd}.csv`. Budget uses `suma-budget-performance-{currency}-{budgetStart:yyyyMMdd}-{budgetEnd:yyyyMMdd}.csv`. Invalid filename characters are replaced with `-` if a future user-derived component is added.

The repository currently treats all Money as two-decimal minor units in `MoneyText`; M17 CSV follows that established convention. Adding currency-specific decimal exponents is outside M17.

### Exact CSV columns

Cash Flow summary:

```text
StartDate,EndDate,Currency,GrossIncome,GrossIncomeMinor,GrossExpense,GrossExpenseMinor,Refunds,RefundsMinor,NetExpense,NetExpenseMinor,NetCashFlow,NetCashFlowMinor
```

Expense by Category:

```text
StartDate,EndDate,Category,CategoryArchived,GrossExpense,GrossExpenseMinor,Refunds,RefundsMinor,NetExpense,NetExpenseMinor,Currency
```

Income by Category:

```text
StartDate,EndDate,Category,CategoryArchived,Income,IncomeMinor,Currency
```

Account Movement detail:

```text
Date,Account,AccountArchived,Direction,Type,Counterparty,Category,Description,Amount,AmountMinor,Currency
```

`Direction` is `Inflow` or `Outflow`. `Type` is the stable enum name: `Income`, `Expense`, `Refund`, or `Transfer`.

Budget Performance:

```text
Budget,PeriodStart,PeriodEnd,Category,CategoryArchived,Allocation,AllocationMinor,Spent,SpentMinor,Remaining,RemainingMinor,UtilizationPercent,Currency,ReserveFromAvailable
```

Boolean fields are invariant lowercase `true`/`false`. `UtilizationPercent` is invariant decimal without a percent symbol.

### Empty CSV behavior

- Cash Flow for a valid no-activity selection emits the header and exactly one summary row containing the requested dates/currency, every decimal total as `0.00`, and every minor-unit total as `0`.
- Expense by Category with no rows emits its header only.
- Income by Category with no rows emits its header only.
- Account Movement detail with no rows emits its header only.
- A selected Budget with no allocations emits the Budget Performance header only.
- No selected Budget/no Budgets disables Budget export.
- No persisted Account currency disables general export.

UTF-8 BOM, CRLF, exact schemas, invariant formatting, and deterministic ordering apply equally to empty exports.

### Export snapshot consistency

CSV always represents a successfully applied coherent snapshot matching the current selection. General export is enabled only when the applied snapshot key exactly equals the current normalized `(Currency, StartDate, EndDate)`. Changing any control immediately makes the selection dirty and disables export until that exact key loads successfully. A pending or failed replacement load cannot export the prior snapshot under new controls.

Account Movement detail/export uses the exact normalized currency/dates of the successfully applied base snapshot. Budget export is enabled only for the currently selected Budget identity after that Budget successfully loads. Export cancellation remains a normal no-op.

## Export Destination and Write Behavior

Use a Windows-native save picker owned by Suma's `MainWindow`, with CSV as the allowed/default type and the deterministic suggested filename. With the current unpackaged WinUI/Windows App SDK app, use the supported Windows App SDK `FileSavePicker` path; if the API requires an owner/window identifier for this target, initialize it from the active `MainWindow` rather than introducing global service location.

The user chooses the exact destination. Cancellation is a normal no-op. Do not hard-code Desktop or Downloads. The picker handles an existing-file replacement decision; Suma must not silently bypass that confirmation. Write only after the picker returns a destination, truncate only the user-approved selected file, await and flush the write, and surface failures without reporting success. Do not upload, log report contents, or retain the chosen path as financial state.

Keep destination selection and file I/O in Desktop. Keep deterministic report-to-CSV serialization in a focused, framework-neutral Application component so it is unit-testable. No general storage abstraction is needed.

## Architecture Recommendation

```text
General base report:
ReportsPage → ReportsViewModel → IReportOperations → ReportOperations
→ fresh async DI scope → GetFinancialReportUseCase → IReportStore
→ ReportStore → SQLite

Account detail:
ReportsPage / ReportsViewModel → IReportOperations → fresh async scope
→ focused Account Movement detail use case → focused set-based range query

Budget Performance:
ReportsViewModel → IReportOperations → fresh async scope
→ existing GetBudgetDetailsUseCase

CSV serialization:
successfully applied report snapshot/detail
→ deterministic Application CSV serializer
→ Desktop Windows-native save picker
→ Desktop writes user-approved destination
```

`ReportsViewModel` retains only `IReportOperations`. `ReportOperations` is the only report adapter retaining `IServiceScopeFactory`, matching M11–M16 root-safe patterns. The Page owns picker interaction because it has UI/window context; it does not calculate finance or query persistence.

Prefer one focused general report use case returning a coherent base snapshot with summary, category groups, and Account movement summaries for one currency/range. Account detail is a separate lazy read. Budget Performance reuses `GetBudgetDetailsUseCase` separately. Prefer one deterministic CSV serializer selected by report type rather than a generic export framework.

M17-B must add an explicit `Reports` navigation route/page/item and preserve the existing one-route/one-page mapping and selection consistency tests.

Do not introduce generic repositories, MediatR, CQRS/query buses, a reporting service framework, direct Desktop access to EF/SQLite/stores, or `IServiceProvider` outside the Operations scope adapter.

## Persistence and Performance Strategy

Add a focused `IReportStore` returning UI-neutral facts, implemented by `ReportStore` with `AsNoTracking` queries.

Recommended database work:

1. One grouped query (or a small fixed number of grouped queries) for Income, Expense, and Refund totals by exact category for currency/range. Refund category must be resolved by joining Refund → original Expense → Category.
2. One grouped projection for per-Account directional totals. Model both Transfer sides without per-Account queries; a fixed pair of source/destination projections is acceptable.
3. One unbounded but range-filtered, projected detail query only when required by the Accounts UI or Account Movement export. Do not hydrate Domain graphs or all-history Transactions.
4. Reuse the M16 persisted-Account currency discovery facts or an equivalent focused set-based query.
5. Reuse `GetBudgetDetailsUseCase`, `BudgetAllocationStore`, and `GetNetExpenseAmountsByCategoryAsync` unchanged for Budget Performance.

Aggregate arithmetic exposed to policy should use checked `long`. SQLite `SUM` can raise integer overflow before projection; tests must cover the chosen query/application boundary. Do not use bounded Recent Activity or the 500-row Activity query for totals or CSV. Do not issue one query per Account or Category. Existing indexes cover Transaction date, source, destination, category, and original transaction ID; M17-A does not authorize a schema/index migration. Measure before proposing a later composite-index migration.

## UI Recommendation

Add a calm native `ReportsPage` consistent with Suma typography, spacing, restrained secondary surfaces, and shell navigation.

Recommended hierarchy:

1. Page title and concise explanation.
2. Compact currency selector, start/end date pickers, and Apply/Refresh action.
3. Report-type selector: Cash Flow, Categories, Accounts, Budget.
4. Cash Flow summary with the exact concepts `Gross Income`, `Gross Expense`, `Refunds`, `Net Expense`, and `Net Cash Flow`. Emphasize `Net Cash Flow`; the other four are supporting values. Do not clamp negative values or reclassify Refunds/Transfers.
5. One focused scrollable report body for the selected type.
6. Secondary `Export CSV` action near the report controls/body, disabled for invalid/no-currency state and while loading/exporting.

Categories can switch between Expense and Income breakdowns inside the Categories report. Accounts shows movement summaries with required on-demand detail rather than recreating the Activity editor. Budget provides its own Budget selector and stored period/currency context.

Use vertical scrolling, adaptive wrapping/stacking of controls, no required horizontal page scroll, and compact list rows rather than DataGrid-heavy ERP presentation. At approximately 820×600 controls stack and all content/export remains reachable through vertical scroll; 1200×760 and 1280×800 may use wider summary arrangements. Do not redesign other pages.

Use the established serialized latest-wins base-load pattern keyed only by currency/start/end: one report load at a time, queued refresh retained, latest currency/date wins, stale results and errors ignored, all callers complete, loading drains, no lock across `await`, and deterministic `TaskCompletionSource` tests. A general report-type switch with an unchanged key does not reload.

The ViewModel separately preserves general selection and Budget selection. It tracks the successfully applied base key and Budget identity so export can never use stale data after a control change or failure.

## Deterministic Test Matrix

### Application

- Income only, Expense only, and combined positive/negative/zero Net Cash Flow.
- Inclusive start boundary, inclusive end boundary, and same-day range.
- `StartDate > EndDate` validation.
- Empty and future ranges.
- Full and partial Refund in range.
- Refund in range whose original Expense is outside range.
- Refund outside range whose original Expense is inside range.
- Refund attributed to original Expense category.
- Transfers excluded from Income/Expense/Net Cash Flow.
- Per-Account Transfer Out/In and all-Account zero net.
- Archived Account and Category historical names/status.
- Exact child Category isolation; no parent roll-up.
- PHP/USD isolation and no combined total.
- Pending/Skipped recurring excluded; Paid occurrence counted only through its Transaction.
- Savings and Expected Income excluded.
- Checked overflow for each aggregate and net formula.
- Deterministic group/detail ordering.
- Budget Performance parity with `GetBudgetDetailsUseCase`, including Refund after Budget period.
- Initial Month-to-date dates use `IDateProvider.Today`.
- Last-month, last-30-days (exactly 30 inclusive dates), and year-to-date preset calculations.
- Account summary order is name ordinal-ignore-case, then hidden AccountId.
- Exact five Cash Flow concepts and unchanged formulas.
- Budget initial selection follows current active, then most-recent deterministic ordering.

### Infrastructure

- Migration-backed SQLite with `MigrateAsync`; never `EnsureCreated`.
- Set-based category totals, Refund→original Expense Category join, inclusive dates, partial Refund, and currency isolation.
- Account directional aggregation including both Transfer sides.
- Archived Account/Category names remain readable.
- Unbounded selected-range result is not truncated at 200/500 rows.
- Query count remains fixed as Account/Category counts grow where practical to assert.
- Empty facts and SQLite/checked overflow behavior.
- No schema change or M17 migration.

### CSV

- Exact headers and column order for every report.
- Invariant two-decimal and exact minor-unit formatting, including zero and negative net values.
- Currency column.
- Comma, quote, CR, LF, leading/trailing spaces, and Unicode escaping/preservation.
- CRLF and UTF-8 BOM.
- Null/empty text.
- Empty Cash Flow emits its header plus exactly one zero summary row.
- Empty grouped/detail CSV emits its header only.
- Deterministic row and byte ordering.
- No internal IDs.
- Filename convention.

### Desktop

- Initial currency/range/report selection.
- Serialized overlapping loads with maximum concurrency one.
- Latest currency/date wins and stale results/errors cannot overwrite state.
- General report-type switching does not reload an unchanged base key.
- Account detail loads only on demand and uses the applied base key.
- Budget selection does not overwrite preserved general currency/date state.
- Current failure clears or retains only coherent same-selection data.
- Dirty/pending selection disables export; failed replacement cannot export the old snapshot under new controls.
- All callers complete; loading/exporting drains; retry works.
- Invalid date range prevents load/export and shows a useful error.
- Export cancel is a no-op; success/failure state is accurate.
- `ReportsViewModel` depends only on `IReportOperations`; `ReportOperations` owns the fresh async scope.
- Route/page/DI registration and navigation selection consistency.

### Runtime QA for M17-B

Use `SUMA_TEST_DATABASE_PATH`. Create real Income, Expense, Transfer, partial Refund, archived historical references, PHP, and USD data through the UI. Compare Reports against Activity and manual calculations; export every CSV type, open externally, verify Unicode/escaping/precision, cancel and replacement flows, restart unchanged, and test 820×600, 1200×760, and 1280×800. Remove temporary QA data afterward.

## Explicit Exclusions

- Report mutations or persisted aggregates.
- FX, exchange rates, and cross-currency totals/transfers.
- Transfers classified as Income or Expense.
- Refunds classified as Income.
- Parent/child Category roll-up.
- Pending/Skipped recurring amounts in actual reports.
- Savings attribution/targets in financial totals.
- Expected Income as actual Income.
- PDF, spreadsheet-native, cloud, email, telemetry, or scheduled export.
- Generic repository/export/query frameworks.
- Settings, PIN, Backup/Restore, Widgets, packaging, and M18/M19 work.

## M17-B Implementation Plan

1. Add UI-neutral report request/result/fact records, date/currency validation, checked formulas, and deterministic ordering in Application.
2. Add `IReportStore` and set-based `ReportStore` queries for range/currency category and Account movement summary facts, plus the focused lazy Account detail query.
3. Reuse M16 currency discovery behavior, initialize explicit Month-to-date dates from `IDateProvider`, and reuse M13 Budget Performance unchanged.
4. Add the focused invariant CSV serializer and filename builder with exact schemas above.
5. Register Application and Infrastructure services.
6. Add `IReportOperations` / `ReportOperations` with a fresh async scope per operation.
7. Add `ReportsViewModel` with the currency/date-keyed latest-wins base-load pump, separate lazy detail loading, preserved general/Budget selections, coherent failure handling, and applied-snapshot export eligibility.
8. Add `ReportsPage`, Windows-native save flow, Reports route/navigation, and responsive Suma-native layout.
9. Add Application, migration-backed Infrastructure, CSV, Desktop concurrency/architecture, and navigation tests.
10. Run full validation and isolated real WinUI/export QA; produce the M17 status report. Do not add a migration unless separately reviewed and authorized.

## Unresolved Questions

No financial-policy question blocks M17-B. This contract fixes event-date Refund semantics for general cash-flow reports and preserves original-Expense cohort semantics for Budget Performance.

One implementation detail must be verified against the installed Windows App SDK 2.4 API surface during M17-B: the exact save-picker type/owner initialization required for this unpackaged WinUI target. That verification must not change the user-selected destination, overwrite-confirmation, CSV, or architecture contract.

## M17-A Completion

Only this contract document was created. No production/test/project/schema/migration file was changed. M17-B and M18 were not started. Nothing was committed, pushed, or merged.
