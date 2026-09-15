using System;
using System.Collections.Generic;

namespace PrismDemo.A.Models
{
    public class HistoryPoint
    {
        public DateTime Time { get; set; }
        public Dictionary<string, double> Values { get; set; } = new();
    }
}
