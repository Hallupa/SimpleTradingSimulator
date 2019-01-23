using System;
using TraderTools.Basics.Helpers;

namespace TraderTools.Basics.Extensions
{
    public static class BrokerCandlesServiceExtensions
    {
        public static Candle? GetFirstCandleThatClosesBeforeDateTime(this IBrokersCandlesService service, string market, IBroker broker, Timeframe timeframe, DateTime dateTime, bool updateCandles = false)
        {
            var candles = service.GetCandles(broker, market, timeframe, updateCandles);

            if (candles == null)
            {
                return null;
            }

            return CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, dateTime);
        }
    }
}