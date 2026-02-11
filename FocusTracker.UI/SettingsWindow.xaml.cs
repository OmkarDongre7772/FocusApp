using FocusTracker.Core;
using System.Windows;

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
            var updated = new AppSettings
            {
                NudgesEnabled = NudgesCheckBox.IsChecked == true,
                FragmentationSensitivity = SensitivityCombo.SelectedIndex + 1,
                FocusPraiseMinutes = int.Parse(FocusMinutesBox.Text),
                MaxNotificationsPerHour = int.Parse(MaxNotifBox.Text),
                DefaultSnoozeMinutes = _settings.Current.DefaultSnoozeMinutes
            };

            _settings.Update(updated);
            Close();
        }
    }
}