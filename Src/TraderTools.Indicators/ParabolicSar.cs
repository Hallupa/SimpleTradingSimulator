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
        private bool _longPosition;
        private double _xp;        // Extreme Price
        private double _af;         // Acceleration factor
        private int _prevBar;
        private double _currentValue;
        private bool _afIncreased;
        private int _reverseBar;
        private double _reverseValue;
        private double _prevSar;
        private double _todaySar;

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
                _currentValue = _prevValue;
                return new SignalAndValue((float) _prevValue, IsFormed);
            }

            if (_candles.Count == 3)
            {
                _longPosition = _candles[_candles.Count - 1].High > _candles[_candles.Count - 2].High;
                var max = _candles.Max(t => t.High);
                var min = _candles.Min(t => t.Low);
                _xp = _longPosition ? max : min;
                _af = Acceleration;
                var v = (float) (_xp + (_longPosition ? -1 : 1) * (max - min) * _af);
                _currentValue = v;
                return new SignalAndValue(v, IsFormed);
            }

            if (_afIncreased && _prevBar != _candles.Count)
                _afIncreased = false;

            if (candle.IsComplete == 1)
                IsFormed = true;

            var value = _prevValue;

            if (_reverseBar != _candles.Count)
            {
                _todaySar = TodaySar(_prevValue + _af * (_xp - _prevValue));

                for (var x = 1; x <= 2; x++)
                {
                    if (_longPosition)
                    {
                        if (_todaySar > _candles[_candles.Count - 1 - x].Low)
                            _todaySar = _candles[_candles.Count - 1 - x].Low;
                    }
                    else
                    {
                        if (_todaySar < _candles[_candles.Count - 1 - x].High)
                            _todaySar = _candles[_candles.Count - 1 - x].High;
                    }
                }

                if ((_longPosition && (_candles[_candles.Count - 1].Low < _todaySar || _candles[_candles.Count - 2].Low < _todaySar))
                        || (!_longPosition && (_candles[_candles.Count - 1].High > _todaySar || _candles[_candles.Count - 2].High > _todaySar)))
                {
                    var v = (float)Reverse();
                    _currentValue = v;
                    return new SignalAndValue(v, IsFormed);
                }

                if (_longPosition)
                {
                    if (_prevBar != _candles.Count || _candles[_candles.Count - 1].Low < _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if (_candles[_candles.Count - 1].High > _xp)
                    {
                        _xp = _candles[_candles.Count - 1].High;
                        AfIncrease();
                    }
                }
                else if (!_longPosition)
                {
                    if (_prevBar != _candles.Count || _candles[_candles.Count - 1].High > _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if (_candles[_candles.Count - 1].Low < _xp)
                    {
                        _xp = _candles[_candles.Count - 1].Low;
                        AfIncrease();
                    }
                }

            }
            else
            {
                if (_longPosition && _candles[_candles.Count - 1].High > _xp)
                    _xp = _candles[_candles.Count - 1].High;
                else if (!_longPosition && _candles[_candles.Count - 1].Low < _xp)
                    _xp = _candles[_candles.Count - 1].Low;

                value = _prevSar;

                _todaySar = TodaySar(_longPosition ? Math.Min(_reverseValue, _candles[_candles.Count - 1].Low) :
                    Math.Max(_reverseValue, _candles[_candles.Count - 1].High));
            }

            _prevBar = _candles.Count;

            _currentValue = value;
            return new SignalAndValue((float)value, IsFormed);
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