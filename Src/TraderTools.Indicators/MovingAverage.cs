using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class MovingAverage : IIndicator
    {
        private double _currentValue;
        private double _prevValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MovingAverage"/>.
        /// </summary>
        public MovingAverage(string name)
        {
            Length = 32;
            Name = name;
            Reset();
        }

        public MovingAverage(string name, int length)
        {
            Length = length;
            Name = name;
            Reset();
        }

        public string Name { get; }

        public int Length { get; set; }

        protected IList<double> Buffer { get; } = new List<double>();
        public bool IsFormed => Buffer.Count >= Length;
        private int _totalValues = 0;

        /// <summary>
        /// To reset the indicator status to initial. The method is called each time when initial settings are changed (for example, the length of period).
        /// </summary>
        public void Reset()
        {
            Buffer.Clear();
            _currentValue = 0;
        }

        public SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;
            return Process(newValue);
        }

        public SignalAndValue Process(double newValue)
        {
            _totalValues++;
            Buffer.Add(newValue);

            if (Buffer.Count > Length)
            {
                Buffer.RemoveAt(0);
            }

            _prevValue = _currentValue;
            _currentValue = Buffer.Sum() / Length;

            return new SignalAndValue((float)_currentValue, IsFormed);
        }

        public void RollbackLastValue()
        {
            _totalValues--;
            _currentValue = _prevValue;
            _prevValue = 0;

            if (_totalValues < Buffer.Count)
            {
                Buffer.RemoveAt(Buffer.Count - 1);
            }
        }
    }
}