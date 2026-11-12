using MilkWala.Models;
using MilkWala.Services.Data;
using Xunit;

namespace MilkWala.Tests;

public class StaffLedgerTests
{
    private static User Add(TestDb db, string name = "Ravi Kumar", string mobile = "9123456780")
    {
        User user = null!;
        db.InTransaction(c => user = StaffLedger.AddDeliveryStaff(c, name, mobile));
        return user;
    }

    [Fact]
    public void Adds_an_active_delivery_account_with_a_normalised_mobile_and_queues_it()
    {
        using var db = new TestDb();

        var user = Add(db, "  Ravi Kumar ", "+91 91234-56780");

        var stored = db.Connection.Get<User>(user.Id);
        Assert.Equal("Ravi Kumar", stored.Name);
        Assert.Equal("9123456780", stored.MobileNumber);
        Assert.Equal(UserRole.DeliveryBoy, stored.Role);
        Assert.True(stored.IsActive);
        Assert.Equal("Users", db.Connection.Table<SyncQueueItem>().Single().TableName);
    }

    [Theory]
    [InlineData("", "9123456780", "name")]
    [InlineData("Ravi", "12345", "10-digit")]
    [InlineData("Ravi", "", "10-digit")]
    public void Rejects_invalid_input(string name, string mobile, string messagePart)
    {
        using var db = new TestDb();

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, name, mobile));

        Assert.Contains(messagePart, ex.Message);
        Assert.Equal(0, db.Count<User>());
    }

    [Fact]
    public void Two_delivery_staff_cannot_share_a_number_even_in_another_format()
    {
        using var db = new TestDb();
        Add(db, "Ravi", "9123456780");

        var ex = Assert.Throws<BusinessRuleException>(() => Add(db, "Someone else", "+91 91234 56780"));

        Assert.Contains("Ravi", ex.Message);
        Assert.Equal(1, db.Count<User>());
    }

    [Fact]
    public void A_customer_who_also_delivers_can_use_the_same_number_because_sign_in_checks_the_role()
    {
        using var db = new TestDb();
        db.AddUser("Rahul (customer)", UserRole.Customer, mobile: "9123456780");

        var staff = Add(db, "Rahul (delivery)", "9123456780");

        Assert.Equal(UserRole.DeliveryBoy, db.Connection.Get<User>(staff.Id).Role);
        Assert.Equal(2, db.Count<User>());
    }
}
