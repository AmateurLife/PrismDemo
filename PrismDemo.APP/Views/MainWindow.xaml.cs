using System.Windows;
using Prism.Regions;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IRegionManager _regionManager;
        public MainWindow(IRegionManager regionManager)
        {
            InitializeComponent();
            _regionManager = regionManager;
            _regionManager.RegisterViewWithRegion( "ContentRegion", typeof(HomePage));

            this.Closing += MainWindow_Closing; // 订阅关闭事件
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var result = ShowMessageBox("确定要退出应用程序吗？", "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; // 取消关闭操作
            }
            // 如果是 Yes，e.Cancel 默认为 false，窗口正常关闭
        }
    }
}
