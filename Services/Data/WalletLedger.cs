using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>
    /// The only place wallet balances change. Every operation must run inside a transaction
    /// (<c>LocalDbService.RunInTransactionAsync</c>): the balance update, the ledger row and the sync entries
    /// either all commit or none do, and the balance is read fresh so two quick taps cannot overspend.
    /// </summary>
    public static class WalletLedger
    {
        /// <summary>Adds money to the customer's wallet and records a credit transaction.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        public static WalletTransaction Credit(SQLiteConnection connection, int customerId, decimal amount, string description)
        {
            ThrowIfNotPositive(amount);
            return Apply(connection, customerId, amount, description);
        }

        /// <summary>Takes money from the customer's wallet and records a debit transaction.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        /// <exception cref="InsufficientBalanceException">The wallet holds less than <paramref name="amount"/>.</exception>
        public static WalletTransaction Debit(SQLiteConnection connection, int customerId, decimal amount, string description)
        {
            ThrowIfNotPositive(amount);
            return Apply(connection, customerId, -amount, description);
        }

        private static WalletTransaction Apply(SQLiteConnection connection, int customerId, decimal signedAmount, string description)
        {
            var customer = connection.Find<Customer>(customerId)
                ?? throw new RecordNotFoundException(nameof(Customer), customerId);

            if (signedAmount < 0 && customer.WalletBalance < -signedAmount)
                throw new InsufficientBalanceException(customer.WalletBalance, -signedAmount);

            customer.WalletBalance += signedAmount;
            customer.UpdatedAt = DateTime.UtcNow;
            connection.Update(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Update);

            var transaction = new WalletTransaction
            {
                CustomerId = customerId,
                Amount = signedAmount, // positive = credit, negative = debit
                TransactionType = signedAmount > 0 ? "Credit" : "Debit",
                Description = description,
                Date = DateTime.Now,
                BalanceAfter = customer.WalletBalance
            };
            connection.Insert(transaction);
            SyncQueueWriter.Enqueue(connection, transaction, SyncOperation.Insert);

            return transaction;
        }

        private static void ThrowIfNotPositive(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }
    }
}
