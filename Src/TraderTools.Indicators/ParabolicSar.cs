using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class ParabolicSar : IIndicator
    {
        public string Name { get; } = "ParabolicSar";

        private double _prevValue;
        private readonly List<ISimpleCandle> _candles = new List<ISimpleCandle>();
        private double _xp;        // Extreme Price
        private double _af;         // Acceleration factor
        private int _prevBar;
        private double _currentValue;
        private bool _afIncreased;
        private int _reverseBar;
        private double _reverseValue;
        private double _prevSar;
        private double _todaySar;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParabolicSar"/>.
        /// </summary>
        public ParabolicSar()
        {
            Acceleration = 0.02;
            AccelerationStep = 0.02;
            AccelerationMax = 0.2;
        }

        public double Acceleration { get; set; }

        public bool IsFormed { get; private set; }

        public double AccelerationStep { get; set; }

        public double AccelerationMax { get; set; }

        public Signal Signal { get; private set; }

        public SignalAndValue Process(ISimpleCandle candle)
        {
            if (_candles.Count == 0)
                _candles.Add(candle);

            _prevValue = _currentValue;

            if (candle.OpenTimeTicks != _candles[_candles.Count - 1].OpenTimeTicks)
            {
                _candles.Add(candle);
            }
            else
                _candles[_candles.Count - 1] = candle;

            if (_candles.Count < 3)
            {
                //_prevValue = candle.Low;
                _currentValue = _prevValue;
                return new SignalAndValue((float)_currentValue, IsFormed, Signal);
            }

            if (_candles.Count == 3)
            {
                Signal = _candles[_candles.Count - 1].High > _candles[_candles.Count - 2].High
                    ? Signal.Long
                    : Signal.Short;
                var max = _candles.Max(t => t.High);
                var min = _candles.Min(t => t.Low);
                _xp = Signal == Signal.Long ? max : min;
                _af = Acceleration;
                var ret = _xp + (Signal == Signal.Long ? -1 : 1) * (max - min) * _af;
                _currentValue = ret;
                return new SignalAndValue((float)_currentValue, IsFormed, Signal);
            }

            if (_afIncreased && _prevBar != _candles.Count)
                _afIncreased = false;

            //if (input.IsFinal)
            IsFormed = true;

            var value = _prevValue;

            if (_reverseBar != _candles.Count)
            {
                _todaySar = TodaySar(_prevValue + _af * (_xp - _prevValue));

                for (var x = 1; x <= 2; x++)
                {
                    if (Signal == Signal.Long)
                    {
                        if (_todaySar > (double)_candles[_candles.Count - 1 - x].Low)
                            _todaySar = (double)_candles[_candles.Count - 1 - x].Low;
                    }
                    else
                    {
                        if (_todaySar < (double)_candles[_candles.Count - 1 - x].High)
                            _todaySar = (double)_candles[_candles.Count - 1 - x].High;
                    }
                }

                if ((Signal == Signal.Long && ((double)_candles[_candles.Count - 1].Low < _todaySar || (double)_candles[_candles.Count - 2].Low < _todaySar))
                        || (Signal != Signal.Long && ((double)_candles[_candles.Count - 1].High > _todaySar || (double)_candles[_candles.Count - 2].High > _todaySar)))
                {
                    var ret = Reverse();
                    _currentValue = ret;
                    return new SignalAndValue((float)_currentValue, IsFormed, Signal);
                }

                if (Signal == Signal.Long)
                {
                    if (_prevBar != _candles.Count || (double)_candles[_candles.Count - 1].Low < _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if ((double)_candles[_candles.Count - 1].High > _xp)
                    {
                        _xp = (double)_candles[_candles.Count - 1].High;
                        AfIncrease();
                    }
                }
                else if (Signal != Signal.Long)
                {
                    if (_prevBar != _candles.Count || (double)_candles[_candles.Count - 1].High > _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if ((double)_candles[_candles.Count - 1].Low < _xp)
                    {
                        _xp = (double)_candles[_candles.Count - 1].Low;
                        AfIncrease();
                    }
                }

            }
            else
            {
                if (Signal == Signal.Long && (double)_candles[_candles.Count - 1].High > _xp)
                    _xp = (double)_candles[_candles.Count - 1].High;
                else if (Signal != Signal.Long && (double)_candles[_candles.Count - 1].Low < _xp)
                    _xp = (double)_candles[_candles.Count - 1].Low;

                value = _prevSar;

                _todaySar = TodaySar(Signal == Signal.Long ? Math.Min(_reverseValue, (double)_candles[_candles.Count - 1].Low) :
                    Math.Max(_reverseValue, (double)_candles[_candles.Count - 1].High));
            }

            _prevBar = _candles.Count;

            _currentValue = value;
            return new SignalAndValue((float)_currentValue, IsFormed, Signal);
        }

        public void RollbackLastValue()
        {
            throw new NotImplementedException();
        }

        private double TodaySar(double todaySar)
        {
            if (Signal == Signal.Long)
            {
                var lowestSar = Math.Min(Math.Min(todaySar, (double)_candles[_candles.Count - 1].Low), (double)_candles[_candles.Count - 2].Low);
                todaySar = (double)_candles[_candles.Count - 1].Low > lowestSar ? lowestSar : Reverse();
            }
            else
            {
                var highestSar = Math.Max(Math.Max(todaySar, (double)_candles[_candles.Count - 1].High), (double)_candles[_candles.Count - 2].High);
                todaySar = (double)_candles[_candles.Count - 1].High < highestSar ? highestSar : Reverse();
            }

            return todaySar;
        }

        private double Reverse()
        {
            var todaySar = _xp;

            if ((Signal == Signal.Long && _prevSar > (double)_candles[_candles.Count - 1].Low) ||
                (Signal != Signal.Long && _prevSar < (double)_candles[_candles.Count - 1].High) || _prevBar != _candles.Count)
            {
                Signal = Signal == Signal.Long ? Signal.Short : Signal.Long;
                _reverseBar = _candles.Count;
                _reverseValue = _xp;
                _af = Acceleration;
                _xp = Signal == Signal.Long ? (double)_candles[_candles.Count - 1].High : (double)_candles[_candles.Count - 1].Low;
                _prevSar = todaySar;
            }
            else
                todaySar = _prevSar;

            return todaySar;
        }

        private void AfIncrease()
        {
            if (_afIncreased)
                return;

            _af = Math.Min(AccelerationMax, _af + AccelerationStep);
            _afIncreased = true;
        }
    }
}