using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI;
using TraderTools.Core.UI.Services;

namespace TraderTools.TradingSimulator
{
    public class EditTradeViewModel : DependencyObject, INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private readonly Func<ICandle> _lastCandle;
        private bool _isEntryButtonPressed;
        // ReSharper disable once NotAccessedField.Local
        private IDisposable _chartClickedDisposable;
        // ReSharper disable once NotAccessedField.Local
        private IDisposable _chartMouseMoveDisposable;
        private bool _isSetStopButtonPressed;
        private bool _isSetLimitButtonPressed;
        private bool _isEntryButtonEnabled;
        private readonly Action<Cursor> _setCursorAction;
        private readonly Action _updateChartAction;
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Constructors
        public EditTradeViewModel(
            TradeDetails trade, ChartingService chartingService, Func<ICandle> lastCandle, bool allowCancel, Action<Cursor> setCursorAction, Action updateChartAction, bool showClose)
        {
            _setCursorAction = setCursorAction;
            _updateChartAction = updateChartAction;
            _lastCandle = lastCandle;
            Trade = trade;
            ChartingService = chartingService;
            AllowCancel = allowCancel;
            ShowCloseButton = showClose;
            ShowCancelButton = !showClose;

            ClearEntryOrderCommand = new DelegateCommand(o => ClearEntryOrder());
            ClearStopCommand = new DelegateCommand(o => ClearStop(), o => IsClearStopEnabled);
            ClearLimitCommand = new DelegateCommand(o => ClearLimit(), o => IsClearLimitEnabled);
            CloseCommand = new DelegateCommand(o => CloseTrade());
            OKCommand = new DelegateCommand(o => OK());
            CancelCommand = new DelegateCommand(o => Cancel());
            IsEntryButtonPressed = false;

            _chartClickedDisposable = ChartingService.ChartClickObservable.Subscribe(ChartClicked);
            _chartMouseMoveDisposable = ChartingService.ChartMoveObservable.Subscribe(ChartMouseMove);

            UpdateUI();
        }
        #endregion

        #region Properties
        public Action CloseEditViewAction { get; set; }
        public DelegateCommand ClearEntryOrderCommand { get; }
        public DelegateCommand ClearStopCommand { get; }
        public DelegateCommand ClearLimitCommand { get; }
        public DelegateCommand OKCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public TradeDetails Trade { get; }
        public ChartingService ChartingService { get; }
        public bool CloseClicked { get; set; }

        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set
            {
                _showCloseButton = value;
                OnPropertyChanged();
            }
        }

        public bool ShowCancelButton
        {
            get => _showCancelButton;
            set
            {
                _showCancelButton = value;
                OnPropertyChanged();
            }
        }

        public bool AllowCancel { get; }
        public bool OKClicked { get; set; }
        public DelegateCommand CloseCommand { get; private set; }

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

                UpdateUI();
                OnPropertyChanged();
            }
        }

        #endregion

        #region Dependency Properties
        private bool _isClearEntryOrderEnabled;

        public bool IsClearEntryEnabled
        {
            get => _isClearEntryOrderEnabled;
            set
            {
                _isClearEntryOrderEnabled = value;

                UpdateUI();
                OnPropertyChanged();
            }
        }

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

                UpdateUI();
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

                UpdateUI();
                OnPropertyChanged();
            }
        }


        public bool IsEntryButtonEnabled
        {
            get => _isEntryButtonEnabled;
            set
            {
                _isEntryButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsClearEntryOrderEnabled
        {
            get => _isClearEntryOrderEnabled;
            set
            {
                _isClearEntryOrderEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsClearStopEnabled => Trade.OrderDateTime == null && Trade.StopPrices.Count > 0 && Trade.StopPrices[Trade.StopPrices.Count - 1].Price != null;

        public bool IsClearLimitEnabled => Trade.LimitPrices.Count > 0 && Trade.LimitPrices[Trade.LimitPrices.Count - 1].Price != null;

        #endregion

        private bool _updatingUI = false;
        private int _tradeDirection;
        private bool _showCloseButton;
        private bool _showCancelButton;

        private void UpdateUI()
        {
            if (_updatingUI) return;

            _updatingUI = true;
            IsEntryButtonEnabled = Trade.EntryDateTime == null;
            IsClearEntryEnabled = Trade.EntryDateTime == null && Trade.OrderPrice != null;

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

            ClearStopCommand.RaiseCanExecuteChanged();
            ClearLimitCommand.RaiseCanExecuteChanged();

            _updatingUI = false;
        }

        private void ClearEntryOrder()
        {
            if (Trade.EntryDateTime == null)
            {
                Trade.OrderPrice = null;
            }

            UpdateUI();
            _updateChartAction();
        }

        private void OK()
        {
            if (Trade.StopPrice == null)
            {
                MessageBox.Show("All trades need stops set so the expectancy can be calculated", "Please enter stop", MessageBoxButton.OK);
                return;
            }

            OKClicked = true;
            CloseEditViewAction();
        }

        private void CloseTrade()
        {
            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                CloseEditViewAction();
                return;
            }

            CloseClicked = true;
            CloseEditViewAction();
        }

        private void Cancel()
        {
            OKClicked = false;
            CloseEditViewAction();
        }

        private void SetTradeLimit(double price)
        {
            IsSetLimitButtonPressed = false;
            var lastCandle = _lastCandle();

            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Long && (price < lastCandle.Close || (Trade.OrderPrice != null && Trade.EntryPrice == null && (decimal)price < Trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid limit", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Short && (price > lastCandle.Close || (Trade.OrderPrice != null && Trade.EntryPrice == null && (decimal)price > Trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid limit", "Invalid value", MessageBoxButton.OK);
                return;
            }

            var date = lastCandle.CloseTime();

            if (Trade.EntryDateTime != null)
            {
                foreach (var limit in Trade.LimitPrices.ToList())
                {
                    if (limit.Date == date) Trade.RemoveLimitPrice(Trade.LimitPrices.IndexOf(limit));
                }

                Trade.AddLimitPrice(date, (decimal)price);
            }
            else
            {
                Trade.ClearLimitPrices();
                Trade.AddLimitPrice(date, (decimal)price);
            }

            UpdateUI();
            _updateChartAction();
        }

        private void SetTradeStop(double price)
        {
            IsSetStopButtonPressed = false;

            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            var lastCandle = _lastCandle();
            if (Trade.TradeDirection == TradeDirection.Long && (price > lastCandle.Close || (Trade.OrderPrice != null && Trade.EntryPrice == null && (decimal)price > Trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid stop", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Short && (price < lastCandle.Close || (Trade.OrderPrice != null && Trade.EntryPrice == null && (decimal)price < Trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid stop", "Invalid value", MessageBoxButton.OK);
                return;
            }

            var date = lastCandle.CloseTime();

            if (Trade.EntryDateTime != null)
            {
                foreach (var stop in Trade.StopPrices.ToList())
                {
                    if (stop.Date == date) Trade.RemoveStopPrice(Trade.StopPrices.IndexOf(stop));
                }

                Trade.AddStopPrice(date, (decimal)price);
            }
            else
            {
                Trade.ClearStopPrices();
                Trade.AddStopPrice(date, (decimal)price);
            }

            UpdateUI();
            _updateChartAction();
        }

        private void ClearStop()
        {
            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (Trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot clear stop");
                return;
            }

            if (Trade.StopPrices.Count > 0)
            {
                if (Trade.OrderDateTime == null)
                {
                    Trade.ClearStopPrices();
                }

                UpdateUI();
                _updateChartAction();
            }
        }

        private void ClearLimit()
        {
            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (Trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot clear limit");
                return;
            }

            var lastCandle = _lastCandle();

            if (Trade.LimitPrices.Count > 0)
            {
                if (Trade.OrderDateTime != null)
                {
                    Trade.AddLimitPrice(lastCandle.CloseTime(), null);
                }
                else
                {
                    Trade.ClearLimitPrices();
                }

                UpdateUI();
                _updateChartAction();
            }
        }

        private void SetTradeEntryPrice(double price)
        {
            if (Trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (Trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot set trade order price");
                return;
            }

            IsEntryButtonPressed = false;
            var lastCandle = _lastCandle();

            if (!IsEntryButtonEnabled) return;

            if (Trade.TradeDirection == TradeDirection.Long && price > lastCandle.Close)
            {
                MessageBox.Show("Invalid entry price - price is above current price", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Long && Trade.StopPrice != null && (decimal)price < Trade.StopPrice.Value)
            {
                MessageBox.Show("Invalid entry price - stop would be above entry", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Long && Trade.LimitPrice != null && (decimal)price > Trade.LimitPrice.Value)
            {
                MessageBox.Show("Invalid entry price - limit would be below entry", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Short && price < lastCandle.Close)
            {
                MessageBox.Show("Invalid entry price - price is below current price", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Short && Trade.StopPrice != null && (decimal)price > Trade.StopPrice.Value)
            {
                MessageBox.Show("Invalid entry price - stop would be below entry", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (Trade.TradeDirection == TradeDirection.Short && Trade.LimitPrice != null && (decimal)price < Trade.LimitPrice.Value)
            {
                MessageBox.Show("Invalid entry price - limit would be above entry", "Invalid value", MessageBoxButton.OK);
                return;
            }

            Trade.OrderPrice = (decimal)price;

            UpdateUI();
            _updateChartAction();
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

        private void ChartMouseMove((DateTime Time, double Price, Action SetIsHandled) details)
        {
            if (IsSetLimitButtonPressed && Trade.StopPrices.Count > 0 && Trade.StopPrices[Trade.StopPrices.Count - 1].Price != null)
            {
                var candle = _lastCandle(); ;

                var entry = Trade.OrderPrice != null && Trade.EntryDateTime == null
                    ? Trade.OrderPrice.Value
                    : (decimal)candle.Close;

                var limit = details.Price;
            }

            if (IsSetStopButtonPressed)
            {
                var stop = (decimal)details.Price;
                var candle = _lastCandle();
                var entry = Trade.OrderPrice != null && Trade.EntryDateTime == null
                    ? Trade.OrderPrice.Value
                    : (decimal)candle.Close;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _chartClickedDisposable?.Dispose();
            _chartMouseMoveDisposable?.Dispose();
        }
    }
}