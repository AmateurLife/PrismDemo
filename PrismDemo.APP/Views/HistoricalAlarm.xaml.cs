using PrismDemo.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PrismDemo.APP.Views
{
    /// <summary>
    /// HistoricalAlarm.xaml 的交互逻辑
    /// </summary>
    public partial class HistoricalAlarm : UserControl
    {
        public HistoricalAlarm()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is AlarmRecord record)
            {
                var viewModel = DataContext as ViewModels.HistoricalAlarmViewModel;
                viewModel?.ShowAlarmDetailCmd.Execute(record);
            }
        }
    }
}
