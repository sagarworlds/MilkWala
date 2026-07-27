using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using MilkWala.Models;
using Microsoft.Maui.Storage;

namespace MilkWala.Services
{
    public class LocalDbService
    {
        private SQLiteAsyncConnection? _db;
        private readonly string _dbPath;

        public LocalDbService()
        {
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "MilkWala_v1.db");
        }

        public async Task InitAsync()
        {
            if (_db != null)
                return;

            _db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

            await _db.CreateTableAsync<User>();
            await _db.CreateTableAsync<LoginSession>();
            await _db.CreateTableAsync<Customer>();
            await _db.CreateTableAsync<Vendor>();
            await _db.CreateTableAsync<DeliveryRoute>();
            await _db.CreateTableAsync<Product>();
            await _db.CreateTableAsync<Delivery>();
            await _db.CreateTableAsync<Subscription>();
            await _db.CreateTableAsync<Payment>();
            await _db.CreateTableAsync<WalletTransaction>();
            await _db.CreateTableAsync<Expense>();
            await _db.CreateTableAsync<Complaint>();
            await _db.CreateTableAsync<SpecialOrder>();
            await _db.CreateTableAsync<FarmerSupplier>();
            await _db.CreateTableAsync<MilkProcurement>();
            await _db.CreateTableAsync<DairyProduct>();
            await _db.CreateTableAsync<StoreOrder>();
            await _db.CreateTableAsync<SyncQueueItem>();
            await _db.CreateTableAsync<SyncLog>();
        }

        public SQLiteAsyncConnection Database
        {
            get
            {
                if (_db == null)
                    throw new InvalidOperationException("Database has not been initialized. Call InitAsync first.");
                return _db;
            }
        }

        #region Generic CRUD Operations
        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            await InitAsync();
            return await Database.Table<T>().ToListAsync();
        }

        public async Task<T?> GetByIdAsync<T>(int id) where T : new()
        {
            await InitAsync();
            return await Database.FindAsync<T>(id);
        }

        public async Task<int> InsertAsync<T>(T item) where T : new()
        {
            await InitAsync();
            return await Database.InsertAsync(item);
        }

        public async Task<int> UpdateAsync<T>(T item) where T : new()
        {
            await InitAsync();
            return await Database.UpdateAsync(item);
        }

        public async Task<int> DeleteAsync<T>(T item) where T : new()
        {
            await InitAsync();
            return await Database.DeleteAsync(item);
        }
        #endregion

        #region Query Helpers
        public async Task<Customer?> GetCustomerByUserIdAsync(int userId)
        {
            await InitAsync();
            return await Database.Table<Customer>().FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<List<Delivery>> GetCustomerDeliveriesAsync(int customerId, DateTime? monthYear = null)
        {
            await InitAsync();
            if (monthYear.HasValue)
            {
                var start = new DateTime(monthYear.Value.Year, monthYear.Value.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                return await Database.Table<Delivery>()
                    .Where(d => d.CustomerId == customerId && d.DeliveryDate >= start && d.DeliveryDate <= end)
                    .OrderBy(d => d.DeliveryDate)
                    .ToListAsync();
            }

            return await Database.Table<Delivery>()
                .Where(d => d.CustomerId == customerId)
                .OrderByDescending(d => d.DeliveryDate)
                .ToListAsync();
        }

        public async Task<List<Delivery>> GetTodayDeliveriesAsync(DateTime date)
        {
            await InitAsync();
            var targetDate = date.Date;
            var list = await Database.Table<Delivery>().ToListAsync();
            return list.FindAll(d => d.DeliveryDate.Date == targetDate);
        }

        public async Task EnqueueSyncAsync(string tableName, int recordId, SyncOperation op, object itemPayload)
        {
            await InitAsync();
            var queueItem = new SyncQueueItem
            {
                TableName = tableName,
                RecordId = recordId,
                Operation = op,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(itemPayload),
                CreatedAt = DateTime.UtcNow,
                Status = SyncStatus.Pending
            };
            await Database.InsertAsync(queueItem);
        }
        #endregion
    }
}
