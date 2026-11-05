using MilkWala.Helpers;
using Xunit;

namespace MilkWala.Tests;

public class BoolSwitchTests
{
    [Theory]
    [InlineData(true, "Active|Inactive", "Active")]
    [InlineData(false, "Active|Inactive", "Inactive")]
    [InlineData(true, "#F59E0B|#10B981", "#F59E0B")]
    [InlineData(false, "#F59E0B|#10B981", "#10B981")]
    public void Pick_returns_the_side_matching_the_value(bool value, string parameter, string expected)
    {
        Assert.Equal(expected, BoolSwitch.Pick(value, parameter, "fallback"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NoSeparator")]
    public void Pick_returns_fallback_for_missing_or_malformed_parameter(string? parameter)
    {
        Assert.Equal("fallback", BoolSwitch.Pick(true, parameter, "fallback"));
        Assert.Equal("fallback", BoolSwitch.Pick(false, parameter, "fallback"));
    }

    [Fact]
    public void Pick_splits_on_first_separator_only()
    {
        Assert.Equal("a", BoolSwitch.Pick(true, "a|b|c", "x"));
        Assert.Equal("b|c", BoolSwitch.Pick(false, "a|b|c", "x"));
    }
}
