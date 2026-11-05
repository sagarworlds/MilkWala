using System.Text.Json;
using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>
    /// Writes offline-sync queue entries on an open connection so the entry can be committed in the same
    /// transaction as the change it describes. That guarantees "the change exists" and "it will be synced"
    /// are never out of step.
    /// </summary>
    public static class SyncQueueWriter
    {
        /// <summary>
        /// Queues <paramref name="item"/> for upload. The table name and primary key come from the entity mapping,
        /// so callers cannot pass a mistyped table name.
        /// </summary>
        /// <param name="connection">Connection of the surrounding transaction.</param>
        /// <param name="item">The entity that was just inserted/updated (its primary key must already be set).</param>
        /// <param name="operation">What happened to the entity.</param>
        /// <exception cref="InvalidOperationException">The entity type has no primary key.</exception>
        public static void Enqueue(SQLiteConnection connection, object item, SyncOperation operation)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(item);

            var mapping = connection.GetMapping(item.GetType());
            var primaryKey = mapping.PK
                ?? throw new InvalidOperationException($"{item.GetType().Name} has no primary key and cannot be synced.");

            connection.Insert(new SyncQueueItem
            {
                TableName = mapping.TableName,
                RecordId = Convert.ToInt32(primaryKey.GetValue(item)),
                Operation = operation,
                PayloadJson = JsonSerializer.Serialize(item, item.GetType()),
                CreatedAt = DateTime.UtcNow, // sync metadata is UTC (see the date convention in Models/Entities.cs)
                Status = SyncStatus.Pending
            });
        }
    }
}
