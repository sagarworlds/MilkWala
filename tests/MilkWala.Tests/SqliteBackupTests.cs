using MilkWala.Models;
using MilkWala.Services.Data;
using SQLite;
using Xunit;

namespace MilkWala.Tests;

public sealed class SqliteBackupTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("milkwala-backup-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string P(string name) => Path.Combine(_dir, name);

    /// <summary>A file-backed database shaped like the app's, with one customer.</summary>
    private SQLiteConnection LiveDb(string name = "live.db", string customerName = "Rahul")
    {
        var connection = new SQLiteConnection(P(name));
        connection.CreateTable<User>();
        connection.CreateTable<Customer>();
        connection.CreateTable<Delivery>();
        connection.CreateTable<Subscription>();
        connection.CreateTable<SyncQueueItem>();
        connection.Insert(new Customer { Name = customerName, Mobile = "9876543210", WalletBalance = 250m });
        return connection;
    }

    [Fact]
    public void A_snapshot_of_the_live_database_holds_the_same_data_and_validates()
    {
        using var live = LiveDb();
        var backup = P("MilkWala_Backup_20261105_070000.db");

        SqliteBackup.CreateSnapshot(live, backup);

        SqliteBackup.Validate(backup);
        using var copy = new SQLiteConnection(backup, SQLiteOpenFlags.ReadOnly);
        var customer = copy.Table<Customer>().Single();
        Assert.Equal("Rahul", customer.Name);
        Assert.Equal(250m, customer.WalletBalance);
    }

    [Fact]
    public void A_snapshot_is_taken_while_the_connection_is_open_and_later_changes_are_not_in_it()
    {
        using var live = LiveDb();
        var backup = P("MilkWala_Backup_a.db");
        SqliteBackup.CreateSnapshot(live, backup);

        live.Insert(new Customer { Name = "Added later" });

        using var copy = new SQLiteConnection(backup, SQLiteOpenFlags.ReadOnly);
        Assert.Equal(1, copy.Table<Customer>().Count());
        Assert.Equal(2, live.Table<Customer>().Count());
    }

    [Fact]
    public void Snapshot_refuses_to_overwrite_an_existing_file()
    {
        using var live = LiveDb();
        var backup = P("MilkWala_Backup_a.db");
        SqliteBackup.CreateSnapshot(live, backup);

        Assert.Throws<BusinessRuleException>(() => SqliteBackup.CreateSnapshot(live, backup));
    }

    [Fact]
    public void Snapshot_creates_the_backup_folder_when_it_is_missing()
    {
        using var live = LiveDb();
        var backup = Path.Combine(_dir, "Backups", "MilkWala_Backup_a.db");

        SqliteBackup.CreateSnapshot(live, backup);

        Assert.True(File.Exists(backup));
    }

    [Fact]
    public void Validate_rejects_a_missing_file()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => SqliteBackup.Validate(P("nope.db")));
        Assert.Contains("could not be found", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_file_that_is_not_a_database()
    {
        File.WriteAllText(P("notes.db"), "this is just text, not a database");

        var ex = Assert.Throws<BusinessRuleException>(() => SqliteBackup.Validate(P("notes.db")));
        Assert.Contains("not a MilkWala backup", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_database_from_some_other_app()
    {
        using (var other = new SQLiteConnection(P("other.db")))
            other.Execute("CREATE TABLE Photos (Id INTEGER PRIMARY KEY, Path TEXT)");

        var ex = Assert.Throws<BusinessRuleException>(() => SqliteBackup.Validate(P("other.db")));
        Assert.Contains("not a MilkWala backup", ex.Message);
    }

    [Fact]
    public void Validate_rejects_a_truncated_backup()
    {
        using var live = LiveDb();
        var backup = P("MilkWala_Backup_a.db");
        SqliteBackup.CreateSnapshot(live, backup);
        var bytes = File.ReadAllBytes(backup);
        File.WriteAllBytes(backup, bytes.Take(bytes.Length / 3).ToArray());

        Assert.Throws<BusinessRuleException>(() => SqliteBackup.Validate(backup));
    }

    [Fact]
    public void Restoring_a_snapshot_by_copying_it_over_the_live_file_brings_back_the_old_data()
    {
        // The file-level step LocalDbService performs after closing its connection.
        var livePath = P("live.db");
        var backup = P("MilkWala_Backup_a.db");
        using (var live = LiveDb())
            SqliteBackup.CreateSnapshot(live, backup);

        using (var live = new SQLiteConnection(livePath))
            live.Execute("UPDATE Customers SET Name = 'Changed after backup'");

        SqliteBackup.Validate(backup);
        File.Copy(backup, livePath, overwrite: true);

        using var restored = new SQLiteConnection(livePath);
        Assert.Equal("Rahul", restored.Table<Customer>().Single().Name);
    }

    [Fact]
    public void List_returns_only_backup_files_newest_first_and_handles_a_missing_folder()
    {
        Assert.Empty(SqliteBackup.List(P("does-not-exist")));

        var older = P("MilkWala_Backup_20261101_070000.db");
        var newer = P("MilkWala_PreRestore_20261105_070000.db");
        File.WriteAllText(older, "x");
        File.WriteAllText(newer, "x");
        File.WriteAllText(P("MilkWala_notes.db"), "x");   // wrong prefix after MilkWala_
        File.WriteAllText(P("random.db"), "x");
        File.SetCreationTimeUtc(older, new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc));
        File.SetCreationTimeUtc(newer, new DateTime(2026, 11, 5, 7, 0, 0, DateTimeKind.Utc));

        var names = SqliteBackup.List(_dir).Select(b => b.Name).ToArray();

        Assert.Equal(new[] { "MilkWala_PreRestore_20261105_070000.db", "MilkWala_Backup_20261101_070000.db" }, names);
    }

    [Fact]
    public void File_names_are_timestamped_and_recognised_by_List()
    {
        var name = SqliteBackup.NewBackupFileName(new DateTime(2026, 11, 5, 7, 8, 9));

        Assert.Equal("MilkWala_Backup_20261105_070809.db", name);
        Assert.StartsWith("MilkWala_PreRestore_", SqliteBackup.NewPreRestoreFileName(DateTime.Now));
    }

    [Theory]
    [InlineData(500, "1 KB")]
    [InlineData(20480, "20 KB")]
    [InlineData(3 * 1024 * 1024, "3.0 MB")]
    public void Size_is_shown_in_a_friendly_unit(long bytes, string expected)
    {
        Assert.Equal(expected, new BackupFile("p", "n", DateTime.Now, bytes).SizeText);
    }
}
