using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PrismDemo.B.Interfaces
{
    public interface IBService : INotifyPropertyChanged
    {
        string ModuleName { get; }

        ObservableCollection<Models.SampleEntry> Entries { get; }

        double CurrentValue { get; set; }

        void Start();

        void Stop();
    }
}