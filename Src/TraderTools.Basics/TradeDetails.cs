using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TraderTools.Basics.Extensions;

namespace TraderTools.Basics
{
    public enum TradeCloseReason
    {
        HitStop,
        HitLimit,
        HitExpiry,
        ManualClose,
        OrderClosed
    }

    public enum OrderKind
    {
        Market,
        EntryPrice
    }

    //[ZeroFormattable]
    public class DatePrice
    {
        public DatePrice(DateTime date, decimal? price)
        {
            Date = date;
            Price = price;
        }

        public DatePrice()
        {
        }

      //  // [Index(0)]
        public virtual DateTime Date { get; set; }
      //  // [Index(1)]
        public virtual decimal? Price { get; set; }
    }

   // [ZeroFormattable]
    public class TradeDetails
    {
        public TradeDetails()
        {
        }

        public TradeDetails(decimal entryOrder, DateTime entryOrderTime,
            TradeDirection direction, decimal amount, string market, DateTime? orderExpireTime,
            decimal? stop, decimal? limit, Timeframe timeframe, string comments, int custom1,
            int custom2, int custom3, int custom4, bool alert)
        {
            SetOrder(entryOrderTime, entryOrder, market, timeframe, direction, amount, orderExpireTime);
            if (stop != null) SetStop(stop.Value, entryOrderTime);
            if (limit != null) SetLimit(limit.Value, entryOrderTime);
            Timeframe = timeframe;
            Alert = alert;
            Comments = comments;
            Custom1 = custom1;
            Custom2 = custom2;
            Custom3 = custom3;
            Custom4 = custom4;
        }

     //   // [Index(0)]
        public virtual string Comments { get; set; }
     //   // [Index(1)]
        public virtual DateTime? EntryDateTime { get; set; }
        // [Index(2)]
        public virtual Guid UniqueId { get; set; } = Guid.NewGuid();
        // [Index(3)]
        public virtual string Id { get; set; }
        // [Index(4)]
        public virtual string Broker { get; set; }
        // [Index(5)]
        public virtual decimal? Commission { get; set; }
        // [Index(6)]
        public virtual string CommissionAsset { get; set; }
        // [Index(7)]
        public virtual string OrderId { get; set; }
        // [Index(8)]
        public virtual OrderKind OrderKind { get; set; }
        // [Index(9)]
        public virtual decimal? EntryPrice { get; set; }
        // [Index(10)]
        public virtual decimal? ClosePrice { get; set; }
        // [Index(11)]
        public virtual decimal? EntryQuantity { get; set; }
        // [Index(12)]
        public virtual decimal? GrossProfitLoss { get; set; }
        // [Index(13)]
        public virtual decimal? NetProfitLoss { get; set; }
        // [Index(14)]
        public virtual decimal? Rollover { get; set; }
        // [Index(15)]
        public virtual decimal? PricePerPip { get; set; }
        // [Index(16)]
        public virtual string Market { get; set; }
        // [Index(17)]
        public virtual string BaseAsset { get; set; }
        // [Index(18)]
        public virtual bool Alert { get; set; }
        // [Index(19)]
        public virtual int Custom1 { get; set; }
        // [Index(20)]
        public virtual int Custom2 { get; set; }
        // [Index(21)]
        public virtual int Custom3 { get; set; }
        // [Index(22)]
        public virtual int Custom4 { get; set; }
        // [Index(23)]
        public virtual int Custom5 { get; set; }
        // [Index(24)]
        public virtual Timeframe? Timeframe { get; set; }
        // [Index(25)]
        public virtual TradeDirection? TradeDirection { get; set; }
        // [Index(26)]
        public virtual DateTime? CloseDateTime { get; set; }

        // [Index(27)]
        public virtual TradeCloseReason? CloseReason { get; set; }

        // [Index(28)]
        public virtual DateTime? OrderDateTime { get; set; }
        // [Index(29)]
        public virtual decimal? OrderPrice { get; set; }
        // [Index(30)]
        public virtual DateTime? OrderExpireTime { get; set; }
        // [Index(31)]
        public virtual decimal? OrderAmount { get; set; }
        // [Index(32)]
        public virtual List<DatePrice> StopPrices { get; set; } = new List<DatePrice>();
        // [Index(33)]
        public virtual List<DatePrice> LimitPrices { get; set; } = new List<DatePrice>();
        // [Index(34)]
        public virtual decimal? RiskAmount { get; set; }
        // [Index(35)]
        public virtual decimal? RiskPercentOfBalance { get; set; }
        // [IgnoreFormat]
        public DateTime? EntryDateTimeLocal => EntryDateTime != null ? (DateTime?)EntryDateTime.Value.ToLocalTime() : null;
        // [IgnoreFormat]
        public DateTime? StartDateTimeLocal => OrderDateTime != null ? (DateTime?)OrderDateTime.Value.ToLocalTime() : EntryDateTimeLocal;
        // [IgnoreFormat]
        public DateTime? StartDateTime => OrderDateTime != null ? (DateTime?)OrderDateTime.Value : EntryDateTime;

        // [IgnoreFormat]
        public decimal? InitialStopPrice
        {
            get
            {
                if (StopPrices.Count == 0)
                {
                    return null;
                }

                return StopPrices[0].Price;
            }
        }

        // [IgnoreFormat]
        public decimal? InitialLimitPrice
        {
            get
            {
                if (LimitPrices.Count == 0)
                {
                    return null;
                }

                return LimitPrices[0].Price;
            }
        }

        // [IgnoreFormat]
        public decimal? StopPrice
        {
            get
            {
                if (StopPrices.Count == 0)
                {
                    return null;
                }

                if (_currentStopPrice == null)
                {
                    _currentStopPrice = StopPrices[StopPrices.Count - 1].Price;
                }

                return _currentStopPrice;
            }
        }

        // [IgnoreFormat]
        public decimal? LimitPrice
        {
            get
            {
                if (LimitPrices.Count == 0)
                {
                    return null;
                }

                return LimitPrices[LimitPrices.Count - 1].Price;
            }
        }

        // [IgnoreFormat]
        public DateTime? CloseDateTimeLocal
        {
            get { return CloseDateTime != null ? (DateTime?)CloseDateTime.Value.ToLocalTime() : null; }
        }

        // [IgnoreFormat]
        public DateTime? OrderDateTimeLocal
        {
            get { return OrderDateTime != null ? (DateTime?)OrderDateTime.Value.ToLocalTime() : null; }
        }


        // [IgnoreFormat]
        public DateTime? OrderExpireTimeLocal
        {
            get { return OrderExpireTime != null ? (DateTime?)OrderExpireTime.Value.ToLocalTime() : null; }
        }


        // [IgnoreFormat]
        public decimal? RMultiple
        {
            get
            {
                if (ClosePrice == null || StopPrices.Count == 0 || EntryPrice == null)
                {
                    return null;
                }

                var risk = Math.Abs(StopPrices[0].Price.Value - EntryPrice.Value);
                if (TradeDirection == Basics.TradeDirection.Long)
                {
                    var gainOrLoss = Math.Abs(ClosePrice.Value - EntryPrice.Value);
                    return (gainOrLoss / risk) * (ClosePrice.Value > EntryPrice.Value ? 1 : -1);
                }
                else
                {
                    var gainOrLoss = Math.Abs(ClosePrice.Value - EntryPrice.Value);
                    return (gainOrLoss / risk) * (ClosePrice.Value > EntryPrice.Value ? -1 : 1);
                }
            }
        }


        public void SetOrder(DateTime dateTime, decimal? price, string market,
            Timeframe timeframe, TradeDirection tradeDirection, decimal orderAmount, DateTime? expires)
        {
            OrderDateTime = dateTime;
            OrderPrice = price;
            Timeframe = timeframe;
            Market = market;
            TradeDirection = tradeDirection;
            OrderExpireTime = expires;
            OrderAmount = orderAmount;
        }

        public void SetEntry(DateTime dateTime, decimal price)
        {
            EntryDateTime = dateTime;
            EntryPrice = price;

            if (OrderDateTime == null)
            {
                OrderDateTime = EntryDateTime;
            }
        }

        private decimal? _currentStopPrice = null;

        public void AddStopPrice(DateTime date, decimal? price)
        {
            StopPrices.Add(new DatePrice(date, price));
            StopPrices = StopPrices.OrderBy(x => x.Date).ToList();
            _currentStopPrice = null;
        }

        public void ClearStopPrices()
        {
            StopPrices.Clear();
            _currentStopPrice = null;
        }

        public List<DatePrice> GetStopPrices()
        {
            return StopPrices.ToList();
        }

        public void RemoveStopPrice(int index)
        {
            if (index >= StopPrices.Count)
            {
                return;
            }

            StopPrices.RemoveAt(index);
            _currentStopPrice = null;
        }

        public void AddLimitPrice(DateTime date, decimal? price)
        {
            LimitPrices.Add(new DatePrice(date, price));
            LimitPrices = LimitPrices.OrderBy(x => x.Date).ToList();
        }

        public void ClearLimitPrices()
        {
            LimitPrices.Clear();
        }

        public List<DatePrice> GetLimitPrices()
        {
            return LimitPrices.ToList();
        }

        public void RemoveLimitPrice(int index)
        {
            if (index >= LimitPrices.Count)
            {
                return;
            }

            LimitPrices.RemoveAt(index);
        }

        public void SetClose(DateTime dateTime, decimal price, TradeCloseReason reason)
        {
            if (OrderDateTime != null && OrderDateTime.Value.Month == 9 &&
                OrderDateTime.Value.Day == 17 &&
                OrderDateTime.Value.Year == 2015 && Timeframe == Basics.Timeframe.D1)
            {
            }

            ClosePrice = price;
            CloseDateTime = dateTime;
            CloseReason = reason;
        }

        public void SetStop(decimal price, DateTime dateTime)
        {
            AddStopPrice(dateTime, price);
        }

        public void SetLimit(decimal price, DateTime dateTime)
        {
            AddLimitPrice(dateTime, price);
        }

        public void SetExpired(DateTime dateTime)
        {
            CloseDateTime = dateTime;
            CloseReason = TradeCloseReason.HitExpiry;
        }

        // [IgnoreFormat]
        public decimal? InitialStopInPips
        {
            get
            {
                if (EntryPrice == null && OrderPrice == null)
                {
                    return null;
                }

                var usedPrice = EntryPrice != null ? EntryPrice.Value : OrderPrice.Value;
                if (StopPrices.Count > 0)
                {
                    var stop = GetStopPrices().First();
                    var stopInPips = Math.Abs(PipsHelper.GetPriceInPips(stop.Price.Value, Market) -
                                              PipsHelper.GetPriceInPips(usedPrice, Market));
                    return stopInPips;
                }

                return null;
            }
        }

        // [IgnoreFormat]
        public decimal? InitialStop
        {
            get
            {
                if (EntryPrice == null && OrderPrice == null)
                {
                    return null;
                }

                if (StopPrices.Count > 0)
                {
                    var stop = GetStopPrices().First();
                    return stop.Price != null ? (decimal?)stop.Price.Value : null;
                }

                return null;
            }
        }

        // [IgnoreFormat]
        public decimal? InitialLimitInPips
        {
            get
            {
                if (EntryPrice == null && OrderPrice == null)
                {
                    return null;
                }

                var usedPrice = EntryPrice != null ? EntryPrice.Value : OrderPrice.Value;
                if (LimitPrices.Count > 0)
                {
                    var limit = GetLimitPrices().First();
                    var limitInPips = Math.Abs(PipsHelper.GetPriceInPips(limit.Price.Value, Market) -
                                              PipsHelper.GetPriceInPips(usedPrice, Market));
                    return limitInPips;
                }

                return null;
            }
        }

        // [IgnoreFormat]
        public decimal? InitialLimit
        {
            get
            {
                if (EntryPrice == null && OrderPrice == null)
                {
                    return null;
                }

                if (LimitPrices.Count > 0)
                {
                    var limit = GetLimitPrices().First();
                    return limit.Price != null ? (decimal?)limit.Price.Value : null;
                }

                return null;
            }
        }

        public void UpdateRisk(IBrokerAccount brokerAccount)
        {
            if (InitialStopInPips == null || PricePerPip == null)
            {
                RiskPercentOfBalance = null;
                RiskAmount = null;
                return;
            }

            RiskAmount = PricePerPip.Value * InitialStopInPips.Value;

            if (StartDateTime == null)
            {
                RiskPercentOfBalance = null;
                return;
            }

            RiskPercentOfBalance = (RiskAmount * 100M) / brokerAccount.GetBalance(StartDateTime);
        }

        public void UpdatePricePerPip(IBroker broker, IBrokersCandlesService candleService, bool updateCandles)
        {
            if (broker.Kind == BrokerKind.Trade ||
                (PricePerPip != null && (EntryQuantity == 0
                                         || EntryQuantity == null) && (OrderAmount == 0 || OrderAmount == null)))
            {
                return;
            }

            var amount = OrderAmount != null && OrderAmount.Value != 0 ? OrderAmount.Value : EntryQuantity.Value;
            var date = OrderDateTime != null ? OrderDateTime.Value : EntryDateTime.Value;

            PricePerPip = PipsHelper.ConvertLotSizeToGBPPerPip(amount, Market, date, candleService, broker, updateCandles);
        }

        public override string ToString()
        {
            var ret = new StringBuilder();

            if (OrderDateTime != null)
            {
                ret.Append($"Order: {OrderDateTime.Value}UTC {OrderAmount:0.00}@{OrderPrice:0.0000}");
            }

            if (EntryDateTime != null)
            {
                if (ret.Length > 0)
                {
                    ret.Append(" ");
                }

                ret.Append($"Entry: {EntryDateTime.Value}UTC Price: {EntryPrice:0.0000}");
            }

            if (CloseDateTime != null)
            {
                if (ret.Length > 0)
                {
                    ret.Append(" ");
                }

                ret.Append($"Close: {CloseDateTime.Value}UTC Price: {ClosePrice:0.0000} Reason: {CloseReason}");
            }

            var initialStopInPips = InitialStopInPips;
            if (initialStopInPips != null)
            {
                if (ret.Length > 0)
                {
                    ret.Append(" ");
                }

                var stop = GetStopPrices().First();
                ret.Append("Initial stop price: ");
                ret.Append($"{stop.Date}UTC {stop.Price:0.0000} ({initialStopInPips:0}pips)");
            }

            if (Timeframe != null)
            {
                if (ret.Length > 0)
                {
                    ret.Append(" ");
                }

                ret.Append($"Timeframe: {Timeframe}");
            }

            return ret.ToString();
        }

        // [IgnoreFormat]
        public string Status
        {
            get
            {
                if (OrderPrice != null && EntryPrice == null && CloseDateTime == null)
                {
                    return "Order";
                }

                if (EntryPrice != null && CloseDateTime == null)
                {
                    return "Open";
                }

                if (CloseDateTime != null)
                {
                    switch (CloseReason)
                    {
                        case TradeCloseReason.HitExpiry:
                            return "Hit expiry";
                        case TradeCloseReason.HitLimit:
                            return "Hit limit";
                        case TradeCloseReason.HitStop:
                            return "Hit stop";
                        case TradeCloseReason.OrderClosed:
                        case TradeCloseReason.ManualClose:
                            return "Closed";
                    }
                }

                return "Unknown";
            }
        }

        // [IgnoreFormat]
        public decimal? StartPrice => OrderPrice ?? EntryPrice;

        // [IgnoreFormat]
        public decimal? Profit => NetProfitLoss ?? GrossProfitLoss;

        public void Initialise()
        {
            if (EntryDateTime != null && OrderDateTime == null)
            {
                OrderDateTime = EntryDateTime;
            }
        }
    }
}