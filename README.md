# 🥛 MilkWala - Milk Vendor & Customer Mobile Application

[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-10.0-512BD4?style=for-the-badge&logo=.net)](https://dotnet.microsoft.com/en-us/apps/maui)
[![C#](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![SQLite](https://img.shields.io/badge/SQLite-Offline--First-003B57?style=for-the-badge&logo=sqlite)](https://www.sqlite.org/)
[![MVVM](https://img.shields.io/badge/MVVM-CommunityToolkit-0078D4?style=for-the-badge)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
[![Architecture](https://img.shields.io/badge/Architecture-Offline--First-10B981?style=for-the-badge)]()
[![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)
[![CI](https://github.com/sagarworlds/MilkWala/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/sagarworlds/MilkWala/actions/workflows/ci.yml)

**MilkWala** is a modern, cross-platform mobile application built using **.NET 10 MAUI**, **SQLite**, **CommunityToolkit.Mvvm**, and an **Offline-First Architecture**. It seamlessly connects Milk Vendors, Delivery Staff, and Customers to automate daily milk subscriptions, delivery tracking, wallet payments, procurement, and inventory management—even in areas with limited or no internet connectivity.

---

## 🌟 Key Features

### 👨‍👩‍👧‍👦 Customer Portal
- **Interactive Dashboard**: Today's delivery status, wallet balance, monthly milk statistics, and recent delivery timeline.
- **Subscription Management**: Subscribe to Cow, Buffalo, Toned, Full Cream, or Organic milk with daily quantity controls and vacation hold dates.
- **Delivery Calendar**: Visual calendar grid showing status badges (`Delivered ✓`, `Skipped`, `Holiday`, `Extra Delivery`).
- **Wallet & Payments**: Real-time wallet balance tracking, quick UPI recharge simulation, and complete credit/debit transaction history.
- **Dairy Products E-Commerce Store**: Add-on catalog (Curd/Dahi, Paneer, Desi Ghee, Butter) scheduled alongside daily milk deliveries.
- **Complaint Module**: Raise quality, billing, or quantity support tickets with real-time status tracking (`Open`, `In Progress`, `Closed`).

### 🚚 Milk Vendor Portal
- **Control Center Hub**: Real-time business KPIs (Total Customers, Today's Deliveries, Collections, Outstanding Dues).
- **Customer Directory**: Add customers (with their daily milk subscription and route), activate or deactivate them. New customers start with an empty wallet and log in with their mobile number.
- **Delivery Management**: Daily delivery checklist with 1-tap `Delivered` / `Skipped` marking and GPS/proof options.
- **Farmer Procurement & Fat% Ledger**: Log raw milk purchases from farmers, test **Fat %** and **SNF %**, calculate rates automatically (`Base ₹35 + Fat% * ₹2.0`), add farmers, and pay them (oldest purchases are settled first).
- **Inventory & Expense Manager**: Track morning/evening stock balance against distributed milk, and record operational expenses (Fuel, Salary, Packaging, Maintenance).
- **Routes & Delivery Staff**: Add routes, add delivery people (they sign in with their mobile number and the Delivery role), change who delivers a route, and move customers between routes. A route that still has customers cannot be deleted.
- **Complaint Desk**: See every customer complaint, start work, and close it with a written answer (or reopen it).
- **Analytics & Financial Reports**: Total sales revenue, litres sold, cash/UPI collections, net profit metrics, and a "milk needed tomorrow" forecast.

### 🚴 Delivery Staff Module
- **Assigned Route Execution**: Delivery staff see only the routes assigned to them (today's deliveries for the customers on those routes) and mark each delivered or skipped with one tap, offline. The vendor creates routes, adds delivery staff and assigns them from the Customers tab ("Manage routes & delivery staff").

### ⚡ Offline-First & Sync Engine
- **Local SQLite Engine**: All C.R.U.D operations execute instantly (<500ms) against a local SQLite database (`MilkWala_v1.db`).
- **Background Sync Queue**: Changes are enqueued into a `SyncQueue` and automatically synced with the cloud when connectivity is restored.
- **Sync Header Bar**: Live online/offline status bar with an interactive network simulation toggle.

### 🛠️ Extra Business Utilities
- **WhatsApp Monthly Statements**: From the customer directory, open a ready-typed monthly statement (each delivery, totals, wallet and dues) in WhatsApp. It is plain text, not a PDF.
- **Demand Forecast**: Litres of each milk type to have ready tomorrow, counted with the same rules as delivery generation (active customers, alternate-day plans, vacation holds, approved extra orders). It is a rule-based count, not a prediction model.
- **Backup & Restore** (vendor): Create a consistent snapshot of all data while the app is running, share it off the phone, and restore it later. Restoring checks the file first and saves the current data as a pre-restore backup so it can be undone.
- **Languages** *(in progress)*: `LocalizationService` holds English, Marathi (मराठी) and Hindi (हिंदी) strings, but the screens are not translated yet and there is no language picker.

---

## 🏗️ Technical Architecture

```
MilkWala/
├── Controls/               # Reusable UI Controls (SyncHeaderView)
├── Converters/             # XAML Value Converters (StatusToColor, CurrencyFormat, StringHasText)
├── Models/                 # Domain Entities & SQLite Mappings
│   ├── Enums.cs            # MilkType, UserRole, DeliveryStatus, PaymentMode, ComplaintStatus
│   └── Entities.cs         # User, Customer, Vendor, Delivery, Subscription, Procurement, etc.
├── Services/               # Core Application & Data Services
│   ├── LocalDbService.cs   # SQLite Connection & Table Operations
│   ├── SyncEngineService.cs# Offline Mutation Queue Worker
│   ├── InvoiceService.cs   # Opens a customer's monthly statement in WhatsApp
│   ├── DemandForecastService.cs # Tomorrow's milk requirement
│   ├── BackupService.cs    # Database snapshot, share and safe restore
│   ├── MockDataSeeder.cs   # Sample Data Generator
│   └── LocalizationService.cs # English, Marathi, Hindi Translations
├── ViewModels/             # ViewModel Layer (CommunityToolkit.Mvvm)
│   ├── BaseViewModel.cs
│   ├── AuthViewModel.cs
│   ├── CustomerViewModels.cs & CustomerStoreViewModel.cs
│   ├── VendorViewModels.cs & VendorProcurementViewModel.cs
│   └── DeliveryBoyViewModel.cs
├── Views/                  # XAML Pages & Shell Navigation
│   ├── AuthPage.xaml
│   ├── AppShell.xaml
│   ├── Customer/           # Dashboard, Subscription, Calendar, Wallet, Complaint, Store
│   ├── Vendor/             # Dashboard, Customers, Deliveries, Inventory, Procurement, Reports
│   └── Delivery/           # DeliveryBoyPage
├── MauiProgram.cs          # Dependency Injection Registration
└── MilkWala.csproj         # Solution Configuration & NuGet Dependencies
```

---

## 💻 Prerequisites & Installation

### Requirements
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Version 10.0.302 or later)
- Visual Studio 2022 / VS Code with .NET MAUI Workload installed
- Windows 10/11 (Build 19041+), Android SDK 21+, or iOS 15+

### Build & Run Instructions

1. **Clone the repository**:
   ```bash
   git clone https://github.com/sagarworlds/MilkWala.git
   cd MilkWala
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the project for Windows**:
   ```bash
   dotnet build MilkWala.csproj -f net10.0-windows10.0.19041.0
   ```

4. **Run the application**:
   ```bash
   dotnet run MilkWala.csproj -f net10.0-windows10.0.19041.0
   ```

### Run the tests

The platform-independent logic (reporting maths, helpers) is covered by a plain `net10.0` xUnit project:

```bash
dotnet test tests/MilkWala.Tests/MilkWala.Tests.csproj
```

CI runs these tests and an Android build of the app on every pull request.

### Code conventions

- **Dates:** business dates and timestamps users see or filter by (deliveries, payments, expenses, orders) are **local time**; sync/audit metadata (`UpdatedAt`, sync queue and log times) is **UTC**. A test enforces this.
- **Signed-in user:** screens get the customer from `ICurrentUserService`; never hardcode an id.
- **Multi-step writes:** use `LocalDbService.RunInTransactionAsync` (or `InsertWithSyncAsync` / `UpdateWithSyncAsync`) so a change and its sync-queue entry commit together. Wallet, store and procurement rules live in `Services/Data/*Ledger.cs`.
- **Daily deliveries & billing:** each day's deliveries are generated from active subscriptions (vacation holds become `Holiday` rows; `Alternate` plans skip off days). Marking a delivery delivered charges the wallet first and puts any shortfall on the customer's dues; correcting it reverses the exact split; a recharge also pays off dues. Rules live in `Services/Scheduling` and `Services/Data/DeliveryLedger.cs`.
- **Messages:** `StatusMessage` (outcome of an action) and `ErrorMessage` (rule broken / failure) live on `BaseViewModel` and are shown in the shared header on every page. Throw `BusinessRuleException` for a rule the user can fix; its text is shown as is.
- **View-model commands:** wrap the body in `RunSafeAsync` so failures are logged and shown in the header banner instead of leaving the screen busy.

---

## 📊 Database Schema Summary

| Table | Purpose |
| :--- | :--- |
| `Users` & `LoginSessions` | User credentials, roles, and persistent offline auth session |
| `Customers` & `Vendors` | Customer master details, wallet balances, outstanding dues, and vendor stock |
| `Deliveries` | Daily delivery records, quantity, rate, delivery status, and timestamps |
| `Subscriptions` | Active customer subscriptions, milk type preferences, and vacation pause dates |
| `FarmerSuppliers` & `MilkProcurement` | Farmer purchase logs, Fat %, SNF %, and payout balances |
| `DairyProducts` & `StoreOrders` | Add-on dairy products catalog and scheduled store orders |
| `Payments` & `WalletTransactions` | Wallet recharge credits, delivery debits, and payment ledger |
| `Expenses` & `Complaints` | Vendor operational expenses and customer support tickets |
| `SyncQueue` & `SyncLogs` | Offline mutation queue, status tracking, and sync audit logs |

---

## ⭐ Show Your Support & 🐛 Report Issues

If you find **MilkWala** helpful or useful, please consider giving this repository a **Star ⭐**!

- 🌟 **Star this Repository**: [sagarworlds/MilkWala](https://github.com/sagarworlds/MilkWala)
- 🐛 **Report Issues or Request Features**: [MilkWala Issues](https://github.com/sagarworlds/MilkWala/issues)
- 🤝 **Contributions**: Pull requests, feedback, and feature suggestions are always welcome!

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
