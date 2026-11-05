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
using MilkWala.Services.Identity;

namespace MilkWala.ViewModels
{
    public partial class CustomerStoreViewModel : CustomerViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<DairyProduct> _dairyProducts = new();

        [ObservableProperty]
        private ObservableCollection<StoreOrder> _myStoreOrders = new();


        public CustomerStoreViewModel(
                LocalDbService dbService,
                SyncEngineService syncEngine,
                LocalizationService localization,
                ILoggerFactory loggerFactory,
                ICurrentUserService currentUser)
            : base(dbService, syncEngine, localization, loggerFactory, currentUser)
        {
            Title = "Dairy Products Store";
        }

        [RelayCommand]
        public async Task LoadStoreCatalogAsync()
        {
            await RunSafeAsync(async () =>
            {
                await DbService.InitAsync();

                var products = await DbService.GetAllAsync<DairyProduct>();
                DairyProducts.Clear();
                foreach (var p in products)
                {
                    DairyProducts.Add(p);
                }

                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                var orders = await DbService.GetAllAsync<StoreOrder>();
                MyStoreOrders.Clear();
                foreach (var o in orders.Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.OrderDate))
                {
                    MyStoreOrders.Add(o);
                }
            });
        }

        [RelayCommand]
        public async Task OrderProductAsync(DairyProduct product)
        {
            await RunSafeAsync(async () =>
            {
                if (product == null) return;

                var customer = await RequireCustomerAsync();
                if (customer == null) return;

                try
                {
                    // Payment, order and sync entries commit together or not at all; the balance is
                    // re-checked inside the transaction so a stale screen cannot overspend the wallet.
                    await DbService.RunInTransactionAsync(connection =>
                    {
                        StoreOrderLedger.Place(connection, customer.Id, product);
                    });
                }
                catch (InsufficientBalanceException)
                {
                    ErrorMessage = $"Insufficient wallet balance for {product.Name}. Please recharge.";
                    return;
                }

                StatusMessage = $"Ordered {product.Name}! Will be delivered with morning milk.";
                await LoadStoreCatalogAsync();
            });
        }
    }
}
