using System;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class MeanDeviation : LengthIndicator
    {
        public MeanDeviation()
        {
            Sma = new SimpleMovingAverage();
            Length = 5;
        }

        public override string Name => "Mean Deviation";

        public SimpleMovingAverage Sma { get; }

        public override bool IsFormed => Sma.IsFormed;

        public override void Reset()
        {
            Sma.Length = Length;
        }

        public override SignalAndValue Process(ISimpleCandle candle)
        {
            var val = candle.Close;

            if (candle.IsComplete == 1)
                Buffer.Add(val);

            var smaValue = Sma.Process(candle).Value;

            if (Buffer.Count > Length)
                Buffer.RemoveAt(0);

            var md = candle.IsComplete == 1
                ? Buffer.Sum(t => Math.Abs(t - smaValue))
                : Buffer.Skip(IsFormed ? 1 : 0).Sum(t => Math.Abs(t - smaValue)) + Math.Abs(val - smaValue);

            return new SignalAndValue(md / Length, IsFormed);
        }
    }
}