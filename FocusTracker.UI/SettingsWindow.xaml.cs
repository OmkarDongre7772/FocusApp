using FocusTracker.Core;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace FocusTracker.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settings;

        public SettingsWindow(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = _settings.Current;

            NudgesCheckBox.IsChecked = s.NudgesEnabled;
            SensitivityCombo.SelectedIndex = s.FragmentationSensitivity - 1;
            FocusMinutesBox.Text = s.FocusPraiseMinutes.ToString();
            MaxNotifBox.Text = s.MaxNotificationsPerHour.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FocusMinutesBox.Text, out int focusMinutes) ||
                !int.TryParse(MaxNotifBox.Text, out int maxNotif))
            {
                MessageBox.Show("Please enter valid numbers.");
                return;
            }

            var updated = new AppSettings
            {
                NudgesEnabled = NudgesCheckBox.IsChecked == true,
                FragmentationSensitivity = SensitivityCombo.SelectedIndex + 1,
                FocusPraiseMinutes = focusMinutes,
                MaxNotificationsPerHour = maxNotif,
                DefaultSnoozeMinutes = _settings.Current.DefaultSnoozeMinutes
            };

            _settings.Update(updated);
            Close();
        }

    }
}