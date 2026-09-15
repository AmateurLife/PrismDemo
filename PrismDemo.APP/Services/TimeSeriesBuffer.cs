using LiveChartsCore.Defaults;
using System.Collections.Generic;
using System.Linq;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 时间序列环形缓冲区，容量固定（默认60点=1小时）。
    /// 新数据 Push 时自动淘汰最旧数据。
    /// </summary>
    public class TimeSeriesBuffer
    {
        private readonly Queue<DateTimePoint> _queue = new();
        private readonly int _capacity;

        /// <summary>缓冲区当前存储的点数</summary>
        public int Count => _queue.Count;

        /// <summary>
        /// 创建指定容量的环形缓冲
        /// </summary>
        /// <param name="capacity">最大点数量，默认60（1小时，每分钟1点）</param>
        public TimeSeriesBuffer(int capacity = 60)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// 压入一个采样点，超出容量时自动淘汰最旧点
        /// </summary>
        /// <param name="time">采样时间</param>
        /// <param name="value">采样值</param>
        public void Push(System.DateTime time, double value)
        {
            _queue.Enqueue(new DateTimePoint(time, value));
            while (_queue.Count > _capacity)
                _queue.Dequeue();
        }

        /// <summary>获取当前所有数据点的快照（副本）</summary>
        public List<DateTimePoint> Snapshot() => _queue.ToList();

        /// <summary>获取最早的数据点，无数据返回 null</summary>
        public DateTimePoint? First() => _queue.Count > 0 ? _queue.Peek() : null;

        /// <summary>获取最新的数据点，无数据返回 null</summary>
        public DateTimePoint? Last() => _queue.Count > 0 ? _queue.Last() : null;

        /// <summary>清空缓冲区</summary>
        public void Clear() => _queue.Clear();
    }
}
