namespace MilkWala.Models
{
    public enum UserRole
    {
        Customer,
        Vendor,
        DeliveryBoy
    }

    public enum MilkType
    {
        Cow,
        Buffalo,
        Toned,
        FullCream,
        Organic
    }

    public enum DeliveryStatus
    {
        Pending,
        Delivered,
        Skipped,
        Holiday,
        Extra,
        Missed
    }

    public enum PaymentMode
    {
        UPI,
        Cash,
        Wallet,
        Online,
        MonthlyBilling
    }

    public enum ComplaintStatus
    {
        Open,
        InProgress,
        Closed
    }

    public enum ComplaintCategory
    {
        Quality,
        DeliveryNotReceived,
        WrongQuantity,
        Billing,
        GeneralFeedback
    }

    public enum SyncStatus
    {
        Pending,
        Synced,
        Failed
    }

    public enum SyncOperation
    {
        Insert,
        Update,
        Delete
    }

    public enum DeliveryTimePreference
    {
        Morning,
        Evening,
        Both
    }

    public enum ExpenseCategory
    {
        Fuel,
        Salary,
        Maintenance,
        Packaging,
        Miscellaneous
    }
}
