using MilkWala.Models;
using MilkWala.Services.Reporting;
using Xunit;

namespace MilkWala.Tests;

public class StatementFormatterTests
{
    private static readonly DateTime November = new(2026, 11, 20);

    private static Customer Rahul() => new()
    {
        Id = 1, Name = "Rahul Sharma", Mobile = "9876543210", WalletBalance = 407m, OutstandingBalance = 63m
    };

    private static Delivery D(int day, DeliveryStatus status, double litres = 1.5, decimal rate = 62m, int month = 11) => new()
    {
        DeliveryDate = new DateTime(2026, month, day), Status = status, QuantityLitres = litres, RatePerLitre = rate, MilkType = MilkType.Cow
    };

    [Fact]
    public void Shows_the_period_of_the_data_not_todays_month()
    {
        var text = StatementFormatter.Build(Rahul(), new[] { D(3, DeliveryStatus.Delivered, month: 9) }, new DateTime(2026, 9, 15));

        Assert.Contains("Period: September 2026", text);
    }

    [Fact]
    public void Lists_each_billed_delivery_and_totals_them()
    {
        var text = StatementFormatter.Build(Rahul(), new[]
        {
            D(1, DeliveryStatus.Delivered),            // 93.00
            D(2, DeliveryStatus.Extra, 2, 62m),        // 124.00
            D(3, DeliveryStatus.Skipped),
            D(4, DeliveryStatus.Holiday, 0),
        }, November);

        Assert.Contains("01 Nov  1.5 L Cow  ₹93.00", text);
        Assert.Contains("02 Nov  2.0 L Cow  ₹124.00", text);
        Assert.DoesNotContain("03 Nov", text); // skipped days are not billed lines
        Assert.Contains("Days delivered: 2", text);
        Assert.Contains("Days skipped / on hold: 2", text);
        Assert.Contains("Total litres: 3.5 L", text);
        Assert.Contains("Total bill: ₹217.00", text);
    }

    [Fact]
    public void Shows_wallet_and_dues()
    {
        var text = StatementFormatter.Build(Rahul(), Array.Empty<Delivery>(), November);

        Assert.Contains("Wallet balance: ₹407.00", text);
        Assert.Contains("Outstanding dues: ₹63.00", text);
        Assert.Contains("*Rahul Sharma*", text);
        Assert.Contains("Mobile: 9876543210", text);
    }

    [Fact]
    public void Ignores_deliveries_from_other_months()
    {
        var text = StatementFormatter.Build(Rahul(), new[]
        {
            D(1, DeliveryStatus.Delivered),
            D(30, DeliveryStatus.Delivered, month: 10),
        }, November);

        Assert.Contains("Days delivered: 1", text);
        Assert.Contains("Total bill: ₹93.00", text);
        Assert.DoesNotContain("30 Oct", text);
    }

    [Fact]
    public void A_month_without_deliveries_says_so()
    {
        var text = StatementFormatter.Build(Rahul(), Array.Empty<Delivery>(), November);

        Assert.Contains("No deliveries this month.", text);
        Assert.Contains("Total bill: ₹0.00", text);
        Assert.DoesNotContain("skipped", text);
    }

    [Fact]
    public void Lines_are_in_date_order_and_the_text_is_culture_independent()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var text = StatementFormatter.Build(Rahul(), new[] { D(9, DeliveryStatus.Delivered), D(2, DeliveryStatus.Delivered) }, November);

            Assert.True(text.IndexOf("02 Nov", StringComparison.Ordinal) < text.IndexOf("09 Nov", StringComparison.Ordinal));
            Assert.Contains("₹93.00", text); // not "93,00"
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_missing_customer_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => StatementFormatter.Build(null!, Array.Empty<Delivery>(), November));
    }
}
