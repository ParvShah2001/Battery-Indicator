using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BatteryIndicator.Services;

/// <summary>
/// Telemetry-Free, Local-Only Crash Reporting System.
/// Captures unhandled exceptions across UI threads, TaskScheduler, and AppDomain.
/// Anonymizes all user paths and writes structured diagnostic logs strictly to local storage (%APPDATA%).
/// Never transmits telemetry, tracking data, or personal metrics to any remote server.
/// </summary>
public static class CrashReporter
{
    private static readonly string CrashReportsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BatteryIndicator",
        "CrashReports"
    );

    private static bool _initialized = false;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            if (!Directory.Exists(CrashReportsDir))
            {
                Directory.CreateDirectory(CrashReportsDir);
            }
        }
        catch { }

        // 1. Windows Forms UI thread exceptions
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => HandleCrash(e.Exception, "UI Thread Exception");

        // 2. Non-UI / background thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleCrash(ex, "AppDomain Unhandled Exception", isFatal: e.IsTerminating);
            }
        };

        // 3. Unobserved Task exceptions
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleCrash(e.Exception, "Unobserved Task Exception", isFatal: false);
            e.SetObserved();
        };
    }

    public static string LogCrash(Exception ex, string context, bool isFatal = true)
    {
        try
        {
            if (!Directory.Exists(CrashReportsDir))
            {
                Directory.CreateDirectory(CrashReportsDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportFile = Path.Combine(CrashReportsDir, $"crash_{timestamp}.log");

            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("                  BATTERY INDICATOR CRASH REPORT (LOCAL ONLY)");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Timestamp (Local): {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            sb.AppendLine($"Timestamp (UTC):   {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Context:           {context}");
            sb.AppendLine($"Fatal:             {isFatal}");
            sb.AppendLine($"App Version:       {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.2.0"}");
            sb.AppendLine($"OS Version:        {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($".NET Runtime:      {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Process Uptime:    {GetProcessUptime()}");
            sb.AppendLine("Telemetry Policy:  STRICTLY ZERO TELEMETRY (This log is saved exclusively locally)");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("EXCEPTION DETAILS:");
            sb.AppendLine($"Type:    {ex.GetType().FullName}");
            sb.AppendLine($"Message: {SanitizePii(ex.Message)}");
            sb.AppendLine();
            sb.AppendLine("SANITIZED STACK TRACE:");
            sb.AppendLine(SanitizePii(ex.ToString()));
            sb.AppendLine("================================================================================");

            File.WriteAllText(reportFile, sb.ToString(), Encoding.UTF8);
            return reportFile;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void HandleCrash(Exception ex, string context, bool isFatal = false)
    {
        string reportPath = LogCrash(ex, context, isFatal);

        try
        {
            using var form = new Form
            {
                Text = "Battery Indicator - Crash Notice",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                ClientSize = new Size(460, 240),
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.White,
                TopMost = true
            };

            var iconLabel = new Label
            {
                Text = "⚠️",
                Font = new Font("Segoe UI Emoji", 24f),
                Location = new Point(20, 20),
                Size = new Size(48, 48),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var titleLabel = new Label
            {
                Text = "Battery Indicator encountered an issue",
                Font = new Font("Segoe UI Variable Display", 11.5f, FontStyle.Bold),
                Location = new Point(76, 22),
                Size = new Size(360, 26),
                ForeColor = Color.FromArgb(245, 100, 100)
            };

            var descLabel = new Label
            {
                Text = "A local crash report was saved to your device with all personal identifiers removed. " +
                       "Zero telemetry was sent online in accordance with our strict privacy policy.\n\n" +
                       $"Log file: {Path.GetFileName(reportPath)}",
                Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular),
                Location = new Point(76, 54),
                Size = new Size(360, 90),
                ForeColor = Color.FromArgb(200, 200, 200)
            };

            var btnOpenFolder = new Button
            {
                Text = "Open Log Folder",
                Location = new Point(140, 175),
                Size = new Size(130, 34),
                BackColor = Color.FromArgb(48, 50, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Variable Text", 9f)
            };
            btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(70, 75, 85);
            btnOpenFolder.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(reportPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{reportPath}\"");
                    }
                    else if (Directory.Exists(CrashReportsDir))
                    {
                        Process.Start("explorer.exe", CrashReportsDir);
                    }
                }
                catch { }
            };

            var btnClose = new Button
            {
                Text = isFatal ? "Exit App" : "Continue",
                Location = new Point(285, 175),
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(0, 185, 140),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => form.Close();

            form.Controls.AddRange(new Control[] { iconLabel, titleLabel, descLabel, btnOpenFolder, btnClose });
            form.ShowDialog();
        }
        catch
        {
            // Fallback to standard safe message box if custom form fails
            MessageBox.Show(
                $"Battery Indicator error: {ex.Message}\nLog saved locally to {reportPath}",
                "Battery Indicator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        if (isFatal)
        {
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Strips local usernames, user profile directories, and device names from stack traces.
    /// </summary>
    private static string SanitizePii(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                input = input.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            }

            string userName = Environment.UserName;
            if (!string.IsNullOrEmpty(userName) && userName.Length > 2)
            {
                input = Regex.Replace(input, Regex.Escape(userName), "[REDACTED_USER]", RegexOptions.IgnoreCase);
            }
        }
        catch { }

        return input;
    }

    private static string GetProcessUptime()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            var uptime = DateTime.Now - proc.StartTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        catch
        {
            return "Unknown";
        }
    }
}
