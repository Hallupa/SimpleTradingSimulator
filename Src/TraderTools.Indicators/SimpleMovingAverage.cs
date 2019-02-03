using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class SimpleMovingAverage : LengthIndicator
    {
        public SimpleMovingAverage()
        {
            Length = 32;
        }

        public SimpleMovingAverage(int length)
        {
            Length = length;
        }

        public override string Name => $"SMA{Length}";

        public override SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;

            if (candle.IsComplete == 1)
            {
                Buffer.Add(newValue);

                if (Buffer.Count > Length)
                    Buffer.RemoveAt(0);
            }

            SignalAndValue ret;
            ret = candle.IsComplete == 1 ? new SignalAndValue(Buffer.Sum() / Length, IsFormed) : new SignalAndValue((Buffer.Skip(1).Sum() + newValue) / Length, IsFormed);

            CurrentValue = ret.Value;
            return ret;
        }

        public float CurrentValue { get; set; }
    }
}