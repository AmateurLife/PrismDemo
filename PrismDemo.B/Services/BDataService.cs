using System;

namespace PrismDemo.B.Services
{
    /// <summary>B 模块的假数据源（框架演示版）。</summary>
    public class BDataService
    {
        private readonly Random _random = new();

        public double NextProgress(double target)
            => Math.Clamp(target + (_random.NextDouble() - 0.5) * 20, 0, 100);
    }
}