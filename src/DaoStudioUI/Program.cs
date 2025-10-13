using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DaoStudioUI
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Configure Serilog
            ConfigureSerilog();

            // Start Avalonia application
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        private static void ConfigureSerilog()
        {
            string logDirectory;
            
            try
            {
                // First try to use a Log folder in the same directory as the executable
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                logDirectory = Path.Combine(exeDirectory, "Logs");
                
                // Test if we can write to this directory
                Directory.CreateDirectory(logDirectory);
                string testFile = Path.Combine(logDirectory, "0AD6EE26CFF4420A8F272B2D893C3B91.txt");
                File.WriteAllText(testFile, "Test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                // Fall back to LocalApplicationData if we can't write to the executable directory
                logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DaoStudio",
                    "Logs");
                
                // Ensure the log directory exists
                Directory.CreateDirectory(logDirectory);
                
                Log.Warning("Could not write to exe directory logs. Using local app data instead. Error: {Error}", ex.Message);
            }
            
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Information() 
#endif
                .WriteTo.File(
                    Path.Combine(logDirectory, "DaoStudio.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .Enrich.FromLogContext()
                .CreateLogger();
            
            Log.Information("DaoStudio starting up with log directory: {LogDirectory}", logDirectory);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
            
            SetupApp(builder);
            return builder;
        }

        //https://github.com/AvaloniaUI/Avalonia/issues/19679
        public static void SetupApp(AppBuilder builder)
        {
            // Fix drop shadow issue on Windows 10
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 22000))
            {
                Window.WindowStateProperty.Changed.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
                Control.LoadedEvent.AddClassHandler<Window>((w, _) => FixWindowFrameOnWin10(w));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        private static void FixWindowFrameOnWin10(Window w)
        {
            // Schedule the DWM frame extension to run in the next render frame
            // to ensure proper timing with the window initialization sequence
            Dispatcher.UIThread.Post(() =>
            {
                var platformHandle = w.TryGetPlatformHandle();
                if (platformHandle == null)
                    return;

                var margins = new MARGINS { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
                DwmExtendFrameIntoClientArea(platformHandle.Handle, ref margins);
            }, DispatcherPriority.Render);
        }

    }
}
