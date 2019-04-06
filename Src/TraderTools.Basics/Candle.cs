using System;
using System.Runtime.InteropServices;
using TraderTools.Basics.Extensions;

namespace TraderTools.Basics
{
    public struct BasicCandleAndIndicators : ISimpleCandle
    {
        public BasicCandleAndIndicators(ICandle candle,
            int signalsCount)
        {
            High = (float)candle.High;
            Low = (float)candle.Low;
            Close = (float)candle.Close;
            Open = (float)candle.Open;
            CloseTimeTicks = candle.CloseTimeTicks;
            OpenTimeTicks = candle.OpenTimeTicks;
            IsComplete = candle.IsComplete;
            Indicators = new SignalAndValue[signalsCount];
        }

        public BasicCandleAndIndicators(
            long openTimeTicks,
            long closeTimeTicks,
            float open,
            float high,
            float low,
            float close,
            byte isComplete,
            int signalsCount)
        {
            OpenTimeTicks = openTimeTicks;
            CloseTimeTicks = closeTimeTicks;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            IsComplete = isComplete;
            Indicators = new SignalAndValue[signalsCount];
        }

        public long OpenTimeTicks { get; set; }
        public long CloseTimeTicks { get; set; }

        //  [Index(0)]
        //public DateTime OpenTime { get; }
        // [Index(1)]
        // public DateTime CloseTime { get; }
        // [Index(2)]
        public float Open { get; }
       // [Index(3)]
        public float High { get; }
      //  [Index(4)]
        public float Low { get; }
      //  [Index(5)]
        public float Close { get; }
      //  [Index(6)]
        public byte IsComplete { get; }
      //  [Index(7)]
        public SignalAndValue[] Indicators { get; set; }

     //   [IgnoreFormat]
        public SignalAndValue this[Indicator indicator]
        {
            get
            {
                return Indicators[(int)indicator];
            }
        }

        public void Set(Indicator indicator, SignalAndValue signalValue)
        {
            if (Indicators == null)
            {
                Indicators = new SignalAndValue[13];
            }

            Indicators[(int)indicator] = signalValue;
        }

       /* public void Serialise(BinaryWriter stream)
        {
            stream.Write(High);
            stream.Write(Low);
            stream.Write(Close);
            stream.Write(Open);
            stream.Write(CloseTime.Ticks);
            stream.Write(OpenTime.Ticks);
            stream.Write(Indicators.Length);

            foreach (var signalAndValue in Indicators)
            {
                signalAndValue.Serialise(stream);
            }
        }*/

        public override string ToString()
        {
            return $"{this.OpenTime()} {this.CloseTime()} {Close} Open:{Open} Close:{Close} High:{High} Low:{Low} IsComplete:{IsComplete}";
        }
    }

    public interface ICandle
    {
        double High { get; set; }
        double Low { get; set; }
        double Close { get; set; }
        double Open { get; set; }
        long OpenTimeTicks { get; set; }
        long CloseTimeTicks { get; set; }
        byte IsComplete { get; set; }
        int Timeframe { get; set; }
        double Volume { get; set; }
        int TradeCount { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Candle : ICandle
    {
        public Guid Id { get; set; }
        public long OpenTimeTicks { get; set; }
        public long CloseTimeTicks { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Volume { get; set; }
        public int TradeCount { get; set; }
        public int Timeframe { get; set; }
        public byte IsComplete { get; set; }

        public Candle(SimpleCandle candle)
        {
            OpenTimeTicks = candle.OpenTimeTicks;
            CloseTimeTicks = candle.CloseTimeTicks;
            High = candle.High;
            Low = candle.Low;
            Open = candle.Open;
            Close = candle.Close;
            Id = Guid.NewGuid();
            TradeCount = 0;
            Timeframe = 0;
            IsComplete = 1;
            Volume = 0;
        }

        public override string ToString()
        {
            return $"OpenTime: {new DateTime(OpenTimeTicks)} CloseTime: {new DateTime(CloseTimeTicks)} Open: {Open} Close: {Close}";
        }
    }
}