using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class RelativeStrengthIndex : LengthIndicator
    {
        private readonly SmoothedMovingAverage _gain;
        private readonly SmoothedMovingAverage _loss;
        private bool _isInitialized;
        private double _last;

        public RelativeStrengthIndex()
        {
            _gain = new SmoothedMovingAverage();
            _loss = new SmoothedMovingAverage();

            Length = 15;
        }

        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public override bool IsFormed => _gain.IsFormed;

        public override string Name => "RSI";

        /// <summary>
        /// To reset the indicator status to initial. The method is called each time when initial settings are changed (for example, the length of period).
        /// </summary>
        public override void Reset()
        {
            _loss.Length = _gain.Length = Length;
            base.Reset();
        }

        public override SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;

            if (!_isInitialized)
            {
                if (candle.IsComplete == 1)
                {
                    _last = newValue;
                    _isInitialized = true;
                }

                return new SignalAndValue(0F, false);
            }

            var delta = newValue - _last;

            var gainValue = _gain.Process(new SimpleCandle
            {
                Close = (float)(delta > 0 ? delta : 0.0),
                IsComplete = candle.IsComplete
            });
            var lossValue = _loss.Process(new SimpleCandle
            {
                Close = (float)(delta > 0 ? 0.0 : -delta),
                IsComplete = candle.IsComplete
            });

            if (candle.IsComplete == 1)
                _last = newValue;

            if (lossValue.Value.Equals(0.0F))
                return new SignalAndValue((float)100.0, IsFormed);

            if ((gainValue.Value / lossValue.Value).Equals(1F))
                return new SignalAndValue((float)0.0, IsFormed);

            return new SignalAndValue((float)(100.0 - 100.0 / (1.0 + gainValue.Value / lossValue.Value)), IsFormed);
        }
    }
}