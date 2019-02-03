using System;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class MovingAverageConvergenceDivergence : IIndicator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MovingAverageConvergenceDivergence"/>.
        /// </summary>
        public MovingAverageConvergenceDivergence(string name = "MACD")
            : this(name, new ExponentialMovingAverage(26), new ExponentialMovingAverage(12))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MovingAverageConvergenceDivergence"/>.
        /// </summary>
        /// <param name="longMa">Long moving average.</param>
        /// <param name="shortMa">Short moving average.</param>
        public MovingAverageConvergenceDivergence(string name, ExponentialMovingAverage longMa, ExponentialMovingAverage shortMa)
        {
            if (longMa == null)
                throw new ArgumentNullException(nameof(longMa));

            if (shortMa == null)
                throw new ArgumentNullException(nameof(shortMa));

            Name = name;
            ShortMa = shortMa;
            LongMa = longMa;
        }

        public string Name { get; }

        /// <summary>
        /// Long moving average.
        /// </summary>
        public ExponentialMovingAverage LongMa { get; }

        /// <summary>
        /// Short moving average.
        /// </summary>
        public ExponentialMovingAverage ShortMa { get; }

        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public bool IsFormed => LongMa.IsFormed;

        public SignalAndValue Process(ISimpleCandle candle)
        {
            var shortValue = ShortMa.Process(candle);
            var longValue = LongMa.Process(candle);
            return new SignalAndValue(shortValue.Value - longValue.Value, IsFormed);
        }
    }
}