using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Models;
using MilkWala.Services;
using MilkWala.Services.Data;

namespace MilkWala.ViewModels
{
    public partial class VendorProcurementViewModel : BaseViewModel
    {
        [ObservableProperty]
        private double _litresProcured = 100;

        [ObservableProperty]
        private double _fatPercent = 4.5;

        [ObservableProperty]
        private double _snfPercent = 8.5;

        [ObservableProperty]
        private decimal _calculatedRate = 45.00m;

        [ObservableProperty]
        private decimal _totalProcurementAmount;

        [ObservableProperty]
        private FarmerSupplier? _selectedSupplier;

        [ObservableProperty]
        private ObservableCollection<FarmerSupplier> _suppliers = new();

        [ObservableProperty]
        private ObservableCollection<MilkProcurement> _procurements = new();

        // --- payout
        [ObservableProperty]
        private decimal _selectedSupplierOutstanding;

        [ObservableProperty]
        private decimal _payoutAmount;

        // --- add farmer
        [ObservableProperty]
        private string _newSupplierName = string.Empty;

        [ObservableProperty]
        private string _newSupplierMobile = string.Empty;

        [ObservableProperty]
        private MilkType _newSupplierMilkType = MilkType.Cow;

        [ObservableProperty]
        private double _newSupplierFatPercent = 4.2;

        [ObservableProperty]
        private decimal _newSupplierRate = 42m;

        public ObservableCollection<MilkType> AvailableMilkTypes { get; } = new()
        {
            MilkType.Cow, MilkType.Buffalo, MilkType.Toned, MilkType.FullCream, MilkType.Organic
        };


        public VendorProcurementViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory)
            : base(dbService, syncEngine, localization, loggerFactory)
        {
            Title = "Farmer Milk Procurement";
        }

        [RelayCommand]
        public async Task LoadProcurementsAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var supplierList = await DbService.GetAllAsync<FarmerSupplier>();
                Suppliers.Clear();
                foreach (var s in supplierList)
                {
                    Suppliers.Add(s);
                }
                // The list holds fresh objects after every reload, so re-select the same supplier by id
                // (keeps the picker in step and shows the updated outstanding payout).
                var selectedId = SelectedSupplier?.Id;
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == selectedId) ?? Suppliers.FirstOrDefault();

                var list = await DbService.GetAllAsync<MilkProcurement>();
                Procurements.Clear();
                foreach (var p in list.OrderByDescending(x => x.Date))
                {
                    Procurements.Add(p);
                }

                CalculateRate();
            });
        }

        // Picking a farmer shows what is owed and pre-fills the payment with the full amount.
        partial void OnSelectedSupplierChanged(FarmerSupplier? value)
        {
            SelectedSupplierOutstanding = value?.OutstandingPayout ?? 0m;
            PayoutAmount = SelectedSupplierOutstanding;
        }

        // Keep the on-screen rate/total preview live while the vendor edits the inputs.
        partial void OnFatPercentChanged(double value) => CalculateRate();

        partial void OnLitresProcuredChanged(double value) => CalculateRate();

        public void CalculateRate()
        {
            // Standard Fat formula: Base Rate ₹35 + (Fat% * ₹2.0)
            CalculatedRate = 35.00m + (decimal)(FatPercent * 2.0);
            TotalProcurementAmount = (decimal)LitresProcured * CalculatedRate;
        }

        [RelayCommand]
        public async Task RecordProcurementAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (SelectedSupplier == null || LitresProcured <= 0)
                {
                    StatusMessage = "Please select a farmer supplier and enter litres.";
                    return;
                }

                await DbService.InitAsync();

                CalculateRate();

                var supplierId = SelectedSupplier.Id;
                var litres = LitresProcured;
                var fat = FatPercent;
                var snf = SnfPercent;
                var rate = CalculatedRate;
                var recordedAt = DateTime.Now;

                // The purchase, the supplier's higher payout and both sync entries commit together or not at all.
                MilkProcurement? proc = null;
                await DbService.RunInTransactionAsync(connection =>
                {
                    proc = ProcurementLedger.Record(connection, supplierId, litres, fat, snf, rate, recordedAt);
                });

                StatusMessage = $"Procurement logged: {LitresProcured}L @ ₹{CalculatedRate:F2}/L (Total ₹{proc!.TotalAmount:N2})";
                await LoadProcurementsAsync();
            });
        }

        [RelayCommand]
        public async Task AddSupplierAsync()
        {
            await RunSafeAsync(async () =>
            {
                FarmerSupplier? supplier = null;
                try
                {
                    await DbService.RunInTransactionAsync(connection =>
                        supplier = SupplierLedger.Add(
                            connection, NewSupplierName, NewSupplierMobile, NewSupplierMilkType,
                            NewSupplierFatPercent, NewSupplierRate));
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                NewSupplierName = string.Empty;
                NewSupplierMobile = string.Empty;
                StatusMessage = $"{supplier!.Name} added as a supplier.";

                await LoadProcurementsAsync();
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == supplier.Id) ?? SelectedSupplier;
            });
        }

        [RelayCommand]
        public async Task PaySupplierAsync()
        {
            await RunSafeAsync(async () =>
            {
                if (SelectedSupplier == null)
                {
                    ErrorMessage = "Select the farmer you are paying.";
                    return;
                }

                var supplierId = SelectedSupplier.Id;
                var amount = PayoutAmount;

                SupplierLedger.PayoutResult? result = null;
                try
                {
                    await DbService.RunInTransactionAsync(connection =>
                        result = SupplierLedger.Pay(connection, supplierId, amount));
                }
                catch (BusinessRuleException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                StatusMessage = $"Paid ₹{amount:N2} to {result!.Supplier.Name}. " +
                    $"{result.PurchasesSettled} purchase(s) fully settled; ₹{result.Supplier.OutstandingPayout:N2} still owed.";
                await LoadProcurementsAsync();
            });
        }
    }
}
