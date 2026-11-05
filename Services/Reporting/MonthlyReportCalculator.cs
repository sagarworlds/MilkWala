using MilkWala.Models;

namespace MilkWala.Services.Reporting
{
    /// <summary>Figures shown on the vendor reports page for one calendar month.</summary>
    public sealed record MonthlyReport(
        decimal TotalSales,
        double TotalLitresSold,
        decimal TotalCollection,
        decimal TotalExpenses,
        decimal TotalProcurementCost,
        decimal NetProfit);

    /// <summary>Pure calculation of the monthly business summary, kept free of I/O so it can be unit tested.</summary>
    public static class MonthlyReportCalculator
    {
        /// <summary>
        /// Builds the summary for the calendar month containing <paramref name="month"/>.
        /// Net profit = sales - operating expenses - cost of milk bought from farmers; leaving the
        /// procurement cost out would overstate profit.
        /// </summary>
        public static MonthlyReport Calculate(
            DateTime month,
            IEnumerable<Delivery> deliveries,
            IEnumerable<Payment> payments,
            IEnumerable<Expense> expenses,
            IEnumerable<MilkProcurement> procurements)
        {
            var monthDeliveries = deliveries.Where(d => InMonth(d.DeliveryDate, month)).ToList();

            decimal sales = DeliveryStats.Revenue(monthDeliveries);
            double litres = DeliveryStats.Litres(monthDeliveries);
            decimal collection = payments.Where(p => InMonth(p.PaymentDate, month)).Sum(p => p.Amount);
            decimal expenseTotal = expenses.Where(e => InMonth(e.Date, month)).Sum(e => e.Amount);
            decimal procurementCost = procurements.Where(p => InMonth(p.Date, month)).Sum(p => p.TotalAmount);

            return new MonthlyReport(
                sales,
                litres,
                collection,
                expenseTotal,
                procurementCost,
                sales - expenseTotal - procurementCost);
        }

        private static bool InMonth(DateTime value, DateTime month) =>
            value.Year == month.Year && value.Month == month.Month;
    }
}
