using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MilkWala.Models;
using Microsoft.Maui.Networking;

namespace MilkWala.Services
{
    public class SyncEngineService
    {
        private readonly LocalDbService _dbService;
        private bool _isOnline = true;
        private bool _isSyncing = false;
        private int _pendingSyncCount = 0;
        private DateTime _lastSyncTime = DateTime.Now;

        public event EventHandler? SyncStatusChanged;

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnSyncStatusChanged();
                    if (_isOnline)
                    {
                        _ = TriggerSyncAsync();
                    }
                }
            }
        }

        public bool IsSyncing
        {
            get => _isSyncing;
            private set
            {
                if (_isSyncing != value)
                {
                    _isSyncing = value;
                    OnSyncStatusChanged();
                }
            }
        }

        public int PendingSyncCount
        {
            get => _pendingSyncCount;
            private set
            {
                if (_pendingSyncCount != value)
                {
                    _pendingSyncCount = value;
                    OnSyncStatusChanged();
                }
            }
        }

        public DateTime LastSyncTime => _lastSyncTime;

        public SyncEngineService(LocalDbService dbService)
        {
            _dbService = dbService;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            IsOnline = e.NetworkAccess == NetworkAccess.Internet;
        }

        public async Task RefreshPendingCountAsync()
        {
            await _dbService.InitAsync();
            var queue = await _dbService.Database.Table<SyncQueueItem>()
                .Where(q => q.Status == SyncStatus.Pending)
                .ToListAsync();
            PendingSyncCount = queue.Count;
        }

        public async Task TriggerSyncAsync()
        {
            if (IsSyncing) return;

            try
            {
                IsSyncing = true;
                await _dbService.InitAsync();

                var pendingItems = await _dbService.Database.Table<SyncQueueItem>()
                    .Where(q => q.Status == SyncStatus.Pending)
                    .ToListAsync();

                int uploadedCount = 0;
                foreach (var item in pendingItems)
                {
                    // Process each item (Simulate server sync with latency)
                    await Task.Delay(150); // Simulate API latency
                    item.Status = SyncStatus.Synced;
                    await _dbService.UpdateAsync(item);
                    uploadedCount++;
                }

                _lastSyncTime = DateTime.Now;
                await RefreshPendingCountAsync();

                // Log sync results
                var log = new SyncLog
                {
                    SyncTime = DateTime.UtcNow,
                    ItemsUploaded = uploadedCount,
                    ItemsDownloaded = 3, // Mock server pull
                    Success = true,
                    LogMessage = $"Successfully synced {uploadedCount} offline changes to cloud."
                };
                await _dbService.InsertAsync(log);
            }
            catch (Exception ex)
            {
                var log = new SyncLog
                {
                    SyncTime = DateTime.UtcNow,
                    ItemsUploaded = 0,
                    ItemsDownloaded = 0,
                    Success = false,
                    LogMessage = $"Sync failed: {ex.Message}"
                };
                await _dbService.InsertAsync(log);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private void OnSyncStatusChanged()
        {
            SyncStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
