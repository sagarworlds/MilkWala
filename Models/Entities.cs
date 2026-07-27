using System;
using SQLite;

namespace MilkWala.Models
{
    [Table("Users")]
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public string MobileNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Customer;
        public string PinHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("LoginSessions")]
    public class LoginSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AuthToken { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
        public bool IsOfflineValid { get; set; } = true;
    }

    [Table("Customers")]
    public class Customer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string AlternateContact { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int RouteId { get; set; }
        public string PreferredDeliveryTime { get; set; } = "Morning"; // Morning / Evening / Both
        public MilkType PreferredMilkType { get; set; } = MilkType.Cow;
        public double PreferredQuantity { get; set; } = 1.0;
        public decimal WalletBalance { get; set; } = 0.0m;
        public decimal OutstandingBalance { get; set; } = 0.0m;
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("Vendors")]
    public class Vendor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string BusinessName { get; set; } = "DairyFresh Milk Services";
        public string OwnerName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal TotalMorningStockLitres { get; set; } = 250.0m;
        public decimal TotalEveningStockLitres { get; set; } = 150.0m;
    }

    [Table("Routes")]
    public class DeliveryRoute
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public string AreaDescription { get; set; } = string.Empty;
        public int AssignedDeliveryBoyId { get; set; }
        public int SequenceOrder { get; set; }
    }

    [Table("Products")]
    public class Product
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public MilkType MilkType { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PricePerLitre { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    [Table("Deliveries")]
    public class Delivery
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        [Indexed]
        public DateTime DeliveryDate { get; set; }
        public MilkType MilkType { get; set; }
        public double QuantityLitres { get; set; }
        public decimal RatePerLitre { get; set; }
        public decimal TotalPrice => (decimal)QuantityLitres * RatePerLitre;
        public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
        public string Notes { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string ProofPhotoUrl { get; set; } = string.Empty;
        public DateTime DeliveredAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("Subscriptions")]
    public class Subscription
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int CustomerId { get; set; }
        public MilkType MilkType { get; set; }
        public double QuantityLitres { get; set; } = 1.0;
        public bool IsPaused { get; set; } = false;
        public DateTime? PauseStartDate { get; set; }
        public DateTime? PauseEndDate { get; set; }
        public string DeliveryFrequency { get; set; } = "Daily"; // Daily / Alternate / Custom
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("Payments")]
    public class Payment
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMode Mode { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string Remarks { get; set; } = string.Empty;
        public bool IsVerified { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("WalletTransactions")]
    public class WalletTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int CustomerId { get; set; }
        public decimal Amount { get; set; } // Positive for Credit, Negative for Debit
        public string TransactionType { get; set; } = "Credit"; // Credit / Debit
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public decimal BalanceAfter { get; set; }
    }

    [Table("Expenses")]
    public class Expense
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public ExpenseCategory Category { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("Complaints")]
    public class Complaint
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed]
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public ComplaintCategory Category { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
        public string ResolutionNotes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("Orders")]
    public class SpecialOrder
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public MilkType MilkType { get; set; }
        public double ExtraQuantityLitres { get; set; }
        public DateTime TargetDate { get; set; }
        public string SpecialInstruction { get; set; } = string.Empty;
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("FarmerSuppliers")]
    public class FarmerSupplier
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public MilkType MilkType { get; set; } = MilkType.Cow;
        public double DefaultFatPercent { get; set; } = 4.2;
        public decimal DefaultRatePerLitre { get; set; } = 42.00m;
        public decimal OutstandingPayout { get; set; } = 0.0m;
    }

    [Table("MilkProcurement")]
    public class MilkProcurement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public MilkType MilkType { get; set; } = MilkType.Cow;
        public double Litres { get; set; }
        public double FatPercent { get; set; }
        public double SnfPercent { get; set; } = 8.5;
        public decimal RatePerLitre { get; set; }
        public decimal TotalAmount => (decimal)Litres * RatePerLitre;
        public bool IsPaid { get; set; } = false;
    }

    [Table("DairyProducts")]
    public class DairyProduct
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Dairy Products"; // Curd, Paneer, Ghee, Butter, Sweets
        public decimal Price { get; set; }
        public string Unit { get; set; } = "500g";
        public string Description { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
    }

    [Table("StoreOrders")]
    public class StoreOrder
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Scheduled for Delivery";
    }

    [Table("SyncQueue")]
    public class SyncQueueItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public SyncOperation Operation { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; } = 0;
        public SyncStatus Status { get; set; } = SyncStatus.Pending;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    [Table("SyncLogs")]
    public class SyncLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime SyncTime { get; set; } = DateTime.UtcNow;
        public int ItemsUploaded { get; set; }
        public int ItemsDownloaded { get; set; }
        public bool Success { get; set; }
        public string LogMessage { get; set; } = string.Empty;
    }
}
