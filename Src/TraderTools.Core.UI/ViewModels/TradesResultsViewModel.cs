using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Hallupa.Library;
using TraderTools.Basics;

namespace TraderTools.Core.UI.ViewModels
{
    public class TradesResultsViewModel : DependencyObject
    {
        private readonly Func<List<TradeDetails>> _getTradesFunc;
        private string _selectedResultOption = "Summary";
        private Dispatcher _dispatcher;

        public ObservableCollection<TradesResult> Results { get; } = new ObservableCollection<TradesResult>();

        public List<string> ResultOptions { get; private set; } = new List<string>
        {
            "Summary",
            "Markets",
            "Months",
            "Timeframes",
            "Strategies",
            "Grouped (10)"
        };

        public TradesResultsViewModel(Func<List<TradeDetails>> getTradesFunc)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _getTradesFunc = getTradesFunc;
        }

        public static readonly DependencyProperty ShowOptionsProperty = DependencyProperty.Register(
            "ShowOptions", typeof(bool), typeof(TradesResultsViewModel), new PropertyMetadata(true));

        public bool ShowOptions
        {
            get { return (bool) GetValue(ShowOptionsProperty); }
            set { SetValue(ShowOptionsProperty, value); }
        }

        public bool ShowProfit { get; set; } = false;

        public bool AdvStrategyNaming { get; set; } = false;

        public string SelectedResultOption
        {
            get => _selectedResultOption;
            set
            {
                if (_selectedResultOption == value)
                {
                    return;
                }

                _selectedResultOption = value;
                Results.Clear();

                Task.Run(() => { UpdateResults(); });
            }
        }

        public void UpdateResults()
        {
            var trades = _getTradesFunc();
            var completedTrades = trades.Where(t => t.CloseDateTime != null).ToList();
            IEnumerable<IGrouping<string, TradeDetails>> groupedTrades = null;

            switch (SelectedResultOption)
            {
                case "Summary":
                    groupedTrades = trades.GroupBy(x => "All trades").ToList();
                    break;
                case "Markets":
                    groupedTrades = trades.GroupBy(x => x.Market).ToList();
                    break;
                case "Months":
                    var now = DateTime.Now;
                    groupedTrades = trades.GroupBy(x =>
                        x.CloseDateTimeLocal != null ? $"{x.CloseDateTimeLocal.Value.Year}/{x.CloseDateTimeLocal.Value.Month:00}"
                            : $"{now.Year}/{now.Month:00}").ToList();
                    break;
                case "Timeframes":
                    groupedTrades = trades.GroupBy(x => $"{x.Timeframe}").ToList();
                    break;
                case "Strategies":

                    if (AdvStrategyNaming)
                    {
                        var regex = new Regex("#[a-zA-Z0-9&]*");
                        groupedTrades = trades.Where(t => t.RMultiple != null).SelectMany(t =>
                        {
                            var ret = new List<(string Name, TradeDetails Trade)>();

                            if (!string.IsNullOrEmpty(t.Comments))
                            {
                                foreach (Match match in regex.Matches(t.Comments))
                                {
                                    ret.Add((match.Value, t));
                                }
                            }
                            else
                            {
                                ret.Add((string.Empty, t));
                            }

                            return ret;
                        }).GroupBy(x => x.Name, x => x.Trade).ToList();
                    }
                    else
                    {
                        groupedTrades = trades.GroupBy(x => x.Comments).ToList();
                    }

                    break;
                case "Grouped (10)":
                    var reversedCompletedTrades = completedTrades.ToList();
                    reversedCompletedTrades.Reverse();
                    groupedTrades = reversedCompletedTrades.GroupBy(x =>
                    {
                        var start = (((int)(reversedCompletedTrades.IndexOf(x) / 10.0)) * 10 + 1).ToString();
                        var end = (((int)(reversedCompletedTrades.IndexOf(x) / 10.0)) * 10 + 10).ToString();
                        return $"Trade {start} - {end}";
                    }).ToList();
                    break;
            }

            if (groupedTrades == null)
            {
                return;
            }

            /*// Remove not needed results
            foreach (var result in Results.ToArray())
            {
                var found = false;
                foreach (var group in groupedTrades)
                {
                    if (result.Name == group.Key)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Results.Remove(result);
                }
            }*/

            // Update or add
            var results = new List<TradesResult>();
            foreach (var group in groupedTrades)
            {
                var result = new TradesResult
                {
                    Name = group.Key
                };
                results.Add(result);

                var groupTrades = group.ToList();
                var winningTrades = groupTrades.Where(t => t.RMultiple != null && t.RMultiple > 0).ToList();
                var losingTrades = groupTrades.Where(t => t.RMultiple != null && t.RMultiple <= 0).ToList();
                var completedGroupedTrades = groupTrades.Where(x => x.EntryDateTime != null).ToList();

                foreach (var t in groupTrades)
                {
                    result.Trades.Add(t);
                }
                
                result.CompletedOrOpenTrades = completedGroupedTrades.Count;
                result.Profit = groupTrades.Sum(t => t.Profit ?? 0);
                result.PercentSuccessfulTrades = result.CompletedOrOpenTrades != 0
                    ? (winningTrades.Count * 100M) / result.CompletedOrOpenTrades
                    : 0;
                result.RSum = groupTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value);

                result.AvRWinningTrades = winningTrades.Count != 0
                    ? winningTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / (decimal)winningTrades.Count
                    : 0;
                result.AvRLosingTrades = losingTrades.Count != 0
                    ? losingTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / (decimal)losingTrades.Count
                    : 0;
                result.RExpectancy = groupTrades.Count(x => x.RMultiple != null) > 0 ? groupTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / groupTrades.Count(x => x.RMultiple != null) : 0;

                /*
                var maxAdverseRList = new List<decimal>();
                var maxPositiveRList = new List<decimal>();
                var count = 0;

                foreach (var trade in completedTrades)
                {
                    count++;
                    var start = trade.OrderDateTime.Value;
                    var end = start.AddSeconds((int)trade.Timeframe.Value * 10);
                    var candles = _candlesService.GetCandles(_broker, trade.Market, trade.Timeframe.Value, false, start, end);
                    var maxAdversePrice = trade.TradeDirection == TradeDirection.Short
                        ? candles.Any() ? candles.Max(c => c.High) : 0
                        : candles.Any() ? candles.Min(c => c.Low) : 0;
                    var maxPositivePrice = trade.TradeDirection == TradeDirection.Short
                        ? candles.Any() ? candles.Max(c => c.Low) : 0
                        : candles.Any() ? candles.Min(c => c.High) : 0;
                    var maxPositivePriceDist = Math.Abs(maxPositivePrice - (double)trade.EntryPrice.Value);
                    var maxAdversePriceDist = Math.Abs(maxAdversePrice - (double)trade.EntryPrice.Value);
                    var stopPriceDist = (double)Math.Abs(trade.EntryPrice.Value - trade.StopPrice.Value);
                    var maxAdverseR = maxAdversePriceDist / stopPriceDist;
                    var maxPositiveR = maxPositivePriceDist / stopPriceDist;
                    maxAdverseRList.Add((decimal)maxAdverseR);
                    maxPositiveRList.Add((decimal)maxPositiveR);

                    if (maxAdverseR < 0)
                    {
                        Debugger.Break();
                    }
                }

                if (maxAdverseRList.Any()) result.AvAdverseRFor10Candles = maxAdverseRList.Average();
                if (maxPositiveRList.Any()) result.AvPositiveRFor20Candles = maxPositiveRList.Average();*/
            }

            _dispatcher.Invoke(() =>
            {
                Results.Clear();
                foreach (var result in results.OrderByDescending(x => x.Name).ToList())
                {
                    Results.Add(result);
                }
            });
        }
    }
}