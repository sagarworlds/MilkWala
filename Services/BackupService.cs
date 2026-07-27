using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace MilkWala.Services
{
    public class BackupService
    {
        public async Task<string> ExportBackupAsync()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "MilkWala_v1.db");
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Local database file does not exist.");

            string backupFolder = Path.Combine(FileSystem.AppDataDirectory, "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string fileName = $"MilkWala_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            string backupPath = Path.Combine(backupFolder, fileName);

            File.Copy(dbPath, backupPath, overwrite: true);
            return await Task.FromResult(backupPath);
        }

        public async Task RestoreBackupAsync(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found.");

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "MilkWala_v1.db");
            File.Copy(backupFilePath, dbPath, overwrite: true);
            await Task.CompletedTask;
        }
    }
}
