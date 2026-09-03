<div align="center">

  <img src="./src/Suma.Desktop/Assets/Branding/SUMALOGO.png" alt="Suma Logo" width="96" height="96" />

  # Suma

  **Money, made clear.**

  A local-first, privacy-focused Windows personal finance desktop app crafted with .NET 10 and WinUI 3.

  <br />

  <img src="./src/Suma.Desktop/Assets/Mascot/sumowelcome.png" alt="Sumo Welcoming You" width="220" />

  <br />

  [![Platform: Windows 10 / 11](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows&logoColor=white)](#)
  [![Framework: .NET 10](https://img.shields.io/badge/Framework-.NET%2010-512BD4?logo=dotnet&logoColor=white)](#)
  [![UI: WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-2D7D9A)](#)
  [![Database: SQLite + EF Core](https://img.shields.io/badge/Database-SQLite%20%2B%20EF%20Core-003B57?logo=sqlite&logoColor=white)](#)
  [![Tests: 496 Passing](https://img.shields.io/badge/Tests-496%20Passing-brightgreen)](#)

</div>

---

## Overview

**Suma** is a modern desktop personal finance application built to eliminate financial anxiety through unambiguous financial clarity. Unlike traditional budgeting tools that overwhelm users with convoluted spreadsheets or cloud-connected services that compromise privacy, Suma runs **100% locally on your machine**.

With Suma's **Available to Spend (ATS)** engine, you always know exactly what money is truly safe to spend today after reserving funds for upcoming bills and protected budget allocations.

---

## Meet Sumo — Your Financial Guardian & Companion

Meet **Sumo**, our friendly mascot who accompanies you throughout Suma. Sumo guards your data locally, monitors your budget envelopes, and celebrates your financial milestones.

<div align="center">

| Spending Clarity | Savings Goals | Local Privacy | Goal Celebration |
| :---: | :---: | :---: | :---: |
| <img src="./src/Suma.Desktop/Assets/Mascot/sumodashboard.png" width="190" alt="Sumo Dashboard" /> | <img src="./src/Suma.Desktop/Assets/Mascot/coinsaving.png" width="190" alt="Sumo Savings" /> | <img src="./src/Suma.Desktop/Assets/Mascot/protected.png" width="190" alt="Sumo Security" /> | <img src="./src/Suma.Desktop/Assets/Mascot/approved.png" width="190" alt="Sumo Approved" /> |
| **Available to Spend**<br />Know your true spendable balance | **Milestone Goals**<br />Watch savings grow coin-by-coin | **PIN Protected**<br />Your data stays on this device | **Unlock Verified**<br />Celebrate financial progress |

</div>

---

## Core Features

### 🟢 Available to Spend (ATS)
- Clear financial snapshot calculated as:
  $$\text{Available to Spend} = \text{Included Account Balances} - \text{Protected Budget Remaining}$$
- Immediate visibility into your actual disposable funds, preventing accidental overspending.
- Real-time breakdowns of included balances vs. protected reserves.

### 💼 Account Management
- Multi-account tracking across **Cash**, **Bank**, **E-Wallet**, **Savings**, and **Other**.
- Toggle individual accounts in or out of the ATS calculation.
- Archive old or inactive accounts while preserving historical ledger accuracy.
- Strict currency isolation per account to prevent accidental cross-currency calculations.

### 🎯 Planning & Budgets
- Flexible period-based budgets with category allocations.
- Protected allocations prevent essential funds from being treated as spendable.
- Visual spending bars and real-time remaining allowance indicators.

### 🏆 Savings Goals
- Set target amounts, deadlines, and track cumulative deposits and withdrawals.
- Progress bars and completion ratios to motivate your financial milestones.

### 📅 Recurring Bills & Scheduled Obligations
- Automated schedule detection and recurring occurrence tracking.
- Upcoming bill reminders so nothing slips through the cracks.

### 📝 Activity Ledger
- Complete transaction ledger supporting **Income**, **Expense**, **Transfer**, and **Refund**.
- Rich categorization, merchant descriptions, and date filtering.

### 🔒 Local-First Privacy & Security
- **Your data never leaves your device**: No external analytics, cloud tracking, or mandatory accounts.
- **PIN Lock Protection**: Optional local numeric PIN verification powered by modern cryptographic key derivation with Sumo standing guard.
- **Atomic Backup & Restore**: Robust backup and restore transactions with phase-validated state verification and recovery safety.

### 🖥️ Desktop-Native Design System
- Built natively with **WinUI 3** and **Windows App SDK**.
- Tailored sage green visual aesthetic (`#6F806D`) with soft cream surfaces, subtle 1px outlines, and smooth typography.
- Fully responsive multi-column layouts adapting seamlessly across fullscreen desktop, standard desktop, and compact windows.

---

## Architecture

Suma follows strict **Clean Architecture** and **Domain-Driven Design (DDD)** principles:

```
Suma/
├── src/
│   ├── Suma.Domain/          # Core entities, ValueObjects, financial invariants, ATS formulas
│   ├── Suma.Application/     # Use cases, CQRS handlers, queries, store abstractions
│   ├── Suma.Infrastructure/  # EF Core SQLite storage, migrations, security, backup transactions
│   ├── Suma.Desktop/         # WinUI 3 XAML UI, MVVM ViewModels, custom dialogs, assets
│   └── Suma.Widgets/         # Windows Widget Board integration components
└── tests/
    ├── Suma.Domain.Tests/          # 212 tests (Domain invariants, money, math)
    ├── Suma.Application.Tests/     # 159 tests (Use-case flows, queries, validators)
    ├── Suma.Infrastructure.Tests/  # 51 tests (EF Core persistence, backups, restores)
    └── Suma.Desktop.Tests/         # 74 tests (MVVM logic, navigation, formatting)
```

---

## Getting Started

### Prerequisites
- **Windows 10** (version 2004 / build 19041 or higher) or **Windows 11**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Running Locally

Clone the repository and run the desktop application using the `x64` platform flag:

```powershell
# Clone the repository
git clone https://github.com/manwelAC/Suma.git
cd Suma

# Run the desktop application
dotnet run --project .\src\Suma.Desktop\Suma.Desktop.csproj -p:Platform=x64
```

> **Note**: WinUI 3 self-contained projects require targeting an architecture (`-p:Platform=x64`).

### Running Tests

Execute the complete test suite across all architectural layers:

```powershell
dotnet test Suma.sln -p:Platform=x64
```

### Code Formatting

Check or apply solution-wide code style formatting:

```powershell
# Verify formatting
dotnet format Suma.sln --verify-no-changes

# Format all files
dotnet format Suma.sln
```

---

## Project Structure & Key Technologies

| Technology | Purpose |
| :--- | :--- |
| **C# 13 / 14 & .NET 10** | Core programming language and runtime platform |
| **WinUI 3 / Windows App SDK** | Modern Windows desktop presentation layer and controls |
| **Entity Framework Core 10** | ORM and data modeling |
| **SQLite** | Fast, local-first transactional database engine |
| **CommunityToolkit.Mvvm** | ObservableObject, relay commands, and MVVM patterns |
| **Serilog** | Structured application diagnostics and logging |
| **xUnit** | Comprehensive unit, integration, and architecture test suite |

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
