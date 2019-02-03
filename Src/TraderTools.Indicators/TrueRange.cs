using System;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class TrueRange : IIndicator
    {
        private ISimpleCandle _prevCandle;

        public string Name => "TrueRange";

        /// <summary>
        /// To get price components to select the maximal value.
        /// </summary>
        /// <param name="currentCandle">The current candle.</param>
        /// <param name="prevCandle">The previous candle.</param>
        /// <returns>Price components.</returns>
        private float[] GetPriceMovements(ISimpleCandle currentCandle, ISimpleCandle prevCandle)
        {
            return new[]
            {
                Math.Abs(currentCandle.High - currentCandle.Low),
                Math.Abs(prevCandle.Close - currentCandle.High),
                Math.Abs(prevCandle.Close - currentCandle.Low)
            };
        }

        public void Reset()
        {
            _prevCandle = null;
            IsFormed = false;
        }

        public bool IsFormed { get; set; }

        public SignalAndValue Process(ISimpleCandle candle)
        {
            if (_prevCandle != null)
            {
                if (candle.IsComplete == 1)
                    IsFormed = true;

                var priceMovements = GetPriceMovements(candle, _prevCandle);

                if (candle.IsComplete == 1)
                    _prevCandle = candle;

                return new SignalAndValue(priceMovements.Max(), IsFormed);
            }

            if (candle.IsComplete == 1)
                _prevCandle = candle;

            return new SignalAndValue(candle.High - candle.Low, IsFormed);
        }
    }
}