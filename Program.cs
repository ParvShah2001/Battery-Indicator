using System;
using System.IO;
using System.Windows.Forms;
using BatteryIndicator.Services;

namespace BatteryIndicator;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Initialize Telemetry-Free Local Crash Reporting
        CrashReporter.Initialize();

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new BatteryIndicatorApplicationContext());
        }
        catch (Exception ex)
        {
            CrashReporter.LogCrash(ex, "Main Process Unhandled Exception", isFatal: true);
        }
    }    
}