using System;
using System.Collections.Generic;
using System.Linq;

namespace TraderTools.Basics
{
    public class ChartDataRunner
    {
        private List<ICandle> _lowestTimeframeCandles;
        private Timeframe _lowestTimeframe;
        private List<Timeframe> _timeframesExcludingLowest;

        public ChartDataRunner(List<(List<ICandle> Candles, Timeframe Timeframe)> allTimeframesCandles)
        {
            foreach (var candlesAndTimeframe in allTimeframesCandles)
            {
                AllCandles.Add(candlesAndTimeframe.Timeframe, candlesAndTimeframe.Candles);
                CandlesIndexes[candlesAndTimeframe.Timeframe] = 0;
                CurrentCandles[candlesAndTimeframe.Timeframe] = new List<ICandle>();
            }

            var lowestTimeframe = allTimeframesCandles.OrderBy(x => x.Timeframe).First();
            _lowestTimeframeCandles = lowestTimeframe.Candles;
            _lowestTimeframe = lowestTimeframe.Timeframe;
            _timeframesExcludingLowest = allTimeframesCandles.Select(x => x.Timeframe).Where(x => x != _lowestTimeframe).ToList();
        }

        public TimeframeLookup<int> CandlesIndexes { get; } = new TimeframeLookup<int>();
        public TimeframeLookup<List<ICandle>> AllCandles { get; }= new TimeframeLookup<List<ICandle>>();
        public TimeframeLookup<List<ICandle>> CurrentCandles { get; } = new TimeframeLookup<List<ICandle>>();
        public ICandle LatestSmallestTimeframeCandle { get; private set; }
        public bool IsComplete { get; private set; }

        public void ProgressLowestTimefameCandle()
        {
            ProgressTime(0, true);
        }

        public void ProgressTime(long progressToDateTicks, bool minOneCandleProgression = false)
        {
            while (CandlesIndexes[_lowestTimeframe] < AllCandles[_lowestTimeframe].Count && 
                   (AllCandles[_lowestTimeframe][CandlesIndexes[_lowestTimeframe]].CloseTimeTicks <= progressToDateTicks || minOneCandleProgression))
            {
                var lowestCandle = AllCandles[_lowestTimeframe][CandlesIndexes[_lowestTimeframe]];
                var endDateTicks = lowestCandle.CloseTimeTicks;
                CurrentCandles[_lowestTimeframe].Add(lowestCandle);

                if (endDateTicks > progressToDateTicks && !minOneCandleProgression) break;

                minOneCandleProgression = false;
                foreach (var timeframe in _timeframesExcludingLowest)
                {
                    var candles = AllCandles[timeframe];
                    var currentCandles = CurrentCandles[timeframe];
                    var lastCandleChecked = false;
                    var candlesAdded = false;

                    // Add in-range candles
                    while (CandlesIndexes[timeframe] < candles.Count && candles[CandlesIndexes[timeframe]].CloseTimeTicks <= endDateTicks)
                    {
                        // Remove incomplete candle if required
                        if (!lastCandleChecked)
                        {
                            lastCandleChecked = true;
                            if (currentCandles.Count > 0 && currentCandles[currentCandles.Count - 1].IsComplete == 0)
                            {
                                currentCandles.RemoveAt(currentCandles.Count - 1);
                            }
                        }

                        // Add candle
                        currentCandles.Add(candles[CandlesIndexes[timeframe]]);
                        CandlesIndexes[timeframe]++;
                        candlesAdded = true;
                    }

                    // Add/update incomplete candle if no candle was added
                    if (!candlesAdded)
                    {
                        if (currentCandles.Count > 0 && currentCandles[currentCandles.Count - 1].IsComplete == 0)
                        {
                            var candle = currentCandles[currentCandles.Count - 1];
                            candle.Close = lowestCandle.Close;
                            candle.CloseTimeTicks = lowestCandle.CloseTimeTicks;
                            if (lowestCandle.High > candle.High) candle.High = lowestCandle.High;
                            if (lowestCandle.Low < candle.Low) candle.Low = lowestCandle.Low;
                        }
                        else
                        {
                            currentCandles.Add(new Candle
                            {
                                Open = lowestCandle.Open,
                                Close = lowestCandle.Close,
                                CloseTimeTicks = lowestCandle.CloseTimeTicks,
                                OpenTimeTicks = lowestCandle.OpenTimeTicks,
                                High = lowestCandle.High,
                                Low = lowestCandle.Low,
                                IsComplete =  0,
                                Timeframe = (int)timeframe,
                                Id = Guid.NewGuid()
                            });
                        }
                    }
                }

                LatestSmallestTimeframeCandle = CurrentCandles[_lowestTimeframe][CandlesIndexes[_lowestTimeframe]];
                CandlesIndexes[_lowestTimeframe]++;
            }

            IsComplete = CandlesIndexes[_lowestTimeframe] == AllCandles[_lowestTimeframe].Count;

            /*var timeframeIndicators = IndicatorsHelper.CreateIndicators(strategy.CreateTimeframeIndicators());

            var timeframeMaxIndicatorValues = new TimeframeLookup<int>();
            foreach (var kvp in timeframeIndicators)
            {
                timeframeMaxIndicatorValues[kvp.Key] = kvp.Value != null ? kvp.Value.Max(t => (int)t.Item1) + 1 : 0;
            }

            var m15Candles = timeframeAllCandles[Timeframe.M15];
            var timeframeExclD1Tiger = timeframes.Where(t => t != Timeframe.D1Tiger).ToList();
            var timeframeAllCandlesProcessed = new TimeframeLookupBasicCandleAndIndicators();
            var timeframeCandleIndexes = new TimeframeLookup<int>();
            var m15TimeframeLookupIndex = TimeframeLookup<int>.GetLookupIndex(Timeframe.M15);

            for (var i = 0; i < m15Candles.Count; i++)
            {
                var m15Candle = timeframeAllCandles[m15TimeframeLookupIndex][i];

                // See if any other candles have completed
                foreach (var timeframe in timeframeExclD1Tiger)
                {
                    if (i == 0)
                    {
                        timeframeAllCandlesProcessed[timeframe] = new List<BasicCandleAndIndicators>();
                    }

                    // Try to added completed candles
                    var completedCandleAdded = false;
                    var timeframeLookupIndex = TimeframeLookup<int>.GetLookupIndex(timeframe);
                    for (var ii = timeframeCandleIndexes[timeframeLookupIndex]; ii < timeframeAllCandles[timeframeLookupIndex].Count; ii++)
                    {
                        var timeframeCandle = timeframeAllCandles[timeframeLookupIndex][ii];

                        // Look for completed candle
                        if (timeframeCandle.CloseTimeTicks <= m15Candle.CloseTimeTicks)
                        {
                            completedCandleAdded = true;
                            var candleWithIndicators = new BasicCandleAndIndicators(timeframeCandle, timeframeMaxIndicatorValues[timeframeLookupIndex]);
                            //UpdateIndicators(timeframeIndicators, timeframeLookupIndex, candleWithIndicators);
                            timeframeCandleIndexes[timeframeLookupIndex] = ii + 1;
                            timeframeAllCandlesProcessed[timeframeLookupIndex].Add(candleWithIndicators);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Try to add incomplete candle
                    if (!completedCandleAdded)
                    {
                        BasicCandleAndIndicators? prevCandle = timeframeAllCandlesProcessed[timeframeLookupIndex].Count > 0
                            ? timeframeAllCandlesProcessed[timeframeLookupIndex][timeframeAllCandlesProcessed[timeframeLookupIndex].Count - 1]
                            : (BasicCandleAndIndicators?)null;

                        if (prevCandle != null && prevCandle.Value.IsComplete == 0)
                        {
                            // Add updated incomplete candle
                            var incompleteCandle = new BasicCandleAndIndicators(
                                prevCandle.Value.OpenTimeTicks,
                                m15Candle.CloseTimeTicks,
                                prevCandle.Value.Open,
                                (float)(m15Candle.High > prevCandle.Value.High ? m15Candle.High : prevCandle.Value.High),
                                (float)(m15Candle.Low < prevCandle.Value.Low ? m15Candle.Low : prevCandle.Value.Low),
                                (float)m15Candle.Close,
                                0,
                                timeframeMaxIndicatorValues[timeframeLookupIndex]
                            );

                            timeframeAllCandlesProcessed[timeframeLookupIndex].Add(incompleteCandle);
                            //UpdateIndicators(timeframeIndicators, timeframeLookupIndex, incompleteCandle);
                        }
                        else
                        {
                            // Add new incomplete candle
                            var incompleteCandle = new BasicCandleAndIndicators(
                                m15Candle.OpenTimeTicks,
                                m15Candle.CloseTimeTicks,
                                (float)m15Candle.Open,
                                (float)m15Candle.High,
                                (float)m15Candle.Low,
                                (float)m15Candle.Close,
                                0,
                                timeframeMaxIndicatorValues[timeframeLookupIndex]
                            );

                            timeframeAllCandlesProcessed[timeframeLookupIndex].Add(incompleteCandle);
                            //UpdateIndicators(timeframeIndicators, timeframeLookupIndex, incompleteCandle);
                        }
                    }
                }
            }*/

            /*
            var lowestTimeframeCandle = CurrentCandles[_lowestTimeframe][CandlesIndexes[_lowestTimeframe]];

            // Setup
            var completedCandleAddedExcludingM1 = false;
            var candleAddedExcludingM1 = false;

            // Update candles for each timeframe
            foreach (var timeframe in _timeframesExcludingLowest)
            {
                var timeframeLookupIndex = TimeframeLookup<int>.GetLookupIndex(timeframe);
                var timeframeAllCandles = AllCandles[timeframeLookupIndex];
                var timeframeCurrentCandles = CurrentCandles[timeframeLookupIndex];

                for (var ii = CandlesIndexes[timeframeLookupIndex]; ii < timeframeAllCandles.Count; ii++)
                {
                    var timeframeCandle = timeframeAllCandles[ii];
                    if (timeframeCandle.CloseTimeTicks <= lowestTimeframeCandle.CloseTimeTicks)
                    {
                        // Remove incomplete candles if not D1 Tiger or if is D1 Tiger and new candle is complete
                        if (timeframe != Timeframe.D1Tiger ||
                            (timeframe == Timeframe.D1Tiger &&
                             (timeframeCandle.IsComplete == 1 ||
                              (timeframeCurrentCandles.Count > 0 &&
                               timeframeCurrentCandles[timeframeCurrentCandles.Count - 1].CloseTimeTicks !=
                               timeframeCandle.CloseTimeTicks))))
                        {
                            for (var iii = timeframeCurrentCandles.Count - 1; iii >= 0; iii--)
                            {
                                if (timeframeCurrentCandles[iii].IsComplete == 0)
                                {
                                    timeframeCurrentCandles.RemoveAt(iii);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        timeframeCurrentCandles.Add(timeframeCandle);
                        CandlesIndexes[timeframeLookupIndex] = ii + 1;
                        candleAddedExcludingM1 = true;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (candleAddedExcludingM1)
            {
                // Update open trades
                //UpdateOpenTrades(strategy, market, openTrades, timeframesCurrentCandles);
            }*/

            // Try to fill or expire orders
            //TryToFillOrExpireOrders(orders, m1Candle, openTrades);
            // Try to close any open trades
            //TryToCloseOpenTrades(openTrades, m1Candle);
        }



        /*var allCandlesTimeframeToIncrement = AllCandles[_timeframeToIncrement];
        if (CandlesIndexes[_timeframeToIncrement] >= allCandlesTimeframeToIncrement.Count)
        {
            return;
        }

        CandlesIndexes[_timeframeToIncrement]++;
        var timeframeToIncrementNewCandle = allCandlesTimeframeToIncrement[CandlesIndexes[_timeframeToIncrement]];
        CurrentCandles[_timeframeToIncrement].Add(timeframeToIncrementNewCandle);
        var endDateTicks = timeframeToIncrementNewCandle.CloseTimeTicks;

        // Add complete candles
        foreach (var timeframeAndCandles in AllCandles)
        {
            if (timeframeAndCandles.Key == _timeframeToIncrement) continue;

            var candles = CurrentCandles[timeframeAndCandles.Key];
            var lastCandleChecked = false;

            // Add existing candles
            while (candles[CandlesIndexes[timeframeAndCandles.Key]].CloseTimeTicks <= endDateTicks)
            {
                if (!lastCandleChecked)
                {
                    lastCandleChecked = true;
                    if (candles.Count > 0 && candles[candles.Count - 1].IsComplete == 0)
                    {
                        candles.RemoveAt(candles.Count - 1);
                    }
                }

                candles.Add(candles[CandlesIndexes[timeframeAndCandles.Key]]);
                CandlesIndexes[timeframeAndCandles.Key]++;
            }
        }*/

    }
}