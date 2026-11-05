namespace MilkWala.Services.Data
{
    /// <summary>A debit was requested for more than the customer's wallet holds.</summary>
    public sealed class InsufficientBalanceException : Exception
    {
        public decimal Available { get; }
        public decimal Required { get; }

        public InsufficientBalanceException(decimal available, decimal required)
            : base($"Wallet balance {available:N2} is less than the required {required:N2}.")
        {
            Available = available;
            Required = required;
        }
    }

    /// <summary>A record referenced by id does not exist (deleted, or the id came from stale state).</summary>
    public sealed class RecordNotFoundException : Exception
    {
        public string EntityName { get; }
        public int RecordId { get; }

        public RecordNotFoundException(string entityName, int recordId)
            : base($"{entityName} with id {recordId} was not found.")
        {
            EntityName = entityName;
            RecordId = recordId;
        }
    }
}
