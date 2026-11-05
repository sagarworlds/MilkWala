using MilkWala.Helpers;
using Xunit;

namespace MilkWala.Tests;

public class MobileNumberTests
{
    [Theory]
    [InlineData("9876543210", "9876543210")]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("+91-98765-43210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("(987) 654-3210", "9876543210")]
    public void Normalize_reduces_formats_to_the_ten_digit_number(string input, string expected)
    {
        Assert.Equal(expected, MobileNumber.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("abc")]
    public void Normalize_returns_empty_for_missing_or_too_short_input(string? input)
    {
        Assert.Equal(string.Empty, MobileNumber.Normalize(input));
    }
}
