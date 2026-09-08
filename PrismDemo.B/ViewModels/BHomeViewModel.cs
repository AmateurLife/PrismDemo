using Prism.Commands;
using Prism.Mvvm;
using PrismDemo.B.Configuration;
using PrismDemo.B.Interfaces;
using System.Collections.ObjectModel;

namespace PrismDemo.B.ViewModels
{
    public class BHomeViewModel : BindableBase
    {
        private readonly IBService _service;

        public ObservableCollection<Models.SampleEntry> Entries => _service.Entries;

        public double Target => Config.Target;
        public int TickMs => Config.TickMs;

        public double CurrentValue => _service.CurrentValue;

        public DelegateCommand StartCommand { get; }
        public DelegateCommand StopCommand { get; }

        public BHomeViewModel(IBService service)
        {
            _service = service;
            _service.PropertyChanged += (_, e) => RaisePropertyChanged(nameof(CurrentValue));

            StartCommand = new DelegateCommand(_service.Start);
            StopCommand = new DelegateCommand(_service.Stop);
        }
    }
}