using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using MilkWala.Services.Data;

namespace MilkWala.Services
{
    /// <summary>Backs up and restores the app's data.</summary>
    public interface IBackupService
    {
        /// <summary>Backups on this device, newest first.</summary>
        IReadOnlyList<BackupFile> ListBackups();

        /// <summary>Writes a consistent snapshot of the live data to a new backup file.</summary>
        Task<BackupFile> CreateBackupAsync();

        /// <summary>
        /// Replaces the live data with the backup. The backup is checked first and the current data is saved to a
        /// "pre-restore" file, so a mistaken restore can itself be undone.
        /// </summary>
        /// <exception cref="BusinessRuleException">The file is not a usable MilkWala backup.</exception>
        Task RestoreAsync(BackupFile backup);

        /// <summary>Opens the system share sheet so the backup can be saved to Drive, e-mailed, etc.</summary>
        Task ShareAsync(BackupFile backup);
    }

    public class BackupService : IBackupService
    {
        private readonly LocalDbService _db;
        private readonly ILogger<BackupService> _logger;

        public BackupService(LocalDbService db, ILogger<BackupService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public IReadOnlyList<BackupFile> ListBackups() => SqliteBackup.List(_db.BackupFolder);

        public async Task<BackupFile> CreateBackupAsync()
        {
            var path = await SnapshotAsync(SqliteBackup.NewBackupFileName(DateTime.Now));
            _logger.LogInformation("Backup written to {BackupPath}", path);
            return SqliteBackup.List(_db.BackupFolder).First(b => b.Path == path);
        }

        public async Task RestoreAsync(BackupFile backup)
        {
            ArgumentNullException.ThrowIfNull(backup);

            // Refuse a bad file before touching anything.
            SqliteBackup.Validate(backup.Path);

            // Safety net: keep what is there now, so the restore can be undone.
            var safety = await SnapshotAsync(SqliteBackup.NewPreRestoreFileName(DateTime.Now));
            _logger.LogInformation("Saved current data to {SafetyPath} before restoring {BackupPath}", safety, backup.Path);

            await _db.ReplaceDatabaseFileAsync(backup.Path);
        }

        public Task ShareAsync(BackupFile backup)
        {
            ArgumentNullException.ThrowIfNull(backup);

            return Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "MilkWala backup",
                File = new ShareFile(backup.Path)
            });
        }

        private async Task<string> SnapshotAsync(string fileName)
        {
            await _db.InitAsync();

            var path = Path.Combine(_db.BackupFolder, fileName);
            Directory.CreateDirectory(_db.BackupFolder);
            if (File.Exists(path))
                throw new BusinessRuleException("A backup with this name already exists. Please try again in a moment.");

            await _db.Database.ExecuteAsync(SqliteBackup.SnapshotSql, path);
            SqliteBackup.Validate(path); // never report success for a file that could not be restored
            return path;
        }
    }
}
