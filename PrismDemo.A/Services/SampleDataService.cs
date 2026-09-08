using System;

namespace PrismDemo.A.Services
{
    /// <summary>
    /// 假数据源服务（框架演示版）。
    /// 真实工程中对应 ClDataService（含数据库表结构和采集），
    /// Demo 仅生成随机示例数据以验证框架数据流。
    /// </summary>
    public class SampleDataService
    {
        private readonly Random _random = new();

        public double NextValue(double min, double max)
            => min + _random.NextDouble() * (max - min);
    }
}