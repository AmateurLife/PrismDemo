using Prism.Commands;
using Prism.Mvvm;
using PrismDemo.A.Configuration;
using PrismDemo.A.Interfaces;
using System.Collections.ObjectModel;

namespace PrismDemo.A.ViewModels
{
    public class AHomeViewModel : BindableBase
    {
        private readonly IASampleService _service;

        public ObservableCollection<Models.SampleItem> Items => _service.Items;

        public double SampleUpper => Config.SampleUpper;
        public double SampleLower => Config.SampleLower;
        public int RefreshSeconds => Config.RefreshSeconds;

        public double LatestValue => _service.LatestValue;

        public DelegateCommand StartCommand { get; }
        public DelegateCommand StopCommand { get; }

        public AHomeViewModel(IASampleService service)
        {
            _service = service;
            _service.PropertyChanged += (_, e) => RaisePropertyChanged(nameof(LatestValue));

            StartCommand = new DelegateCommand(_service.Start);
            StopCommand = new DelegateCommand(_service.Stop);
        }
    }
}