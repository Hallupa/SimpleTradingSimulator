using System.IO;

namespace TraderTools.Basics
{
    public struct SignalAndValue
    {
        public SignalAndValue(float value, bool isFormed, Signal signal = Basics.Signal.None)
        {
            Value = value;
            IsFormed = isFormed;
            Signal = (byte)signal;
        }

        public float Value { get; set; }
        public bool IsFormed { get; set; }
        public byte Signal { get; set; }
    }
}