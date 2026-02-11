using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Timer = System.Timers.Timer;

namespace FocusTracker.Core
{
    public class AppTracker
    {
        private Timer _timer;
        private string _lastApp = "";
        private bool _isIdle = false;

        // 🔔 EVENTS
        public event Action<string>? AppChanged;
        public event Action? IdleStarted;
        public event Action? IdleEnded;

        // ===== Windows APIs =====
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public AppTracker()
        {
            _timer = new Timer(1000); // 1 second
            _timer.Elapsed += OnTick;
        }

        public void Start()
        {
            _timer.Start();
        }

        private void OnTick(object? sender, ElapsedEventArgs e)
        {
            CheckActiveApp();
            CheckIdleState();
        }

        // 🪟 Foreground app
        private void CheckActiveApp()
        {
            var app = GetActiveAppName();
            if (app == null) return;

            if (app != _lastApp)
            {
                _lastApp = app;
                AppChanged?.Invoke(app);
            }
        }

        // 💤 Idle detection
        private void CheckIdleState()
        {
            var idleTime = GetIdleTime();

            if (!_isIdle && idleTime > TimeSpan.FromSeconds(10))
            {
                _isIdle = true;
                IdleStarted?.Invoke();
            }

            if (_isIdle && idleTime < TimeSpan.FromSeconds(2))
            {
                _isIdle = false;
                IdleEnded?.Invoke();
            }
        }

        private TimeSpan GetIdleTime()
        {
            var info = new LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            GetLastInputInfo(ref info);

            uint idleTicks = (uint)Environment.TickCount - info.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        private string? GetActiveAppName()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint pid);

            try
            {
                var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return null;
            }
        }
    }
}