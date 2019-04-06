using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using Hallupa.Library.UI;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI.Services;
using TraderTools.Core.UI.Views;
using TraderTools.Indicators;
using TraderTools.UI.Views;

namespace TraderTools.Core.UI.ViewModels
{
    public class AnnotationDetails
    {
        public string Y1 { get; set; }
        public string Y2 { get; set; }
        public string X1 { get; set; }
        public string X2 { get; set; }
    }

    public enum MainIndicators
    {
        EMA8_EMA25_EMA50,
        EMA20_MA50_MA200
    }

    public abstract class TradeViewModelBase : DoubleChartViewModel, INotifyPropertyChanged
    {
        [Import] public IBrokersCandlesService BrokerCandles { get; private set; }

        protected IBroker Broker { get; set; }

        private int _selectedMainIndicatorsIndex;
        private Dispatcher _dispatcher;

        public string DataDirectory { get; set; }

        protected TradeViewModelBase()
        {
            DependencyContainer.ComposeParts(this);

            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1Tiger,
                Timeframe.D1,
                Timeframe.H8,
                Timeframe.H4,
                Timeframe.H2,
                Timeframe.H1,
                Timeframe.M1,
            };

            EditCommand = new DelegateCommand(EditTrade);
            DeleteCommand = new DelegateCommand(o => DeleteTrade());
            ViewTradeCommand = new DelegateCommand(t => ViewTrade((TradeDetails)t));
            ViewTradeSetupCommand = new DelegateCommand(t => ViewTradeSetup((TradeDetails)t));

            TradesView = (CollectionView)CollectionViewSource.GetDefaultView(Trades);
            TradesView.Filter = TradesViewFilter;

            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public CollectionView TradesView { get; private set; }

        public List<Timeframe> TimeFrameItems { get; set; }

        public TradeListDisplayOptionsFlag TradeListDisplayOptions { get; set; } =
            TradeListDisplayOptionsFlag.PoundsPerPip | TradeListDisplayOptionsFlag.InitialStop
                                                     | TradeListDisplayOptionsFlag.InitialLimit
                                                     | TradeListDisplayOptionsFlag.OrderPrice
                                                     | TradeListDisplayOptionsFlag.OrderDate
                                                     | TradeListDisplayOptionsFlag.Comments
                                                     | TradeListDisplayOptionsFlag.ResultR
                                                     | TradeListDisplayOptionsFlag.Broker
                                                     | TradeListDisplayOptionsFlag.ViewTrade;

        protected TradeDetails TradeShowingOnChart { get; private set; }
        public DelegateCommand ViewTradeCommand { get; private set; }
        public DelegateCommand ViewTradeSetupCommand { get; private set; }

        public int SelectedMainIndicatorsIndex
        {
            get => _selectedMainIndicatorsIndex;
            set => _selectedMainIndicatorsIndex = value;
        }

        public ICommand EditCommand { get; }

        public DelegateCommand DeleteCommand { get; }

        public ObservableCollectionEx<TradeDetails> Trades { get; } = new ObservableCollectionEx<TradeDetails>();

        protected override void SelectedLargeChartTimeChanged()
        {
            //SelectedTradeUpdated();
        }

        private bool TradesViewFilter(object obj)
        {
            var t = (TradeDetails)obj;

            if (!ShowOpenTradesOnly)
            {
                return true;
            }

            return t.EntryPrice != null && t.CloseDateTime == null;
        }

        [Import] public ChartingService ChartingService { get; private set; }

        public TradeDetails SelectedTrade { get; set; }

        public static readonly DependencyProperty IsViewTradeEnabledProperty = DependencyProperty.Register(
            "IsViewTradeEnabled", typeof(bool), typeof(TradeViewModelBase), new PropertyMetadata(true));

        public bool IsViewTradeEnabled
        {
            get { return (bool)GetValue(IsViewTradeEnabledProperty); }
            set { SetValue(IsViewTradeEnabledProperty, value); }
        }

        public static readonly DependencyProperty ShowOpenTradesOnlyProperty = DependencyProperty.Register(
            "ShowOpenTradesOnly", typeof(bool), typeof(TradeViewModelBase), new PropertyMetadata(default(bool), ShowOpenTradesOnlyChanged));

        private static void ShowOpenTradesOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tvm = (TradeViewModelBase)d;
            tvm.TradesView.Refresh();
        }

        public bool ShowOpenTradesOnly
        {
            get { return (bool)GetValue(ShowOpenTradesOnlyProperty); }
            set { SetValue(ShowOpenTradesOnlyProperty, value); }
        }

        protected virtual void DeleteTrade()
        {
        }


        public void ViewTrade(TradeDetails tradeDetails)
        {
            if (!IsViewTradeEnabled) return;
            IsViewTradeEnabled = false;

            Task.Run(() =>
            {
                ShowTrade(tradeDetails, true);

                _dispatcher.Invoke(() => { IsViewTradeEnabled = true; });
            });
        }

        public void ViewTradeSetup(TradeDetails tradeDetails)
        {
            if (!IsViewTradeEnabled) return;
            IsViewTradeEnabled = false;

            Task.Run(() =>
            {
                ShowTradeSetup(tradeDetails, true);

                _dispatcher.Invoke(() => { IsViewTradeEnabled = true; });
            });
        }

        private void EditTrade(object obj)
        {
            if (SelectedTrade == null)
            {
                return;
            }

            var view = new TradeDetailsView();
            var viewModel = new TradeDetailsViewModel(SelectedTrade);
            view.DataContext = viewModel;
            view.Closing += ViewOnClosing;
            view.Show();
        }

        protected virtual void ViewOnClosing(object sender, CancelEventArgs e)
        {
        }

        protected void ShowTrade(TradeDetails trade, Timeframe smallChartTimeframe, List<ICandle> smallChartCandles, Timeframe largeChartTimeframe, List<ICandle> largeChartCandles)
        {
            _dispatcher.BeginInvoke((Action)(() =>
            {
                ChartViewModel.ChartPaneViewModels.Clear();
                ChartViewModelSmaller1.ChartPaneViewModels.Clear();
                TradeShowingOnChart = trade;

                if (trade == null)
                {
                    return;
                }

                ChartHelper.SetChartViewModelPriceData(largeChartCandles, ChartViewModel, largeChartTimeframe);

                if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
                {
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(8), Colors.DarkBlue, largeChartTimeframe, largeChartCandles);
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(25), Colors.Blue, largeChartTimeframe, largeChartCandles);
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(50), Colors.LightBlue, largeChartTimeframe, largeChartCandles);
                }
                else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
                {
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(8), Colors.DarkBlue, largeChartTimeframe, largeChartCandles);
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new SimpleMovingAverage(50), Colors.Blue, largeChartTimeframe, largeChartCandles);
                    ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], trade.Market,
                        new SimpleMovingAverage(200), Colors.LightBlue, largeChartTimeframe, largeChartCandles);
                }

                if (largeChartTimeframe != Timeframe.D1Tiger)
                {
                    var macdPane = new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
                    {
                        IsFirstChartPane = false,
                        IsLastChartPane = true,
                        Height = 140
                    };
                    ChartViewModel.ChartPaneViewModels.Add(macdPane);
                    ChartHelper.AddIndicator(macdPane, trade.Market, new MovingAverageConvergenceDivergence(), Colors.Red, largeChartTimeframe, largeChartCandles);
                    ChartHelper.AddIndicator(macdPane, trade.Market, new MovingAverageConvergenceDivergenceSignal(), Colors.Blue, largeChartTimeframe, largeChartCandles);
                }

                ChartHelper.SetChartViewModelVisibleRange(trade, ChartViewModel, largeChartCandles,
                    largeChartTimeframe);


                ChartHelper.SetChartViewModelPriceData(smallChartCandles, ChartViewModelSmaller1, smallChartTimeframe);

                if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
                {
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(8), Colors.DarkBlue, smallChartTimeframe, smallChartCandles);
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(25), Colors.Blue, smallChartTimeframe, smallChartCandles);
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(50), Colors.LightBlue, smallChartTimeframe, smallChartCandles);
                }
                else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
                {
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new ExponentialMovingAverage(20), Colors.DarkBlue, smallChartTimeframe, smallChartCandles);
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new SimpleMovingAverage(50), Colors.Blue, smallChartTimeframe, smallChartCandles);
                    ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], trade.Market,
                        new SimpleMovingAverage(200), Colors.LightBlue, smallChartTimeframe, smallChartCandles);
                }

                ChartHelper.SetChartViewModelVisibleRange(trade, ChartViewModelSmaller1, smallChartCandles,
                    smallChartTimeframe);


                ChartViewModel.ChartPaneViewModels[0].TradeAnnotations =
                    ChartHelper.CreateTradeAnnotations(ChartViewModel, TradeAnnotationsToShow.All, largeChartTimeframe, largeChartCandles,
                        trade);
                ChartViewModelSmaller1.ChartPaneViewModels[0].TradeAnnotations =
                    ChartHelper.CreateTradeAnnotations(ChartViewModelSmaller1, TradeAnnotationsToShow.All, smallChartTimeframe,
                        smallChartCandles, trade);

                AddCustomAnnotations(trade, ChartViewModel);
                AddCustomAnnotations(trade, ChartViewModelSmaller1);
            }));
        }

        protected void ShowTrade(TradeDetails trade, bool updateCandles = false)
        {
            // Setup main chart
            var largeChartTimeframe = GetSelectedTimeframe(trade);
            var smallChartTimeframe = Timeframe.D1;

            DateTime? start = null, end = null;

            if (largeChartTimeframe == Timeframe.M1)
            {
                start = trade.StartDateTime.Value.AddMinutes(-20);
                end = trade.CloseDateTime != null
                    ? trade.CloseDateTime.Value.AddMinutes(20)
                    : trade.StartDateTime.Value.AddMinutes(240);
            }
            else
            {
                end = trade.CloseDateTime?.AddDays(5);
            }

            var largeChartCandles = largeChartTimeframe != Timeframe.D1Tiger
                ? BrokerCandles.GetCandles(Broker, trade.Market, largeChartTimeframe, updateCandles, cacheData: false, minOpenTimeUtc: start, maxCloseTimeUtc: end)
                : BrokerCandles.GetCandlesUptoSpecificTime(Broker, trade.Market, Timeframe.D1Tiger, updateCandles, null, trade.CloseDateTime);
            var smallChartCandles = BrokerCandles.GetCandles(Broker, trade.Market, smallChartTimeframe, updateCandles, maxCloseTimeUtc: trade.CloseDateTime?.AddDays(30));

            ShowTrade(trade, smallChartTimeframe, smallChartCandles, largeChartTimeframe, largeChartCandles);
        }

        protected void ShowTradeSetup(TradeDetails trade, bool updateCandles = false)
        {
            if (trade.StartDateTime == null) return;

            // Setup main chart
            var largeChartTimeframe = trade.Timeframe ?? Timeframe.H2;
            var smallChartTimeframe = Timeframe.D1;

            DateTime? start = null;
            if (largeChartTimeframe == Timeframe.M1)
            {
                start = trade.StartDateTime.Value.AddMinutes(-20);
            }

            var tradeStartTime = trade.StartDateTime.Value;
            var smallChartCandles = BrokerCandles.GetCandles(Broker, trade.Market, smallChartTimeframe != Timeframe.D1Tiger ? smallChartTimeframe : Timeframe.D1, updateCandles, maxCloseTimeUtc: trade.CloseDateTime?.AddDays(5));
            var largeChartCandles = BrokerCandles.GetCandlesUptoSpecificTime(Broker, trade.Market, largeChartTimeframe, updateCandles, start, trade.StartDateTime.Value);

            ShowTrade(trade, smallChartTimeframe, smallChartCandles, largeChartTimeframe, largeChartCandles);
        }

        private void AddCustomAnnotations(TradeDetails trade, ChartViewModel cvm)
        {
            var path = GetTradeAnnotationsPath(trade, cvm);

            if (File.Exists(path))
            {
                var annotationDetails = JsonConvert.DeserializeObject<List<AnnotationDetails>>(File.ReadAllText(path));

                foreach (var annotationDetail in annotationDetails)
                {
                    IComparable x1;
                    if (int.TryParse(annotationDetail.X1, out var x1Int))
                    {
                        x1 = x1Int;
                    }
                    else
                    {
                        x1 = DateTime.Parse(annotationDetail.X1);
                    }

                    IComparable x2;
                    if (int.TryParse(annotationDetail.X2, out var x2Int))
                    {
                        x2 = x2Int;
                    }
                    else
                    {
                        x2 = DateTime.Parse(annotationDetail.X2);
                    }

                    var annotation = new LineAnnotation
                    {
                        Tag = "Added",
                        IsEditable = true,
                        StrokeThickness = 3,
                        Opacity = 0.7,
                        Stroke = Brushes.Black,
                        X1 = x1,
                        X2 = x2,
                        Y1 = double.Parse(annotationDetail.Y1),
                        Y2 = double.Parse(annotationDetail.Y2)
                    };

                    cvm.ChartPaneViewModels[0].TradeAnnotations.Add(annotation);
                }
            }
        }

        protected string GetTradeAnnotationsPath(TradeDetails trade, ChartViewModel cvm)
        {
            var chartName = cvm == ChartViewModel ? "Main" : "Secondary";
            var annotationsDirectory = Path.Combine(DataDirectory, "TradeAnnotations");
            return Path.Combine(annotationsDirectory, $"{trade.UniqueId}_{chartName}.json");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}