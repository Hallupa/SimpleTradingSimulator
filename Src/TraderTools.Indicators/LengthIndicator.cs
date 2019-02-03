using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public abstract class LengthIndicator : IIndicator
    {
        private int _length = 1;

        public virtual void Reset()
        {
            Buffer.Clear();
        }

        public virtual bool IsFormed => Buffer.Count >= Length;

        public abstract string Name { get; }

        public abstract SignalAndValue Process(ISimpleCandle candle);

        protected List<float> Buffer { get; } = new List<float>();

        public int Length
        {
            get => _length;
            set
            {
                _length = value;
                Reset();
            }
        }
    }
}