using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class CommodityChannelIndex : LengthIndicator
    {
        private readonly MeanDeviation _mean = new MeanDeviation();

        public CommodityChannelIndex()
        {
            Length = 15;
        }

        public override string Name => "CCI";

        public override bool IsFormed => _mean.IsFormed;

        public override void Reset()
        {
            _mean.Length = Length;
        }

        public override SignalAndValue Process(ISimpleCandle candle)
        {
            var aveP = (candle.High + candle.Low + candle.Close) / 3.0;

            var meanValue = _mean.Process(
                new SimpleCandle
                {
                    Close = (float)aveP,
                    IsComplete = candle.IsComplete
                });

            if (IsFormed && !meanValue.Value.Equals(0.0F))
                return new SignalAndValue((((float)aveP - _mean.Sma.CurrentValue) / (0.015F * meanValue.Value)), IsFormed);

            return new SignalAndValue(0.0F, false);
        }
    }
}