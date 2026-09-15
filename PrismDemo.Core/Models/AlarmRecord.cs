using System;

namespace PrismDemo.Core.Models
{
    public class AlarmRecord
    {
        public int Id { get; set; }
        public int ConfigId { get; set; }
        public string ConfigType { get; set; }
        public DateTime AlarmTime { get; set; }
        public string DeviceName { get; set; }
        public string Value { get; set; }
        public string AlarmContent { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                ProcessStatus = value ? "已处理" : "未处理";
            }
        }

        public bool IsShowed { get; set; }
        public DateTime? ConfirmTime { get; set; }
        public string ProcessStatus { get; set; }
    }
}
