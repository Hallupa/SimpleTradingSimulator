using System.Linq;
using System.Windows;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.TradingSimulator.Extensions
{
    public static class TradeExtensions
    {
        public static void SetTradeLimit(this Trade trade, Candle lastCandle, double price)
        {
            if (trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (trade.TradeDirection == TradeDirection.Long && ((trade.OrderPrice == null && price < lastCandle.CloseBid) || (trade.OrderPrice != null && trade.EntryPrice == null && (decimal)price < trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid limit", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (trade.TradeDirection == TradeDirection.Short && ((trade.OrderPrice == null && price > lastCandle.CloseAsk) || (trade.OrderPrice != null && trade.EntryPrice == null && (decimal)price > trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid limit", "Invalid value", MessageBoxButton.OK);
                return;
            }

            var date = lastCandle.CloseTime();

            if (trade.EntryDateTime != null)
            {
                foreach (var limit in trade.LimitPrices.ToList())
                {
                    if (limit.Date == date) trade.RemoveLimitPrice(trade.LimitPrices.IndexOf(limit));
                }

                trade.AddLimitPrice(date, (decimal)price);
            }
            else
            {
                trade.ClearLimitPrices();
                trade.AddLimitPrice(date, (decimal)price);
            }
        }

        public static void SetTradeStop(this Trade trade, Candle lastCandle, double price)
        {
            if (trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (trade.TradeDirection == TradeDirection.Long && ((trade.OrderPrice == null && price > lastCandle.CloseBid) || (trade.OrderPrice != null && trade.EntryPrice == null && (decimal)price > trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid stop", "Invalid value", MessageBoxButton.OK);
                return;
            }

            if (trade.TradeDirection == TradeDirection.Short && ((trade.OrderPrice == null && price < lastCandle.CloseAsk) || (trade.OrderPrice != null && trade.EntryPrice == null && (decimal)price < trade.OrderPrice.Value)))
            {
                MessageBox.Show("Invalid stop", "Invalid value", MessageBoxButton.OK);
                return;
            }

            var date = lastCandle.CloseTime();

            trade.AddStopPrice(date, (decimal)price);
        }

        public static void ClearStop(this Trade trade, Candle lastCandle)
        {
            if (trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot clear stop");
                return;
            }

            if (trade.StopPrices.Count > 0)
            {
                var date = lastCandle.CloseTime();
                trade.AddStopPrice(date, null);
            }
        }

        public static void ClearLimit(this Trade trade, Candle lastCandle)
        {
            if (trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot clear limit");
                return;
            }

            if (trade.LimitPrices.Count > 0)
            {
                trade.AddLimitPrice(lastCandle.CloseTime(), null);
            }
        }

        public static void SetTradeEntryPrice(this Trade trade, Candle lastCandle, double price)
        {
            if (trade.CloseDateTime != null)
            {
                MessageBox.Show("Trade is now closed");
                return;
            }

            if (trade.EntryPrice != null)
            {
                MessageBox.Show("Trade is now open so cannot set trade order price");
                return;
            }

            trade.AddOrderPrice(lastCandle.CloseTime(), (decimal)price);

            if (trade.TradeDirection == TradeDirection.Long)
            {
                trade.OrderType = price < lastCandle.CloseAsk ? OrderType.LimitEntry : OrderType.StopEntry;
            }
            else
            {
                trade.OrderType = price < lastCandle.CloseBid ? OrderType.StopEntry : OrderType.LimitEntry;
            }
        }
    }
}