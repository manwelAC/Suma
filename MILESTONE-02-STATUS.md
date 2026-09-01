# Suma — Milestone 02 Status

**Report date:** 2026-09-01  
**Milestone:** 02 — Domain Foundations  
**Branch:** `milestone/02-domain-foundations`  
**Overall status:** Implementation and validation complete; ready for orchestrator review.  
**Commit status:** Not committed, as requested.  
**Merge status:** Not merged into `main`.

## Executive Summary

Milestone 02 implements the first framework-independent Domain foundation for Suma:

- Entity identity
- Immutable money values using integer minor units
- Accounts and account behavior
- Categories and category behavior
- Account and category enums
- Real xUnit v3 domain tests

The full x64 solution builds with zero warnings and zero errors. The Domain test project discovers and passes all 38 test cases.

## Scope Completed

- [x] Minimal `Entity` base class with generated `Guid` identity
- [x] Immutable `Money` value object
- [x] `Account`
- [x] `AccountType`
- [x] `Category`
- [x] `CategoryTransactionKind`
- [x] Money unit tests
- [x] Account unit tests
- [x] Category unit tests
- [x] Removal of `.gitkeep` only from directories that now contain implementation files
- [x] xUnit v3 test discovery configuration for `Suma.Domain.Tests`
- [x] Restore, x64 build, and test validation

## Files Created

### Domain

```text
src/Suma.Domain/
├── Common/
│   └── Entity.cs
├── ValueObjects/
│   └── Money.cs
├── Accounts/
│   ├── Account.cs
│   └── AccountType.cs
└── Categories/
    ├── Category.cs
    └── CategoryTransactionKind.cs
```

### Tests

```text
tests/Suma.Domain.Tests/
├── ValueObjects/
│   └── MoneyTests.cs
├── Accounts/
│   └── AccountTests.cs
└── Categories/
    └── CategoryTests.cs
```

## Files Modified or Removed

Modified:

```text
tests/Suma.Domain.Tests/Suma.Domain.Tests.csproj
```

The test project now enables `TestingPlatformDotnetTestSupport` so .NET 10 `dotnet test` discovers the xUnit v3 tests.

Removed because their directories now contain real files:

```text
src/Suma.Domain/Accounts/.gitkeep
src/Suma.Domain/Categories/.gitkeep
src/Suma.Domain/Common/.gitkeep
src/Suma.Domain/ValueObjects/.gitkeep
```

No other `.gitkeep` files were removed.

## Domain Rules Implemented

### Entity

- Generates a non-empty `Guid` when an entity is created.
- Contains no timestamps, soft-delete fields, or persistence concerns.

### Money

- Stores amounts exclusively as `long AmountMinor`.
- Does not use `float` or `double`.
- Requires a three-letter ISO-style currency code.
- Trims and normalizes currency codes to uppercase.
- Allows positive, zero, and negative values.
- Is immutable and uses value equality.
- Supports addition and subtraction.
- Uses checked arithmetic to prevent silent integer overflow.
- Supports comparison operators and `IComparable<Money>`.
- Provides `Zero(currencyCode)`, `IsPositive`, `IsNegative`, and `IsZero`.
- Explicitly rejects arithmetic and comparison across different currencies.

### Account

- Requires a non-empty name.
- Trims the supplied name.
- Requires a defined `AccountType`.
- Stores opening balance as `Money`.
- Requires the opening-balance currency to match the normalized account currency.
- Allows positive, zero, and negative opening balances.
- New accounts are not archived.
- Provides `Archive()` and `Restore()` behavior.
- Provides `SetAvailableToSpendInclusion(bool)` behavior.
- Does not contain `CurrentBalance`; future balances remain transaction-derived.

### Category

- Requires a non-empty name.
- Trims the supplied name.
- Requires a defined `CategoryTransactionKind`.
- Supports an optional parent category and icon key.
- Rejects negative sort order.
- Prevents a category from using its own ID as its parent.
- New categories are not archived.
- Provides `Archive()` and `Restore()` behavior.
- Contains no EF Core attributes or database navigation properties.

## Enums Implemented

```text
AccountType:
- Cash
- Bank
- EWallet
- Savings
- Other

CategoryTransactionKind:
- Expense
- Income
```

`Transfer` was intentionally excluded from category kinds.

## Test Coverage

### Money — 18 cases

- Creation using minor units
- Value equality
- Addition
- Subtraction
- Positive values
- Negative values
- Zero factory and zero state
- Currency trimming and uppercase normalization
- Invalid currency rejection
- Mismatched-currency addition rejection
- Mismatched-currency subtraction rejection
- Same-currency ordering
- Mismatched-currency comparison rejection

### Account — 12 cases

- Valid creation and generated identity
- Null, empty, and whitespace name rejection
- Opening-balance currency mismatch rejection
- Positive, zero, and negative opening balances
- Archive
- Restore
- Available-to-Spend inclusion updates

### Category — 8 cases

- Valid creation and generated identity
- Null, empty, and whitespace name rejection
- Negative sort-order rejection
- Self-parent rejection
- Archive
- Restore

## Validation Results

Environment:

```text
.NET SDK: 10.0.400
xUnit v3: 3.2.2
Test runner: Microsoft Testing Platform v1
Target: net10.0 / x64
```

Restore:

```text
dotnet restore
Result: Succeeded
```

Build:

```text
dotnet build -p:Platform=x64
Result: Succeeded
Warnings: 0
Errors: 0
```

Tests:

```text
dotnet test -p:Platform=x64
Result: Succeeded
Passed: 38
Failed: 0
Skipped: 0
Total: 38
```

The Application and Infrastructure test projects remain intentionally empty and emit `No test is available` notices. These notices were reviewed, do not fail the command, and are outside Milestone 02's Domain test scope.

Formatting and static checks:

- `dotnet format --verify-no-changes` completed successfully.
- `git diff --check` found no whitespace errors.
- Git emitted an expected Windows LF-to-CRLF conversion notice for the modified test project file.
- No prohibited Domain framework references were found.
- No deferred `CurrentBalance`, repository, DbContext, DTO, or ViewModel symbols were introduced.

## Architectural Review

`Suma.Domain` remains framework-independent. Its project file has no package or project references.

No references were introduced to:

- Entity Framework Core
- SQLite
- WinUI
- Windows App SDK
- Microsoft.Extensions
- Infrastructure
- Desktop

No generic repository, DbContext, EF configuration, migration, DTO, ViewModel, or UI implementation was added.

### Notable Decision

`Category.SetParentCategory(Guid?)` was added as an explicit behavior method. Because entity IDs are generated internally, this method makes the self-parent invariant enforceable without exposing an ID setter or introducing persistence-oriented construction.

Standard argument and operation exceptions were sufficient for the current invariants, so no unnecessary domain exception hierarchy was created.

## Git Diff Summary

Current branch:

```text
milestone/02-domain-foundations
```

Working-tree summary:

```text
9 new C# files
527 new C# lines
1 modified test project file
4 removed .gitkeep files
No commit created
No merge performed
```

The working tree intentionally remains uncommitted for review.

## Deferred — Not Milestone 02 Defects

The following remain explicitly deferred:

- Transactions and transaction types
- Transfers and refunds
- Mutable or calculated account balances
- Budgets and budget allocations
- Recurring transactions and occurrences
- Savings goals and contributions
- Repository contracts and implementations
- EF Core entity configurations and migrations
- Available-to-Spend calculation
- Application use cases
- ViewModels and UI
- Widgets
- PIN security
- Reports, backup, restore, and exports

Milestone 03 has not been started.

## Orchestrator Review Checklist

- [ ] Review the Domain public APIs and invariants.
- [ ] Review `Money` currency and arithmetic behavior.
- [ ] Review the `Category.SetParentCategory` design decision.
- [ ] Review all 38 discovered and passing test cases.
- [ ] Confirm that the Application and Infrastructure empty-test notices remain acceptable.
- [ ] Approve or request changes.
- [ ] After approval, authorize the Git commit and subsequent integration workflow.

## Handoff Status

**Ready for review.** No commit, merge, push, or Milestone 03 work will be performed without approval.
