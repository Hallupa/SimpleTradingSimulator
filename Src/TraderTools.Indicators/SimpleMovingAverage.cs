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

        public override string Name => "Moving Average";

        public override SignalAndValue Process(ISimpleCandle candle)
        {
            var newValue = candle.Close;

            if (candle.IsComplete == 1)
            {
                Buffer.Add(newValue);

                if (Buffer.Count > Length)
                    Buffer.RemoveAt(0);
            }

            if (candle.IsComplete == 1)
                return new SignalAndValue(Buffer.Sum() / Length, IsFormed);

            return new SignalAndValue((Buffer.Skip(1).Sum() + newValue) / Length, IsFormed);
        }
    }
}