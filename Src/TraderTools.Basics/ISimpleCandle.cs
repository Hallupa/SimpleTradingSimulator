using System;

namespace TraderTools.Basics
{
    public interface ISimpleCandle
    {
        float High { get; }
        float Low { get; }
        float Close { get; }
        float Open { get; }
        long OpenTimeTicks { get; set; }
        long CloseTimeTicks { get; set; }
    }
}