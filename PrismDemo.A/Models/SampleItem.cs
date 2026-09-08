namespace PrismDemo.A.Models
{
    /// <summary>模块 A 的示例数据项（假数据占位，无业务含义）。</summary>
    public class SampleItem
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string Status => Value >= 50 ? "Normal" : "Low";
    }
}