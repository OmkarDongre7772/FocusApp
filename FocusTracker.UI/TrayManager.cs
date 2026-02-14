using System;
using System.Windows.Forms;
using Application = System.Windows.Application;
using FocusTracker.Core;


namespace FocusTracker.UI
{
    public class TrayManager
    {
        public NotifyIcon TrayIcon { get; }

        public TrayManager(
            Action onOpen,
            Action onExit,
            //Action<TimeSpan> onSnooze,
            Action onSettings,
            Action<TimeSpan> onFocusStart,
            Action onFocusStop)

        {
            TrayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(
                            System.IO.Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "Assets",
                                "FocusTrackerLogo.ico")),

                Text = "FocusTracker",
                Visible = true
            };

            var menu = new ContextMenuStrip();

            menu.Items.Add("Open", null, (s, e) => onOpen());
            menu.Items.Add("Settings", null, (s, e) => onSettings());
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("Start Focus (1 min)", null,
    (s, e) => onFocusStart(TimeSpan.FromMinutes(1)));

            menu.Items.Add("Start Focus (25 min)", null,
    (s, e) => onFocusStart(TimeSpan.FromMinutes(25)));

            menu.Items.Add("Start Focus (45 min)", null,
                (s, e) => onFocusStart(TimeSpan.FromMinutes(45)));

            menu.Items.Add("Start Focus (60 min)", null,
                (s, e) => onFocusStart(TimeSpan.FromMinutes(60)));

            menu.Items.Add("Stop Focus Mode", null,
                (s, e) => onFocusStop());

            menu.Items.Add(new ToolStripSeparator());

            //menu.Items.Add("Snooze 15 min", null,
            //    (s, e) => onSnooze(TimeSpan.FromMinutes(15)));

            //menu.Items.Add("Snooze 30 min", null,
            //    (s, e) => onSnooze(TimeSpan.FromMinutes(30)));

            //menu.Items.Add("Snooze 1 hour", null,
            //    (s, e) => onSnooze(TimeSpan.FromHours(1)));

            //menu.Items.Add(new ToolStripSeparator());


            menu.Items.Add("Exit", null, (s, e) => onExit());

            TrayIcon.ContextMenuStrip = menu;
            TrayIcon.DoubleClick += (s, e) => onOpen();
        }

        public void Dispose()
        {
            TrayIcon.Visible = false;
            TrayIcon.Dispose();
        }
    }
}