using System;
using System.Collections.Generic;
using System.Linq;

namespace TraderTools.Basics.Helpers
{
    public static class CandlesHelper
    {
        public static Candle? GetFirstCandleThatClosesBeforeDateTime(IList<Candle> candles, DateTime dateTime)
        {
            var ret = candles.Where(x => x.CloseTimeTicks <= dateTime.Ticks).OrderByDescending(x => x.CloseTimeTicks).ToList();

            if (ret.Count == 0)
            {
                return null;
            }

            return ret[0];
        }
    }
}