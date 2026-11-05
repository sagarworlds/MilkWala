using MilkWala.Models;
using MilkWala.Services.Reporting;
using Xunit;

namespace MilkWala.Tests;

public class MonthlyReportCalculatorTests
{
    private static readonly DateTime November = new(2026, 11, 15);

    [Fact]
    public void Only_records_from_the_requested_month_are_counted()
    {
        var deliveries = new[]
        {
            new Delivery { DeliveryDate = new DateTime(2026, 11, 1), Status = DeliveryStatus.Delivered, QuantityLitres = 2, RatePerLitre = 50m },
            new Delivery { DeliveryDate = new DateTime(2026, 10, 31), Status = DeliveryStatus.Delivered, QuantityLitres = 9, RatePerLitre = 50m },
            new Delivery { DeliveryDate = new DateTime(2025, 11, 5), Status = DeliveryStatus.Delivered, QuantityLitres = 9, RatePerLitre = 50m },
        };
        var payments = new[]
        {
            new Payment { PaymentDate = new DateTime(2026, 11, 3), Amount = 300m },
            new Payment { PaymentDate = new DateTime(2026, 12, 1), Amount = 999m },
        };
        var expenses = new[]
        {
            new Expense { Date = new DateTime(2026, 11, 2), Amount = 20m },
            new Expense { Date = new DateTime(2026, 9, 2), Amount = 999m },
        };

        var report = MonthlyReportCalculator.Calculate(November, deliveries, payments, expenses, Array.Empty<MilkProcurement>());

        Assert.Equal(100m, report.TotalSales);
        Assert.Equal(2.0, report.TotalLitresSold);
        Assert.Equal(300m, report.TotalCollection);
        Assert.Equal(20m, report.TotalExpenses);
    }

    [Fact]
    public void Net_profit_subtracts_expenses_and_farmer_milk_purchases()
    {
        var deliveries = new[]
        {
            new Delivery { DeliveryDate = new DateTime(2026, 11, 4), Status = DeliveryStatus.Delivered, QuantityLitres = 100, RatePerLitre = 60m }, // 6000
        };
        var expenses = new[] { new Expense { Date = new DateTime(2026, 11, 4), Amount = 500m } };
        var procurements = new[]
        {
            new MilkProcurement { Date = new DateTime(2026, 11, 4), Litres = 100, RatePerLitre = 45m }, // 4500
        };

        var report = MonthlyReportCalculator.Calculate(November, deliveries, Array.Empty<Payment>(), expenses, procurements);

        Assert.Equal(4500m, report.TotalProcurementCost);
        Assert.Equal(1000m, report.NetProfit);
    }

    [Fact]
    public void Skipped_and_pending_deliveries_do_not_count_as_sales()
    {
        var deliveries = new[]
        {
            new Delivery { DeliveryDate = new DateTime(2026, 11, 4), Status = DeliveryStatus.Skipped, QuantityLitres = 3, RatePerLitre = 60m },
            new Delivery { DeliveryDate = new DateTime(2026, 11, 5), Status = DeliveryStatus.Pending, QuantityLitres = 3, RatePerLitre = 60m },
        };

        var report = MonthlyReportCalculator.Calculate(November, deliveries, Array.Empty<Payment>(), Array.Empty<Expense>(), Array.Empty<MilkProcurement>());

        Assert.Equal(0m, report.TotalSales);
        Assert.Equal(0.0, report.TotalLitresSold);
        Assert.Equal(0m, report.NetProfit);
    }

    [Fact]
    public void Empty_inputs_produce_an_all_zero_report()
    {
        var report = MonthlyReportCalculator.Calculate(November, [], [], [], []);

        Assert.Equal(new MonthlyReport(0m, 0.0, 0m, 0m, 0m, 0m), report);
    }
}
