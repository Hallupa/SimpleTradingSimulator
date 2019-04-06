using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics.Helpers;

namespace TraderTools.Basics.Extensions
{
    public static class BrokerCandlesServiceExtensions
    {
        public static ICandle GetFirstCandleThatClosesBeforeDateTime(
            this IBrokersCandlesService service, string market, IBroker broker, Timeframe timeframe, DateTime dateTime, bool updateCandles = false)
        {
            var candles = service.GetCandles(broker, market, timeframe, updateCandles, maxCloseTimeUtc: dateTime);
            if (candles == null)
            {
                return null;
            }

            return CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, dateTime);
        }

        public static List<ICandle> GetCandlesUptoSpecificTime(this IBrokersCandlesService brokerCandles, IBroker broker, string market, Timeframe timeframe, bool updateCandles, DateTime? startUtc, DateTime? endUtc)
        {
            var allLargeChartCandles = brokerCandles.GetCandles(broker, market, timeframe != Timeframe.D1Tiger ? timeframe : Timeframe.D1, updateCandles, cacheData: false, minOpenTimeUtc: startUtc, maxCloseTimeUtc: endUtc);
            var smallestTimeframeCandles = brokerCandles.GetCandles(broker, market, Timeframe.M1, updateCandles, cacheData: false, maxCloseTimeUtc: endUtc);

            var largeChartCandles = new List<ICandle>();
            var endTicks = endUtc?.Ticks ?? -1;
            var endTimeTicks = endUtc?.Ticks;

            // Add complete candle
            for (var i = 0; i < allLargeChartCandles.Count; i++)
            {
                var currentCandle = allLargeChartCandles[i];
                if (endTimeTicks == null || currentCandle.CloseTimeTicks <= endTimeTicks)
                {
                    largeChartCandles.Add(currentCandle);
                }
            }

            // Add incomplete candle
            var latestCandleTimeTicks = largeChartCandles[largeChartCandles.Count - 1].CloseTimeTicks;
            double? open = null, close = null, high = null, low = null;
            long? openTimeTicks = null, closeTimeTicks = null;

            foreach (var smallestTimeframeCandle in smallestTimeframeCandles)
            {
                if (smallestTimeframeCandle.OpenTimeTicks >= latestCandleTimeTicks && (smallestTimeframeCandle.CloseTimeTicks <= endTicks || endTicks == -1))
                {
                    if (openTimeTicks == null) openTimeTicks = smallestTimeframeCandle.OpenTimeTicks;
                    if (open == null || smallestTimeframeCandle.Open < open)
                        open = smallestTimeframeCandle.Open;
                    if (high == null || smallestTimeframeCandle.High > high)
                        high = smallestTimeframeCandle.High;
                    if (low == null || smallestTimeframeCandle.Low < low) low = smallestTimeframeCandle.Low;

                    closeTimeTicks = smallestTimeframeCandle.CloseTimeTicks;
                    close = smallestTimeframeCandle.Close;
                }

                if (smallestTimeframeCandle.CloseTime() > endUtc)
                {
                    break;
                }
            }

            if (open != null)
            {
                largeChartCandles.Add(new Candle
                {
                    Open = open.Value,
                    Close = close.Value,
                    High = high.Value,
                    Low = low.Value,
                    CloseTimeTicks = closeTimeTicks.Value,
                    OpenTimeTicks = openTimeTicks.Value,
                    IsComplete = 0
                });
            }

            if (timeframe == Timeframe.D1Tiger)
            {
                largeChartCandles = TigerHelper.GetCandles(largeChartCandles, market, 13)
                    .Select(c => (ICandle)new Candle
                    {
                        High = c.High,
                        Low = c.Low,
                        Close = c.Close,
                        Open = c.Open,
                        CloseTimeTicks = c.CloseTimeTicks,
                        OpenTimeTicks = c.OpenTimeTicks,
                        IsComplete = c.IsComplete
                    }).ToList();
            }

            return largeChartCandles;
        }
    }
}