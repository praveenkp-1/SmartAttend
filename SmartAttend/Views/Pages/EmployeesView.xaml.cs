using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmartAttend.Database;
using SmartAttend.Models;
using SmartAttend.ViewModels;

namespace SmartAttend.View
{
    public partial class EmployeesView : UserControl
    {
        private EmployeesViewModel _viewModel;
        private string _pendingActionUserId = "";
        private string _currentDrawerUserId = "";
        private bool _editFP = false;
        private bool _editRFID = false;
        private bool _editPIN = false;

        public EmployeesView()
        {
            InitializeComponent();
            _viewModel = new EmployeesViewModel();
            DataContext = _viewModel;

            Loaded += (s, e) =>
            {
                LoadDepartments();
                LoadEmployees();
            };
        }

        // ── Data loading ───────────────────────────────────────────────────────

        private void LoadDepartments()
        {
            CmbDepartment.Items.Clear();
            CmbDepartment.Items.Add(new ComboBoxItem { Content = "All Departments", IsSelected = true });

            EditDepartment.Items.Clear();

            foreach (var d in _viewModel.LoadDepartments())
            {
                CmbDepartment.Items.Add(new ComboBoxItem { Content = d });
                EditDepartment.Items.Add(new ComboBoxItem { Content = d });
            }

            CmbDepartment.SelectedIndex = 0;
        }

        private void LoadEmployees()
        {
            _viewModel.LoadEmployees();
            EmployeeGrid.ItemsSource = _viewModel.AllEmployees;
            UpdateChips();
        }

        // ── Toolbar filters ────────────────────────────────────────────────────

        private void Search_Changed(object sender, object e)
        {
            if (EmployeeGrid == null) return;

            string search = TxtSearch.Text.Trim();
            string dept = (CmbDepartment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Departments";
            string status = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Status";
            string authMethod = (CmbAuthMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Auth Methods";

            EmployeeGrid.ItemsSource = _viewModel.FilterEmployees(search, dept, status, authMethod);
        }

        // ── Stat chips ─────────────────────────────────────────────────────────

        private void UpdateChips()
        {
            var all = _viewModel.AllEmployees;
            TxtTotalCount.Text = all.Count.ToString();
            TxtActiveCount.Text = all.Count(e => e.Status == "Active").ToString();
            TxtInactiveCount.Text = all.Count(e => e.Status == "Inactive").ToString();
            TxtFpCount.Text = all.Count(e => e.HasFingerprint).ToString();
            TxtRfidCount.Text = all.Count(e => e.HasRFID).ToString();
        }

        private void ChipAll_Click(object sender, MouseButtonEventArgs e)
        {
            CmbStatus.SelectedIndex = 0;
            Search_Changed(sender, e);
        }

        private void ChipActive_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (ComboBoxItem item in CmbStatus.Items)
                if (item.Content?.ToString() == "Active") { item.IsSelected = true; break; }
            Search_Changed(sender, e);
        }

        private void ChipInactive_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (ComboBoxItem item in CmbStatus.Items)
                if (item.Content?.ToString() == "Inactive") { item.IsSelected = true; break; }
            Search_Changed(sender, e);
        }

        // ── Add Employee ───────────────────────────────────────────────────────

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateTo("Enrollment");
        }

        // ── Action buttons (inline + kebab) ────────────────────────────────────

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string userId = btn.Tag?.ToString() ?? "";
            if (!string.IsNullOrEmpty(userId)) OpenEditModal(userId);
        }

        private void Deactivate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string userId = btn.Tag?.ToString() ?? "";
            if (!string.IsNullOrEmpty(userId)) OpenDeactivateModal(userId);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string userId = btn.Tag?.ToString() ?? "";
            if (!string.IsNullOrEmpty(userId)) OpenDeleteModal(userId);
        }

        private void Kebab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var grid = btn.Parent as Grid;
            var popup = grid?.Children.OfType<Popup>().FirstOrDefault();
            if (popup != null) popup.IsOpen = true;
            e.Handled = true;
        }

        private void KebabViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            CloseAllPopups();
            OpenDrawer(btn.Tag?.ToString() ?? "");
        }

        private void KebabEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            CloseAllPopups();
            string userId = btn.Tag?.ToString() ?? "";
            if (!string.IsNullOrEmpty(userId)) OpenEditModal(userId);
        }

        private void KebabDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            CloseAllPopups();
            Delete_Click(sender, e);
        }

        private void CloseAllPopups()
        {
            foreach (var row in GetVisualChildren<DataGridRow>(EmployeeGrid))
                foreach (var popup in GetVisualChildren<Popup>(row))
                    popup.IsOpen = false;
        }

        // ── Deactivate modal ───────────────────────────────────────────────────

        private void OpenDeactivateModal(string userId)
        {
            var emp = _viewModel.AllEmployees.FirstOrDefault(x => x.UserId == userId);
            if (emp == null) return;

            _pendingActionUserId = userId;
            bool isActive = emp.Status == "Active";

            DeactivateModalTitle.Text = isActive ? "Deactivate Employee" : "Activate Employee";
            DeactivateModalSub.Text = isActive
                ? "This employee will lose system access."
                : "This employee will regain system access.";
            DeactivateModalBody.Text = isActive
                ? $"Are you sure you want to deactivate {emp.Name}? They will no longer be able to check in."
                : $"Are you sure you want to activate {emp.Name}?";
            DeactivateConfirmBtn.Content = isActive ? "Deactivate" : "Activate";

            DeactivateModal.Visibility = Visibility.Visible;
            DeleteModal.Visibility = Visibility.Collapsed;
            EditModal.Visibility = Visibility.Collapsed;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void DeactivateConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pendingActionUserId)) return;
            string targetUserId = _pendingActionUserId; // capture before CloseModal wipes it

            var emp = _viewModel.AllEmployees.FirstOrDefault(x => x.UserId == targetUserId);
            if (emp == null) return;

            string newStatus = emp.Status == "Active" ? "Inactive" : "Active";
            DatabaseHelper.UpdateEmployeeStatus(targetUserId, newStatus);

            CloseModal();
            LoadEmployees();

            if (_currentDrawerUserId == targetUserId)
                PopulateDrawerContent(_currentDrawerUserId);
        }

        // ── Delete modal ───────────────────────────────────────────────────────

        private void OpenDeleteModal(string userId)
        {
            var emp = _viewModel.AllEmployees.FirstOrDefault(x => x.UserId == userId);
            if (emp == null) return;

            _pendingActionUserId = userId;
            DeleteConfirmInput.Text = "";
            DeleteModalBody.Text =
                $"You are about to permanently delete {emp.Name} ({emp.UserId}). " +
                $"All attendance records for this employee will also be removed.";

            DeleteModal.Visibility = Visibility.Visible;
            DeactivateModal.Visibility = Visibility.Collapsed;
            EditModal.Visibility = Visibility.Collapsed;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void DeleteConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pendingActionUserId)) return;

            if (DeleteConfirmInput.Text.Trim() != _pendingActionUserId)
            {
                DeleteConfirmInput.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 90, 90));
                return;
            }

            DatabaseHelper.DeleteEmployee(_pendingActionUserId);

            if (_currentDrawerUserId == _pendingActionUserId)
            {
                DetailDrawer.BeginAnimation(Border.WidthProperty,
                    new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) });
                _currentDrawerUserId = "";
            }

            CloseModal();
            LoadEmployees();
        }

        // ── Edit modal ─────────────────────────────────────────────────────────

        private void OpenEditModal(string userId)
        {
            var emp = _viewModel.AllEmployees.FirstOrDefault(x => x.UserId == userId);
            if (emp == null) return;

            _pendingActionUserId = userId;

            // Header
            EditModalTitle.Text = "Edit Employee";
            EditModalSub.Text = "Update employee information and access settings";

            // Avatar bar
            EditModalAvatar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(emp.AvatarColor));
            EditModalInitials.Text = emp.Initials;
            EditModalInitials.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(emp.AvatarTextColor));
            EditModalName.Text = emp.Name;
            EditModalId.Text = emp.UserId;
            EditModalStatusText.Text = emp.Status;
            EditModalStatusBadge.Background = new SolidColorBrush(
                emp.Status == "Active"
                    ? (Color)ColorConverter.ConvertFromString("#E6FAF8")
                    : (Color)ColorConverter.ConvertFromString("#F0F2F5"));
            EditModalStatusText.Foreground = new SolidColorBrush(
                emp.Status == "Active"
                    ? (Color)ColorConverter.ConvertFromString("#007d6e")
                    : (Color)ColorConverter.ConvertFromString("#6b7a8d"));

            // Fields
            EditFullName.Text = emp.Name;
            EditEmpId.Text = emp.UserId;
            EditContact.Text = emp.Contact;
            EditPosition.Text = emp.Position;

            // Department — match by content
            EditDepartment.SelectedItem = null;
            foreach (ComboBoxItem item in EditDepartment.Items)
            {
                if (item.Content?.ToString() == emp.Department)
                {
                    EditDepartment.SelectedItem = item;
                    break;
                }
            }

            // Status — match by content
            EditStatus.SelectedItem = null;
            foreach (ComboBoxItem item in EditStatus.Items)
            {
                if (item.Content?.ToString() == emp.Status)
                {
                    EditStatus.SelectedItem = item;
                    break;
                }
            }

            // Join date
            if (DateTime.TryParse(emp.JoinDate, out DateTime jd))
                EditJoinDate.SelectedDate = jd;
            else
                EditJoinDate.SelectedDate = null;

            // Show modal
            EditModal.Visibility = Visibility.Visible;
            DeactivateModal.Visibility = Visibility.Collapsed;
            DeleteModal.Visibility = Visibility.Collapsed;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void EditModalClose_Click(object sender, RoutedEventArgs e)
        {
            EditModal.Visibility = Visibility.Collapsed;
            ModalOverlay.Visibility = Visibility.Collapsed;
            _pendingActionUserId = "";
        }

        private void EditModalSave_Click(object sender, RoutedEventArgs e)
        {
            string fullName = EditFullName.Text.Trim();
            string dept = (EditDepartment.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string status = (EditStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string contact = EditContact.Text.Trim();
            string position = EditPosition.Text.Trim();
            string joinDate = EditJoinDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "";

            // Validate required fields
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(dept))
            {
                if (string.IsNullOrEmpty(fullName))
                    EditFullName.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 90, 90));
                if (string.IsNullOrEmpty(dept))
                    // highlight the department border
                    foreach (var child in GetVisualChildren<Border>(EditDepartment))
                        child.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 90, 90));
                return;
            }

            DatabaseHelper.UpdateEmployee(
    _pendingActionUserId,
    fullName, dept, contact, status, joinDate, position);

            EditModal.Visibility = Visibility.Collapsed;
            ModalOverlay.Visibility = Visibility.Collapsed;

            LoadEmployees();

            if (_currentDrawerUserId == _pendingActionUserId)
                PopulateDrawerContent(_currentDrawerUserId);

            _pendingActionUserId = "";
        }

        // ── Shared modal close ─────────────────────────────────────────────────

        private void ModalCancel_Click(object sender, RoutedEventArgs e) => CloseModal();

        private void CloseModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            DeactivateModal.Visibility = Visibility.Collapsed;
            DeleteModal.Visibility = Visibility.Collapsed;
            EditModal.Visibility = Visibility.Collapsed;
            _pendingActionUserId = "";
        }

        // ── Detail drawer ──────────────────────────────────────────────────────

        private void PopulateDrawerContent(string userId)
        {
            var emp = _viewModel.AllEmployees.FirstOrDefault(x => x.UserId == userId);
            if (emp == null) return;

            _currentDrawerUserId = userId;
            DrawerName.Text = emp.Name;
            DrawerUserId.Text = emp.UserId;
            DrawerInitials.Text = emp.Initials;
            DrawerAvatar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(emp.AvatarColor));
            DrawerLiveChip.Visibility = emp.IsCheckedInToday ? Visibility.Visible : Visibility.Collapsed;
            DrawerDeactivateBtn.Content = emp.Status == "Active" ? "Deactivate" : "Activate";
            DrawerDeactivateBtn.Tag = emp.UserId;
            DrawerInfoRows.ItemsSource = new[]
            {
        new DrawerInfoRow { Label = "Department",      Value = emp.Department },
        new DrawerInfoRow { Label = "Position",        Value = emp.Position },
        new DrawerInfoRow { Label = "Contact",         Value = emp.Contact },
        new DrawerInfoRow { Label = "Status",          Value = emp.Status },
        new DrawerInfoRow { Label = "Join Date",       Value = emp.JoinDate },
        new DrawerInfoRow { Label = "Attendance Rate", Value = emp.AttendanceRateText },
        new DrawerInfoRow { Label = "Last Seen",       Value = emp.LastSeen },
        new DrawerInfoRow { Label = "Fingerprint",     Value = emp.HasFingerprint ? "✓ Enrolled"  : "Not enrolled" },
        new DrawerInfoRow { Label = "RFID Card",       Value = emp.HasRFID        ? "✓ Assigned"  : "Not assigned" },
        new DrawerInfoRow { Label = "PIN",             Value = emp.HasPIN         ? "✓ Set"       : "Not set" },
    };
            var history = DatabaseHelper.GetEmployeeAttendanceLast10Days(userId);

            // history[0] = 10 days ago ... history[last] = today (assumed chronological order)
            int count = history.Count;
            DateTime startDate = DateTime.Today.AddDays(-(count - 1));

            DrawerAttBars.ItemsSource = history.Select((present, i) =>
            {
                DateTime day = startDate.AddDays(i);
                return new DrawerAttBar
                {
                    BarHeight = present ? 28 : 10,
                    BarColor = present ? "#00C6AE" : "#FF5A5A",
                    DayLabel = day.ToString("ddd").Substring(0, 1) // single-letter: M, T, W...
                };
            });
        }

        private void OpenDrawer(string userId)
        {
            PopulateDrawerContent(userId);
            DetailDrawer.BeginAnimation(Border.WidthProperty, new DoubleAnimation
            {
                From = 0,
                To = 320,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void CloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            DetailDrawer.BeginAnimation(Border.WidthProperty, new DoubleAnimation
            {
                From = 320,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
            _currentDrawerUserId = "";
        }

        private void DrawerEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentDrawerUserId))
                OpenEditModal(_currentDrawerUserId);
        }

        private void EmployeeGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EmployeeGrid.SelectedItem is EmployeeRow row)
                OpenDrawer(row.UserId);
        }

        // ── Scroll helper ──────────────────────────────────────────────────────

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }

        // ── Visual tree helper ─────────────────────────────────────────────────

        private static IEnumerable<T> GetVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var desc in GetVisualChildren<T>(child))
                    yield return desc;
            }
        }

        // ── Inner classes ──────────────────────────────────────────────────────

        public class DrawerInfoRow
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public class DrawerAttBar
        {
            public double BarHeight { get; set; }
            public string BarColor { get; set; }
            public string DayLabel { get; set; }
        }
    }
}