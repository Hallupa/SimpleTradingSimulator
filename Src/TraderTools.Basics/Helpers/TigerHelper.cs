using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics.Extensions;

namespace TraderTools.Basics.Helpers
{
    public struct TigerCandle
    {
        public BasicCandleAndIndicators Candle { get; set; }
        public byte LastDirection { get; set; }
        public float EmaSum { get; set; }
        public short EmaTotal { get; set; }

        public override string ToString()
        {
            return Candle.ToString();
        }
    }

    public static class TigerHelper
    {
        private const float StepPips = 10.0F;
        private const float OpeningPricePercentage = 0.50F;
        private const float ReversalPercentage = 2.00F;
        private const int EmaLength = 50;
        private const double Ema50Multiplier = 2.0 / (EmaLength + 1);

        public enum CreateTigerCandleMethod
        {
            UsingCompleteDayCandle,
            UsingPartialDayCandleFirstTigerCandleForDayCandle,
            UsingPartialDayCandleContinuationTigerCandle,
        }

        private static void CreateTigerCandle(float high, float low, float close, float open, DateTime openDate, DateTime closeDate,
            byte lastDirection, bool isComplete, TigerCandle? lastTigerCandle, List<TigerCandle> tigerCandles, int signalsCount)
        {
            var tigerCandle = new TigerCandle
            {
                Candle = new BasicCandleAndIndicators(openDate.Ticks, closeDate.Ticks, open, high, low, close, isComplete ? (byte)1 : (byte)0, signalsCount),
                LastDirection = lastDirection,
                EmaSum = 0,
                EmaTotal = 0
            };

            // Scenarios:
            // a [ complete day ]                 => [T][T][T][T]
            // b [ complete day ]  [partial 1]    => [T][T][T][T][P1][P1]
            // b [ complete day ]  [partial 2 ]   => [T][T][T][T][P1][P1][P2][P2][P2]
            // a [ complete day ]  [complete   ]  => [T][T][T][T][P1][P1][P2][P2][P2][T][T][T][T]

            // Scenario a
            var emaSum = lastTigerCandle != null ? lastTigerCandle.Value.EmaSum : (float)0.0;
            var emaTotal = lastTigerCandle != null ? lastTigerCandle.Value.EmaTotal : (short)0;
            var emaClose = lastTigerCandle != null ? lastTigerCandle.Value.Candle[Indicator.EMA50].Value : (float)0.0;

            if (emaTotal < EmaLength)
            {
                tigerCandle.EmaSum = emaSum + tigerCandle.Candle.Close;
                tigerCandle.EmaTotal = (short)(emaTotal + 1);
                tigerCandle.Candle.Set(Indicator.EMA50, new SignalAndValue(tigerCandle.EmaSum / tigerCandle.EmaTotal, false));
            }
            else
            {
                if (emaTotal < EmaLength + 5)
                {
                    tigerCandle.EmaSum = emaSum + tigerCandle.Candle.Close;
                    tigerCandle.EmaTotal = (short)(emaTotal + 1);
                }
                else
                {
                    tigerCandle.EmaSum = tigerCandle.Candle.Close;
                    tigerCandle.EmaTotal = emaTotal;
                }

                tigerCandle.Candle.Set(Indicator.EMA50, new SignalAndValue((float)((close - emaClose) * Ema50Multiplier + close), true));
            }

            tigerCandles.Add(tigerCandle);
        }

        public static void ProcessDayCandle<T>(
            ref T firstDayCandle, ref T dayCandle, bool isDayCandleComplete,
            List<TigerCandle> tigerCandles, string market,
            int signalsCount) where T : ISimpleCandle
        {
            var stepPipsAdj = (float)PipsHelper.GetOnePipInDecimals(market) * StepPips; //move to tigercandle
            //var isDayCandleComplete = doubleCandleWithComplete != null ? doubleCandleWithComplete.IsComplete : (byte)0;
            //var isPrevTigerCandleComplete = tigerCandles.Count > 0 && tigerCandles[tigerCandles.Count - 1].Candle.IsComplete;

            var method = isDayCandleComplete ? CreateTigerCandleMethod.UsingCompleteDayCandle : CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle;

            var prevCandlesTigerCandlesCount = -1;
            var prevCandlesIndexUsed = -1;

            Func<int> getPrevCandleToUse = () =>
            {
                if (method == CreateTigerCandleMethod.UsingCompleteDayCandle
                    || method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle)
                {
                    if (tigerCandles.Count == prevCandlesTigerCandlesCount)
                    {
                        return prevCandlesIndexUsed;
                    }

                    for (var i = tigerCandles.Count - 1; i >= 0; i--)
                    {
                        if (tigerCandles[i].Candle.IsComplete == 1)
                        {
                            prevCandlesTigerCandlesCount = tigerCandles.Count;
                            prevCandlesIndexUsed = i;
                            return i;
                        }
                    }
                }
                else if (method == CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle)
                {
                    return tigerCandles.Count - 1;
                }

                return -1;
            };

            Func<float> currentClose = () =>
            {
                var index = getPrevCandleToUse();
                if (index == -1) return 0;
                return tigerCandles[index].Candle.Close;
            };
            Func<byte> currentLastDirection = () =>
            {
                var index = getPrevCandleToUse();
                if (index == -1) return 255;
                return tigerCandles[index].LastDirection;
            };
            Func<float> currentOpen = () =>
            {
                var index = getPrevCandleToUse();
                if (index == -1) return 0;
                return tigerCandles[index].Candle.Open;
            };
            
            if (currentLastDirection() == 255)
            {
                float open_val = (float)Math.Floor(firstDayCandle.Close / stepPipsAdj) * stepPipsAdj;
                while (dayCandle.Close > open_val + stepPipsAdj)
                {
                    var prevCandleIndex = getPrevCandleToUse();
                    var open = open_val;
                    var close = open + stepPipsAdj;
                    var low = open;
                    var high = close;
                    //var date = ret.Count == 0 ? new DateTime(0) : sourceDate(ret.Count - 1).AddSeconds(1);

                    // Set date as source date so it doesn't cause issues in the strategy runner
                    CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 1, isDayCandleComplete,
                        prevCandleIndex > 0 ? tigerCandles[prevCandleIndex] : (TigerCandle?) null, tigerCandles, signalsCount);
                    if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;

                    open_val = open_val + stepPipsAdj;
                }

                open_val = (float)Math.Floor(firstDayCandle.Close / stepPipsAdj) * stepPipsAdj;

                while (dayCandle.Close < open_val - stepPipsAdj)
                {
                    var prevCandleIndex = getPrevCandleToUse();
                    var open = open_val;
                    var close = open - stepPipsAdj;
                    var high = open;
                    var low = close;
                    //var date = ret.Count == 0 ? new DateTime(0) : sourceDate(ret.Count - 1).AddSeconds(1);

                    // Set date as source date so it doesn't cause issues in the strategy runner
                    CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 1, isDayCandleComplete,
                        prevCandleIndex > 0 ? tigerCandles[prevCandleIndex] : (TigerCandle?)null, tigerCandles, signalsCount);
                    if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;

                    open_val = open_val - stepPipsAdj;
                }
            }
            else
            {
                if (currentLastDirection() == 1)
                {
                    while (dayCandle.Close > currentClose() + stepPipsAdj)
                    {
                        var prevCandleIndex = getPrevCandleToUse();
                        var prevCandle = prevCandleIndex >= 0 ? tigerCandles[prevCandleIndex] : (TigerCandle?)null;
                        float open;
                        if (prevCandle.Value.Candle.Close > prevCandle.Value.Candle.Open)
                            open = prevCandle.Value.Candle.Open + (stepPipsAdj * OpeningPricePercentage);
                        else
                            open = prevCandle.Value.Candle.Close + (stepPipsAdj * OpeningPricePercentage);

                        var close = open + stepPipsAdj;
                        var low = Math.Min(open, close);
                        var high = Math.Max(open, close);
                        //var date = currentDate() < sourceDate(i) ? sourceDate(i) : currentDate().AddSeconds(1);

                        // Set date as source date so it doesn't cause issues in the strategy runner
                        CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 1, isDayCandleComplete,
                            tigerCandles[prevCandleIndex], tigerCandles, signalsCount);
                        if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;
                    }

                    var prevIndex = getPrevCandleToUse();
                    var prev = prevIndex >= 0 ? tigerCandles[prevIndex] : (TigerCandle?)null;
                    if (dayCandle.Close < prev.Value.Candle.Close - (ReversalPercentage) * stepPipsAdj)
                    {
                        while (dayCandle.Close < currentClose() - stepPipsAdj)
                        {
                            var prevCandleIndex = getPrevCandleToUse();
                            var prevCandle = tigerCandles[prevCandleIndex];

                            float open;
                            if (prevCandle.Candle.Close > prevCandle.Candle.Open)
                                open = (float)currentClose() - (stepPipsAdj * OpeningPricePercentage);
                            else
                                open = prevCandle.Candle.Open - (stepPipsAdj * OpeningPricePercentage);

                            var close = open - stepPipsAdj;
                            var low = (float)Math.Min(open, close);
                            var high = (float)Math.Max(open, close);

                            //var date = currentDate() < sourceDate(i) ? sourceDate(i) : currentDate().AddSeconds(1);

                            // Set date as source date so it doesn't cause issues in the strategy runner
                            CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 0, isDayCandleComplete, prevCandle, tigerCandles, signalsCount);
                            if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;
                        }
                    }
                }

                if (currentLastDirection() == 0)
                {
                    while (dayCandle.Close < currentClose() - stepPipsAdj)
                    {
                        var prevCandleIndex = getPrevCandleToUse();
                        var prevCandle = prevCandleIndex >= 0 ? tigerCandles[prevCandleIndex] : (TigerCandle?)null;

                        float open;
                        if (prevCandle.Value.Candle.Close < prevCandle.Value.Candle.Open)
                            open = prevCandle.Value.Candle.Open - (stepPipsAdj * OpeningPricePercentage);
                        else
                            open = prevCandle.Value.Candle.Close - (stepPipsAdj * OpeningPricePercentage);

                        var close = open - stepPipsAdj;
                        var low = Math.Min(open, close);
                        var high = Math.Max(open, close);
                        //var date = currentDate() < sourceDate(i) ? sourceDate(i) : currentDate().AddSeconds(1);

                        // Set date as source date so it doesn't cause issues in the strategy runner
                        CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 0,
                            isDayCandleComplete, prevCandle, tigerCandles, signalsCount);
                        if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;
                    }

                    if (dayCandle.Close > currentClose() + (ReversalPercentage) * stepPipsAdj)
                    {
                        //lastDirection = 1;
                        while (dayCandle.Close > currentClose() + stepPipsAdj)
                        {
                            var prevCandleIndex = getPrevCandleToUse();
                            var prevCandle = prevCandleIndex >= 0 ? tigerCandles[prevCandleIndex] : (TigerCandle?)null;

                            float open;
                            if (currentClose() < currentOpen())
                                open = prevCandle.Value.Candle.Close + (stepPipsAdj * OpeningPricePercentage);
                            else
                                open = prevCandle.Value.Candle.Open + (stepPipsAdj * OpeningPricePercentage);

                            var close = open + stepPipsAdj;
                            var low = Math.Min(open, close);
                            var high = Math.Max(open, close);
                            //var date = currentDate() < sourceDate(i) ? sourceDate(i) : currentDate().AddSeconds(1);

                            // Set date as source date so it doesn't cause issues in the strategy runner
                            CreateTigerCandle(high, low, close, open, dayCandle.OpenTime(), dayCandle.CloseTime(), 1, isDayCandleComplete, prevCandle, tigerCandles, signalsCount);
                            if (method == CreateTigerCandleMethod.UsingPartialDayCandleFirstTigerCandleForDayCandle) method = CreateTigerCandleMethod.UsingPartialDayCandleContinuationTigerCandle;
                        }
                    }
                }
            }
        }

        public static List<ISimpleCandle> GetCandles(List<ICandle> dayCandles, string market, int signalsCount)
        {
            var tigerCandles = new List<TigerCandle>();

            var firstdayCandle = new SimpleCandle(dayCandles[0]);
            for (var i = 0; i < dayCandles.Count; i++)
            {
                var dayCandle = dayCandles[i];
                var basicDayCandle = new SimpleCandle(dayCandle);
                ProcessDayCandle(ref firstdayCandle, ref basicDayCandle, dayCandle.IsComplete == 1, tigerCandles, market, signalsCount);
            }

            return tigerCandles.Select(x => (ISimpleCandle)x.Candle).ToList();
        }
    }
}