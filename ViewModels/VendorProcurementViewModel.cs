using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilkWala.Models;
using MilkWala.Services;

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

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public VendorProcurementViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Farmer Milk Procurement";
        }

        [RelayCommand]
        public async Task LoadProcurementsAsync()
        {
            IsBusy = true;
            await DbService.InitAsync();

            var supplierList = await DbService.GetAllAsync<FarmerSupplier>();
            Suppliers.Clear();
            foreach (var s in supplierList)
            {
                Suppliers.Add(s);
            }
            if (Suppliers.Count > 0 && SelectedSupplier == null)
            {
                SelectedSupplier = Suppliers.First();
            }

            var list = await DbService.GetAllAsync<MilkProcurement>();
            Procurements.Clear();
            foreach (var p in list.OrderByDescending(x => x.Date))
            {
                Procurements.Add(p);
            }

            CalculateRate();
            IsBusy = false;
        }

        public void CalculateRate()
        {
            // Standard Fat formula: Base Rate ₹35 + (Fat% * ₹2.0)
            CalculatedRate = 35.00m + (decimal)(FatPercent * 2.0);
            TotalProcurementAmount = (decimal)LitresProcured * CalculatedRate;
        }

        [RelayCommand]
        public async Task RecordProcurementAsync()
        {
            if (SelectedSupplier == null || LitresProcured <= 0)
            {
                StatusMessage = "Please select a farmer supplier and enter litres.";
                return;
            }

            IsBusy = true;
            await DbService.InitAsync();

            CalculateRate();

            var proc = new MilkProcurement
            {
                SupplierId = SelectedSupplier.Id,
                SupplierName = SelectedSupplier.Name,
                Date = DateTime.Now,
                MilkType = SelectedSupplier.MilkType,
                Litres = LitresProcured,
                FatPercent = FatPercent,
                SnfPercent = SnfPercent,
                RatePerLitre = CalculatedRate,
                IsPaid = false
            };

            await DbService.InsertAsync(proc);
            await DbService.EnqueueSyncAsync("MilkProcurement", proc.Id, SyncOperation.Insert, proc);

            SelectedSupplier.OutstandingPayout += proc.TotalAmount;
            await DbService.UpdateAsync(SelectedSupplier);

            StatusMessage = $"Procurement logged: {LitresProcured}L @ ₹{CalculatedRate:F2}/L (Total ₹{proc.TotalAmount:N2})";
            await LoadProcurementsAsync();
            IsBusy = false;
        }
    }
}
