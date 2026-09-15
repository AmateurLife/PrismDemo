using System.Windows;

namespace PrismDemo.APP.Views
{
    public partial class PasswordDialog : Window
    {
        private readonly string _correctPassword;

        public PasswordDialog(string correctPassword)
        {
            InitializeComponent();
            _correctPassword = correctPassword;
            Owner = Application.Current.MainWindow;
            PasswordBox.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordBox.Password == _correctPassword)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Visibility = Visibility.Visible;
                PasswordBox.Password = string.Empty;
                PasswordBox.Focus();
            }
        }

        private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ConfirmButton_Click(sender, e);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
