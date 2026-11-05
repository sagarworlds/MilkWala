using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkWala.Helpers;
using MilkWala.Models;

namespace MilkWala.Services
{
    public class MockDataSeeder
    {
        private readonly LocalDbService _dbService;
        private readonly ILogger<MockDataSeeder> _logger;
        private readonly SingleFlightInitializer _initializer;

        public MockDataSeeder(LocalDbService dbService, ILogger<MockDataSeeder> logger)
        {
            _dbService = dbService;
            _logger = logger;
            _initializer = new SingleFlightInitializer(SeedCoreAsync);
        }

        /// <summary>
        /// Seeds the demo data once. The app starts this in the background and the login flow awaits it, so
        /// no screen can read the database before seeding has finished and both callers share a single run.
        /// </summary>
        /// <exception cref="Exception">Rethrows the underlying database error after logging it; a later call retries.</exception>
        public Task EnsureSeededAsync() => _initializer.EnsureAsync();

        private async Task SeedCoreAsync()
        {
            try
            {
                await SeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seeding demo data failed");
                throw;
            }
        }

        private async Task SeedAsync()
        {
            await _dbService.InitAsync();

            var users = await _dbService.GetAllAsync<User>();
            if (users.Count > 0)
                return; // Data already seeded

            // 1. Create Users
            var customerUser = new User { Id = 1, Name = "Rahul Sharma", MobileNumber = "9876543210", Role = UserRole.Customer };
            var vendorUser = new User { Id = 2, Name = "Ganesh Dairy Vendor", MobileNumber = "9988776655", Role = UserRole.Vendor };
            var deliveryUser = new User { Id = 3, Name = "Amit Delivery", MobileNumber = "9123456789", Role = UserRole.DeliveryBoy };

            await _dbService.InsertAsync(customerUser);
            await _dbService.InsertAsync(vendorUser);
            await _dbService.InsertAsync(deliveryUser);

            // 2. Products
            var products = new List<Product>
            {
                new Product { MilkType = MilkType.Cow, Name = "Fresh Cow Milk", PricePerLitre = 62.00m, Description = "Pure natural farm cow milk" },
                new Product { MilkType = MilkType.Buffalo, Name = "Pure Buffalo Milk", PricePerLitre = 76.00m, Description = "Rich high fat buffalo milk" },
                new Product { MilkType = MilkType.Toned, Name = "Toned Milk", PricePerLitre = 52.00m, Description = "Low fat standardized milk" },
                new Product { MilkType = MilkType.FullCream, Name = "Full Cream Milk", PricePerLitre = 70.00m, Description = "Thick high cream milk" },
                new Product { MilkType = MilkType.Organic, Name = "A2 Organic Milk", PricePerLitre = 95.00m, Description = "Certified organic grass-fed A2 milk" }
            };
            foreach (var p in products)
                await _dbService.InsertAsync(p);

            // 3. Delivery Route
            var route1 = new DeliveryRoute { Id = 1, RouteName = "Route A - Model Town & Green Park", AreaDescription = "Sector 14 to 22", AssignedDeliveryBoyId = 3, SequenceOrder = 1 };
            var route2 = new DeliveryRoute { Id = 2, RouteName = "Route B - Sunshine Heights & Colony", AreaDescription = "Main Road West", AssignedDeliveryBoyId = 3, SequenceOrder = 2 };
            await _dbService.InsertAsync(route1);
            await _dbService.InsertAsync(route2);

            // 4. Vendor Details
            var vendor = new Vendor
            {
                Id = 1,
                BusinessName = "MilkWala Dairy Services",
                OwnerName = "Ganesh Patil",
                Mobile = "9988776655",
                Address = "Plot 45, Dairy Complex, City",
                TotalMorningStockLitres = 350.0m,
                TotalEveningStockLitres = 200.0m
            };
            await _dbService.InsertAsync(vendor);

            // 5. Customers
            var c1 = new Customer
            {
                UserId = 1,
                Name = "Rahul Sharma",
                Mobile = "9876543210",
                AlternateContact = "9876543211",
                Address = "Flat 402, Green Park Apartments, Sector 15",
                RouteId = 1,
                PreferredDeliveryTime = "Morning",
                PreferredMilkType = MilkType.Cow,
                PreferredQuantity = 1.5,
                WalletBalance = 1250.00m,
                OutstandingBalance = 0.00m,
                IsActive = true
            };
            var c2 = new Customer
            {
                UserId = 4,
                Name = "Priya Deshmukh",
                Mobile = "9822110033",
                AlternateContact = "9822110034",
                Address = "Bunglow No 12, Sunshine Colony",
                RouteId = 1,
                PreferredDeliveryTime = "Morning",
                PreferredMilkType = MilkType.Buffalo,
                PreferredQuantity = 2.0,
                WalletBalance = 450.00m,
                OutstandingBalance = 180.00m,
                IsActive = true
            };
            var c3 = new Customer
            {
                UserId = 5,
                Name = "Suresh Verma",
                Mobile = "9711223344",
                Address = "House 88, Model Town",
                RouteId = 2,
                PreferredDeliveryTime = "Both",
                PreferredMilkType = MilkType.Toned,
                PreferredQuantity = 1.0,
                WalletBalance = 800.00m,
                OutstandingBalance = 0.00m,
                IsActive = true
            };
            var c4 = new Customer
            {
                UserId = 6,
                Name = "Anita Kulkarni",
                Mobile = "9944556677",
                Address = "Flat 101, Om Sai Residency",
                RouteId = 2,
                PreferredDeliveryTime = "Morning",
                PreferredMilkType = MilkType.Organic,
                PreferredQuantity = 1.0,
                WalletBalance = 2100.00m,
                OutstandingBalance = 0.00m,
                IsActive = true
            };

            await _dbService.InsertAsync(c1);
            await _dbService.InsertAsync(c2);
            await _dbService.InsertAsync(c3);
            await _dbService.InsertAsync(c4);

            // 6. Subscriptions
            var sub1 = new Subscription { CustomerId = c1.Id, MilkType = MilkType.Cow, QuantityLitres = 1.5, IsPaused = false };
            var sub2 = new Subscription { CustomerId = c2.Id, MilkType = MilkType.Buffalo, QuantityLitres = 2.0, IsPaused = false };
            var sub3 = new Subscription { CustomerId = c3.Id, MilkType = MilkType.Toned, QuantityLitres = 1.0, IsPaused = false };
            var sub4 = new Subscription { CustomerId = c4.Id, MilkType = MilkType.Organic, QuantityLitres = 1.0, IsPaused = false };
            await _dbService.InsertAsync(sub1);
            await _dbService.InsertAsync(sub2);
            await _dbService.InsertAsync(sub3);
            await _dbService.InsertAsync(sub4);

            // 7. Month Deliveries for Rahul Sharma (Customer 1)
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            int days = (today - startOfMonth).Days + 1;

            for (int i = 0; i < days; i++)
            {
                var delDate = startOfMonth.AddDays(i);
                DeliveryStatus status = DeliveryStatus.Delivered;
                double qty = 1.5;

                if (i == 3) { status = DeliveryStatus.Skipped; qty = 0; }
                else if (i == 7) { status = DeliveryStatus.Holiday; qty = 0; }
                else if (i == 10) { status = DeliveryStatus.Extra; qty = 3.0; }
                else if (delDate == today) { status = DeliveryStatus.Delivered; }

                var d = new Delivery
                {
                    CustomerId = c1.Id,
                    CustomerName = c1.Name,
                    Address = c1.Address,
                    DeliveryDate = delDate,
                    MilkType = MilkType.Cow,
                    QuantityLitres = qty,
                    RatePerLitre = 62.00m,
                    Status = status,
                    DeliveredAt = delDate.AddHours(6).AddMinutes(30)
                };
                await _dbService.InsertAsync(d);
            }

            // Deliveries for other customers today
            await _dbService.InsertAsync(new Delivery { CustomerId = c2.Id, CustomerName = c2.Name, Address = c2.Address, DeliveryDate = today, MilkType = MilkType.Buffalo, QuantityLitres = 2.0, RatePerLitre = 76.00m, Status = DeliveryStatus.Delivered, DeliveredAt = today.AddHours(7) });
            await _dbService.InsertAsync(new Delivery { CustomerId = c3.Id, CustomerName = c3.Name, Address = c3.Address, DeliveryDate = today, MilkType = MilkType.Toned, QuantityLitres = 1.0, RatePerLitre = 52.00m, Status = DeliveryStatus.Pending });
            await _dbService.InsertAsync(new Delivery { CustomerId = c4.Id, CustomerName = c4.Name, Address = c4.Address, DeliveryDate = today, MilkType = MilkType.Organic, QuantityLitres = 1.0, RatePerLitre = 95.00m, Status = DeliveryStatus.Delivered, DeliveredAt = today.AddHours(7).AddMinutes(15) });

            // 8. Wallet Transactions
            await _dbService.InsertAsync(new WalletTransaction { CustomerId = c1.Id, Amount = 2000.00m, TransactionType = "Credit", Description = "Wallet Recharge via UPI", Date = today.AddDays(-15), BalanceAfter = 2000.00m });
            await _dbService.InsertAsync(new WalletTransaction { CustomerId = c1.Id, Amount = -750.00m, TransactionType = "Debit", Description = "Monthly Bill Deduction", Date = today.AddDays(-5), BalanceAfter = 1250.00m });

            // 9. Expenses
            await _dbService.InsertAsync(new Expense { Category = ExpenseCategory.Fuel, Amount = 450.00m, Description = "Delivery Vehicle Fuel", Date = today.AddDays(-2) });
            await _dbService.InsertAsync(new Expense { Category = ExpenseCategory.Packaging, Amount = 1200.00m, Description = "Milk Pouches & Seals", Date = today.AddDays(-10) });

            // 10. Complaint
            await _dbService.InsertAsync(new Complaint
            {
                CustomerId = c1.Id,
                CustomerName = c1.Name,
                Category = ComplaintCategory.WrongQuantity,
                Subject = "Received 1 Litre instead of 1.5 Litres",
                Description = "Morning delivery bag contained only 1 pouch.",
                Status = ComplaintStatus.Closed,
                ResolutionNotes = "Delivered extra 0.5L packet in evening delivery.",
                CreatedAt = today.AddDays(-6)
            });

            // 11. Farmer Suppliers & Procurement
            var f1 = new FarmerSupplier { Id = 1, Name = "Ramesh Farmer (Village Farm)", Mobile = "9811223344", MilkType = MilkType.Cow, DefaultFatPercent = 4.3, DefaultRatePerLitre = 44.00m, OutstandingPayout = 1200.00m };
            var f2 = new FarmerSupplier { Id = 2, Name = "Suresh Dairy Farm", Mobile = "9822334455", MilkType = MilkType.Buffalo, DefaultFatPercent = 6.8, DefaultRatePerLitre = 54.00m, OutstandingPayout = 3500.00m };
            await _dbService.InsertAsync(f1);
            await _dbService.InsertAsync(f2);

            await _dbService.InsertAsync(new MilkProcurement { SupplierId = 1, SupplierName = f1.Name, Date = today, MilkType = MilkType.Cow, Litres = 150, FatPercent = 4.3, SnfPercent = 8.5, RatePerLitre = 44.00m, IsPaid = false });
            await _dbService.InsertAsync(new MilkProcurement { SupplierId = 2, SupplierName = f2.Name, Date = today, MilkType = MilkType.Buffalo, Litres = 200, FatPercent = 6.8, SnfPercent = 9.0, RatePerLitre = 54.00m, IsPaid = false });

            // 12. Dairy Store Products
            await _dbService.InsertAsync(new DairyProduct { Name = "Fresh Dahi (Curd)", Category = "Curd & Yoghurt", Price = 40.00m, Unit = "500g Pouch", Description = "Thick natural fresh farm curd", IsAvailable = true });
            await _dbService.InsertAsync(new DairyProduct { Name = "Malai Paneer", Category = "Paneer", Price = 95.00m, Unit = "200g Pack", Description = "Soft fresh cottage cheese", IsAvailable = true });
            await _dbService.InsertAsync(new DairyProduct { Name = "Pure Desi Cow Ghee", Category = "Ghee", Price = 380.00m, Unit = "500ml Jar", Description = "Traditional Bilona cow ghee", IsAvailable = true });
            await _dbService.InsertAsync(new DairyProduct { Name = "White Makkhan (Butter)", Category = "Butter", Price = 80.00m, Unit = "200g Pack", Description = "Unsalted traditional white butter", IsAvailable = true });
        }
    }
}
