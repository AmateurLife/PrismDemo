using System;
using System.Windows.Controls;

namespace PrismDemo.A.Views
{
    public partial class HistoryChart3 : UserControl
    {
        public HistoryChart3()
        {
            InitializeComponent();
            Unloaded += (s, e) =>
            {
                try { (Chart as IDisposable)?.Dispose(); } catch { }
            };
        }
    }
}
