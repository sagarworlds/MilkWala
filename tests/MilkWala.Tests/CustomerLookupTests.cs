using MilkWala.Models;
using MilkWala.Services.Identity;
using Xunit;

namespace MilkWala.Tests;

public class CustomerLookupTests
{
    private static readonly Customer Rahul = new() { Id = 1, UserId = 1, Name = "Rahul", Mobile = "9876543210" };
    private static readonly Customer Priya = new() { Id = 2, UserId = 4, Name = "Priya", Mobile = "9822110033" };
    private static readonly Customer[] Customers = { Rahul, Priya };
    private static readonly User[] Users =
    {
        new() { Id = 1, MobileNumber = "9876543210", Role = UserRole.Customer },
        new() { Id = 2, MobileNumber = "9988776655", Role = UserRole.Vendor },
    };

    [Fact]
    public void Finds_the_profile_through_the_user_account()
    {
        Assert.Same(Rahul, CustomerLookup.Find("9876543210", Users, Customers));
    }

    [Fact]
    public void Falls_back_to_the_profile_mobile_when_the_customer_has_no_user_account()
    {
        Assert.Same(Priya, CustomerLookup.Find("9822110033", Users, Customers));
    }

    [Fact]
    public void Matches_across_number_formats()
    {
        Assert.Same(Rahul, CustomerLookup.Find("+91 98765 43210", Users, Customers));
        Assert.Same(Priya, CustomerLookup.Find("919822110033", Users, Customers));
    }

    [Fact]
    public void Unknown_number_finds_nobody_rather_than_the_first_customer()
    {
        Assert.Null(CustomerLookup.Find("9000000000", Users, Customers));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void Blank_or_invalid_number_finds_nobody(string? mobile)
    {
        Assert.Null(CustomerLookup.Find(mobile, Users, Customers));
    }

    [Fact]
    public void A_vendor_account_is_not_treated_as_a_customer_user()
    {
        // 9988776655 belongs to a vendor user and to no customer profile.
        Assert.Null(CustomerLookup.Find("9988776655", Users, Customers));
    }
}
