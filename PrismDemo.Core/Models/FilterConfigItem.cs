using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PrismDemo.Core.Models
{
    public class FilterConfigItem : INotifyPropertyChanged
    {
        private string _name;
        private string _connectName;
        private bool _isStore;
        private bool _isFilter;
        private int? _filterLength;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ConnectName
        {
            get => _connectName;
            set
            {
                if (_connectName != value)
                {
                    _connectName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsStore
        {
            get => _isStore;
            set
            {
                if (_isStore != value)
                {
                    _isStore = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsFilter
        {
            get => _isFilter;
            set
            {
                if (_isFilter != value)
                {
                    _isFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        public int? FilterLength
        {
            get => _filterLength;
            set
            {
                if (_filterLength != value)
                {
                    _filterLength = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
