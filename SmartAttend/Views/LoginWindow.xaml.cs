using System.Windows;
using SmartAttend.Models;
using SmartAttend.ViewModels;

namespace SmartAttend
{
    public partial class LoginWindow : Window
    {
        private LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var login = new LoginModel
            {
                Username = TxtUsername.Text.Trim(),
                Password = TxtPassword.Password.Trim()
            };

            bool success = _viewModel.Login(login);

            if (!success)
            {
                TxtError.Text = _viewModel.ErrorMessage;
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }
        private void BtnForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            ForgotPasswordWindow forgot = new ForgotPasswordWindow();
            forgot.Show();
            this.Close();
        }
    }
}