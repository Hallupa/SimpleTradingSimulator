using System;
using System.Collections.Generic;

namespace TraderTools.Basics
{
    public interface IBrokersCandlesService
    {
        List<ICandle> GetCandles(IBroker broker, string market, Timeframe timeframe, bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null, bool cacheData = true);

        void UpdateCandles(IBroker broker, string market, Timeframe timeframe);

        void UnloadCandles(string market, Timeframe timeframe, IBroker broker);
    }
}