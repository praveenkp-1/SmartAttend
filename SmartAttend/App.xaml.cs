using System.IO;
using System.Windows;
using SmartAttend.Database;
using SmartAttend.Services;
using QuestPDF.Infrastructure;

namespace SmartAttend
{
    public partial class App : Application
    {
        private ApiServer? _server;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Catch any unhandled errors
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show(
                    ex.Exception.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ex.Handled = true;
            };

            // Initialize database
            DatabaseHelper.Initialize();

            // Start API server in background
            _server = new ApiServer();
            _server.OnAttendanceReceived += (message) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    // Refresh dashboard if open
                    RefreshDashboard();
                    System.Diagnostics.Debug.WriteLine(
                        "Attendance: " + message);
                });
            };
            _server.OnEnrollmentReceived += (message) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    // Refresh enrollment table automatically
                    RefreshEnrollment();
                    System.Diagnostics.Debug.WriteLine(
                        "Enrollment: " + message);
                });
            };
            Task.Run(() => _server.StartAsync());

            // Check if setup is complete
            if (!DatabaseHelper.IsSetupComplete())
            {
                // Always offer the choice: restore from a manually
                // selected backup file, or start fresh. No more
                // trying to auto-detect a backup folder up front.
                RestorePromptWindow prompt = new RestorePromptWindow();
                Current.MainWindow = prompt;
                prompt.Show();
            }
            else
            {
                string requireLogin =
                    DatabaseHelper.GetSetting("require_login");
                if (requireLogin == "true")
                {
                    LoginWindow login = new LoginWindow();
                    Current.MainWindow = login;
                    login.Show();
                }
                else
                {
                    MainWindow main = new MainWindow();
                    Current.MainWindow = main;
                    main.Show();
                }
            }

            QuestPDF.Settings.License = LicenseType.Community;
        }

        private void RefreshDashboard()
        {
            if (Current.MainWindow is MainWindow main)
            {
                if (main.MainContent.Content is
                    SmartAttend.View.DashboardView dashboard)
                    dashboard.LoadDashboard();
            }
        }

        private void RefreshEnrollment()
        {
            if (Current.MainWindow is MainWindow main)
            {
                if (main.MainContent.Content is
                    SmartAttend.View.EnrollmentView enrollment)
                    enrollment.RefreshEnrollmentStatus();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _server?.Stop();
            base.OnExit(e);
        }
    }
}
