using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MilkWala.Models;

namespace MilkWala.Services
{
    public class DemandForecastResult
    {
        public MilkType MilkType { get; set; }
        public double SubscribedLitres { get; set; }
        public double VacationPausedLitres { get; set; }
        public double SpecialOrderExtraLitres { get; set; }
        public double PredictedRequiredLitres => Math.Max(0, (SubscribedLitres - VacationPausedLitres) + SpecialOrderExtraLitres);
    }

    public class DemandForecastService
    {
        private readonly LocalDbService _dbService;

        public DemandForecastService(LocalDbService dbService)
        {
            _dbService = dbService;
        }

        public async Task<List<DemandForecastResult>> ForecastTomorrowDemandAsync()
        {
            await _dbService.InitAsync();

            var targetDate = DateTime.Today.AddDays(1);
            var subscriptions = await _dbService.GetAllAsync<Subscription>();
            var specialOrders = await _dbService.GetAllAsync<SpecialOrder>();

            var forecastList = new List<DemandForecastResult>();

            foreach (MilkType milkType in Enum.GetValues(typeof(MilkType)))
            {
                var typeSubs = subscriptions.Where(s => s.MilkType == milkType).ToList();

                double totalSub = typeSubs.Sum(s => s.QuantityLitres);
                double pausedSub = typeSubs.Where(s => s.IsPaused &&
                    (s.PauseStartDate == null || s.PauseStartDate <= targetDate) &&
                    (s.PauseEndDate == null || s.PauseEndDate >= targetDate)).Sum(s => s.QuantityLitres);

                double extraOrders = specialOrders
                    .Where(o => o.MilkType == milkType && o.TargetDate.Date == targetDate.Date && o.IsApproved)
                    .Sum(o => o.ExtraQuantityLitres);

                forecastList.Add(new DemandForecastResult
                {
                    MilkType = milkType,
                    SubscribedLitres = totalSub,
                    VacationPausedLitres = pausedSub,
                    SpecialOrderExtraLitres = extraOrders
                });
            }

            return forecastList;
        }
    }
}
