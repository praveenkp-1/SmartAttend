using System.Windows;
using SmartAttend.ViewModels;

namespace SmartAttend
{
    public partial class ChangePasswordWindow : Window
    {
        private ChangePasswordViewModel _viewModel;

        public ChangePasswordWindow()
        {
            InitializeComponent();
            _viewModel = new ChangePasswordViewModel();
            DataContext = _viewModel;
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            bool success = _viewModel.ChangePassword(
                TxtCurretPassword.Password.Trim(),
                TxtPassword.Password.Trim(),
                TxtConfirmPassword.Password.Trim());

            if (!success)
            {
                TxtError.Text = _viewModel.ErrorMessage;
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            MessageBox.Show(
                "Password was changed.",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            this.Close();
        }
    }
}