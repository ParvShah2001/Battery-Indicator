using System;
using System.IO;
using System.Windows.Forms;

namespace BatteryIndicator;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new BatteryIndicatorApplicationContext());
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
            MessageBox.Show(ex.ToString(), "Battery Indicator Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}