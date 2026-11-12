using MilkWala.Helpers;
using Xunit;

namespace MilkWala.Tests;

public class WhatsAppLinkTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("+91 98765 43210")]
    [InlineData("919876543210")]
    [InlineData("098765-43210")]
    public void Any_common_format_becomes_the_same_link(string mobile)
    {
        Assert.StartsWith("https://wa.me/919876543210?text=", WhatsAppLink.Build(mobile, "hi"));
    }

    [Fact]
    public void The_message_is_url_encoded_including_line_breaks_and_symbols()
    {
        var link = WhatsAppLink.Build("9876543210", "Total: ₹93.00\n*Thanks* & bye");

        Assert.EndsWith("?text=" + Uri.EscapeDataString("Total: ₹93.00\n*Thanks* & bye"), link);
        Assert.DoesNotContain(" ", link);
        Assert.DoesNotContain("\n", link);
        Assert.DoesNotContain("&", link!.Split('?')[1]); // the & must be encoded so it cannot cut the message off
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("not a number")]
    public void An_invalid_number_gives_no_link(string? mobile)
    {
        Assert.Null(WhatsAppLink.Build(mobile, "hi"));
    }
}
