using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class T3CommodityChannelIndex : IIndicator
    {
        private CommodityChannelIndex _cci;
        private int _t3Period;
        private float e1 = float.NaN;
        private float e2 = float.NaN;
        private float e3 = float.NaN;
        private float e4 = float.NaN;
        private float e5 = float.NaN;
        private float e6 = float.NaN;

        public T3CommodityChannelIndex()
        {
            _cci = new CommodityChannelIndex { Length = 14 };
            _t3Period = 5;
        }

        public bool IsFormed => _cci.IsFormed;
        public string Name => "T3-CCI";

        public SignalAndValue Process(ISimpleCandle candle)
        {
            Func<float, float> nz = x => float.IsNaN(x) ? 0 : x;

            var price = candle.Close;
            float b = 0.618F;

            float b2 = b * b;
            float b3 = b2 * b;
            float c1 = -b3;
            float c2 = (3 * (b2 + b3));
            float c3 = -3 * (2 * b2 + b + b3);
            float c4 = (1 + 3 * b + b3 + 3 * b2);
            float nn = _t3Period;
            float nr = 1F + 0.5F * (nn - 1);
            float w1 = 2F / (nr + 1);
            float w2 = 1F - w1;
            var xcci = _cci.Process(candle).Value;
            var e1tmp = w1 * xcci + w2 * nz(e1);
            var e2tmp = w1 * e1tmp + w2 * nz(e2);
            var e3tmp = w1 * e2tmp + w2 * nz(e3);
            var e4tmp = w1 * e3tmp + w2 * nz(e4);
            var e5tmp = w1 * e4tmp + w2 * nz(e5);
            var e6tmp = w1 * e5tmp + w2 * nz(e6);

            if (candle.IsComplete == 1)
            {
                e1 = e1tmp;
                e2 = e2tmp;
                e3 = e3tmp;
                e4 = e4tmp;
                e5 = e5tmp;
                e6 = e6tmp;
            }

            var xccir = c1 * e6 + c2 * e5 + c3 * e4 + c4 * e3;

            return new SignalAndValue(xccir, IsFormed);
        }

        public void Reset()
        {
            e1 = float.NaN;
            e2 = float.NaN;
            e3 = float.NaN;
            e4 = float.NaN;
            e5 = float.NaN;
            e6 = float.NaN;
            _cci.Reset();
        }
    }
}