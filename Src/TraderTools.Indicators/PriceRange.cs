using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class PriceRange : IIndicator
    {
        private readonly int _length;
        protected IList<double> HighBuffer { get; } = new List<double>();
        protected IList<double> LowBuffer { get; } = new List<double>();

        public PriceRange(int length)
        {
            _length = length;
            Name = "Range " + _length;
        }

        public string Name { get; }

        public bool IsFormed => HighBuffer.Count >= _length;
        
        public SignalAndValue Process(ISimpleCandle candle)
        {
            HighBuffer.Add(candle.High);
            LowBuffer.Add(candle.Low);

            if (HighBuffer.Count > _length)
            {
                HighBuffer.RemoveAt(0);
                LowBuffer.RemoveAt(0);
            }

            return new SignalAndValue((float)(HighBuffer.Max() - LowBuffer.Min()), IsFormed);
        }

        public void RollbackLastValue()
        {
            if (HighBuffer.Count > 0)
            {
                HighBuffer.RemoveAt(HighBuffer.Count - 1);
                LowBuffer.RemoveAt(LowBuffer.Count - 1);
            }
        }
    }
}