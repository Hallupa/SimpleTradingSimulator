using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using log4net;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;
using TraderTools.Core.UI.ViewModels;
using TraderTools.Core.UI.Views;
using TraderTools.Indicators;
using TraderTools.TradingSimulator.Services;

namespace TraderTools.TradingSimulator
{
    public class IndicatorDisplayOptions
    {
        public IIndicator Indicator { get; set; }
        public Color Colour { get; set; }
        public Brush Brush { get; set; }
        public bool ShowOnLargeChartInSeparatePane { get; set; }
        public ChartPaneViewModel Pane { get; set; }
    }

    public class MainWindowViewModel : DoubleChartViewModel, INotifyPropertyChanged
    {
        #region Fields
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Func<string> _getTradeCommentsFunc;
        private readonly Action<string> _showMessageAction;
        private readonly Action<Cursor> _setCursorAction;
        private readonly Action<TradeDetails, Action, EditTradeViewModel> _showEditTradeAction;
        private readonly Func<IDisposable> _suspendChartUpdatesAction;
        private readonly Action _updateWindowAction;
        private Dispatcher _dispatcher;
        private Random _rnd = new Random();
        private Dictionary<Timeframe, List<ICandle>> _currentCandles = new Dictionary<Timeframe, List<ICandle>>();
        private bool _isTradeEnabled;
        private int _orderExpiryCandlesIndex;
        private int _h2EndDateIndex;
        private bool _running;
        private TradeDetails _tradeBeingSetup;
        private List<ICandle> _allSmallestTimeframeCandles;
        private List<ICandle> _allH2Candles;
        private List<ICandle> _allD1Candles;
        public event PropertyChangedEventHandler PropertyChanged;
        private CandlesService _candlesService;
        private ObservableCollection<TradeDetails> _trades = new ObservableCollection<TradeDetails>();
        private List<string> _markets;
        private IDisposable _chartModeDisposable;
        private string _market;
        private TradeDetails _tradeBeingEdited;
        private TradeDetails _selectedTrade;
        #endregion

        public MainWindowViewModel(
            Func<string> getTradeCommentsFunc,
            Action<string> showMessageAction,
            Action<Cursor> setCursorAction,
            Action<TradeDetails, Action, EditTradeViewModel> showEditTradeAction,
            Func<IDisposable> suspendChartUpdatesAction,
            Action updateWindowAction)
        {
            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1,
                Timeframe.H4,
                Timeframe.H2,
                Timeframe.H1,
                Timeframe.M1,
            };

            // Setup available indicators
            AddAvailableIndicator(new ExponentialMovingAverage(8), Colors.DarkBlue, false, true);
            AddAvailableIndicator(new ExponentialMovingAverage(20), Colors.Blue, false, false);
            AddAvailableIndicator(new ExponentialMovingAverage(25), Colors.Blue, false, true);
            AddAvailableIndicator(new ExponentialMovingAverage(50), Colors.LightBlue, false, true);
            AddAvailableIndicator(new SimpleMovingAverage(50), Colors.Green, false, false);
            AddAvailableIndicator(new SimpleMovingAverage(200), Colors.LightGreen, false, false);
            AddAvailableIndicator(new AverageTrueRange(), Colors.Red, true, false);
            AddAvailableIndicator(new CommodityChannelIndex(), Colors.Red, true, false);
            AddAvailableIndicator(new T3CommodityChannelIndex(), Colors.Red, true, false);

            NewChartCommand = new DelegateCommand(o => Next(), o => !Running);
            NextCandleCommand = new DelegateCommand(o => ProgressTime());
            ClearTradesCommand = new DelegateCommand(o => ClearTrades());
            EditTradeCommand = new DelegateCommand(o => EditTrade(SelectedTrade), o => _tradeBeingEdited == null && SelectedTrade != null && SelectedTrade.CloseDateTime == null);
            StartLongTradeCommand = new DelegateCommand(o => StartTrade(TradeDirection.Long), o => _tradeBeingEdited == null);
            StartShortTradeCommand = new DelegateCommand(o => StartTrade(TradeDirection.Short), o => _tradeBeingEdited == null);

            _getTradeCommentsFunc = getTradeCommentsFunc;
            _showMessageAction = showMessageAction;
            _setCursorAction = setCursorAction;
            _showEditTradeAction = showEditTradeAction;
            _suspendChartUpdatesAction = suspendChartUpdatesAction;
            _updateWindowAction = updateWindowAction;
            DependencyContainer.ComposeParts(this);
            _dispatcher = Dispatcher.CurrentDispatcher;
            _candlesService = new CandlesService();

            _chartModeDisposable = ChartingService.ChartModeObservable.Subscribe(ChartModeChanged);


            var regex = new Regex("FXCM_([a-zA-Z0-9]*)_");
            var markets = new HashSet<string>();
            foreach (var candlesPath in Directory.GetFiles(_candlesService.CandlesDirectory))
            {
                var marketName = regex.Match(Path.GetFileName(candlesPath)).Groups[1].Value;
                markets.Add(marketName);
            }

            _markets = markets.ToList();

            IsTradeEnabled = false;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraderSimulator");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            _orderExpiryCandlesIndex = 0;

            ResultsViewModel = new TradesResultsViewModel(() => Trades.ToList()) { ShowOptions = false };

            Next();
        }

        // ReSharper disable once UnusedMember.Global
        public TradeListDisplayOptionsFlag TradeListDisplayOptions { get; set; } =
            TradeListDisplayOptionsFlag.PoundsPerPip | TradeListDisplayOptionsFlag.Stop
                                                     | TradeListDisplayOptionsFlag.Limit
                                                     | TradeListDisplayOptionsFlag.OrderDate
                                                     | TradeListDisplayOptionsFlag.OrderPrice
                                                     | TradeListDisplayOptionsFlag.ResultR;

        private void ClearTrades()
        {
            Trades.Clear();
            ResultsViewModel.UpdateResults();
        }

        public TradeDetails SelectedTrade
        {
            get => _selectedTrade;
            set
            {
                _selectedTrade = value;
                EditTradeCommand.RaiseCanExecuteChanged();
                SetupAnnotations();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<IndicatorDisplayOptions> AvailableIndiciators { get; } = new ObservableCollection<IndicatorDisplayOptions>();

        public ObservableCollection<IndicatorDisplayOptions> SelectedIndicators { get; } = new ObservableCollection<IndicatorDisplayOptions>();

        private void AddAvailableIndicator(IIndicator indicator, Color colour, bool showInLargeChartInSeparatePane, bool selectIndicator)
        {
            var indicatorOptions = new IndicatorDisplayOptions
            {
                Indicator = indicator,
                Colour = colour,
                Brush = new SolidColorBrush(colour),
                ShowOnLargeChartInSeparatePane = showInLargeChartInSeparatePane,
                Pane =
                    showInLargeChartInSeparatePane
                    ? new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                    {
                        IsFirstChartPane = false,
                        IsLastChartPane = true,
                        Height = 100
                    }
                    : null
            };

            AvailableIndiciators.Add(indicatorOptions);

            if (selectIndicator)
            {
                SelectedIndicators.Add(indicatorOptions);
            }
        }

        private void ChartModeChanged(ChartMode? chartMode)
        {
        }

        public DelegateCommand NextCandleCommand { get; set; }
        public DelegateCommand NewChartCommand { get; set; }
        public DelegateCommand StartLongTradeCommand { get; }
        public DelegateCommand StartShortTradeCommand { get; }
        public List<Timeframe> TimeFrameItems { get; set; }
        public DelegateCommand ClearTradesCommand { get; set; }
        public DelegateCommand EditTradeCommand { get; set; }
        [Import] public ChartingService ChartingService { get; private set; }

        public TradeDetails TradeBeingEdited
        {
            get => _tradeBeingEdited;
            set
            {
                _tradeBeingEdited = value;
                OnPropertyChanged();
                EditTradeCommand.RaiseCanExecuteChanged();
                StartLongTradeCommand.RaiseCanExecuteChanged();
                StartShortTradeCommand.RaiseCanExecuteChanged();
                SetupAnnotations();
            }
        }

        private void EditTrade(TradeDetails trade)
        {
            if (TradeBeingEdited != null) return;
            TradeBeingEdited = trade;

            var vm = new EditTradeViewModel(trade, ChartingService, () => _allH2Candles[_h2EndDateIndex], true, _setCursorAction, SetupAnnotations, true);
            _showEditTradeAction(
                trade,
                () =>
                {
                    if (vm.CloseClicked)
                    {
                        if (trade.CloseDateTime == null)
                        {
                            var candle = _allH2Candles[_h2EndDateIndex];
                            var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);
                            trade.SetClose(date, (decimal) candle.Close, TradeCloseReason.ManualClose);
                        }

                        ResultsViewModel.UpdateResults();
                    }

                    SetupAnnotations();
                    TradeBeingEdited = null;
                },
                vm);
        }

        private void StartTrade(TradeDirection direction)
        {
            if (TradeBeingEdited != null) return;

            var timeframe = GetSelectedTimeframe(null);
            var candle = _currentCandles[Timeframe.D1][_currentCandles[Timeframe.D1].Count - 1];
            var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);

            var newTrade = new TradeDetails
            {
                Market = _market,
                TradeDirection = direction,
                OrderDateTime = date,
                OrderAmount = 100,
                Timeframe = timeframe
            };
            TradeBeingEdited = newTrade;
            SelectedTrade = newTrade;

            Trades.Insert(0, newTrade);
            var vm = new EditTradeViewModel(
                newTrade, ChartingService, () => _allH2Candles[_h2EndDateIndex], true, _setCursorAction, SetupAnnotations,
                false);
            _showEditTradeAction(
                newTrade,
                () =>
                {
                    if (!vm.OKClicked)
                    {
                        SelectedTrade = null;
                        Trades.Remove(newTrade);
                        SetupAnnotations();
                    }
                    else
                    {
                        newTrade.OrderKind = newTrade.OrderPrice == null ? OrderKind.Market : OrderKind.EntryPrice;
                        if (newTrade.OrderPrice == null) newTrade.SetEntry(date, (decimal)candle.Close);
                        SelectedTrade = newTrade;
                    }

                    TradeBeingEdited = null;
                    vm.Dispose();
                },
                vm);
        }

        #region Properties
        public ObservableCollection<TradeDetails> Trades { get; } = new ObservableCollection<TradeDetails>();
        public TradesResultsViewModel ResultsViewModel { get; }

        public bool IsTradeEnabled
        {
            get => _isTradeEnabled;
            set
            {
                _isTradeEnabled = value;
                OnPropertyChanged();
            }
        }


        public int OrderExpiryCandlesIndex
        {
            get => _orderExpiryCandlesIndex;
            set
            {
                _orderExpiryCandlesIndex = value;
                OnPropertyChanged();
            }
        }

        private void SetupAnnotations()
        {
            if (!_currentCandles.ContainsKey(GetSelectedTimeframe(null)) || ChartViewModel.ChartPaneViewModels.Count == 0 || ChartViewModelSmaller1.ChartPaneViewModels.Count == 0)
            {
                return;
            }

            if (ChartViewModel.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();
            if (ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();

            var mainAnnotations = ChartViewModel.ChartPaneViewModels[0].TradeAnnotations;
            var smallAnnotations = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations;

            ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = null;
            ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = null;

            var fullTradeAnnotations = TradeAnnotationsToShow.All;
            var partialTradeAnnotations = TradeAnnotationsToShow.EntryMarker | TradeAnnotationsToShow.CloseMarker;

            using (_suspendChartUpdatesAction())
            {
                // Remove existing annotations
                for (var i = mainAnnotations.Count - 1; i >= 0; i--)
                {
                    var annotation = mainAnnotations[i];
                    if (annotation is LineAnnotation l)
                    {
                        if (l.Tag != null && ((string)l.Tag).StartsWith("Added")) continue;
                    }

                    mainAnnotations.RemoveAt(i);
                }

                for (var i = smallAnnotations.Count - 1; i >= 0; i--)
                {
                    var annotation = smallAnnotations[i];
                    if (annotation is LineAnnotation l)
                    {
                        if (l.Tag != null && ((string)l.Tag).StartsWith("Added")) continue;
                    }

                    smallAnnotations.RemoveAt(i);
                }

                foreach (var trade in Trades)
                {
                    var tradeOpen = trade.CloseDateTime == null;
                    var tradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModel, trade == SelectedTrade || trade == TradeBeingEdited ? fullTradeAnnotations : partialTradeAnnotations, GetSelectedTimeframe(trade), _currentCandles[GetSelectedTimeframe(trade)], trade);
                    foreach (var tradeAnnotation in tradeAnnotations)
                    {
                        mainAnnotations.Add(tradeAnnotation);
                    }

                    var smallChartTradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModelSmaller1, trade == SelectedTrade || trade == TradeBeingEdited ? fullTradeAnnotations : partialTradeAnnotations, Timeframe.D1, _currentCandles[Timeframe.D1], trade);
                    foreach (var tradeAnnotation in smallChartTradeAnnotations)
                    {
                        smallAnnotations.Add(tradeAnnotation);
                    }
                }
            }

            ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = mainAnnotations;
            ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = smallAnnotations;
        }
        #endregion

        private void Next()
        {
            Log.Info("Loading next chart");
            _market = _markets[_rnd.Next(0, _markets.Count)];
            OrderExpiryCandlesIndex = 0;
            var allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
            _h2EndDateIndex = _rnd.Next(12 * 50, allH2Candles.Count - 12 * 100);

            _allH2Candles = null;
            _allD1Candles = null;
            _allSmallestTimeframeCandles = null;

            SetupChart();
        }

        public void SetupChart(bool recreateChart = true)
        {
            // Get candles
            if (_allSmallestTimeframeCandles == null)
            {
                _allSmallestTimeframeCandles = _candlesService.GetCandles(_market, Timeframe.M5);
                _allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
                _allD1Candles = _candlesService.GetCandles(_market, Timeframe.D1);
            }

            var endDateUtc = new DateTime(_allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);

            var currentH2Candles = _allH2Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();
            var currentD1Candles = _allD1Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();

            Candle? d1Incomplete = null;

            for (var i = 0; i <= _h2EndDateIndex; i++)
            {
                var h2 = _allH2Candles[i];

                if (h2.CloseTimeTicks > currentD1Candles[currentD1Candles.Count - 1].CloseTimeTicks)
                {
                    d1Incomplete = new Candle
                    {
                        Timeframe = (int)Timeframe.D1,
                        Close = h2.Close,
                        Open = d1Incomplete?.Open ?? h2.Open,
                        CloseTimeTicks = h2.CloseTimeTicks,
                        High = d1Incomplete != null && d1Incomplete.Value.High > h2.High ? d1Incomplete.Value.High : h2.High,
                        Low = d1Incomplete != null && d1Incomplete.Value.Low < h2.Low ? d1Incomplete.Value.Low : h2.Low,
                        OpenTimeTicks = d1Incomplete?.OpenTimeTicks ?? h2.OpenTimeTicks,
                        IsComplete = 0,
                        Id = Guid.NewGuid()
                    };
                }
            }

            if (d1Incomplete != null) currentD1Candles.Add(d1Incomplete.Value);

            _currentCandles[Timeframe.D1] = currentD1Candles;
            _currentCandles[Timeframe.H2] = currentH2Candles;

            SetChartCandles(recreateChart);
        }

        public bool Running
        {
            get => _running;
            private set
            {
                _running = value;
                OnPropertyChanged();
                NewChartCommand.RaiseCanExecuteChanged();
                NextCandleCommand.RaiseCanExecuteChanged();
            }
        }

        private void SetChartCandles(bool recreate)
        {
            if (recreate)
            {
                ChartViewModel.ChartPaneViewModels.Clear();
                ChartViewModelSmaller1.ChartPaneViewModels.Clear();
            }

            var largeChartTimeframe = GetSelectedTimeframe(null);

            if (ChartViewModel.ChartPaneViewModels.Count > 0) ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels.Clear();
            if (ChartViewModelSmaller1.ChartPaneViewModels.Count > 0) ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels.Clear();

            ChartHelper.SetChartViewModelPriceData(_currentCandles[largeChartTimeframe], ChartViewModel, largeChartTimeframe);
            ChartHelper.SetChartViewModelPriceData(_currentCandles[Timeframe.D1], ChartViewModelSmaller1, Timeframe.D1);

            if (recreate)
            {
                ChartHelper.SetChartXVisibleRange(ChartViewModel, _currentCandles[largeChartTimeframe].Count - 60, _currentCandles[largeChartTimeframe].Count + 10);
                ChartHelper.SetChartXVisibleRange(ChartViewModelSmaller1, _currentCandles[Timeframe.D1].Count - 60, _currentCandles[Timeframe.D1].Count + 10);

                var annotations = new AnnotationCollection();
                var annotationsSmallerCharts = new AnnotationCollection();
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = annotations;
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = annotationsSmallerCharts;
            }
            else
            {
                var maxIndex = _currentCandles[largeChartTimeframe].Count - 1;
                var currentDisplayRange = ChartViewModel.XVisibleRange.Max - ChartViewModel.XVisibleRange.Min;
                if (ChartViewModel.XVisibleRange.Max <= maxIndex + (currentDisplayRange * 0.08))
                {
                    var change = (maxIndex - ChartViewModel.XVisibleRange.Max) + (currentDisplayRange * 0.5);
                    ChartViewModel.XVisibleRange.SetMinMax(ChartViewModel.XVisibleRange.Min + change, ChartViewModel.XVisibleRange.Max + change);
                }

                maxIndex = _currentCandles[Timeframe.D1].Count - 1;
                currentDisplayRange = ChartViewModelSmaller1.XVisibleRange.Max - ChartViewModelSmaller1.XVisibleRange.Min;
                if (ChartViewModelSmaller1.XVisibleRange.Max <= maxIndex + (currentDisplayRange * 0.08))
                {
                    var change = (maxIndex - ChartViewModelSmaller1.XVisibleRange.Max) + (currentDisplayRange * 0.5);
                    ChartViewModelSmaller1.XVisibleRange.SetMinMax(ChartViewModelSmaller1.XVisibleRange.Min + change, ChartViewModelSmaller1.XVisibleRange.Max + change);
                }
            }

            // Remove any indicator panes not used
            foreach (var unselectedIndicator in AvailableIndiciators.Where(x => !SelectedIndicators.Contains(x)))
            {
                if (unselectedIndicator.ShowOnLargeChartInSeparatePane)
                {
                    if (ChartViewModel.ChartPaneViewModels.Contains(unselectedIndicator.Pane))
                    {
                        ChartViewModel.ChartPaneViewModels.Remove(unselectedIndicator.Pane);
                    }
                }
            }

            // Add indicators
            foreach (var selectedIndicator in SelectedIndicators)
            {
                if (selectedIndicator.ShowOnLargeChartInSeparatePane)
                {
                    if (!ChartViewModel.ChartPaneViewModels.Contains(selectedIndicator.Pane))
                    {
                        ChartViewModel.ChartPaneViewModels.Add(selectedIndicator.Pane);
                    }

                    selectedIndicator.Indicator.Reset();
                    if (selectedIndicator.Pane.ChartSeriesViewModels.Count > 0) selectedIndicator.Pane.ChartSeriesViewModels.Clear();
                    ChartHelper.AddIndicator(selectedIndicator.Pane, _market, selectedIndicator.Indicator, selectedIndicator.Colour, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                }
                else
                {
                    selectedIndicator.Indicator.Reset();
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, selectedIndicator.Indicator, selectedIndicator.Colour, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                    selectedIndicator.Indicator.Reset();
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, selectedIndicator.Indicator, selectedIndicator.Colour, Timeframe.D1, _currentCandles[Timeframe.D1]);
                }
            }
        }

        public void KeyDown(Key key, int? candleIndex, decimal? price)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (candleIndex != null && price != null)
                {
                    if (key == Key.H) AddHorizontalLine(price.Value);
                }

                if (key == Key.N && !Running) Next();

                if (key == Key.F)
                {
                    ProgressTime();
                }
            }
        }

        private void AddHorizontalLine(decimal priceValue)
        {
            var currentLine = new HorizontalLineAnnotation()
            {
                Tag = "Added",
                StrokeThickness = 1.5,
                Opacity = 0.6,
                Stroke = Brushes.Yellow,
                Y1 = priceValue,
                IsEditable = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Add(currentLine);

            currentLine = new HorizontalLineAnnotation()
            {
                Tag = "Added",
                StrokeThickness = 1.5,
                Opacity = 0.6,
                Stroke = Brushes.Yellow,
                Y1 = priceValue,
                IsEditable = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Add(currentLine);
        }

        private void ProgressTime()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var allH2Candles = _allH2Candles;

                _h2EndDateIndex++;

                if (_h2EndDateIndex >= allH2Candles.Count) _h2EndDateIndex = allH2Candles.Count - 1;

                var endDate = new DateTime(allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);
                var startDate = new DateTime(allH2Candles[_h2EndDateIndex - 1].CloseTimeTicks, DateTimeKind.Utc);
                var updated = false;

                for (var i = 0; i < _allSmallestTimeframeCandles.Count; i++)
                {
                    var m1Candle = _allSmallestTimeframeCandles[i];
                    var m1EndDate = new DateTime(m1Candle.CloseTimeTicks, DateTimeKind.Utc);
                    var m1StartDate = new DateTime(m1Candle.OpenTimeTicks, DateTimeKind.Utc);

                    if (!(m1EndDate >= startDate && m1StartDate <= endDate)) continue;

                    // Simulate trades
                    foreach (var trade in Trades)
                    {
                        trade.SimulateTrade(m1Candle, out var tradeUpdated);
                        updated = updated || tradeUpdated;
                    }
                }

                SetupChart(false);

                if (updated)
                {
                    ResultsViewModel.UpdateResults();
                }

                SetupAnnotations();

                _updateWindowAction();
            }));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}