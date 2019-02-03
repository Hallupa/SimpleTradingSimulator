using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Abt.Controls.SciChart;
using Abt.Controls.SciChart.Common.Extensions;
using Abt.Controls.SciChart.Model.DataSeries;
using Abt.Controls.SciChart.Visuals.Annotations;
using Abt.Controls.SciChart.Visuals.RenderableSeries;
using TraderTools.Basics;
using TraderTools.Basics.Helpers;
using TraderTools.Core.UI.Controls;
using TraderTools.Core;

namespace TraderTools.Core.UI
{
    public static class ChartHelper
    {
        static ChartHelper()
        {
            LocalUtcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
        }

        public static TimeSpan LocalUtcOffset { get; private set; }

        public static void SetChartViewModelPriceData(IList<Candle> candles, ChartViewModel cvm, Timeframe timeframe)
        {
            var priceDataSeries = new OhlcDataSeries<DateTime, double>();
            var time = new DateTime(0);
            var xvalues = new List<DateTime>();
            var openValues = new List<double>();
            var highValues = new List<double>();
            var lowValues = new List<double>();
            var closeValues = new List<double>();

            for (var i = 0; i < candles.Count; i++)
            {
                if (timeframe == Timeframe.D1Tiger)
                {
                    time = DateTime.SpecifyKind(new DateTime(candles[i].OpenTimeTicks, DateTimeKind.Utc) + LocalUtcOffset, DateTimeKind.Local).AddMilliseconds(i);
                }
                else
                {
                    time = DateTime.SpecifyKind(new DateTime(candles[i].OpenTimeTicks, DateTimeKind.Utc) + LocalUtcOffset, DateTimeKind.Local);
                }

                xvalues.Add(time);
                openValues.Add((double)candles[i].Open);
                highValues.Add((double)candles[i].High);
                lowValues.Add((double)candles[i].Low);
                closeValues.Add((double)candles[i].Close);
            }

            priceDataSeries.Append(xvalues, openValues, highValues, lowValues, closeValues);

            var pricePaneVm = cvm.ChartPaneViewModels.Count > 0 ? cvm.ChartPaneViewModels[0] : null;
            if (pricePaneVm == null)
            {
                pricePaneVm = new ChartPaneViewModel(cvm, cvm.ViewportManager)
                {
                    IsFirstChartPane = true,
                    IsLastChartPane = false
                };

                pricePaneVm.ChartSeriesViewModels.Add(new ChartSeriesViewModel(priceDataSeries, new FastCandlestickRenderableSeries { AntiAliasing = false }));
                cvm.ChartPaneViewModels.Add(pricePaneVm);
            }
            else
            {
                pricePaneVm.ChartSeriesViewModels.Clear();
                pricePaneVm.ChartSeriesViewModels.Add(new ChartSeriesViewModel(priceDataSeries, new FastCandlestickRenderableSeries { AntiAliasing = false }));
            }
        }

        public static IDataSeries CreateIndicatorSeries(string market, IIndicator indicator, Color color, Timeframe timeframe, IList<Candle> candles)
        {
            var series = new XyDataSeries<DateTime, double>();
            var xvalues = new List<DateTime>();
            var yvalues = new List<double>();

            foreach (var candle in candles)
            {
                var signalAndValue = indicator.Process(new SimpleCandle(candle));
                if (indicator.IsFormed)
                {
                    xvalues.Add(DateTime.SpecifyKind(new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc) + LocalUtcOffset, DateTimeKind.Local));
                    yvalues.Add((double)signalAndValue.Value);
                }
                else
                {
                    xvalues.Add(DateTime.SpecifyKind(new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc) + LocalUtcOffset, DateTimeKind.Local));
                    yvalues.Add(double.NaN);
                }
            }

            series.Append(xvalues, yvalues);

            return series;
        }

        public static void AddIndicator(
            ChartPaneViewModel paneViewModel, string market, IIndicator indicator, Color color, Timeframe timeframe, IList<Candle> candles)
        {
            var series = CreateIndicatorSeries(market, indicator, color, timeframe, candles);

            paneViewModel.ChartSeriesViewModels.Add(new ChartSeriesViewModel(series, new FastLineRenderableSeries
            {
                AntiAliasing = false,
                SeriesColor = color,
                StrokeThickness = 2
            }));
        }

        private static DateTime? GetChartDateTime(DateTime? dateTime, IList<Candle> candles, Timeframe timeframe)
        {
            if (dateTime == null)
            {
                return null;
            }

            if (timeframe == Timeframe.D1Tiger)
            {
                var index = 0;
                while (index < candles.Count)
                {
                    if (candles[index].OpenTimeTicks >= dateTime.Value.Ticks)
                    {
                        return new DateTime(candles[index].CloseTimeTicks);
                    }

                    index++;
                }
            }

            return dateTime;
        }

        public static AnnotationCollection CreateTradeAnnotations(ChartViewModel cvm, bool basicAnnotations,
            Timeframe timeframe, IList<Candle> candles, TradeDetails trade)
        {
            // Setup annotations
            var annotations = new AnnotationCollection();
            if (candles.Count == 0) return annotations;

            var dataSeries = cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries;
            var startTime = GetChartDateTime(trade.StartDateTimeLocal, candles, timeframe);
            var endTimeLocal = GetChartDateTime(trade.CloseDateTimeLocal != null ? trade.CloseDateTimeLocal.Value : DateTime.Now, candles, timeframe);

            if (startTime != null && (trade.OrderPrice != null || trade.EntryPrice != null))
            {
                var price = trade.OrderPrice != null
                    ? trade.OrderPrice.Value
                    : trade.EntryPrice.Value;

                if (trade.EntryDateTimeLocal != null)
                {
                    AddBuySellMarker(trade.TradeDirection.Value, annotations, trade, GetChartDateTime(trade.EntryDateTimeLocal.Value, candles, timeframe).Value, price);
                }

                AddHorizontalLine(price, GetChartDateTime(startTime.Value, candles, timeframe).Value, GetChartDateTime(endTimeLocal.Value, candles, timeframe).Value,
                    dataSeries, annotations, trade, Colors.White);
            }

            // Add close price line
            AddCloseAnnotations(trade, annotations, dataSeries, candles, timeframe);

            // Add stop prices
            if (!basicAnnotations)
            {
                var stopPrices = trade.GetStopPrices();
                if (stopPrices.Count > 0)
                {
                    stopPrices.Add(new DatePrice(trade.CloseDateTime != null
                        ? GetChartDateTime(trade.CloseDateTime.Value.ToLocalTime(), candles, timeframe).Value
                        : GetChartDateTime(new DateTime(candles[candles.Count - 1].CloseTimeTicks, DateTimeKind.Utc).ToLocalTime(), candles, timeframe).Value, null));

                    AddLineAnnotations(stopPrices, cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        annotations, Colors.Red);
                }
            }

            // Add limit prices
            if (!basicAnnotations)
            {
                var limitPrices = trade.GetLimitPrices();
                if (limitPrices.Count > 0)
                {
                    limitPrices.Add(new DatePrice(trade.CloseDateTime != null
                        ? GetChartDateTime(trade.CloseDateTime.Value.ToLocalTime(), candles, timeframe).Value
                        : GetChartDateTime(new DateTime(candles[candles.Count - 1].CloseTimeTicks, DateTimeKind.Utc), candles, timeframe).Value, null));

                    AddLineAnnotations(limitPrices, cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        annotations, Colors.DodgerBlue);
                }
            }

            return annotations;
        }

        private static void AddBuySellMarker(TradeDirection direction, AnnotationCollection annotations, TradeDetails trade, DateTime timeLocal, decimal price)
        {
            var annotation = direction == TradeDirection.Long ? new BuyMarkerAnnotation() : (CustomAnnotation)new SellMarkerAnnotation();
            annotation.Width = 18;
            annotation.Height = 18;
            ((Path)annotation.Content).Stretch = Stretch.Fill;
            annotation.Margin = new Thickness(0, direction == TradeDirection.Long ? 5 : -5, 0, 0);
            annotation.DataContext = trade;
            var brush = new SolidColorBrush
            {
                Color = direction == TradeDirection.Long ? Colors.Green : Colors.DarkRed,
                Opacity = 0.7
            };

            ((Path)annotation.Content).Fill = brush;
            annotation.X1 = timeLocal;
            annotation.BorderThickness = new Thickness(20);
            annotation.Y1 = (double)price;
            annotations.Add(annotation);
        }

        public static void AddHorizontalLine(decimal price, DateTime start, DateTime end, IDataSeries dataSeries, AnnotationCollection annotations,
            TradeDetails trade, Color colour)
        {
            var dateStartIndex = dataSeries.FindIndex(start, SearchMode.RoundDown);
            var dateEndIndex = dataSeries.FindIndex(end, SearchMode.RoundUp);

            var lineAnnotation = new LineAnnotation
            {
                DataContext = trade,
                X1 = dateStartIndex,
                Y1 = price,
                X2 = dateEndIndex,
                Y2 = price,
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection(new[] { 2.0, 2.0 }),
                Opacity = 0.5,
                Stroke = new SolidColorBrush(colour)
            };
            annotations.Add(lineAnnotation);
        }

        private static void AddCloseAnnotations(TradeDetails trade, AnnotationCollection annotations, IDataSeries dataSeries, IList<Candle> candles, Timeframe timeframe)
        {
            if (trade.ClosePrice != null)
            {
                var oppositeTradeDirection = trade.TradeDirection.Value == TradeDirection.Long
                    ? TradeDirection.Short
                    : TradeDirection.Long;
                AddBuySellMarker(oppositeTradeDirection, annotations, trade, GetChartDateTime(trade.CloseDateTimeLocal.Value, candles, timeframe).Value,
                    trade.ClosePrice.Value);
                AddHorizontalLine(trade.ClosePrice.Value, GetChartDateTime(trade.EntryDateTimeLocal.Value, candles, timeframe).Value,
                    GetChartDateTime(trade.CloseDateTimeLocal.Value, candles, timeframe).Value, dataSeries, annotations, trade, Colors.Red);
            }
        }

        private static void AddLineAnnotations(
            List<DatePrice> prices, IDataSeries series, AnnotationCollection annotations, Color colour)
        {
            int? startIndex = null;
            decimal? currentPrice = null;
            foreach (var p in prices)
            {
                if (p.Price != currentPrice)
                {
                    // Price changed
                    if (currentPrice != null && startIndex != null)
                    {
                        var endIndex = series.FindIndex(p.Date.ToLocalTime(), SearchMode.Nearest);

                        if (endIndex == startIndex.Value)
                        {
                            endIndex++;
                        }

                        var brush = new SolidColorBrush(colour) { Opacity = 0.5 };
                        var annotation = new LineAnnotation
                        {
                            X1 = startIndex.Value,
                            X2 = endIndex,
                            Y1 = currentPrice.Value,
                            Y2 = currentPrice.Value,
                            Stroke = brush
                        };

                        annotations.Add(annotation);
                    }

                    startIndex = series.FindIndex(p.Date.ToLocalTime(), SearchMode.Nearest);
                    currentPrice = p.Price;
                }
            }
        }

        public static void SetChartViewModelVisibleRange(
            TradeDetails trade, ChartViewModel cvm, IList<Candle> candles, Timeframe timeframe)
        {
            if (candles.Count == 0) return;

            var startTime = trade.OrderDateTime ?? trade.EntryDateTime.Value;
            var endTime = trade.CloseDateTime ?? new DateTime(candles.Last().CloseTimeTicks, DateTimeKind.Utc);

            var startCandle = CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, startTime.ToLocalTime());

            var endCandle = CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, endTime.ToLocalTime()) ?? candles.Last();

            var candlesBeforeTrade = 25;
            var candlesAfterTrade = 25;

            var min = candles.IndexOf(startCandle.Value) - candlesBeforeTrade;
            var max = candles.IndexOf(endCandle) + candlesAfterTrade;

            if (min < 0)
            {
                min = 0;
            }

            SetChartXVisibleRange(cvm, min, max);

            var miny = double.NaN;
            var maxy = double.NaN;
            for (var i = min; i < candles.Count; i++)
            {
                if (double.IsNaN(miny) || candles[i].Low < miny) miny = candles[i].Low;
                if (double.IsNaN(maxy) || candles[i].High > maxy) maxy = candles[i].High;
            }

            if (trade.LimitPrice != null && trade.LimitPrice < (decimal)miny) miny = (double)trade.LimitPrice;
            if (trade.LimitPrice != null && trade.LimitPrice > (decimal)maxy) maxy = (double)trade.LimitPrice;
            if (trade.StopPrice != null && trade.StopPrice < (decimal)miny) miny = (double)trade.StopPrice;
            if (trade.StopPrice != null && trade.StopPrice > (decimal)maxy) maxy = (double)trade.StopPrice;
        }

        public static void SetChartXVisibleRange(ChartViewModel cvm, int min, int max)
        {
            if (min <= cvm.XVisibleRange.Max)
            {
                cvm.XVisibleRange.Min = min;
                cvm.XVisibleRange.Max = max;
            }
            else
            {
                cvm.XVisibleRange.Max = max;
                cvm.XVisibleRange.Min = min;
            }
        }
    }
}