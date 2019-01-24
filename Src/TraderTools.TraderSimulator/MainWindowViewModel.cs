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
using Hallupa.Library.UI;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;
using TraderTools.Core.UI.ViewModels;
using TraderTools.Indicators;
using TraderTools.TradingTrainer.Services;
using TraderTools.UI.Views;

namespace TraderTools.TradingTrainer
{
    public class MainWindowViewModel : DoubleChartViewModel, INotifyPropertyChanged
    {
        #region Fields
        private TradeDetails _selectedTrade = null;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Func<string> _getTradeCommentsFunc;
        private readonly Action<string> _showMessageAction;
        private readonly Action<Cursor> _setCursorAction;
        private Dispatcher _dispatcher;
        private Random _rnd = new Random();
        private Dictionary<Timeframe, List<Candle>> _currentCandles = new Dictionary<Timeframe, List<Candle>>();
        private Dictionary<Timeframe, List<Candle>> _allCandles = new Dictionary<Timeframe, List<Candle>>();
        private string _market;
        private bool _isTradeEnabled;
        private string _tmpPath;
        private string _finalPath;
        private int _orderExpiryCandlesIndex;
        private int _h2EndDateIndex;
        private bool _running;
        private TradeDetails _currentTrade;
        private List<Candle> _allSmallestTimeframeCandles;
        private List<Candle> _allH2Candles;
        //private List<Candle> _allH4Candles;
        private List<Candle> _allD1Candles;
        private bool _isCloseEnabled;
        private bool _closeHalfTradeAtLimit;
        public event PropertyChangedEventHandler PropertyChanged;
        private CandlesService _candlesService;
        private ObservableCollection<TradeDetails> _trades = new ObservableCollection<TradeDetails>();
        private int _selectedMainIndicatorsIndex;
        private List<string> _markets;
        private IDisposable _chartClickedDisposable;
        private bool _isSetStopButtonPressed;
        private bool _isSetLimitButtonPressed;
        private bool _isEntryButtonPressed;
        private IDisposable _chartModeDisposable;

        #endregion

        public MainWindowViewModel(Func<string> getTradeCommentsFunc, Action<string> showMessageAction, Action<Cursor> setCursorAction)
        {
            CreateEmptyTrade();
            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1Tiger,
                Timeframe.D1,
                Timeframe.H4,
                Timeframe.H2,
                Timeframe.H1,
                Timeframe.M1,
            };

            DeleteCommand = new DelegateCommand(o => DeleteTrade());
            NewChartCommand = new DelegateCommand(o => Next(), o => !Running);
            NextCandleCommand = new DelegateCommand(o => ProgressTime());

            _getTradeCommentsFunc = getTradeCommentsFunc;
            _showMessageAction = showMessageAction;
            _setCursorAction = setCursorAction;
            DependencyContainer.ComposeParts(this);
            _dispatcher = Dispatcher.CurrentDispatcher;
            _candlesService = new CandlesService();

            _chartModeDisposable = ChartingService.ChartModeObservable.Subscribe(c => ChartModeChanged(c));

            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);

            var regex = new Regex("FXCM_([a-zA-Z0-9]*)_");
            var markets = new HashSet<string>();
            foreach (var candlesPath in Directory.GetFiles(_candlesService.CandlesDirectory))
            {
                var marketName = regex.Match(Path.GetFileName(candlesPath)).Groups[1].Value;
                markets.Add(marketName);
            }

            _markets = markets.ToList();

            // Setup brokers and load accounts

            LongCommand = new DelegateCommand(o => Trade(TradeDirection.Long), o => !Running);
            ShortCommand = new DelegateCommand(o => Trade(TradeDirection.Short), o => !Running);
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

            if (File.Exists(_finalPath))
            {
                Trades = new ObservableCollection<TradeDetails>(JsonConvert.DeserializeObject<List<TradeDetails>>(File.ReadAllText(_finalPath)));
                SimResultsViewModel.UpdateResults();
            }

            Next();
        }

        private void ChartModeChanged(ChartMode? chartMode)
        {
            UpdateUIState();
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
            var date = _allH2Candles[_h2EndDateIndex].CloseTime();

            _currentTrade.OrderPrice = (decimal)price;

            SetAnnotations();
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
        }

        public DelegateCommand NextCandleCommand { get; set; }

        public DelegateCommand NewChartCommand { get; set; }

        public List<Timeframe> TimeFrameItems { get; set; }

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

        public int SelectedMainIndicatorsIndex
        {
            get => _selectedMainIndicatorsIndex;
            set => _selectedMainIndicatorsIndex = value;
        }
        public DelegateCommand DeleteCommand { get; }

        private void CreateEmptyTrade()
        {
            _currentTrade = new TradeDetails();
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

                foreach (var stop in existingTrade.GetStopPrices())
                {
                    _currentTrade.AddStopPrice(stop.Date, stop.Price);
                }
            }
            else
            {
                CreateEmptyTrade();
            }

            CloseHalfTradeAtLimit = false;
            OrderExpiryCandlesIndex = 0;

            UpdateUIState();
            SetAnnotations();
            _showMessageAction("Trade closed");
        }

        #region Properties
        public ObservableCollection<TradeDetails> Trades { get; } = new ObservableCollection<TradeDetails>();
        public DelegateCommand CloseCommand { get; private set; }
        public SimulationResultsViewModel SimResultsViewModel { get; }

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

        protected void DeleteTrade()
        {
            if (SelectedTrade == null)
            {
                return;
            }

            var selectedTrade = SelectedTrade;
            SelectedTrade = null;
            Trades.Remove(selectedTrade);
            SaveTrades();
            SimResultsViewModel.UpdateResults();
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
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.RemoveAt(i);
            }

            for (var i = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Count - 1; i >= 0; i--)
            {
                var annotation = ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations[i];
                if (annotation is LineAnnotation l)
                {
                    if ((string)l.Tag == "Added") continue;
                }
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.RemoveAt(i);
            }

            if (_currentTrade.StopPrices.Count > 0)
            {
                var stopPosition = _currentTrade.StopPrices[_currentTrade.StopPrices.Count - 1].Price;
                if (stopPosition != null)
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

            if (_currentTrade.LimitPrices.Count > 0)
            {
                var limtPosition = _currentTrade.LimitPrices[_currentTrade.LimitPrices.Count - 1].Price;
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

            if (_currentTrade.EntryPrice == null && _currentTrade.OrderPrice != null)
            {
                var orderPrice = _currentTrade.OrderPrice.Value;
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


            var tradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModel, false, GetSelectedTimeframe(_currentTrade), _currentCandles[GetSelectedTimeframe(_currentTrade)], _currentTrade);
            foreach (var tradeAnnotation in tradeAnnotations)
            {
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Add(tradeAnnotation);
            }

            var smallChartTradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModelSmaller1, true, Timeframe.D1,
                _currentCandles[Timeframe.D1], _currentTrade);
            foreach (var tradeAnnotation in smallChartTradeAnnotations)
            {
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations.Add(tradeAnnotation);
            }
        }

        public TradeDetails SelectedTrade
        {
            get { return _selectedTrade; }
            set
            {
                _selectedTrade = value;
            }
        }
        #endregion

        private void Next()
        {
            if (_currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null)
            {
                _showMessageAction("Current trade needs to be completed before changing chart");
                return;
            }

            Log.Info("Loading next chart");
            _market = _markets[_rnd.Next(0, _markets.Count)];
            SelectedTrade = null;
            CreateEmptyTrade();
            CloseHalfTradeAtLimit = false;
            OrderExpiryCandlesIndex = 0;
            var allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
            _h2EndDateIndex = _rnd.Next(12 * 50, allH2Candles.Count - 12 * 50);

            _allH2Candles = null;
            //_allH4Candles = null;
            _allD1Candles = null;
            _allSmallestTimeframeCandles = null;
            UpdateUIState();

            SetupChart();
        }

        private bool _uiStateUpdating = false;
        private void UpdateUIState()
        {
            if (_uiStateUpdating) return;

            _uiStateUpdating = true;
            IsTradeEnabled = _currentTrade.OrderDateTime == null && _currentTrade.EntryDateTime == null;
            IsCloseEnabled = _currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null;
            Running = _currentTrade.OrderDateTime != null || _currentTrade.EntryDateTime != null;

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

            _uiStateUpdating = false;
        }

        private void SetupChart(bool recreateChart = true)
        {
            // Get candles
            if (_allSmallestTimeframeCandles == null)
            {
                _allSmallestTimeframeCandles = _candlesService.GetCandles(_market, Timeframe.M5);
                _allH2Candles = _candlesService.GetCandles(_market, Timeframe.H2);
                //_allH4Candles = _candlesService.GetCandles(_market, Timeframe.H4);
                _allD1Candles = _candlesService.GetCandles(_market, Timeframe.D1);
            }

            var endDateUtc = new DateTime(_allH2Candles[_h2EndDateIndex].CloseTimeTicks, DateTimeKind.Utc);

            var currentH2Candles = _allH2Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();
            //var currentH4Candles = _allH4Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();
            var currentD1Candles = _allD1Candles.Where(x => new DateTime(x.CloseTimeTicks, DateTimeKind.Utc) <= endDateUtc).ToList();

            //Candle? h4Incomplete = null;
            Candle? d1Incomplete = null;

            for (var i = 0; i <= _h2EndDateIndex; i++)
            {
                var h2 = _allH2Candles[i];
                /*if (h2.CloseTimeTicks > currentH4Candles[currentH4Candles.Count - 1].CloseTimeTicks)
                {
                    h4Incomplete = new Candle
                    {
                        Timeframe = (int)Timeframe.H2,
                        Close = h2.Close,
                        Open = h4Incomplete?.Open ?? h2.Open,
                        CloseTimeTicks = h2.CloseTimeTicks,
                        High = h4Incomplete != null && h4Incomplete.Value.High > h2.High ? h4Incomplete.Value.High : h2.High,
                        Low = h4Incomplete != null && h4Incomplete.Value.Low < h2.Low ? h4Incomplete.Value.Low : h2.Low,
                        OpenTimeTicks = h4Incomplete?.OpenTimeTicks ?? h2.OpenTimeTicks,
                        IsComplete = 0,
                        Id = Guid.NewGuid()
                    };
                }*/

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

            //if (h4Incomplete != null) currentH4Candles.Add(h4Incomplete.Value);
            if (d1Incomplete != null) currentD1Candles.Add(d1Incomplete.Value);

            _currentCandles[Timeframe.D1] = currentD1Candles;
            _currentCandles[Timeframe.H2] = currentH2Candles;
            //_currentCandles[Timeframe.H4] = currentH4Candles;

            SetChartCandles(recreateChart);
        }

        protected override void SelectedLargeChartTimeChanged()
        {
            if (SelectedTrade == null)
            {
                SetChartCandles(false);
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
            _currentTrade.Market = _market;
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

            if (_market == null) return;

            var largeChartTimeframe = GetSelectedTimeframe(null);

            if (ChartViewModel.ChartPaneViewModels.Count > 0) ChartViewModel.ChartPaneViewModels[0].ChartSeriesViewModels.Clear();
            if (ChartViewModelSmaller1.ChartPaneViewModels.Count > 0) ChartViewModelSmaller1.ChartPaneViewModels[0].ChartSeriesViewModels.Clear();

            ChartHelper.SetChartViewModelPriceData(_currentCandles[largeChartTimeframe], ChartViewModel, largeChartTimeframe);

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA8", 8), Colors.DarkBlue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA25", 25), Colors.Blue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA50", 50), Colors.LightBlue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
            }
            else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA20", 20), Colors.DarkBlue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new MovingAverage("MA50", 50), Colors.Blue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], _market, new MovingAverage("MA200", 200), Colors.LightBlue, largeChartTimeframe, _currentCandles[largeChartTimeframe]);
            }

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.SetChartViewModelPriceData(_currentCandles[Timeframe.D1], ChartViewModelSmaller1, Timeframe.D1);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA8", 8), Colors.DarkBlue, Timeframe.D1, _currentCandles[Timeframe.D1]);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA25", 25), Colors.Blue, Timeframe.D1, _currentCandles[Timeframe.D1]);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA50", 50), Colors.LightBlue, Timeframe.D1, _currentCandles[Timeframe.D1]);
            }
            else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
            {
                ChartHelper.SetChartViewModelPriceData(_currentCandles[Timeframe.D1], ChartViewModelSmaller1, Timeframe.D1);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new ExponentialMovingAverage("EMA20", 20), Colors.DarkBlue, Timeframe.D1, _currentCandles[Timeframe.D1]);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new MovingAverage("MA50", 50), Colors.Blue, Timeframe.D1, _currentCandles[Timeframe.D1]);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], _market, new MovingAverage("MA200", 200), Colors.LightBlue, Timeframe.D1, _currentCandles[Timeframe.D1]);
            }

            if (recreate)
            {
                ChartHelper.SetChartXVisibleRange(ChartViewModel, _currentCandles[largeChartTimeframe].Count - 95, _currentCandles[largeChartTimeframe].Count + 10);
                ChartHelper.SetChartXVisibleRange(ChartViewModelSmaller1, _currentCandles[Timeframe.D1].Count - 120, _currentCandles[Timeframe.D1].Count + 10);

                var annotations = new AnnotationCollection();
                var annotationsSmallerCharts = new AnnotationCollection();
                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = annotations;
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations = annotationsSmallerCharts;
            }
            else
            {
                var maxIndex = _currentCandles[largeChartTimeframe].Count - 1;
                if (ChartViewModel.XVisibleRange.Max <= maxIndex + 10)
                {
                    var change = (maxIndex - ChartViewModel.XVisibleRange.Max) + 30;
                    ChartViewModel.XVisibleRange.SetMinMax(ChartViewModel.XVisibleRange.Min + change, ChartViewModel.XVisibleRange.Max + change);
                }

                maxIndex = _currentCandles[Timeframe.D1].Count - 1;
                if (ChartViewModelSmaller1.XVisibleRange.Max <= maxIndex + 20)
                {
                    var change = (maxIndex - ChartViewModelSmaller1.XVisibleRange.Max) + 50;
                    ChartViewModelSmaller1.XVisibleRange.SetMinMax(ChartViewModelSmaller1.XVisibleRange.Min + change, ChartViewModelSmaller1.XVisibleRange.Max + change);
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
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}