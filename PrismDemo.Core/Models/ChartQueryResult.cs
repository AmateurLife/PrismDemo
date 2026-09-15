using System.Collections.Generic;

namespace PrismDemo.Core.Models
{
    public class ChartQueryResult
    {
        public double[] Timestamps { get; set; }
        public Dictionary<string, double[]> Values { get; set; }
    }
}
