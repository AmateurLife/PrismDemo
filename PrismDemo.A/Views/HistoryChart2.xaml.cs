using System;
using System.Windows.Controls;

namespace PrismDemo.A.Views
{
    public partial class HistoryChart2 : UserControl
    {
        public HistoryChart2()
        {
            InitializeComponent();
            Unloaded += (s, e) =>
            {
                try { (Chart as IDisposable)?.Dispose(); } catch { }
            };
        }
    }
}
