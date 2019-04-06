using System;

namespace TraderTools.Basics.Extensions
{
    public static class TradeDetailsExtensions
    {
        public static void SimulateTrade(this TradeDetails trade, ICandle candle, out bool updated)
        {
            if (trade.CloseDateTime != null)
            {
                updated = false;
                return;
            }

            // Try to close trade
            if (trade.EntryPrice != null && trade.CloseReason == null)
            {
                var openTrade = trade;

                if (openTrade.StopPrice != null && openTrade.TradeDirection.Value == TradeDirection.Long && candle.Low <= (double)openTrade.StopPrice.Value)
                {
                    var stopPrice = Math.Min((decimal)candle.High, openTrade.StopPrice.Value);

                    openTrade.SetClose(candle.OpenTime(), stopPrice, TradeCloseReason.HitStop);
                    updated = true;
                }
                else if (openTrade.StopPrice != null && openTrade.TradeDirection.Value == TradeDirection.Short && candle.High >= (double)openTrade.StopPrice.Value)
                {
                    var stopPrice = Math.Max((decimal)candle.Low, openTrade.StopPrice.Value);
                    openTrade.SetClose(candle.OpenTime(), stopPrice, TradeCloseReason.HitStop);
                    updated = true;
                }
                else if (openTrade.LimitPrice != null && openTrade.TradeDirection.Value == TradeDirection.Long && candle.High >= (double)openTrade.LimitPrice.Value)
                {
                    var stopPrice = Math.Max((decimal)candle.Low, openTrade.StopPrice.Value);
                    openTrade.SetClose(candle.OpenTime(), stopPrice, TradeCloseReason.HitLimit);
                    updated = true;
                }
                else if (openTrade.LimitPrice != null && openTrade.TradeDirection.Value == TradeDirection.Short && candle.Low <= (double)openTrade.LimitPrice.Value)
                {
                    var stopPrice = Math.Min((decimal)candle.High, openTrade.StopPrice.Value);
                    openTrade.SetClose(candle.OpenTime(), stopPrice, TradeCloseReason.HitLimit);
                    updated = true;
                }
            }

            // Try to fill order
            if (trade.EntryPrice == null && trade.OrderPrice != null)
            {
                var order = trade;

                if (order.OrderPrice != null)
                {
                    if (order.TradeDirection == TradeDirection.Long && candle.Low <= (double)order.OrderPrice)
                    {
                        var entryPrice = Math.Min((decimal)candle.High, order.OrderPrice.Value);
                        order.SetEntry(candle.OpenTime(), entryPrice);
                        updated = true;
                    }
                    else if (order.TradeDirection == TradeDirection.Short && candle.High >= (double)order.OrderPrice)
                    {
                        var entryPrice = Math.Max((decimal)candle.Low, order.OrderPrice.Value);
                        order.SetEntry(candle.OpenTime(), entryPrice);
                        updated = true;
                    }
                }
                else if (order.OrderPrice == null)
                {
                    order.SetEntry(candle.OpenTime(), (decimal)candle.Close);
                    updated = true;
                }

                if (trade.EntryPrice == null && order.OrderExpireTime != null && candle.CloseTime() >= order.OrderExpireTime)
                {
                    order.SetExpired(candle.OpenTime());
                    updated = true;
                }
            }

            updated = false;
        }
    }
}