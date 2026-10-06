using System.Windows;
using System.Windows.Media;
using SmartAttend.ViewModels;

namespace SmartAttend
{
    public partial class ForgotPasswordWindow : Window
    {
        private readonly ForgotPasswordViewModel _viewModel;

        public ForgotPasswordWindow()
        {
            InitializeComponent();
            _viewModel = new ForgotPasswordViewModel();
            DataContext = _viewModel;
        }

        // ── Step 1: Verify username ────────────────────────
        // ── Step 2: Reset password  ────────────────────────
        private void BtnPrimary_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsUsernameVerified)
            {
                // Step 1 — verify username and reveal question
                bool found = _viewModel.VerifyUsername(
                    TxtUsername.Text.Trim());

                if (!found)
                {
                    ShowError(_viewModel.Message);
                    return;
                }

                // Reveal the question panel
                TxtQuestion.Text = _viewModel.SecurityQuestion;
                PanelQuestion.Visibility = Visibility.Visible;
                TxtUsername.IsEnabled = false;
                BtnPrimary.Content = "Reset Password";
                HideMessage();
            }
            else
            {
                // Step 2 — verify answer and save new password
                bool success = _viewModel.ResetPassword(
                    TxtAnswer.Text.Trim(),
                    TxtNewPassword.Password,
                    TxtConfirmPassword.Password);

                if (!success)
                {
                    ShowError(_viewModel.Message);
                    return;
                }

                ShowSuccess(_viewModel.Message);
                BtnPrimary.IsEnabled = false;

                // Wait 1.5 s then go back to login
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = System.TimeSpan.FromSeconds(1.5)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    OpenLogin();
                };
                timer.Start();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => OpenLogin();

        private void OpenLogin()
        {
            LoginWindow login = new LoginWindow();
            login.Show();
            this.Close();
        }

        // ── Helpers ────────────────────────────────────────
        private void ShowError(string message)
        {
            TxtMessage.Foreground = new SolidColorBrush(
                Color.FromRgb(192, 57, 43));        // red
            TxtMessage.Text = message;
            TxtMessage.Visibility = Visibility.Visible;
        }

        private void ShowSuccess(string message)
        {
            TxtMessage.Foreground = new SolidColorBrush(
                Color.FromRgb(39, 174, 96));         // green
            TxtMessage.Text = message;
            TxtMessage.Visibility = Visibility.Visible;
        }

        private void HideMessage()
        {
            TxtMessage.Visibility = Visibility.Collapsed;
        }
    }
}