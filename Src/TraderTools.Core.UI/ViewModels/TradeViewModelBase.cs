using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Abt.Controls.SciChart.Visuals.Annotations;
using Hallupa.Library;
using Hallupa.Library.UI;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Core.UI.Services;
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
        protected TradeDetails _selectedTrade = null;

        [Import] public IBrokersCandlesService BrokerCandles { get; private set; }

        private IBroker Broker { get; set; }

        private ObservableCollectionEx<TradeDetails> _trades = new ObservableCollectionEx<TradeDetails>();
        private IBroker _broker;
        private int _selectedMainIndicatorsIndex;

        public string DataDirectory { get; set; }

        protected TradeViewModelBase()
        {
            DependencyContainer.ComposeParts(this);

            TimeFrameItems = new List<Timeframe>
            {
                Timeframe.D1Tiger,
                Timeframe.D1,
                Timeframe.H4,
                Timeframe.H2,
                Timeframe.H1,
                Timeframe.M1,
            };

            EditCommand = new DelegateCommand(EditTrade);
            DeleteCommand = new DelegateCommand(o => DeleteTrade());
        }

        public List<Timeframe> TimeFrameItems { get; set; }

        public int SelectedMainIndicatorsIndex
        {
            get => _selectedMainIndicatorsIndex;
            set => _selectedMainIndicatorsIndex = value;
        }

        public ICommand EditCommand { get; }

        public DelegateCommand DeleteCommand { get; }

        public ObservableCollectionEx<TradeDetails> Trades
        {
            get { return _trades; }
            set
            {
                _trades = value;
                OnPropertyChanged();
            }
        }

        protected override void SelectedLargeChartTimeChanged()
        {
            SelectedTradeUpdated();
        }

        [Import] public ChartingService ChartingService { get; private set; }

        public TradeDetails SelectedTrade
        {
            get { return _selectedTrade; }
            set
            {
                _selectedTrade = value;
                SelectedTradeUpdated();
            }
        }

        protected virtual void DeleteTrade()
        {
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

        protected virtual void SelectedTradeUpdated()
        {
            ChartViewModel.ChartPaneViewModels.Clear();
            ChartViewModelSmaller1.ChartPaneViewModels.Clear();

            if (SelectedTrade == null)
            {
                return;
            }

            var selectedTrade = SelectedTrade;

            // Setup main chart
            var timeframe = GetSelectedTimeframe(selectedTrade);

            DateTime? start = null, end = null;

            if (timeframe == Timeframe.M1)
            {
                start = selectedTrade.StartDateTime.Value.AddMinutes(-20);
                end = selectedTrade.CloseDateTime != null
                    ? selectedTrade.CloseDateTime.Value.AddMinutes(20)
                    : selectedTrade.StartDateTime.Value.AddMinutes(240);
            }

            var candles = BrokerCandles.GetCandles(Broker, selectedTrade.Market, timeframe, false, cacheData: false,
                minOpenTimeUtc: start, maxCloseTimeUtc: end);

            ChartHelper.SetChartViewModelPriceData(candles, ChartViewModel, timeframe);

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(8), Colors.DarkBlue, timeframe, candles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(25), Colors.Blue, timeframe, candles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(50), Colors.LightBlue, timeframe, candles);
            }
            else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(8), Colors.DarkBlue, timeframe, candles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new SimpleMovingAverage(50), Colors.Blue, timeframe, candles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], selectedTrade.Market, new SimpleMovingAverage(200), Colors.LightBlue, timeframe, candles);
            }

            var macdPane = new ChartPaneViewModel(ChartViewModel, ChartViewModel.ViewportManager)
            {
                IsFirstChartPane = false,
                IsLastChartPane = true,
                Height = 140
            };
            ChartViewModel.ChartPaneViewModels.Add(macdPane);
            ChartHelper.AddIndicator(macdPane, selectedTrade.Market, new MovingAverageConvergenceDivergence(), Colors.Red, timeframe, candles);
            ChartHelper.AddIndicator(macdPane, selectedTrade.Market, new MovingAverageConvergenceDivergenceSignal(), Colors.Blue, timeframe, candles);
            ChartHelper.SetChartViewModelVisibleRange(selectedTrade, ChartViewModel, candles, timeframe);

            var dayCandles = BrokerCandles.GetCandles(Broker, selectedTrade.Market, Timeframe.D1, false);

            ChartHelper.SetChartViewModelPriceData(dayCandles, ChartViewModelSmaller1, Timeframe.D1);

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(8), Colors.DarkBlue, Timeframe.D1, dayCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(25), Colors.Blue, Timeframe.D1, dayCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(50), Colors.LightBlue, Timeframe.D1, dayCandles);
            }
            else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
            {
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new ExponentialMovingAverage(20), Colors.DarkBlue, Timeframe.D1, dayCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new SimpleMovingAverage(50), Colors.Blue, Timeframe.D1, dayCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], selectedTrade.Market, new SimpleMovingAverage(200), Colors.LightBlue, Timeframe.D1, dayCandles);
            }

            ChartHelper.SetChartViewModelVisibleRange(selectedTrade, ChartViewModelSmaller1, dayCandles, Timeframe.D1);


            ChartViewModel.ChartPaneViewModels[0].TradeAnnotations = ChartHelper.CreateTradeAnnotations(ChartViewModel, false, timeframe, candles, selectedTrade);

            AddCustomAnnotations(selectedTrade);
        }

        private void AddCustomAnnotations(TradeDetails trade)
        {
            var path = GetTradeAnnotationsPath(trade);

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
                        Stroke = Brushes.Yellow,
                        X1 = x1,
                        X2 = x2,
                        Y1 = double.Parse(annotationDetail.Y1),
                        Y2 = double.Parse(annotationDetail.Y2)
                    };

                    ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Add(annotation);
                }
            }
        }

        protected string GetTradeAnnotationsPath(TradeDetails trade)
        {
            var annotationsDirectory = Path.Combine(DataDirectory, "TradeAnnotations");
            return Path.Combine(annotationsDirectory, trade.UniqueId + ".json");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}