using System;

namespace TraderTools.Basics.Extensions
{
    public enum CandleColour
    {
        White,
        Black,
        None
    }

    public static class CandleExtensions
    {
        public static CandleColour Colour(this ICandle candle)
        {
            if (candle.Close > candle.Open)
            {
                return CandleColour.White;
            }

            if (candle.Close < candle.Open)
            {
                return CandleColour.Black;
            }

            return CandleColour.None;
        }

        public static DateTime OpenTime(this ISimpleCandle candle)
        {
            return new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc);
        }

        public static DateTime CloseTime(this ISimpleCandle candle)
        {
            return new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);
        }

        public static DateTime OpenTime(this ICandle candle)
        {
            return new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc);
        }

        public static DateTime CloseTime(this ICandle candle)
        {
            return new DateTime(candle.CloseTimeTicks, DateTimeKind.Utc);
        }
    }
}