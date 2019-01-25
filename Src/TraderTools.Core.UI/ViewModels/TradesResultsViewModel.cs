using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using Hallupa.Library;
using TraderTools.Basics;

namespace TraderTools.Core.UI.ViewModels
{
    public class SimulationResultsViewModel
    {
        private readonly Func<List<TradeDetails>> _getTradesFunc;
        private string _selectedResultOption = "Summary";
        private Dispatcher _dispatcher;

        public ObservableCollection<StrategyRunResult> Results { get; } = new ObservableCollection<StrategyRunResult>();

        public List<string> ResultOptions { get; private set; } = new List<string>
        {
            "Summary",
            "Markets",
            "Months",
            "Timeframes",
            "Strategies",
            "Grouped (10)"
        };

        public SimulationResultsViewModel(Func<List<TradeDetails>> getTradesFunc)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _getTradesFunc = getTradesFunc;
        }

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
                    groupedTrades = trades.GroupBy(x =>
                        $"{x.OrderDateTimeLocal.Value.Year}/{x.OrderDateTimeLocal.Value.Month:00}").ToList();
                    break;
                case "Timeframes":
                    groupedTrades = trades.GroupBy(x => $"{x.Timeframe}").ToList();
                    break;
                case "Strategies":
                    var advStratNaming = false;

                    if (advStratNaming)
                    {
                        var regex = new Regex("#[a-zA-Z0-9&]*");
                        groupedTrades = trades.SelectMany(t =>
                        {
                            var ret = new List<(string Name, TradeDetails Trade)>();
                            foreach (Match match in regex.Matches(t.Comments))
                            {
                                ret.Add((match.Value, t));
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
                    var completedTrades = trades.Where(t => t.RMultiple != null).Reverse().ToList();
                    groupedTrades = completedTrades.GroupBy(x =>
                    {
                        var start = (((int) (completedTrades.IndexOf(x) / 10.0)) * 10 + 1).ToString();
                        var end = (((int)(completedTrades.IndexOf(x) / 10.0)) * 10 + 10).ToString();
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
            var results = new List<StrategyRunResult>();
            foreach (var group in groupedTrades)
            {
                var result = new StrategyRunResult
                {
                    Name = group.Key
                };
                results.Add(result);

                var groupTrades = group.ToList();
                var winningTrades = groupTrades.Where(t => t.RMultiple != null && t.RMultiple > 0).ToList();
                var losingTrades = groupTrades.Where(t => t.RMultiple != null && t.RMultiple <= 0).ToList();
                var completedTrades = groupTrades.Where(x => x.ClosePrice != null).ToList();

                result.CompletedTrades = completedTrades.Count;
                result.PercentSuccessfulTrades = result.CompletedTrades != 0
                    ? (winningTrades.Count * 100M) / result.CompletedTrades
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