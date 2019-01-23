using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hallupa.Library;
using TraderTools.Basics;

namespace TraderTools.Core.UI.ViewModels
{
    public class TradeDetailsViewModel : INotifyPropertyChanged
    {
        #region Fields
        private TradeDetails _trade;
        #endregion

        #region Constructors
        public TradeDetailsViewModel(TradeDetails trade)
        {
            DependencyContainer.ComposeParts(this);

            Trade = trade;
            Date = Trade.StartDateTime != null
                ? Trade.StartDateTime.Value.ToString("dd/MM/yy HH:mm")
                : DateTime.UtcNow.ToString("dd/MM/yy HH:mm");

            RefreshDetails();

            AddLimitCommand= new DelegateCommand(AddLimit);
            AddStopCommand = new DelegateCommand(AddStop);
            RemoveLimitCommand = new DelegateCommand(RemoveLimit);
            RemoveStopCommand = new DelegateCommand(RemoveStop);
            SetOrderDateTimePriceCommand = new DelegateCommand(SetOrderDateTimePrice);
        }
        #endregion

        #region Properties

        public TradeDetails Trade
        {
            get => _trade;
            set
            {
                _trade = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DatePrice> LimitPrices { get; } = new ObservableCollection<DatePrice>();
        public ObservableCollection<DatePrice> StopPrices { get; } = new ObservableCollection<DatePrice>();
        public DelegateCommand AddLimitCommand { get; }
        public DelegateCommand AddStopCommand { get; }
        public DelegateCommand RemoveLimitCommand { get; }
        public DelegateCommand RemoveStopCommand { get; }
        public DelegateCommand SetOrderDateTimePriceCommand { get; }
        public int SelectedLimitIndex { get; set; }
        public int SelectedStopIndex { get; set; }
        public string Date { get; set; }
        public string Price { get; set; }
        public bool UsePips { get; set; }
        #endregion

        private void RefreshDetails()
        {
            DependencyContainer.ComposeParts(this);

            LimitPrices.Clear();
            foreach (var limitPrice in Trade.GetLimitPrices())
            {
                LimitPrices.Add(new DatePrice(limitPrice.Date, limitPrice.Price));
            }

            StopPrices.Clear();
            foreach (var stopPrice in Trade.GetStopPrices())
            {
                StopPrices.Add(new DatePrice(stopPrice.Date, stopPrice.Price));
            }
        }

        private void RemoveLimit(object obj)
        {
            if (SelectedLimitIndex == -1)
            {
                return;
            }

            Trade.RemoveLimitPrice(SelectedLimitIndex);
            RefreshDetails();
        }

        private void RemoveStop(object obj)
        {
            if (SelectedStopIndex == -1)
            {
                return;
            }

            Trade.RemoveStopPrice(SelectedStopIndex);
            RefreshDetails();
        }

        private void AddLimit(object obj)
        {
            Trade.AddLimitPrice(GetDatetime(), Trade.TradeDirection == TradeDirection.Long ? GetPrice(PipsChange.Add) : GetPrice(PipsChange.Minus));
            RefreshDetails();
        }

        private void AddStop(object obj)
        {
            Trade.AddStopPrice(GetDatetime(), Trade.TradeDirection == TradeDirection.Long ? GetPrice(PipsChange.Minus) : GetPrice(PipsChange.Add));
            RefreshDetails();
        }

        private void SetOrderDateTimePrice(object obj)
        {
            Trade.OrderPrice = GetPrice();
            Trade.OrderDateTime = GetDatetime();

            // Refresh by changing Trade
            var t = Trade;
            Trade = null;
            Trade = t;
        }

        public enum PipsChange
        {
            Add,
            Minus
        }

        private decimal GetPrice(PipsChange pipsChange = PipsChange.Add)
        {
            if (UsePips)
            {
                var price = Trade.OrderPrice ?? Trade.EntryPrice.Value;
                var priceInPips = PipsHelper.GetPriceInPips(price, Trade.Market);
                priceInPips += pipsChange == PipsChange.Add ? decimal.Parse(Price) : -decimal.Parse(Price);

                return PipsHelper.GetPriceFromPips(priceInPips, Trade.Market);
            }

            return decimal.Parse(Price);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime GetDatetime()
        {
            var initialDate = DateTime.Parse(Date);

            return new DateTime(initialDate.Year, initialDate.Month, initialDate.Day, initialDate.Hour, initialDate.Minute,
                initialDate.Second, DateTimeKind.Utc);
        }
    }
}