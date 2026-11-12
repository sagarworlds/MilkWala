using MilkWala.Models;
using MilkWala.Services.Identity;
using Xunit;

namespace MilkWala.Tests;

public class UserLookupTests
{
    private static readonly User[] Users =
    {
        new() { Id = 1, MobileNumber = "9876543210", Role = UserRole.Customer },
        new() { Id = 2, MobileNumber = "9988776655", Role = UserRole.Vendor },
        new() { Id = 3, MobileNumber = "9123456789", Role = UserRole.DeliveryBoy },
        new() { Id = 4, MobileNumber = "9000000004", Role = UserRole.DeliveryBoy, IsActive = false },
    };

    [Fact]
    public void Finds_the_account_with_the_matching_number_and_role()
    {
        Assert.Equal(3, UserLookup.Find("9123456789", UserRole.DeliveryBoy, Users)!.Id);
    }

    [Fact]
    public void Matches_across_number_formats()
    {
        Assert.Equal(3, UserLookup.Find("+91 91234 56789", UserRole.DeliveryBoy, Users)!.Id);
    }

    [Fact]
    public void A_number_of_another_role_does_not_sign_in_as_delivery_staff()
    {
        Assert.Null(UserLookup.Find("9876543210", UserRole.DeliveryBoy, Users)); // a customer's number
        Assert.Null(UserLookup.Find("9988776655", UserRole.DeliveryBoy, Users)); // the vendor's number
    }

    [Fact]
    public void A_deactivated_account_is_not_found()
    {
        Assert.Null(UserLookup.Find("9000000004", UserRole.DeliveryBoy, Users));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("9999999999")]
    public void Blank_invalid_or_unknown_numbers_find_nobody(string? mobile)
    {
        Assert.Null(UserLookup.Find(mobile, UserRole.DeliveryBoy, Users));
    }
}
