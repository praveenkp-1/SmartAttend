using System.Windows;
using SmartAttend.Models;
using SmartAttend.ViewModels;
using System.Windows.Controls;

namespace SmartAttend
{
    public partial class SetupWindow : Window
    {
        private SetupViewModel _viewModel;

        public SetupWindow()
        {
            InitializeComponent();
            _viewModel = new SetupViewModel();
            DataContext = _viewModel;
        }
        private void BtnSetup_Click(object sender, RoutedEventArgs e)
        {
            var selectedQuestion =
                CmbSecurityQuestion.SelectedItem as ComboBoxItem;

            var setup = new SetupModel
            {
                CompanyName = TxtCompany.Text.Trim(),
                Username = TxtUsername.Text.Trim(),
                Password = TxtPassword.Password,
                ConfirmPassword = TxtConfirmPassword.Password,

                // NEW
                SecurityQuestion = selectedQuestion?.Content?.ToString() ?? "",
                SecurityAnswer = TxtSecurityAnswer.Text.Trim()
            };

            bool success = _viewModel.RunSetup(setup);
            if (!success)
            {
                TxtError.Text = _viewModel.ErrorMessage;
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            MessageBox.Show(
                $"Setup complete!\nWelcome to SmartAttend, {setup.CompanyName}!",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LoginWindow login = new LoginWindow();
            login.Show();
            this.Close();
        }


    }
}