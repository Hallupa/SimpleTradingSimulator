using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TraderTools.Basics
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SimpleCandle : ISimpleCandle
    {
        public SimpleCandle(Candle candle)
        {
            Open = (float)candle.Open;
            Close = (float)candle.Close;
            High = (float)candle.High;
            Low = (float)candle.Low;
            OpenTimeTicks = candle.OpenTimeTicks;
            CloseTimeTicks = candle.CloseTimeTicks;
            IsComplete = candle.IsComplete;
        }

        public long OpenTimeTicks { get; set; }
        public long CloseTimeTicks { get; set; }
        public float Open { get; set; }
        public float Close { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
        public byte IsComplete { get; set; }
    }
}