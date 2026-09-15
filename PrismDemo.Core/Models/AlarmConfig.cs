using Prism.Mvvm;
using System;

namespace PrismDemo.Core.Models
{
    public class AlarmConfig : BindableBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _connectname;
        public string ConnectName
        {
            get => _connectname;
            set => SetProperty(ref _connectname, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _unit;
        public string Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        private decimal? _upperLimit;
        public decimal? UpperLimit
        {
            get => _upperLimit;
            set => SetProperty(ref _upperLimit, value);
        }

        private string _upperLimitMessage;
        public string UpperLimitMessage
        {
            get => _upperLimitMessage;
            set => SetProperty(ref _upperLimitMessage, value);
        }

        private decimal? _upperAllowCount;
        public decimal? UpperAllowCount
        {
            get => _upperAllowCount;
            set => SetProperty(ref _upperAllowCount, value);
        }

        private bool? _upperLimitTrigger;
        public bool? UpperLimitTrigger
        {
            get => _upperLimitTrigger;
            set => SetProperty(ref _upperLimitTrigger, value);
        }

        private decimal? _lowerLimit;
        public decimal? LowerLimit
        {
            get => _lowerLimit;
            set => SetProperty(ref _lowerLimit, value);
        }

        private string _lowerLimitMessage;
        public string LowerLimitMessage
        {
            get => _lowerLimitMessage;
            set => SetProperty(ref _lowerLimitMessage, value);
        }

        private decimal? _lowerAllowCount;
        public decimal? LowerAllowCount
        {
            get => _lowerAllowCount;
            set => SetProperty(ref _lowerAllowCount, value);
        }

        private bool? _lowerLimitTrigger;
        public bool? LowerLimitTrigger
        {
            get => _lowerLimitTrigger;
            set => SetProperty(ref _lowerLimitTrigger, value);
        }

        private decimal? _mutationThreshold;
        public decimal? MutationThreshold
        {
            get => _mutationThreshold;
            set => SetProperty(ref _mutationThreshold, value);
        }

        private string _mutationThresholdMessage;
        public string MutationThresholdMessage
        {
            get => _mutationThresholdMessage;
            set => SetProperty(ref _mutationThresholdMessage, value);
        }

        private decimal? _mutationAllowCount;
        public decimal? MutationAllowCount
        {
            get => _mutationAllowCount;
            set => SetProperty(ref _mutationAllowCount, value);
        }

        private bool? _mutationThresholdTrigger;
        public bool? MutationThresholdTrigger
        {
            get => _mutationThresholdTrigger;
            set => SetProperty(ref _mutationThresholdTrigger, value);
        }

        private string _unchangedThresholdMessage;
        public string UnchangedThresholdMessage
        {
            get => _unchangedThresholdMessage;
            set => SetProperty(ref _unchangedThresholdMessage, value);
        }

        private decimal? _unchangedAllowCount;
        public decimal? UnchangedAllowCount
        {
            get => _unchangedAllowCount;
            set => SetProperty(ref _unchangedAllowCount, value);
        }

        private bool? _unchangedThresholdTrigger;
        public bool? UnchangedThresholdTrigger
        {
            get => _unchangedThresholdTrigger;
            set => SetProperty(ref _unchangedThresholdTrigger, value);
        }

        private int _alarmLevel;
        public int AlarmLevel
        {
            get => _alarmLevel;
            set => SetProperty(ref _alarmLevel, value);
        }

        private int _isEnabled;
        public int IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private DateTime _updateTime = DateTime.Now;
        public DateTime UpdateTime
        {
            get => _updateTime;
            set => SetProperty(ref _updateTime, value);
        }
    }
}
