using System;
using System.Windows.Controls;

namespace PrismDemo.A.Views
{
    public partial class HistoryChart1 : UserControl
    {
        public HistoryChart1()
        {
            InitializeComponent();
            Unloaded += (s, e) =>
            {
                try { (Chart as IDisposable)?.Dispose(); } catch { }
            };
        }
    }
}
