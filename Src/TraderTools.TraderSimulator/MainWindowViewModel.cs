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
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;
using TraderTools.Core.UI.ViewModels;
using TraderTools.Indicators;
using TraderTools.TradingTrainer.Services;

namespace TraderTools.TradingTrainer
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
        private Dispatcher _dispatcher;
        private Random _rnd = new Random();
        private Dictionary<Timeframe, List<Candle>> _currentCandles = new Dictionary<Timeframe, List<Candle>>();
        private Dictionary<Timeframe, List<Candle>> _allCandles = new Dictionary<Timeframe, List<Candle>>();
        private bool _isTradeEnabled;
        private string _tmpPath;
        private string _finalPath;
        private int _orderExpiryCandlesIndex;
        private int _h2EndDateIndex;
        private bool _running;
        private TradeDetails _currentTrade;
        private TradeDetails _displayedTrade;
        private List<Candle> _allSmallestTimeframeCandles;
        private List<Candle> _allH2Candles;
        private List<Candle> _allD1Candles;
        private bool _isCloseEnabled;
        private bool _closeHalfTradeAtLimit;
        public event PropertyChangedEventHandler PropertyChanged;
        private CandlesService _candlesService;
        private ObservableCollection<TradeDetails> _trades = new ObservableCollection<TradeDetails>();
        private List<string> _markets;
        private IDisposable _chartClickedDisposable;
        private bool _isSetStopButtonPressed;
        private bool _isSetLimitButtonPressed;
        private bool _isEntryButtonPressed;
        private IDisposable _chartModeDisposable;
        private TextAnnotation _smallChartTextAnnotation = new TextAnnotation();
        private TextAnnotation _largeChartTextAnnotation = new TextAnnotation();
        #endregion

        public MainWindowViewModel(Func<string> getTradeCommentsFunc, Action<string> showMessageAction, Action<Cursor> setCursorAction)
        {
            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1Tiger,
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

            NewChartCommand = new DelegateCommand(o => Next(), o => !Running);
            NextCandleCommand = new DelegateCommand(o => ProgressTime());
            ClearStopCommand = new DelegateCommand(o => ClearStop(), o => IsClearStopEnabled);
            ClearLimitCommand = new DelegateCommand(o => ClearLimit(), o => IsClearLimitEnabled);
            ClearEntryOrderCommand = new DelegateCommand(o => ClearEntryOrder(), o => IsClearEntryOrderEnabled);
            DeleteCommand = new DelegateCommand(o => DeleteTrade((TradeDetails)o));

            _getTradeCommentsFunc = getTradeCommentsFunc;
            _showMessageAction = showMessageAction;
            _setCursorAction = setCursorAction;
            DependencyContainer.ComposeParts(this);
            _dispatcher = Dispatcher.CurrentDispatcher;
            _candlesService = new CandlesService();

            _chartModeDisposable = ChartingService.ChartModeObservable.Subscribe(c => ChartModeChanged(c));

            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);
            _chartMouseMoveDisposable = ChartingService.ChartMoveObservable.Subscribe(ChartMouseMove);

            var regex = new Regex("FXCM_([a-zA-Z0-9]*)_");
            var markets = new HashSet<string>();
            foreach (var candlesPath in Directory.GetFiles(_candlesService.CandlesDirectory))
            {
                var marketName = regex.Match(Path.GetFileName(candlesPath)).Groups[1].Value;
                markets.Add(marketName);
            }

            _markets = markets.ToList();

            LongCommand = new DelegateCommand(o => Trade(TradeDirection.Long), o => !Running);
            ShortCommand = new DelegateCommand(o => Trade(TradeDirection.Short), o => !Running);
            ViewTradeCommand = new DelegateCommand(t => ViewTrade((TradeDetails)t));
            CloseCommand = new DelegateCommand(o => CloseTrade(), o => Running);
            IsTradeEnabled = false;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraderSimulator");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            _tmpPath = Path.Combine(path, "Trades.tmp");
            _finalPath = Path.Combine(path, "Trades.json");
            _orderExpiryCandlesIndex = 0;

            SimResultsViewModel = new SimulationResultsViewModel(() => Trades.ToList());
            SimResultsViewModel.ResultOptions.Remove("Months");
            SimResultsViewModel.ResultOptions.Remove("Timeframes");
            SimResultsViewModel.ResultOptions.Remove("Grouped (10)");

            if (File.Exists(_finalPath))
            {
                Trades = new ObservableCollection<TradeDetails>(JsonConvert.DeserializeObject<List<TradeDetails>>(File.ReadAllText(_finalPath)));
                SimResultsViewModel.UpdateResults();
            }

            Next();
        }

        private void DeleteTrade(TradeDetails t)
        {
            Trades.Remove(t);
            SimResultsViewModel.UpdateResults();
            SaveTrades();
            if (_displayedTrade == t) Next();
        }

        private void ViewTrade(TradeDetails tradeDetails)
        {
            if (Running)
            {
                _showMessageAction("Complete active trade before viewing old trades");
                return;
            }

            Running = false;
            _displayedTrade = tradeDetails;
            _allSmallestTimeframeCandles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.M5);
            _allH2Candles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.H2);
            _allD1Candles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.D1);
            SetupChart(true);
            UpdateUIState();
        }

        public DelegateCommand ViewTradeCommand { get; private set; }

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

        private void ClearStop()
        {
            if (_currentTrade.StopPrices.Count > 0)
            {
                if (_currentTrade.OrderDateTime != null)
                {
                    _currentTrade.AddStopPrice(_allH2Candles[_h2EndDateIndex].CloseTime(), null);
                }
                else
                {
                    _currentTrade.StopPrices.Clear();
                }

                SetAnnotations();
                UpdateUIState();
            }
        }

        private void ClearLimit()
        {
            if (_currentTrade.LimitPrices.Count > 0)
            {
                if (_currentTrade.OrderDateTime != null)
                {
                    _currentTrade.AddLimitPrice(_allH2Candles[_h2EndDateIndex].CloseTime(), null);
                }
                else
                {
                    _currentTrade.LimitPrices.Clear();
                }

                SetAnnotations();
                UpdateUIState();
            }
        }

        private void ClearEntryOrder()
        {
            if (_currentTrade.EntryDateTime == null)
            {
                _currentTrade.OrderPrice = null;
            }

            SetAnnotations();
            UpdateUIState();
        }

        public bool IsClearStopEnabled => _currentTrade.StopPrices.Count > 0 && _currentTrade.StopPrices[_currentTrade.StopPrices.Count - 1].Price != null;
        public bool IsClearLimitEnabled => _currentTrade.LimitPrices.Count > 0 && _currentTrade.LimitPrices[_currentTrade.LimitPrices.Count - 1].Price != null;
        public bool IsClearEntryOrderEnabled => _currentTrade.OrderDateTime == null && _currentTrade.OrderPrice != null;
        public bool IsOrderExpiryCandlesEnabled => !Running && !ViewingCompletedTrade;

        public bool IsEntryButtonEnabled
        {
            get => _isEntryButtonEnabled;
            set
            {
                _isEntryButtonEnabled = value;
                OnPropertyChanged();
            }
        }


        private void ChartModeChanged(ChartMode? chartMode)
        {
            UpdateUIState();
        }

        private void ChartMouseMove((DateTime Time, double Price, Action SetIsHandled) details)
        {
            var makeVisible = false;


            if (IsSetLimitButtonPressed && _currentTrade.StopPrices.Count > 0 && _currentTrade.StopPrices[_currentTrade.StopPrices.Count - 1].Price != null)
            {
                var candle = _allH2Candles[_h2EndDateIndex];

                var entry = _currentTrade.OrderPrice != null && _currentTrade.EntryDateTime == null
                    ? _currentTrade.OrderPrice.Value
                    : (decimal)candle.Close;

                var limit = details.Price;
                var pips = Math.Abs(PipsHelper.GetPriceInPips((decimal)limit - entry, _displayedTrade.Market));

                _largeChartTextAnnotation.Text = $"{pips:0.00} pips";
                _smallChartTextAnnotation.Text = $"{pips:0.00} pips";
                makeVisible = true;
            }

            if (IsSetStopButtonPressed)
            {
                var stop = (decimal)details.Price;
                var candle = _allH2Candles[_h2EndDateIndex];
                var entry = _currentTrade.OrderPrice != null && _currentTrade.EntryDateTime == null
                    ? _currentTrade.OrderPrice.Value
                    : (decimal)candle.Close;
                var pips = Math.Abs(PipsHelper.GetPriceInPips(entry - stop, _displayedTrade.Market));

                _largeChartTextAnnotation.Text = $"{pips:0.00} pips";
                _smallChartTextAnnotation.Text = $"{pips:0.00} pips";
                makeVisible = true;
            }


            if (makeVisible)
            {
                _largeChartTextAnnotation.BorderThickness = new Thickness(0, 0, 0, 2);
                _largeChartTextAnnotation.BorderBrush = Brushes.Black;
                _largeChartTextAnnotation.FontSize = 14;
                _largeChartTextAnnotation.X1 = details.Time;
                _largeChartTextAnnotation.Y1 = details.Price;
                _largeChartTextAnnotation.X2 = details.Time;
                _largeChartTextAnnotation.Y2 = details.Price;
                _largeChartTextAnnotation.VerticalAnchorPoint = VerticalAnchorPoint.Bottom;
                _largeChartTextAnnotation.Visibility = Visibility.Visible;

                _smallChartTextAnnotation.BorderThickness = new Thickness(0, 0, 0, 2);
                _smallChartTextAnnotation.BorderBrush = Brushes.Black;
                _smallChartTextAnnotation.FontSize = 14;
                _smallChartTextAnnotation.X1 = details.Time;
                _smallChartTextAnnotation.Y1 = details.Price;
                _smallChartTextAnnotation.X2 = details.Time;
                _smallChartTextAnnotation.Y2 = details.Price;
                _smallChartTextAnnotation.VerticalAnchorPoint = VerticalAnchorPoint.Bottom;
                _smallChartTextAnnotation.Visibility = Visibility.Visible;

                if (!ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Contains(_largeChartTextAnnotation))
                {
                    ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Add(_largeChartTextAnnotation);
                }

                if (!ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Contains(_smallChartTextAnnotation))
                {
                    ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Add(_smallChartTextAnnotation);
                }
            }
            else if (!makeVisible && (ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Contains(_largeChartTextAnnotation) || ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Contains(_smallChartTextAnnotation)))
            {
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Remove(_largeChartTextAnnotation);
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Remove(_smallChartTextAnnotation);
            }
        }

        private void ChartClicked((DateTime Time, double Price, Action SetIsHandled) details)
        {
            if (IsSetStopButtonPressed)
            {
                SetTradeStop(details.Price);
                details.SetIsHandled();
            }

            if (IsSetLimitButtonPressed)
            {
                SetTradeLimit(details.Price);
                details.SetIsHandled();
            }

            if (IsEntryButtonPressed)
            {
                SetTradeEntryPrice(details.Price);
                details.SetIsHandled();
            }
        }

        private void SetTradeEntryPrice(double price)
        {
            IsEntryButtonPressed = false;
            _currentTrade.OrderPrice = (decimal)price;

            SetAnnotations();
            UpdateUIState();
        }

        private void SetTradeLimit(double price)
        {
            IsSetLimitButtonPressed = false;
            var date = _allH2Candles[_h2EndDateIndex].CloseTime();

            if (_currentTrade.EntryDateTime != null)
            {
                foreach (var limit in _currentTrade.LimitPrices.ToList())
                {
                    if (limit.Date == date) _currentTrade.LimitPrices.Remove(limit);
                }

                _currentTrade.AddLimitPrice(date, (decimal)price);
            }
            else
            {
                _currentTrade.LimitPrices.Clear();
                _currentTrade.AddLimitPrice(date, (decimal)price);
            }

            SetAnnotations();
            UpdateUIState();
        }

        private void SetTradeStop(double price)
        {
            IsSetStopButtonPressed = false;
            var date = _allH2Candles[_h2EndDateIndex].CloseTime();

            if (_currentTrade.EntryDateTime != null)
            {
                foreach (var stop in _currentTrade.StopPrices.ToList())
                {
                    if (stop.Date == date) _currentTrade.StopPrices.Remove(stop);
                }

                _currentTrade.AddStopPrice(date, (decimal)price);
            }
            else
            {
                _currentTrade.StopPrices.Clear();
                _currentTrade.AddStopPrice(date, (decimal)price);
            }

            SetAnnotations();
            UpdateUIState();
        }

        public DelegateCommand NextCandleCommand { get; set; }

        public DelegateCommand NewChartCommand { get; set; }

        public List<Timeframe> TimeFrameItems { get; set; }

        public bool ViewingCompletedTrade => _displayedTrade != _currentTrade;

        public bool IsSetStopButtonPressed
        {
            get => _isSetStopButtonPressed;
            set
            {
                _isSetStopButtonPressed = value;
                if (value == true)
                {
                    IsSetLimitButtonPressed = false;
                    IsEntryButtonPressed = false;
                    ChartingService.ChartMode = null;
                }

                UpdateUIState();
                OnPropertyChanged();
            }
        }

        public bool IsSetLimitButtonPressed
        {
            get => _isSetLimitButtonPressed;
            set
            {
                _isSetLimitButtonPressed = value;
                if (value == true)
                {
                    IsSetStopButtonPressed = false;
                    IsEntryButtonPressed = false;
                    ChartingService.ChartMode = null;
                }

                UpdateUIState();
                OnPropertyChanged();
            }
        }

        public bool IsEntryButtonPressed
        {
            get => _isEntryButtonPressed;
            set
            {
                _isEntryButtonPressed = value;
                if (value == true)
                {
                    IsSetStopButtonPressed = false;
                    IsSetLimitButtonPressed = false;
                    ChartingService.ChartMode = null;
                }

                UpdateUIState();
                OnPropertyChanged();
            }
        }

        [Import] public ChartingService ChartingService { get; private set; }

        public int SelectedMainIndicatorsIndex { get; set; }
        public DelegateCommand ClearStopCommand { get; }
        public DelegateCommand ClearLimitCommand { get; }
        public DelegateCommand ClearEntryOrderCommand { get; }
        public DelegateCommand DeleteCommand { get; }

        private void CreateEmptyTrade(string market)
        {
            _currentTrade = new TradeDetails { Market = market };
            _displayedTrade = _currentTrade;
        }

        private void CloseTrade()
        {
            var showMsg = false;
            if (_currentTrade.CloseReason == null)
            {
                var candle = _allH2Candles[_h2EndDateIndex];
                var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);
                _currentTrade.SetClose(date, (decimal)candle.Close, TradeCloseReason.ManualClose);
            }
            else
            {
                showMsg = true;
            }

            if (_currentTrade.CloseReason == TradeCloseReason.HitLimit && CloseHalfTradeAtLimit)
            {
                _currentTrade.OrderAmount = _currentTrade.OrderAmount / 2.0M;
                _currentTrade.EntryQuantity = _currentTrade.EntryQuantity / 2.0M;
            }

            Trades.Insert(0, _currentTrade);
            SimResultsViewModel.UpdateResults();
            SaveTrades();


            if (_currentTrade.CloseReason == TradeCloseReason.HitLimit && CloseHalfTradeAtLimit)
            {
                showMsg = false;
                CloseHalfTradeAtLimit = false;
                var existingTrade = _currentTrade;
                _currentTrade = new TradeDetails
                {
                    Comments = existingTrade.Comments,
                    Market = existingTrade.Market,
                    Broker = existingTrade.Broker,
                    OrderDateTime = existingTrade.OrderDateTime,
                    OrderPrice = existingTrade.OrderPrice,
                    OrderAmount = existingTrade.OrderAmount,
                    OrderKind = existingTrade.OrderKind,
                    TradeDirection = existingTrade.TradeDirection,
                    Timeframe = existingTrade.Timeframe,
                    EntryPrice = existingTrade.EntryPrice,
                    EntryDateTime = existingTrade.EntryDateTime,
                    EntryQuantity = existingTrade.EntryQuantity,
                    OrderExpireTime = existingTrade.OrderExpireTime,
                    PricePerPip = existingTrade.PricePerPip
                };
                _displayedTrade = _currentTrade;

                foreach (var stop in existingTrade.GetStopPrices())
                {
                    _currentTrade.AddStopPrice(stop.Date, stop.Price);
                }
            }
            else
            {
                CreateEmptyTrade(_currentTrade.Market);
            }

            CloseHalfTradeAtLimit = false;
            OrderExpiryCandlesIndex = 0;

            UpdateUIState();
            SetAnnotations();

            if (showMsg)
            {
                _showMessageAction("Trade closed");
            }
        }

        #region Properties
        public ObservableCollection<TradeDetails> Trades { get; } = new ObservableCollection<TradeDetails>();
        public DelegateCommand CloseCommand { get; private set; }
        public SimulationResultsViewModel SimResultsViewModel { get; }

        public bool IsCloseHalfTradeAtLimitEnabled
        {
            get => _isCloseHalfTradeAtLimitEnabled;
            set
            {
                _isCloseHalfTradeAtLimitEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsTradeEnabled
        {
            get => _isTradeEnabled;
            set
            {
                _isTradeEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsCloseEnabled
        {
            get => _isCloseEnabled;
            set
            {
                _isCloseEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool CloseHalfTradeAtLimit
        {
            get => _closeHalfTradeAtLimit;
            set
            {
                _closeHalfTradeAtLimit = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand LongCommand { get; }

        public DelegateCommand ShortCommand { get; }

        public DelegateCommand ResimulateTradeCommand { get; }

        public int OrderExpiryCandlesIndex
        {
            get => _orderExpiryCandlesIndex;
            set
            {
                _orderExpiryCandlesIndex = value;
                OnPropertyChanged();
            }
        }

        private void SetAnnotations()
        {
            if (!_currentCandles.ContainsKey(GetSelectedTimeframe(null)) || ChartViewModel.ChartPaneViewModels.Count == 0 || ChartViewModelSmaller1.ChartPaneViewModels.Count == 0)
            {
                return;
            }

            var largeChartCandles = _currentCandles[GetSelectedTimeframe(null)];
            var smallChartCandles = _currentCandles[GetSelectedTimeframe(null)];

            if (ChartViewModel.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();
            if (ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();

            for (var i = ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Count - 1; i >= 0; i--)
            {
                var annotation = ChartViewModel.ChartPaneViewModels[0].TradeAnnotations[i];
                if (annotation is LineAnnotation l)
                {
                    if ((string)l.Tag == "Added") continue;
                }

                if (annotation.Equals(_largeChartTextAnnotation) || annotation.Equals(_smallChartTextAnnotation)) continue;

                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.RemoveAt(i);
            }

            for (var i = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Count - 1; i >= 0; i--)
            {
                var annotation = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations[i];
                if (annotation is LineAnnotation l)
                {
                    if ((string)l.Tag == "Added") continue;
                }

                if (annotation.Equals(_largeChartTextAnnotation) || annotation.Equals(_smallChartTextAnnotation)) continue;

                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.RemoveAt(i);
            }

            if (_displayedTrade.StopPrices.Count > 0)
            {
                var stopPosition = _displayedTrade.StopPrices[_displayedTrade.StopPrices.Count - 1].Price;
                if (stopPosition != null && _displayedTrade.CloseDateTime == null)
                {
                    ChartHelper.AddHorizontalLine(
                        stopPosition.Value,
                        new DateTime(largeChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                        new DateTime(largeChartCandles[largeChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                        ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        ChartViewModel.ChartPaneViewModels[0].TradeAnnotations,
                        null,
                        Colors.Red);

                    ChartHelper.AddHorizontalLine(
                        stopPosition.Value,
                        new DateTime(smallChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                        new DateTime(smallChartCandles[smallChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                        ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations,
                        null,
                        Colors.Red);
                }
            }

            if (_displayedTrade.LimitPrices.Count > 0 && _displayedTrade.CloseDateTime == null)
            {
                var limtPosition = _displayedTrade.LimitPrices[_displayedTrade.LimitPrices.Count - 1].Price;
                if (limtPosition != null)
                {
                    ChartHelper.AddHorizontalLine(
                        limtPosition.Value,
                        new DateTime(largeChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                        new DateTime(largeChartCandles[largeChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                        ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        ChartViewModel.ChartPaneViewModels[0].TradeAnnotations,
                        null,
                        Colors.Green);

                    ChartHelper.AddHorizontalLine(
                        limtPosition.Value,
                        new DateTime(smallChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                        new DateTime(smallChartCandles[smallChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                        ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                        ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations,
                        null,
                        Colors.Green);
                }
            }

            if (_displayedTrade.EntryPrice == null && _displayedTrade.OrderPrice != null)
            {
                var orderPrice = _displayedTrade.OrderPrice.Value;
                ChartHelper.AddHorizontalLine(
                    orderPrice,
                    new DateTime(largeChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                    new DateTime(largeChartCandles[largeChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                    ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                    ChartViewModel.ChartPaneViewModels[0].TradeAnnotations,
                    null,
                    Colors.Blue);

                ChartHelper.AddHorizontalLine(
                    orderPrice,
                    new DateTime(smallChartCandles[0].OpenTimeTicks, DateTimeKind.Utc),
                    new DateTime(smallChartCandles[smallChartCandles.Count - 1].OpenTimeTicks, DateTimeKind.Utc),
                    ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries,
                    ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations,
                    null,
                    Colors.Blue);
            }


            var tradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModel, false, GetSelectedTimeframe(_displayedTrade), _currentCandles[GetSelectedTimeframe(_displayedTrade)], _displayedTrade);
            foreach (var tradeAnnotation in tradeAnnotations)
            {
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Add(tradeAnnotation);
            }

            var smallChartTradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModelSmaller1, true, Timeframe.D1,
                _currentCandles[Timeframe.D1], _displayedTrade);
            foreach (var tradeAnnotation in smallChartTradeAnnotations)
            {
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Add(tradeAnnotation);
            }
        }
        #endregion

        private void Next()
        {
            if (_currentTrade != null && (_currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null))
            {
                _showMessageAction("Current trade needs to be completed before changing chart");
                return;
            }

            Log.Info("Loading next chart");
            var market = _markets[_rnd.Next(0, _markets.Count)];
            CreateEmptyTrade(market);
            CloseHalfTradeAtLimit = false;
            OrderExpiryCandlesIndex = 0;
            var allH2Candles = _candlesService.GetCandles(market, Timeframe.H2);
            _h2EndDateIndex = _rnd.Next(12 * 50, allH2Candles.Count - 12 * 100);

            _allH2Candles = null;
            _allD1Candles = null;
            _allSmallestTimeframeCandles = null;
            UpdateUIState();

            SetupChart();
        }

        private bool _uiStateUpdating = false;
        private bool _isCloseHalfTradeAtLimitEnabled;
        private IDisposable _chartMouseMoveDisposable;
        private bool _isEntryButtonEnabled;

        private void UpdateUIState()
        {
            if (_uiStateUpdating) return;

            var viewingCompletedTrade = _currentTrade != _displayedTrade;
            _uiStateUpdating = true;
            IsTradeEnabled = !viewingCompletedTrade && _currentTrade.OrderDateTime == null && _currentTrade.EntryDateTime == null;
            IsCloseEnabled = !viewingCompletedTrade && (_currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null);
            IsEntryButtonEnabled = !viewingCompletedTrade && _currentTrade.EntryDateTime == null;
            Running = !viewingCompletedTrade && (_currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null);
            IsCloseHalfTradeAtLimitEnabled = !viewingCompletedTrade && _currentTrade.LimitPrices.Count > 0 && _currentTrade.EntryPrice == null;

            if (ChartingService.ChartMode == ChartMode.AddLine)
            {
                IsSetLimitButtonPressed = false;
                IsSetStopButtonPressed = false;
                IsEntryButtonPressed = false;
            }

            if (IsSetStopButtonPressed || IsEntryButtonPressed || IsSetLimitButtonPressed || ChartingService.ChartMode == ChartMode.AddLine)
            {
                _setCursorAction(Cursors.Cross);
            }
            else
            {
                _setCursorAction(Cursors.Arrow);
            }

            ClearLimitCommand.RaiseCanExecuteChanged();
            ClearStopCommand.RaiseCanExecuteChanged();
            ClearEntryOrderCommand.RaiseCanExecuteChanged();
            OnPropertyChanged("ViewingCompletedTrade");
            OnPropertyChanged("IsOrderExpiryCandlesEnabled");

            _uiStateUpdating = false;
        }

        public void SetupChart(bool recreateChart = true)
        {
            // Get candles
            if (_allSmallestTimeframeCandles == null)
            {
                _allSmallestTimeframeCandles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.M5);
                _allH2Candles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.H2);
                _allD1Candles = _candlesService.GetCandles(_displayedTrade.Market, Timeframe.D1);
            }

            var endDateUtc = new DateTime(_allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);

            var currentH2Candles = _allH2Candles.Where(x => _currentTrade != _displayedTrade || new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();
            var currentD1Candles = _allD1Candles.Where(x => _currentTrade != _displayedTrade || new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();

            Candle? d1Incomplete = null;

            for (var i = 0; i <= (_currentTrade == _displayedTrade ? _h2EndDateIndex : _allH2Candles.Count - 1); i++)
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

            if (recreateChart && _displayedTrade != _currentTrade)
            {
                ChartHelper.SetChartViewModelVisibleRange(_displayedTrade, ChartViewModel, _allH2Candles, Timeframe.H2);
                ChartHelper.SetChartViewModelVisibleRange(_displayedTrade, ChartViewModelSmaller1, _allD1Candles, Timeframe.D1);
            }
        }

        private void Trade(TradeDirection direction)
        {
            if (_currentTrade.StopPrices.Count == 0)
            {
                _showMessageAction("New trades must have a stop set");
                return;
            }

            IsEntryButtonPressed = false;
            IsTradeEnabled = false;
            Running = true;

            var timeframe = GetSelectedTimeframe(null);
            var candle = _currentCandles[Timeframe.D1][_currentCandles[Timeframe.D1].Count - 1];
            var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);

            _currentTrade.Comments = _getTradeCommentsFunc();
            _currentTrade.Broker = "FXCM";
            _currentTrade.OrderDateTime = date;
            _currentTrade.OrderAmount = 100;
            _currentTrade.OrderKind = _currentTrade.OrderPrice == null ? OrderKind.Market : OrderKind.EntryPrice;
            _currentTrade.TradeDirection = direction;
            _currentTrade.Timeframe = timeframe;


            if (_currentTrade.OrderPrice == null) _currentTrade.SetEntry(date, (decimal)candle.Close);

            IsCloseEnabled = true;

            if (_currentTrade.OrderKind == OrderKind.EntryPrice && OrderExpiryCandlesIndex > 0)
            {
                _currentTrade.OrderExpireTime = date.AddSeconds(OrderExpiryCandlesIndex * (int)timeframe);
            }

            UpdateUIState();
        }

        public bool Running
        {
            get => _running;
            private set
            {
                _running = value;
                OnPropertyChanged();
                ShortCommand.RaiseCanExecuteChanged();
                LongCommand.RaiseCanExecuteChanged();
                CloseCommand.RaiseCanExecuteChanged();
                NewChartCommand.RaiseCanExecuteChanged();
                NextCandleCommand.RaiseCanExecuteChanged();
            }
        }

        private void SaveTrades()
        {
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);

            File.WriteAllText(_tmpPath, JsonConvert.SerializeObject(Trades.ToList()));

            if (File.Exists(_finalPath)) File.Delete(_finalPath);

            File.Move(_tmpPath, _finalPath);
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
                    ChartHelper.AddIndicator(selectedIndicator.Pane, _displayedTrade.Market, selectedIndicator.Indicator, selectedIndicator.Colour, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                }
                else
                {
                    selectedIndicator.Indicator.Reset();
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _displayedTrade.Market, selectedIndicator.Indicator, selectedIndicator.Colour, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                    selectedIndicator.Indicator.Reset();
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _displayedTrade.Market, selectedIndicator.Indicator, selectedIndicator.Colour, Timeframe.D1, _currentCandles[Timeframe.D1]);
                }
            }

            SetAnnotations();
        }

        public void KeyDown(Key key, int? candleIndex, decimal? price)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (candleIndex != null && price != null)
                {
                    if (key == Key.S) IsSetStopButtonPressed = true;
                    if (key == Key.L) IsSetLimitButtonPressed = true;
                    if (key == Key.E && !Running) IsEntryButtonPressed = true;
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
            var allH2Candles = _allH2Candles;

            _h2EndDateIndex++;

            if (_h2EndDateIndex >= allH2Candles.Count) _h2EndDateIndex = allH2Candles.Count - 1;

            if (_currentTrade != null)
            {
                var endDate = new DateTime(allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);
                var startDate = new DateTime(allH2Candles[_h2EndDateIndex - 1].CloseTimeTicks, DateTimeKind.Utc);

                for (var i = 0; i < _allSmallestTimeframeCandles.Count; i++)
                {
                    var m1Candle = _allSmallestTimeframeCandles[i];
                    var m1EndDate = new DateTime(m1Candle.CloseTimeTicks, DateTimeKind.Utc);
                    var m1StartDate = new DateTime(m1Candle.OpenTimeTicks, DateTimeKind.Utc);

                    if (!(m1EndDate >= startDate && m1StartDate <= endDate)) continue;

                    // Try to close trade
                    if (_currentTrade.EntryPrice != null && _currentTrade.CloseReason == null)
                    {
                        var openTrade = _currentTrade;

                        if (openTrade.StopPrice != null && openTrade.TradeDirection.Value == TradeDirection.Long &&
                            m1Candle.Low <= (double)openTrade.StopPrice.Value)
                        {
                            var stopPrice = Math.Min((decimal)m1Candle.High, openTrade.StopPrice.Value);

                            openTrade.SetClose(m1StartDate, stopPrice, TradeCloseReason.HitStop);
                            break;
                        }

                        if (openTrade.StopPrice != null && openTrade.TradeDirection.Value == TradeDirection.Short &&
                            m1Candle.High >= (double)openTrade.StopPrice.Value)
                        {
                            var stopPrice = Math.Max((decimal)m1Candle.Low, openTrade.StopPrice.Value);
                            openTrade.SetClose(startDate, stopPrice, TradeCloseReason.HitStop);
                            break;
                        }

                        if (openTrade.LimitPrice != null && openTrade.TradeDirection.Value == TradeDirection.Long &&
                            m1Candle.High >= (double)openTrade.LimitPrice.Value)
                        {
                            var stopPrice = Math.Max((decimal)m1Candle.Low, openTrade.StopPrice.Value);
                            openTrade.SetClose(startDate, stopPrice, TradeCloseReason.HitLimit);
                            break;
                        }

                        if (openTrade.LimitPrice != null && openTrade.TradeDirection.Value == TradeDirection.Short &&
                            m1Candle.Low <= (double)openTrade.LimitPrice.Value)
                        {
                            var stopPrice = Math.Min((decimal)m1Candle.High, openTrade.StopPrice.Value);
                            openTrade.SetClose(startDate, stopPrice, TradeCloseReason.HitLimit);
                            break;
                        }
                    }

                    // Try to fill order
                    if (_currentTrade.EntryPrice == null && _currentTrade.OrderPrice != null)
                    {
                        var order = _currentTrade;

                        if (order.OrderPrice != null)
                        {
                            if (order.TradeDirection == TradeDirection.Long &&
                                m1Candle.High >= (double)order.OrderPrice &&
                                m1Candle.Low <= (double)order.OrderPrice)
                            {
                                var entryPrice = Math.Max((decimal)m1Candle.Low, order.OrderPrice.Value);
                                order.SetEntry(m1StartDate, entryPrice);
                            }
                            else if (order.TradeDirection == TradeDirection.Short &&
                                     m1Candle.High >= (double)order.OrderPrice &&
                                     m1Candle.Low <= (double)order.OrderPrice)
                            {
                                var entryPrice = Math.Min((decimal)m1Candle.High, order.OrderPrice.Value);
                                order.SetEntry(m1StartDate, entryPrice);
                            }
                        }
                        else if (order.OrderPrice == null)
                        {
                            order.SetEntry(m1StartDate, (decimal)m1Candle.Close);
                        }

                        if (_currentTrade.EntryPrice == null && order.OrderExpireTime != null && m1Candle.CloseTime() >= order.OrderExpireTime)
                        {
                            order.SetExpired(m1Candle.OpenTime());
                        }
                    }
                }

                if (_currentTrade.CloseReason != null)
                {
                    CloseTrade();
                }
            }

            SetupChart(false);
            UpdateUIState();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}