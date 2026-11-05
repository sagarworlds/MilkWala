using MilkWala.Models;
using SQLite;

namespace MilkWala.Services.Data
{
    /// <summary>Places dairy-store orders. Run inside a transaction so payment and order are one unit.</summary>
    public static class StoreOrderLedger
    {
        /// <summary>
        /// Charges the product price to the customer's wallet and creates the order.
        /// If the wallet is too low nothing is written.
        /// </summary>
        /// <exception cref="InsufficientBalanceException">The wallet cannot cover the product price.</exception>
        /// <exception cref="RecordNotFoundException">The customer does not exist.</exception>
        public static StoreOrder Place(SQLiteConnection connection, int customerId, DairyProduct product)
        {
            ArgumentNullException.ThrowIfNull(product);

            WalletLedger.Debit(connection, customerId, product.Price, $"Purchased {product.Name} from Store");

            var order = new StoreOrder
            {
                CustomerId = customerId,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = 1,
                TotalPrice = product.Price,
                OrderDate = DateTime.Now,
                Status = "Scheduled with tomorrow's milk"
            };
            connection.Insert(order);
            SyncQueueWriter.Enqueue(connection, order, SyncOperation.Insert);

            return order;
        }
    }
}
