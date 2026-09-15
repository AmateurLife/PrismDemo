using System.Windows;

namespace PrismDemo.Core.Views
{
    /// <summary>
    /// 数值输入对话框（2026-04-07 从 APP 项目移至 Core 以便跨模块复用）
    /// </summary>
    public partial class InputDialog : Window
    {
        /// <summary>
        /// 用户输入的数值
        /// </summary>
        public string InputValue => InputTextBox.Text;

        public InputDialog(string title, string currentValue)
        {
            InitializeComponent();
            TitleText.Text = title;
            InputTextBox.Text = currentValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
