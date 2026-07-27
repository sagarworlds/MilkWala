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
    public partial class CustomerStoreViewModel : BaseViewModel
    {
        [ObservableProperty]
        private ObservableCollection<DairyProduct> _dairyProducts = new();

        [ObservableProperty]
        private ObservableCollection<StoreOrder> _myStoreOrders = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public CustomerStoreViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
            : base(dbService, syncEngine, localization)
        {
            Title = "Dairy Products Store";
        }

        [RelayCommand]
        public async Task LoadStoreCatalogAsync()
        {
            IsBusy = true;
            await DbService.InitAsync();

            var products = await DbService.GetAllAsync<DairyProduct>();
            DairyProducts.Clear();
            foreach (var p in products)
            {
                DairyProducts.Add(p);
            }

            var orders = await DbService.GetAllAsync<StoreOrder>();
            MyStoreOrders.Clear();
            foreach (var o in orders.Where(x => x.CustomerId == 1).OrderByDescending(x => x.OrderDate))
            {
                MyStoreOrders.Add(o);
            }

            IsBusy = false;
        }

        [RelayCommand]
        public async Task OrderProductAsync(DairyProduct product)
        {
            if (product == null) return;

            IsBusy = true;
            await DbService.InitAsync();

            var customer = await DbService.GetCustomerByUserIdAsync(1);
            if (customer != null && customer.WalletBalance < product.Price)
            {
                StatusMessage = $"Insufficient wallet balance for {product.Name}. Please recharge.";
                IsBusy = false;
                return;
            }

            var order = new StoreOrder
            {
                CustomerId = 1,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                TotalPrice = product.Price,
                OrderDate = DateTime.Now,
                Status = "Scheduled with tomorrow's milk"
            };

            await DbService.InsertAsync(order);
            await DbService.EnqueueSyncAsync("StoreOrders", order.Id, SyncOperation.Insert, order);

            if (customer != null)
            {
                customer.WalletBalance -= product.Price;
                await DbService.UpdateAsync(customer);

                var tx = new WalletTransaction
                {
                    CustomerId = 1,
                    Amount = -product.Price,
                    TransactionType = "Debit",
                    Description = $"Purchased {product.Name} from Store",
                    Date = DateTime.Now,
                    BalanceAfter = customer.WalletBalance
                };
                await DbService.InsertAsync(tx);
            }

            StatusMessage = $"Ordered {product.Name}! Will be delivered with morning milk.";
            await LoadStoreCatalogAsync();
            IsBusy = false;
        }
    }
}
