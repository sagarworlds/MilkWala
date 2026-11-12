using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>A backup file on disk.</summary>
    public sealed record BackupFile(string Path, string Name, DateTime CreatedAt, long SizeBytes)
    {
        public string SizeText => SizeBytes >= 1024 * 1024 ? $"{SizeBytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, SizeBytes / 1024)} KB";
    }

    /// <summary>File-level backup helpers, kept free of app/MAUI code so they can be tested against real SQLite files.</summary>
    public static class SqliteBackup
    {
        /// <summary>
        /// SQL that writes a consistent copy of the live database to the file given as the parameter.
        /// Unlike copying the .db file, it is safe while the app is using the database, and the copy is compacted.
        /// The target file must not exist yet.
        /// </summary>
        public const string SnapshotSql = "VACUUM INTO ?";

        private const string BackupPrefix = "MilkWala_Backup_";
        private const string PreRestorePrefix = "MilkWala_PreRestore_";

        // Tables whose absence means the file is some other database, not a MilkWala backup.
        private static readonly string[] RequiredTables = { "Users", "Customers", "Deliveries", "Subscriptions", "SyncQueue" };

        public static string NewBackupFileName(DateTime now) => $"{BackupPrefix}{now:yyyyMMdd_HHmmss}.db";

        public static string NewPreRestoreFileName(DateTime now) => $"{PreRestorePrefix}{now:yyyyMMdd_HHmmss}.db";

        /// <summary>Writes a snapshot of <paramref name="source"/> to <paramref name="destinationPath"/>.</summary>
        /// <exception cref="BusinessRuleException">The destination already exists.</exception>
        public static void CreateSnapshot(SQLiteConnection source, string destinationPath)
        {
            if (File.Exists(destinationPath))
                throw new BusinessRuleException("A backup with this name already exists. Please try again in a moment.");

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath)!);
            source.Execute(SnapshotSql, destinationPath);
        }

        /// <summary>
        /// Confirms the file is an intact MilkWala database before it is allowed to replace the live one.
        /// </summary>
        /// <exception cref="BusinessRuleException">Missing file, not a SQLite database, not a MilkWala backup, or damaged.</exception>
        public static void Validate(string path)
        {
            if (!File.Exists(path))
                throw new BusinessRuleException("The backup file could not be found.");

            try
            {
                using var connection = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);

                var tables = connection.Query<TableName>("SELECT name AS Name FROM sqlite_master WHERE type = 'table'")
                    .Select(t => t.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!RequiredTables.All(tables.Contains))
                    throw new BusinessRuleException("This file is not a MilkWala backup.");

                var integrity = connection.ExecuteScalar<string>("PRAGMA integrity_check");
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new BusinessRuleException("This backup is damaged and cannot be restored.");
            }
            catch (SQLiteException)
            {
                // Not a database at all (wrong file type, truncated, encrypted, ...).
                throw new BusinessRuleException("This file is not a MilkWala backup.");
            }
        }

        /// <summary>Backups in <paramref name="folder"/>, newest first. A missing folder means no backups yet.</summary>
        public static IReadOnlyList<BackupFile> List(string folder)
        {
            if (!Directory.Exists(folder))
                return Array.Empty<BackupFile>();

            return Directory.EnumerateFiles(folder, "MilkWala_*.db")
                .Select(path => new FileInfo(path))
                .Where(f => f.Name.StartsWith(BackupPrefix, StringComparison.Ordinal) || f.Name.StartsWith(PreRestorePrefix, StringComparison.Ordinal))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ThenByDescending(f => f.Name, StringComparer.Ordinal)
                .Select(f => new BackupFile(f.FullName, f.Name, f.CreationTime, f.Length))
                .ToList();
        }

        private sealed class TableName
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
