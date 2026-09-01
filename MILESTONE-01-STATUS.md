# Suma — Milestone 01 Status

**Report date:** 2026-09-01  
**Milestone:** 01 — Project foundation  
**Overall status:** Scaffold and environment validation complete; initial Git baseline commit remains pending.

## Accomplished

- [x] Inspected the local .NET, Windows SDK, and Visual Studio Build Tools environment.
- [x] Initialized the current workspace as a Git repository.
- [x] Created `Suma.sln`.
- [x] Created all five source projects:
  - `src/Suma.Domain`
  - `src/Suma.Application`
  - `src/Suma.Infrastructure`
  - `src/Suma.Desktop`
  - `src/Suma.Widgets`
- [x] Created all three test projects:
  - `tests/Suma.Domain.Tests`
  - `tests/Suma.Application.Tests`
  - `tests/Suma.Infrastructure.Tests`
- [x] Configured the required direct project references:

  ```text
  Suma.Application      -> Suma.Domain
  Suma.Infrastructure   -> Suma.Application, Suma.Domain
  Suma.Desktop          -> Suma.Application, Suma.Domain
  Suma.Widgets          -> Suma.Application
  Domain.Tests          -> Suma.Domain
  Application.Tests     -> Suma.Application
  Infrastructure.Tests  -> Suma.Infrastructure
  ```

- [x] Added central build settings through `Directory.Build.props`.
- [x] Added central NuGet version management through `Directory.Packages.props`.
- [x] Targeted .NET 10 through `global.json` and the project target frameworks.
- [x] Declared the foundational packages for WinUI 3, MVVM, EF Core with SQLite, dependency injection, configuration, Serilog, and xUnit.
- [x] Corrected `xunit.v3` from the unavailable version 3.1.4 to version 3.2.2.
- [x] Created the requested feature-based folder structure.
- [x] Added minimal Application and Infrastructure dependency-injection registration seams.
- [x] Added an empty EF Core `SumaDbContext` without entities, schema, or migrations.
- [x] Added a minimal unpackaged WinUI 3 application shell with `App.xaml` and `MainWindow.xaml`.
- [x] Created `Assets/Branding` without inventing or replacing the finalized logo.
- [x] Added a `.gitignore` covering:
  - .NET and IDE build output
  - WinUI/MSIX artifacts
  - SQLite databases, shared-memory, and WAL files
  - Suma backup files
  - Logs and local backup directories
- [x] Statically validated all eight project XML files.
- [x] Statically validated that every project reference resolves to an existing project.
- [x] Statically validated that every package reference has a centrally managed version.
- [x] Confirmed representative database, backup, log, and build files are ignored by Git.
- [x] Corrected `.gitignore` so wildcard patterns work properly.
- [x] Corrected `App.xaml.cs` to inherit explicitly from `Microsoft.UI.Xaml.Application`, resolving the conflict with the `Suma.Application` namespace.
- [x] Confirmed .NET SDK 10.0.400 is installed and detected.
- [x] Confirmed `dotnet restore` succeeds.
- [x] Confirmed `dotnet build -p:Platform=x64` succeeds.
- [x] Confirmed the `Suma.Desktop` WinUI/XAML project compiles successfully.
- [x] Confirmed the Suma WinUI desktop application launches successfully.
- [x] Confirmed `dotnet test -p:Platform=x64` succeeds.
- [x] Reviewed and accepted the current `No test is available` warnings because the Milestone 02 domain tests have not been implemented yet.
- [x] Renamed the Git branch from `master` to `main`.
- [x] Avoided implementing Milestone 02 domain entities or other out-of-scope product features.

## Current Unfixed Problems

### 1. The repository has no initial commit — repository baseline pending

Git is initialized, but there are no commits and all scaffold files are currently untracked.

Impact:

- There is no recoverable Milestone 01 baseline in Git history.
- Later changes cannot be cleanly compared with the initial scaffold.

Recommended action:

```powershell
git add .
git commit -m "Scaffold Suma project foundation"
```

Review staged files before committing. Do not commit database, backup, log, signing-key, or generated build files.

## Validation Summary

| Check | Status | Notes |
|---|---|---|
| Environment inspection | Complete | .NET SDK 10.0.400 detected |
| Solution/project structure | Complete | Eight project files present |
| Dependency graph | Complete | Static reference validation passed |
| Central package mapping | Complete | Static validation passed |
| Git ignore protection | Complete | Wildcard rules corrected and representative sensitive/generated files are ignored |
| NuGet restore | Complete | `dotnet restore` succeeds |
| Compilation | Complete | `dotnet build -p:Platform=x64` succeeds |
| WinUI/XAML compilation | Complete | `Suma.Desktop` compiles successfully |
| Desktop launch | Complete | Suma WinUI application launches successfully |
| Automated tests | Complete | Command succeeds; zero tests currently exist by design |
| Test warnings | Accepted | `No test is available` is expected until Milestone 02 tests are added |
| Git branch | Complete | Renamed from `master` to `main` |
| Initial Git commit | Pending | Repository contains only untracked files |

## Intentionally Deferred — Not Current Defects

The following items were intentionally excluded from Milestone 01 and should not be treated as missing fixes:

- Domain entity base class
- `Money` value object
- Financial enums
- `Account` and `Category` entities
- Final EF Core model and migrations
- Repository implementations
- Financial calculations
- Dashboard and production UI
- PIN security
- Widget implementation
- Recurring transaction engine
- Reports, backup/restore, and exports
- Production data or visual polish

## Exit Criteria for Milestone 01

Milestone 01 can be closed when all of the following are true:

- [x] A compatible .NET 10 SDK is installed and detected.
- [x] `dotnet restore` succeeds.
- [x] `dotnet build -p:Platform=x64` succeeds without errors.
- [x] `dotnet test -p:Platform=x64` succeeds.
- [x] The WinUI desktop project compiles successfully.
- [x] The WinUI desktop application launches successfully.
- [x] Build and test warnings have been reviewed and resolved or explicitly accepted.
- [ ] The validated scaffold is committed as the initial Git baseline.

## Next Recommended Milestone

Do not begin automatically. After Milestone 01 passes its exit criteria, request approval for:

```text
Milestone 02 — Domain foundations
- Entity base
- Money value object
- Enums
- Account
- Category
- Unit tests
```
