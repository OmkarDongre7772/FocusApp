using FocusTracker.Core;
using System.Diagnostics;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace FocusTracker.UI
{
    public partial class LoginWindow : Window
    {
        private readonly IpcClient _ipc = new();

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Email and password required.");
                return;
            }

            Debug.WriteLine("Login Clicked=> email:"+ EmailBox.Text.Trim()+", password:"+ PasswordBox.Password +"..............");

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "Login",
                Username = EmailBox.Text.Trim(),
                Password = PasswordBox.Password,
                TeamId = string.IsNullOrWhiteSpace(TeamBox.Text)
                            ? null
                            : TeamBox.Text.Trim()
            });

            if (response?.Success == true)
            {
                MessageBox.Show("Login successful");
                Close();
            }
            else
            {
                MessageBox.Show(response?.Message ?? "Login failed");
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EmailBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Email and password required.");
                return;
            }

            var response = await _ipc.SendAsync(new IpcRequest
            {
                Command = "Register",
                Username = EmailBox.Text.Trim(),
                Password = PasswordBox.Password,
                TeamId = string.IsNullOrWhiteSpace(TeamBox.Text)
                            ? null
                            : TeamBox.Text.Trim()
            });

            if (response?.Success == true)
            {
                MessageBox.Show("Registration successful. You can now login.");
            }
            else
            {
                MessageBox.Show(response?.Message ?? "Registration failed");
            }
        }
    }
}
