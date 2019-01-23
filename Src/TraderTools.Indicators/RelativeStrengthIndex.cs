using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class RelativeStrengthIndex : IIndicator
    {
        private readonly SmoothedMovingAverage _gain;
        private readonly SmoothedMovingAverage _loss;
        private bool _isInitialized;
        private double _last;

        public RelativeStrengthIndex()
        {
            _gain = new SmoothedMovingAverage();
            _loss = new SmoothedMovingAverage();

            Length = 14;
        }

        public int Length { get; set; }
        protected IList<decimal> Buffer { get; } = new List<decimal>();

        public bool IsFormed => _gain.IsFormed;

        public string Name => "RSI";

        public SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;

            if (!_isInitialized)
            {
                _last = newValue;
                _isInitialized = true;

                return new SignalAndValue(0, IsFormed, Signal.None);
            }

            var delta = newValue - _last;

            var gainValue = _gain.Process(delta > 0 ? delta : 0).Value;
            var lossValue = _loss.Process(delta > 0 ? 0 : -delta).Value;

            _last = newValue;

            if (lossValue == 0)
                return new SignalAndValue(100, IsFormed, Signal.None);

            if (gainValue / lossValue == 1)
                return new SignalAndValue(0, IsFormed, Signal.None);

            return new SignalAndValue((float)(100.0 - 100.0 / (1.0 + gainValue / lossValue)), IsFormed, Signal.None);
        }

        public void RollbackLastValue()
        {
            throw new System.NotImplementedException();
        }
    }
}