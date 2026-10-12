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
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BatteryIndicator");
                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                File.WriteAllText(Path.Combine(appData, "crash.log"), ex.ToString());
            }
            catch { }
            MessageBox.Show(ex.ToString(), "Battery Indicator Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}