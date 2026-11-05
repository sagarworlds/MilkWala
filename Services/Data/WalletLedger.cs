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

        /// <summary>Outcome of a wallet recharge.</summary>
        /// <param name="Credit">The wallet credit for the money paid in.</param>
        /// <param name="Payment">The collection record the vendor sees in "today's collection".</param>
        /// <param name="DuesCleared">Outstanding dues paid off from the new balance (0 if none).</param>
        /// <param name="BalanceAfter">Wallet balance once dues were cleared.</param>
        public sealed record RechargeResult(WalletTransaction Credit, Payment Payment, decimal DuesCleared, decimal BalanceAfter);

        /// <summary>
        /// Customer pays money in: credits the wallet, records the payment as a collection, then uses the balance to
        /// pay off any outstanding dues (oldest obligation first) so the customer is not left owing while holding credit.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        public static RechargeResult Recharge(
            SQLiteConnection connection,
            int customerId,
            decimal amount,
            PaymentMode mode,
            string reference,
            string description)
        {
            var credit = Credit(connection, customerId, amount, description);

            var payment = new Payment
            {
                CustomerId = customerId,
                Amount = amount,
                Mode = mode,
                ReferenceNumber = reference,
                PaymentDate = DateTime.Now,
                Remarks = "Wallet recharge",
                IsVerified = true
            };
            connection.Insert(payment);
            SyncQueueWriter.Enqueue(connection, payment, SyncOperation.Insert);

            var customer = connection.Find<Customer>(customerId)!;
            decimal cleared = Math.Min(customer.OutstandingBalance, customer.WalletBalance);
            if (cleared > 0)
            {
                Debit(connection, customerId, cleared, "Outstanding dues cleared from wallet");
                AdjustOutstanding(connection, customerId, -cleared);
            }

            decimal balanceAfter = connection.Find<Customer>(customerId)!.WalletBalance;
            return new RechargeResult(credit, payment, cleared, balanceAfter);
        }

        /// <summary>Raises (positive) or lowers (negative) the customer's outstanding dues and queues the change for sync.</summary>
        internal static void AdjustOutstanding(SQLiteConnection connection, int customerId, decimal delta)
        {
            var customer = connection.Find<Customer>(customerId)
                ?? throw new RecordNotFoundException(nameof(Customer), customerId);

            customer.OutstandingBalance += delta;
            customer.UpdatedAt = DateTime.UtcNow;
            connection.Update(customer);
            SyncQueueWriter.Enqueue(connection, customer, SyncOperation.Update);
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
