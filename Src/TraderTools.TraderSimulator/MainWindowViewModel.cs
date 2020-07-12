using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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
using TraderTools.Core.UI.Views;
using TraderTools.Indicators;
using TraderTools.TradingSimulator.Extensions;
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
        private readonly Func<IDisposable> _suspendChartUpdatesAction;
        private readonly Action _updateWindowAction;
        private Dispatcher _dispatcher;
        private Random _rnd = new Random();
        private Dictionary<Timeframe, List<Candle>> _currentCandles = new Dictionary<Timeframe, List<Candle>>();
        private bool _isTradeEnabled;
        private int _orderExpiryCandlesIndex;
        private int _h2EndDateIndex;
        private bool _running;
        private Trade _tradeBeingSetup;
        private List<Candle> _allSmallestTimeframeCandles;
        private List<Candle> _allH2Candles;
        private List<Candle> _allH4Candles;
        private List<Candle> _allD1Candles;
        public event PropertyChangedEventHandler PropertyChanged;
        private ObservableCollection<Trade> _trades = new ObservableCollection<Trade>();
        private List<string> _markets;
        private IDisposable _chartModeDisposable;
        private string _market;
        private Trade _selectedTrade;
        private IDisposable _chartClickedDisposable;
        private IDisposable _chartMouseMoveDisposable;

        [Import] private IBrokersCandlesService _brokerCandlesService;
        [Import] IMarketDetailsService _marketsService;
        [Import] private ITradeDetailsAutoCalculatorService _tradeCalculatorService;
        [Import] private IDataDirectoryService _dataDirectoryService;
        private string _directory;
        private Action _selectedTradeChangedAction;
        private bool _setLimitChecked;
        private bool _setStopChecked;
        private bool _setOrderChecked;
        private CandlesService _candlesService;
        private bool _setCloseEnabled;
        private bool _setOrderEnabled;
        private bool _setLimitEnabled;
        private bool _setStopEnabled;

        #endregion

        public MainWindowViewModel(
            Func<string> getTradeCommentsFunc,
            Action<string> showMessageAction,
            Action<Cursor> setCursorAction,
            Func<IDisposable> suspendChartUpdatesAction,
            Action updateWindowAction)
        {
            DependencyContainer.ComposeParts(this);

            _directory = _dataDirectoryService.MainDirectoryWithApplicationName;
            if (!Directory.Exists(_directory))
            {
                Directory.CreateDirectory(_directory);
            }

            _candlesService = new CandlesService();

            /* // Create candles
            Task.Run(() =>
            {
                Log.Info("Creating candles");
                _candlesService.ConvertCandles(_brokerCandlesService, _fxcm, _marketsService.GetAllMarketDetails().Select(m => m.Name).ToList());
                Log.Info("Candles created");
            });
            return;*/

            TimeFrameItems = new List<Timeframe> { Timeframe.D1, Timeframe.H4, Timeframe.H2, Timeframe.H1, Timeframe.M1 };

            LargeChartTimeframeOptions.Clear();
            LargeChartTimeframeOptions.Add(Timeframe.H4);
            LargeChartTimeframeOptions.Add(Timeframe.H2);

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

            NewChartCommand = new DelegateCommand(o => NewChartMarket(), o => !Running);
            NextCandleCommand = new DelegateCommand(o => ProgressTime());
            ClearTradesCommand = new DelegateCommand(o => ClearTrades());
            CloseTradeCommand = new DelegateCommand(o => CloseTrade());
            DeleteTradeCommand = new DelegateCommand(o => DeleteTrade());
            StartLongTradeCommand = new DelegateCommand(o => StartTrade(TradeDirection.Long));
            StartShortTradeCommand = new DelegateCommand(o => StartTrade(TradeDirection.Short));

            _getTradeCommentsFunc = getTradeCommentsFunc;
            _showMessageAction = showMessageAction;
            _setCursorAction = setCursorAction;
            _suspendChartUpdatesAction = suspendChartUpdatesAction;
            _updateWindowAction = updateWindowAction;
            DependencyContainer.ComposeParts(this);
            _dispatcher = Dispatcher.CurrentDispatcher;

            _chartModeDisposable = ChartingService.ChartModeObservable.Subscribe(ChartModeChanged);

            TradeListDisplayOptions = TradeListDisplayOptionsFlag.ClosePrice
                                  | TradeListDisplayOptionsFlag.Comments
                                  | TradeListDisplayOptionsFlag.OrderPrice
                                  | TradeListDisplayOptionsFlag.Limit
                                  | TradeListDisplayOptionsFlag.OrderDate
                                  | TradeListDisplayOptionsFlag.ResultR
                                  | TradeListDisplayOptionsFlag.Status
                                  | TradeListDisplayOptionsFlag.Stop
                                  | TradeListDisplayOptionsFlag.Strategies;

            _markets = new List<string>();
            foreach (var m in _marketsService.GetAllMarketDetails().Select(m => m.Name))
            {
                if (File.Exists(_candlesService.GetCandlesPath(m, Timeframe.M5))
                    && File.Exists(_candlesService.GetCandlesPath(m, Timeframe.H2))
                    && File.Exists(_candlesService.GetCandlesPath(m, Timeframe.H4))
                    && File.Exists(_candlesService.GetCandlesPath(m, Timeframe.D1)))
                {
                    _markets.Add(m);
                }
            }

            IsTradeEnabled = false;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraderSimulator");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            _orderExpiryCandlesIndex = 0;

            ResultsViewModel = new TradesResultsViewModel(() => Trades.ToList()) { ShowOptions = true, ShowSubOptions = false, AdvStrategyNaming = true };
            ResultsViewModel.ResultOptions.Remove("Timeframe");
            ResultsViewModel.ShowOptions = false;
            ResultsViewModel.ShowIncludeOpenClosedTradesOptions = false;

            LoadTrades();
            RemoveIncompleteTrades();
            NewChartMarket();

            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);
        }

        public bool SetLimitChecked
        {
            get => _setLimitChecked;
            set
            {
                if (value)
                {
                    SetStopChecked = false;
                    SetOrderChecked = false;
                }

                _setLimitChecked = value; 
                OnPropertyChanged();
            }
        }

        public bool SetStopChecked
        {
            get => _setStopChecked;
            set
            {
                if (value)
                {
                    SetLimitChecked = false;
                    SetOrderChecked = false;
                }

                _setStopChecked = value; 
                OnPropertyChanged();
            }
        }

        public bool SetOrderChecked
        {
            get => _setOrderChecked;
            set
            {
                if (value)
                {
                    SetLimitChecked = false;
                    SetStopChecked = false;
                }

                _setOrderChecked = value;
                OnPropertyChanged();
            }
        }

        public bool SetCloseEnabled
        {
            get => _setCloseEnabled;
            set
            {
                _setCloseEnabled = value; 
                OnPropertyChanged();
            }
        }

        public bool SetOrderEnabled
        {
            get => _setOrderEnabled;
            set
            {
                _setOrderEnabled = value; 
                OnPropertyChanged();
            }
        }

        public bool SetLimitEnabled
        {
            get => _setLimitEnabled;
            set
            {
                _setLimitEnabled = value; 
                OnPropertyChanged();
            }
        }

        public bool SetStopEnabled
        {
            get => _setStopEnabled;
            set
            {
                _setStopEnabled = value; 
                OnPropertyChanged();
            }
        }

        private void ChartClicked((DateTime Time, double Price, Action SetIsHandled) details)
        {
            if (SelectedTrade == null) return;

            var lastCandle = _allH2Candles[_h2EndDateIndex];
            
            if (SetStopChecked)
            {
                SelectedTrade.SetTradeStop(lastCandle, details.Price);
                SetStopChecked = false;
                SetupAnnotations();
                details.SetIsHandled();
            }

            if (SetLimitChecked)
            {
                SelectedTrade.SetTradeLimit(lastCandle, details.Price);
                SetLimitChecked = false;
                SetupAnnotations();
                details.SetIsHandled();
            }

            if (SetOrderChecked)
            {
                SelectedTrade.SetTradeEntryPrice(lastCandle, details.Price);
                SetOrderChecked = false;
                SetupAnnotations();
                details.SetIsHandled();
            }
        }

        private void DeleteTrade()
        {
            if (SelectedTrade != null)
            {
                var tradeToRemove = SelectedTrade;
                SelectedTrade = null;
                Trades.Remove(tradeToRemove);
                TradeAutoCalculator.RemoveTrade(tradeToRemove);
                SaveTrades();
                ResultsViewModel.UpdateResults();
                SetupAnnotations();
            }
        }

        private void CloseTrade()
        {
            if (SelectedTrade != null)
            {
                CloseTrade(SelectedTrade);
            }
        }

        public static readonly DependencyProperty TradeSelectionModeProperty = DependencyProperty.Register(
            "TradeSelectionMode", typeof(DataGridSelectionMode), typeof(MainWindowViewModel), new PropertyMetadata(DataGridSelectionMode.Single));

        private int _loadedTradesCount;

        public DataGridSelectionMode TradeSelectionMode
        {
            get { return (DataGridSelectionMode) GetValue(TradeSelectionModeProperty); }
            set { SetValue(TradeSelectionModeProperty, value); }
        }

        // ReSharper disable once UnusedMember.Global
        public TradeListDisplayOptionsFlag TradeListDisplayOptions { get; set; }

        private void ClearTrades()
        {
            foreach (var trade in Trades)
            {
                _tradeCalculatorService.RemoveTrade(trade);
                trade.PropertyChanged -= TradeOnPropertyChanged;
            }

            Trades.Clear();
            ResultsViewModel.UpdateResults();
        }

        public Trade SelectedTrade
        {
            get => _selectedTrade;
            set
            {
                _selectedTrade = value;
                SetupAnnotations();
                OnPropertyChanged();
                _selectedTradeChangedAction?.Invoke();
                UpdateUI();
            }
        }

        public ObservableCollection<IndicatorDisplayOptions> AvailableIndiciators { get; } = new ObservableCollection<IndicatorDisplayOptions>();

        public ObservableCollection<IndicatorDisplayOptions> SelectedIndicators { get; } = new ObservableCollection<IndicatorDisplayOptions>();

        private void UpdateUI()
        {
            SetOrderEnabled = SelectedTrade != null && SelectedTrade.EntryPrice == null && SelectedTrade.ClosePrice == null;
            SetCloseEnabled = SelectedTrade != null && (SelectedTrade.EntryPrice != null || SelectedTrade.OrderPrice != null) && SelectedTrade.ClosePrice == null;
            SetLimitEnabled = SelectedTrade != null && SelectedTrade.ClosePrice == null;
            SetStopEnabled = SelectedTrade != null && SelectedTrade.ClosePrice == null;
        }

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

        private void RemoveIncompleteTrades()
        {
            foreach(var t in Trades.ToList())
            {
                if (t.CloseDateTime == null)
                {
                    Trades.Remove(t);
                    TradeAutoCalculator.RemoveTrade(t);
                }
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
        public DelegateCommand CloseTradeCommand { get; set; }
        public DelegateCommand DeleteTradeCommand { get; set; }
        [Import] public ChartingService ChartingService { get; private set; }
        [Import] public ITradeDetailsAutoCalculatorService TradeAutoCalculator { get; private set; }

        private void CloseTrade(Trade trade)
        {
            if (trade.CloseDateTime == null)
            {
                var candle = _allH2Candles[_h2EndDateIndex];
                var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);

                var price = trade.TradeDirection == TradeDirection.Long
                    ? (decimal) candle.CloseBid
                    : (decimal) candle.CloseAsk;
                trade.SetClose(date,
                    trade.EntryPrice != null ? price : (decimal?)null,
                    TradeCloseReason.ManualClose);
            }

            ResultsViewModel.UpdateResults();
            SetupAnnotations();
        }

        private void LoadTrades()
        {
            var path = Path.Combine(_directory, "Trades.json");

            if (File.Exists(path))
            {
                var trades = JsonConvert.DeserializeObject<List<Trade>>(File.ReadAllText(path));
                foreach (var t in trades)
                {
                    Trades.Add(t);
                }

                _loadedTradesCount = trades.Count;
            }

            ResultsViewModel.UpdateResults();
            SetupAnnotations();
        }

        private void SaveTrades()
        {
            // Rename backups
            int maxBackups = 20;
            for (var i = maxBackups; i >= 1; i--)
            {
                var backupPath = Path.Combine(_directory, $"Trades_{i}.json");

                if (i == maxBackups && File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                if (i != maxBackups && File.Exists(backupPath))
                {
                    var newBackupPath = Path.Combine(_directory, $"Trades_{i + 1}.json");
                    File.Move(backupPath, newBackupPath);
                }
            }

            var path = Path.Combine(_directory, "Trades.json");
            if (File.Exists(path))
            {
                var backupPath = Path.Combine(_directory, "Trades_1.json");
                File.Move(path, backupPath);
            }

            var tmpPath = Path.Combine(_directory, "Trades_tmp.json");
            var json = JsonConvert.SerializeObject(Trades);
            File.WriteAllText(tmpPath, json);

            File.Copy(tmpPath, path);
            File.Delete(tmpPath);
        }

        private void StartTrade(TradeDirection direction)
        {
            if (Trades.Any(t => t.OrderPrice == null && t.EntryPrice == null))
            {
                MessageBox.Show("New trade is already being setup", "New trade already being created", MessageBoxButton.OK);
                return;
            }

            var timeframe = LargeChartTimeframe;
            var candle = _currentCandles[Timeframe.D1][_currentCandles[Timeframe.D1].Count - 1];
            var date = new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);

            var nextId = Trades.Count(t => !string.IsNullOrEmpty(t.Id)) > 0
                ? Trades.Where(t => !string.IsNullOrEmpty(t.Id)).Select(t => int.Parse(t.Id)).Max() + 1
                : 1;

            var newTrade = new Trade
            {
                Id = nextId.ToString(),
                Broker = "FXCM",
                Market = _market,
                TradeDirection = direction,
                OrderDateTime = date,
                OrderAmount = 100,
                Timeframe = timeframe
            };
            Trades.Insert(0, newTrade);
            SelectedTrade = newTrade;

            SaveTrades();

            newTrade.PropertyChanged += TradeOnPropertyChanged;
        }

        private void TradeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ResultsViewModel.UpdateResults();
        }

        #region Properties
        public ObservableCollection<Trade> Trades { get; } = new ObservableCollection<Trade>();
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
            if (!_currentCandles.ContainsKey(LargeChartTimeframe) || ChartViewModel.ChartPaneViewModels.Count == 0 || ChartViewModelSmaller1.ChartPaneViewModels.Count == 0)
            {
                return;
            }

            if (ChartViewModel.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();
            if (ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations == null) ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();

            var mainAnnotations = ChartViewModel.ChartPaneViewModels[0].TradeAnnotations;
            var smallAnnotations = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations;

            var fullTradeAnnotations = TradeAnnotationsToShow.All;
            var partialTradeAnnotations = TradeAnnotationsToShow.EntryMarker | TradeAnnotationsToShow.CloseMarker | TradeAnnotationsToShow.MakeEntryCloseMarkerSmaller;

            using (_suspendChartUpdatesAction())
            {
                RemoveExistingAnnotations(mainAnnotations, smallAnnotations);

                foreach (var trade in Trades.Skip(_loadedTradesCount))
                {
                    if (trade.Market != _market) continue;

                    ChartHelper.CreateTradeAnnotations(mainAnnotations, ChartViewModel, trade == SelectedTrade ? fullTradeAnnotations : partialTradeAnnotations, _currentCandles[LargeChartTimeframe], trade);
                    ChartHelper.CreateTradeAnnotations(smallAnnotations, ChartViewModelSmaller1, trade == SelectedTrade ? fullTradeAnnotations : partialTradeAnnotations, _currentCandles[Timeframe.D1], trade);
                }
            }
        }

        private static void RemoveExistingAnnotations(AnnotationCollection mainAnnotations, AnnotationCollection smallAnnotations)
        {
            // Remove existing annotations
            for (var i = mainAnnotations.Count - 1; i >= 0; i--)
            {
                var annotation = mainAnnotations[i];
                if (annotation is LineAnnotation l)
                {
                    if (l.Tag != null && ((string) l.Tag).StartsWith("Added")) continue;
                }

                mainAnnotations.RemoveAt(i);
            }

            for (var i = smallAnnotations.Count - 1; i >= 0; i--)
            {
                var annotation = smallAnnotations[i];
                if (annotation is LineAnnotation l)
                {
                    if (l.Tag != null && ((string) l.Tag).StartsWith("Added")) continue;
                }

                smallAnnotations.RemoveAt(i);
            }
        }

        #endregion

        private void NewChartMarket()
        {
            Log.Info("Loading next chart");
            RemoveIncompleteTrades();
            _market = _markets[_rnd.Next(0, _markets.Count)];
            OrderExpiryCandlesIndex = 0;
            _allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
            _h2EndDateIndex = _rnd.Next(12 * 50, _allH2Candles.Count - 12 * 100);

            _allSmallestTimeframeCandles = _candlesService.GetCandles(_market, Timeframe.M5);
            _allH4Candles = _candlesService.GetCandles(_market, Timeframe.H4);
            _allD1Candles = _candlesService.GetCandles(_market, Timeframe.D1);

            if (_allSmallestTimeframeCandles.Count == 0 || _allH2Candles.Count == 0)
            {
                NewChartMarket();
                return;
            }

            SetupChart();
        }

        protected override void LargeChartTimeframeChanged()
        {
            SetupChart();
        }

        public void SetupChart(bool recreateChart = true)
        {
            // Get candles
            if (_allSmallestTimeframeCandles == null)
            {
                _allSmallestTimeframeCandles = _candlesService.GetCandles(_market, Timeframe.M5);
                _allH4Candles = _candlesService.GetCandles(_market, Timeframe.H4);
                _allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
                _allD1Candles = _candlesService.GetCandles(_market, Timeframe.D1);
            }

            var endDateUtc = new DateTime(_allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);

            var currentH4Candles = _allH4Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();
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
                        CloseAsk = h2.CloseAsk,
                        CloseBid = h2.CloseBid,
                        OpenAsk = d1Incomplete?.OpenAsk ?? h2.OpenAsk,
                        OpenBid = d1Incomplete?.OpenBid ?? h2.OpenBid,
                        CloseTimeTicks = h2.CloseTimeTicks,
                        HighBid = d1Incomplete != null && d1Incomplete.Value.HighBid > h2.HighBid ? d1Incomplete.Value.HighBid : h2.HighBid,
                        HighAsk = d1Incomplete != null && d1Incomplete.Value.HighAsk > h2.HighAsk ? d1Incomplete.Value.HighAsk : h2.HighAsk,
                        LowBid = d1Incomplete != null && d1Incomplete.Value.LowBid < h2.LowBid ? d1Incomplete.Value.LowBid : h2.LowBid,
                        LowAsk = d1Incomplete != null && d1Incomplete.Value.LowAsk < h2.LowAsk ? d1Incomplete.Value.LowAsk : h2.LowAsk,
                        OpenTimeTicks = d1Incomplete?.OpenTimeTicks ?? h2.OpenTimeTicks,
                        IsComplete = 0
                    };
                }
            }

            if (d1Incomplete != null) currentD1Candles.Add(d1Incomplete.Value);

            _currentCandles[Timeframe.D1] = currentD1Candles;
            _currentCandles[Timeframe.H2] = currentH2Candles;
            _currentCandles[Timeframe.H4] = currentH4Candles;

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

            var largeChartTimeframe = LargeChartTimeframe;

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

                if (key == Key.N && !Running) NewChartMarket();

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
            if (Trades.Any(t => t.StopPrice == null))
            {
                MessageBox.Show("Cannot progress time as there is a trade without a stop price", "Cannot progress time", MessageBoxButton.OK);
                return;
            }

            var updated = false;
            // Enter market for any market entry trades
            var lastCandle = _allH2Candles[_h2EndDateIndex];
            foreach (var t in Trades.Where(t => t.OrderPrice == null && t.EntryPrice == null && t.CloseDateTime == null))
            {
                var close = t.TradeDirection == TradeDirection.Long
                    ? (decimal) lastCandle.CloseAsk
                    : (decimal) lastCandle.CloseBid;

                t.SetEntry(lastCandle.CloseTime(), close, t.OrderAmount.Value);
                updated = true;
            }

            Dispatcher.BeginInvoke((Action)(() =>
            {
                var allH2Candles = _allH2Candles;

                _h2EndDateIndex++;

                if (_h2EndDateIndex >= allH2Candles.Count) _h2EndDateIndex = allH2Candles.Count - 1;

                var endDate = new DateTime(allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);
                var startDate = new DateTime(allH2Candles[_h2EndDateIndex - 1].CloseTimeTicks, DateTimeKind.Utc);

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
                    UpdateUI();
                }

                SetupAnnotations();

                _updateWindowAction();
            }));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Closing()
        {
            SaveTrades();
        }
    }
}