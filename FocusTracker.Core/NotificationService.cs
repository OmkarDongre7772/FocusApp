using System.Windows.Forms;

namespace FocusTracker.Core
{
    public class NotificationService
    {
        private readonly NotifyIcon _notifyIcon;

        public NotificationService(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
        }

        public void Show(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(3000); // 3 seconds
        }
    }
}