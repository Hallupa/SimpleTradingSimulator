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

        public string Name => $"SMA{Length}";

        public bool IsFormed => Buffer.Count >= Length;

        protected IList<double> Buffer { get; } = new List<double>();

        public void Reset()
        {
            _prevFinalValue = 0;
        }

        public SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;

            if (!IsFormed)
            {
                if (candle.IsComplete == 1)
                {
                    Buffer.Add(newValue);

                    _prevFinalValue = Buffer.Sum() / Length;
                }

                return new SignalAndValue((float)_prevFinalValue, IsFormed);
            }

            var curValue = (_prevFinalValue * (Length - 1) + newValue) / Length;

            if (candle.IsComplete == 1)
                _prevFinalValue = curValue;

            return new SignalAndValue((float)curValue, IsFormed);
        }
    }
}