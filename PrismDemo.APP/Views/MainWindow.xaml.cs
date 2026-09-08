using Prism.Regions;
using System.Windows;

namespace PrismDemo.APP.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(IRegionManager regionManager)
        {
            InitializeComponent();
            regionManager.RegisterViewWithRegion("ContentRegion", typeof(HomePage));
        }
    }
}