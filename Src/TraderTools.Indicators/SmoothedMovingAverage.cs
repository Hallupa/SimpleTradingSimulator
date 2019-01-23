using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class SmoothedMovingAverage : IIndicator
    {
        private double _prevFinalValue;

        public SmoothedMovingAverage()
        {
            Length = 32;
        }

        public int Length { get; set; }

        public string Name => "SMA";

        public bool IsFormed => Buffer.Count >= Length;

        protected IList<double> Buffer { get; } = new List<double>();

        public SignalAndValue Process(ISimpleCandle candle)
        {
            return Process(candle.Close);
        }

        public void RollbackLastValue()
        {
            throw new System.NotImplementedException();
        }

        public SignalAndValue Process(double newValue)
        {
            if (!IsFormed)
            {
                Buffer.Add(newValue);

                _prevFinalValue = Buffer.Sum() / Length;

                return new SignalAndValue((float)_prevFinalValue, IsFormed, Signal.None);
            }

            var curValue = (_prevFinalValue * (Length - 1) + newValue) / Length;

            _prevFinalValue = curValue;

            return new SignalAndValue((float)curValue, IsFormed, Signal.None);
        }
    }
}
