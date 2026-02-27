using System;

namespace Upbit_Manager.UI.Series
{
    public record TradeTick
    {
        public DateTime Time { get; init; }
        public double Price { get; init; }
        public double Volume { get; init; }
        public string Side { get; init; }
    }
}
