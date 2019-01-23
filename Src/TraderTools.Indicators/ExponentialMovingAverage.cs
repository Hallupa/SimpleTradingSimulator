using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class ExponentialMovingAverage : IIndicator
    {
        private double _currentValue;
        private double _multiplier = 1;
        private int _length;
        private double _prevValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExponentialMovingAverage"/>.
        /// </summary>
        public ExponentialMovingAverage(string name)
        {
            Length = 32;
            Name = name;
            Reset();
        }

        public ExponentialMovingAverage(string name, int length)
        {
            Length = length;
            Name = name;
            Reset();
        }

        public string Name { get; }

        public int Length
        {
            get { return _length; }
            set
            {
                _length = value;
                _multiplier = 2.0 / (Length + 1);
            }
        }

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
            if (!IsFormed)
            {
                //if (candle.IsFinal)
                {
                    _totalValues++;
                    Buffer.Add(newValue);

                    _prevValue = _currentValue;
                    _currentValue = Buffer.Sum() / Length;

                    return new SignalAndValue((float)_currentValue, IsFormed);
                }
                /*else
                {
                    return (Buffer.Skip(1).Sum() + newValue) / Length;
                }*/
            }
            else
            {
                var curValue = (newValue - _currentValue) * _multiplier + _currentValue;
                _totalValues++;
                //if (candle.IsFinal)
                _prevValue = _currentValue;
                _currentValue = curValue;

                return new SignalAndValue((float)curValue, IsFormed);
            }
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