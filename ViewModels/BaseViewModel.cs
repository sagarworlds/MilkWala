using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MilkWala.Helpers;
using MilkWala.Services;

namespace MilkWala.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        protected readonly LocalDbService DbService;
        protected readonly SyncEngineService SyncEngine;
        protected readonly LocalizationService Localization;
        protected readonly ILogger Logger;

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

        /// <summary>Last failure from a guarded command, shown in the shared header banner. Empty when all is well.</summary>
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public BaseViewModel(
            LocalDbService dbService,
            SyncEngineService syncEngine,
            LocalizationService localization,
            ILoggerFactory loggerFactory)
        {
            DbService = dbService;
            SyncEngine = syncEngine;
            Localization = localization;
            Logger = loggerFactory.CreateLogger(GetType());

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

        /// <summary>
        /// Runs a command body with busy-state handling and failure reporting: sets <see cref="IsBusy"/> for the
        /// duration, logs any exception with context, and shows a readable message in <see cref="ErrorMessage"/>.
        /// Nested calls (a command invoking another command) leave the outer call in charge of <see cref="IsBusy"/>.
        /// </summary>
        /// <param name="work">The command body.</param>
        /// <param name="operation">Filled in with the calling method's name; used to word the message.</param>
        protected async Task RunSafeAsync(Func<Task> work, [CallerMemberName] string operation = "")
        {
            ErrorMessage = string.Empty;

            bool ownsBusyState = !IsBusy;
            if (ownsBusyState)
                IsBusy = true;

            try
            {
                var outcome = await OperationGuard.RunAsync(
                    work,
                    ex => Logger.LogError(ex, "{Operation} failed in {ViewModel}", operation, GetType().Name),
                    operation);

                if (!outcome.Succeeded)
                    ErrorMessage = outcome.ErrorMessage ?? string.Empty;
            }
            finally
            {
                if (ownsBusyState)
                    IsBusy = false;
            }
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
