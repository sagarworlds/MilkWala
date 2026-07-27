using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilkWala.Services;

namespace MilkWala.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        protected readonly LocalDbService DbService;
        protected readonly SyncEngineService SyncEngine;
        protected readonly LocalizationService Localization;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isOnline;

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private int _pendingSyncCount;

        [ObservableProperty]
        private string _syncStatusText = "Online (Synced)";

        public BaseViewModel(LocalDbService dbService, SyncEngineService syncEngine, LocalizationService localization)
        {
            DbService = dbService;
            SyncEngine = syncEngine;
            Localization = localization;

            SyncEngine.SyncStatusChanged += OnSyncStatusChanged;
            UpdateSyncState();
        }

        private void OnSyncStatusChanged(object? sender, EventArgs e)
        {
            UpdateSyncState();
        }

        protected void UpdateSyncState()
        {
            IsOnline = SyncEngine.IsOnline;
            IsSyncing = SyncEngine.IsSyncing;
            PendingSyncCount = SyncEngine.PendingSyncCount;

            if (IsSyncing)
                SyncStatusText = "Syncing with cloud...";
            else if (!IsOnline)
                SyncStatusText = $"Offline Mode ({PendingSyncCount} pending)";
            else if (PendingSyncCount > 0)
                SyncStatusText = $"Online ({PendingSyncCount} pending upload)";
            else
                SyncStatusText = "Online & Synced";
        }

        [RelayCommand]
        public async Task ToggleSyncAsync()
        {
            SyncEngine.IsOnline = !SyncEngine.IsOnline;
            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task ForceSyncNowAsync()
        {
            await SyncEngine.TriggerSyncAsync();
        }
    }
}
