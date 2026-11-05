using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MilkWala.Helpers;
using MilkWala.Models;
using MilkWala.Services.Data;

namespace MilkWala.Services
{
    public class LocalDbService
    {
        private SQLiteAsyncConnection? _db;
        private readonly string _dbPath;
        private readonly ILogger<LocalDbService> _logger;
        private readonly SingleFlightInitializer _initializer;

        public LocalDbService(ILogger<LocalDbService> logger)
        {
            _logger = logger;
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "MilkWala_v1.db");
            _initializer = new SingleFlightInitializer(InitializeCoreAsync);
        }

        /// <summary>
        /// Opens the database and creates missing tables. Safe to call from many places at once:
        /// all callers share one initialisation, so we never open two connections or create tables twice.
        /// </summary>
        /// <exception cref="Exception">Rethrows the underlying SQLite/IO error after logging it; a later call retries.</exception>
        public Task InitAsync() => _initializer.EnsureAsync();

        private async Task InitializeCoreAsync()
        {
            var db = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

            try
            {
                await db.CreateTableAsync<User>();
                await db.CreateTableAsync<LoginSession>();
                await db.CreateTableAsync<Customer>();
                await db.CreateTableAsync<Vendor>();
                await db.CreateTableAsync<DeliveryRoute>();
                await db.CreateTableAsync<Product>();
                await db.CreateTableAsync<Delivery>();
                await db.CreateTableAsync<Subscription>();
                await db.CreateTableAsync<Payment>();
                await db.CreateTableAsync<WalletTransaction>();
                await db.CreateTableAsync<Expense>();
                await db.CreateTableAsync<Complaint>();
                await db.CreateTableAsync<SpecialOrder>();
                await db.CreateTableAsync<FarmerSupplier>();
                await db.CreateTableAsync<MilkProcurement>();
                await db.CreateTableAsync<DairyProduct>();
                await db.CreateTableAsync<StoreOrder>();
                await db.CreateTableAsync<SyncQueueItem>();
                await db.CreateTableAsync<SyncLog>();

                // Publish the connection only once every table exists, so Database never exposes a half-built schema.
                _db = db;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise local database at {DatabasePath}", _dbPath);
                await db.CloseAsync();
                throw;
            }
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
                var end = start.AddMonths(1);
                return await Database.Table<Delivery>()
                    .Where(d => d.CustomerId == customerId && d.DeliveryDate >= start && d.DeliveryDate < end)
                    .OrderBy(d => d.DeliveryDate)
                    .ToListAsync();
            }

            return await Database.Table<Delivery>()
                .Where(d => d.CustomerId == customerId)
                .OrderByDescending(d => d.DeliveryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Deliveries dated within [<paramref name="startInclusive"/>, <paramref name="endExclusive"/>), filtered in SQL
        /// (DeliveryDate is indexed) instead of loading the whole table.
        /// </summary>
        public async Task<List<Delivery>> GetDeliveriesBetweenAsync(DateTime startInclusive, DateTime endExclusive)
        {
            await InitAsync();
            return await Database.Table<Delivery>()
                .Where(d => d.DeliveryDate >= startInclusive && d.DeliveryDate < endExclusive)
                .OrderBy(d => d.DeliveryDate)
                .ToListAsync();
        }

        public Task<List<Delivery>> GetTodayDeliveriesAsync(DateTime date)
        {
            var start = date.Date;
            return GetDeliveriesBetweenAsync(start, start.AddDays(1));
        }

        /// <summary>
        /// Runs <paramref name="work"/> in one SQLite transaction. If it throws, every write it made is rolled back
        /// and the exception is rethrown.
        /// </summary>
        public async Task RunInTransactionAsync(Action<SQLiteConnection> work)
        {
            ArgumentNullException.ThrowIfNull(work);
            await InitAsync();
            await Database.RunInTransactionAsync(work);
        }

        /// <summary>Inserts the entity and queues it for sync, atomically. The entity's Id is populated on return.</summary>
        public Task InsertWithSyncAsync(object item) =>
            RunInTransactionAsync(connection =>
            {
                connection.Insert(item);
                SyncQueueWriter.Enqueue(connection, item, SyncOperation.Insert);
            });

        /// <summary>Updates the entity and queues the change for sync, atomically.</summary>
        public Task UpdateWithSyncAsync(object item) =>
            RunInTransactionAsync(connection =>
            {
                connection.Update(item);
                SyncQueueWriter.Enqueue(connection, item, SyncOperation.Update);
            });
        #endregion
    }
}
