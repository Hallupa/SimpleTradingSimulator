namespace TraderTools.Basics
{
    public interface IIndicator
    {
        bool IsFormed { get; }

        string Name { get; }

        SignalAndValue Process(ISimpleCandle candle);

        void Reset();
    }
}