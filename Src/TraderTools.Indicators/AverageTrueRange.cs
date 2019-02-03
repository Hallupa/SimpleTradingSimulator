using System;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class AverageTrueRange : IIndicator
    {
        public AverageTrueRange()
        {
            MovingAverage = new WilderMovingAverage();
            TrueRange = new TrueRange();
        }

        public WilderMovingAverage MovingAverage { get; }

        public TrueRange TrueRange { get; }

        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public bool IsFormed { get; set; }

        public string Name => "ATR";

        public void Reset()
        {
            IsFormed = false;

            MovingAverage.Reset();
            TrueRange.Reset();
        }

        public SignalAndValue Process(ISimpleCandle candle)
        {
            IsFormed = MovingAverage.IsFormed;

            var v = TrueRange.Process(candle);
            return MovingAverage.Process(new SimpleCandle
            {
                Close = v.Value,
                IsComplete = candle.IsComplete
            });
        }
    }
}