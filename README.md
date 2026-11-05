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
- **Customer Directory**: Add, edit, activate, or deactivate customer profiles and assign route sequences.
- **Delivery Management**: Daily delivery checklist with 1-tap `Delivered` / `Skipped` marking and GPS/proof options.
- **Farmer Procurement & Fat% Ledger**: Log raw milk purchases from farmers, test **Fat %** and **SNF %**, calculate rates automatically (`Base ₹35 + Fat% * ₹2.0`), and manage supplier payouts.
- **Inventory & Expense Manager**: Track morning/evening stock balance against distributed milk, and record operational expenses (Fuel, Salary, Packaging, Maintenance).
- **Analytics & Financial Reports**: Total sales revenue, litres sold, cash/UPI collections, net profit metrics, and AI-powered demand forecasting.

### 🚴 Delivery Staff Module
- **Assigned Route Execution**: View assigned route sequence, customer house list, 1-tap delivery status marking operating 100% offline.

### ⚡ Offline-First & Sync Engine
- **Local SQLite Engine**: All C.R.U.D operations execute instantly (<500ms) against a local SQLite database (`MilkWala_v1.db`).
- **Background Sync Queue**: Changes are enqueued into a `SyncQueue` and automatically synced with the cloud when connectivity is restored.
- **Sync Header Bar**: Live online/offline status bar with an interactive network simulation toggle.

### 🛠️ Extra Business Utilities
- **WhatsApp Monthly Invoices**: 1-click monthly PDF statement generator with direct WhatsApp share links.
- **AI Demand Forecasting**: Predicts exact litres of each milk type needed for tomorrow based on active subscriptions, vacation holds, and special orders.
- **SQLite Local Backup & Restore**: 1-click timestamped database snapshot export and restore.
- **Multi-Language Support**: Built-in dynamic translation for **English**, **Marathi (मराठी)**, and **Hindi (हिंदी)**.

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
│   ├── InvoiceService.cs   # WhatsApp Invoice & Statement Generator
│   ├── DemandForecastService.cs # AI Stock Requirement Predictor
│   ├── BackupService.cs    # Local SQLite DB Snapshot Backup & Restore
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
