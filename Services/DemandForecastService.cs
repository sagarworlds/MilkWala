using MilkWala.Models;
using MilkWala.Services.Reporting;

namespace MilkWala.Services
{
    /// <summary>Tells the vendor how much milk to have ready for a day.</summary>
    public interface IDemandForecastService
    {
        /// <summary>Milk to have ready tomorrow, one entry per milk type.</summary>
        Task<IReadOnlyList<DemandForecastResult>> ForecastTomorrowDemandAsync();
    }

    public class DemandForecastService : IDemandForecastService
    {
        private readonly LocalDbService _dbService;

        public DemandForecastService(LocalDbService dbService)
        {
            _dbService = dbService;
        }

        public async Task<IReadOnlyList<DemandForecastResult>> ForecastTomorrowDemandAsync()
        {
            await _dbService.InitAsync();

            return DemandForecaster.Forecast(
                DateTime.Today.AddDays(1),
                await _dbService.GetAllAsync<Subscription>(),
                await _dbService.GetAllAsync<Customer>(),
                await _dbService.GetAllAsync<SpecialOrder>());
        }
    }
}
