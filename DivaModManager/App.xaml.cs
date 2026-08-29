using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace DivaModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");

        private static void LogException(string source, Exception ex)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source})\n" +
                    $"Message: {ex?.Message}\n" +
                    $"Inner Exception: {ex?.InnerException}\n" +
                    $"Stack Trace:\n{ex?.StackTrace}\n" +
                    new string('-', 60) + "\n";
                File.AppendAllText(LogPath, entry);
            }
            catch { }
        }

        protected static bool AlreadyRunning()
        {
            bool running = false;
            try
            {
                // Getting collection of process  
                Process currentProcess = Process.GetCurrentProcess();

                // Check with other process already running   
                foreach (var p in Process.GetProcesses())
                {
                    if (p.Id != currentProcess.Id) // Check running process   
                    {
                        if (p.ProcessName.Equals(currentProcess.ProcessName) && p.MainModule.FileName.Equals(currentProcess.MainModule.FileName))
                        {
                            running = true;
                            break;
                        }
                    }
                }
            }
            catch { }
            return running;
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App arrancó (OnStartup)\n");

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += App_AppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;

            RegistryConfig.InstallGBHandler();
            MainWindow mw = new MainWindow();
            bool running = AlreadyRunning();
            if (!running)
                mw.Show();
            if (e.Args.Length > 1 && e.Args[0] == "-download")
                new ModDownloader().Download(e.Args[1], running);
            else if (running)
                MessageBox.Show("Diva Mod Manager is already running", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }
        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("DispatcherUnhandledException (hilo de UI)", e.Exception);

            MessageBox.Show($"Unhandled exception occured:\n{e.Exception.Message}\n\nInner Exception:\n{e.Exception.InnerException}" +
                $"\n\nStack Trace:\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK,
                             MessageBoxImage.Error);

            e.Handled = true;
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                ((MainWindow)Current.MainWindow).ModGrid.IsEnabled = true;
                ((MainWindow)Current.MainWindow).ConfigButton.IsEnabled = true;
                ((MainWindow)Current.MainWindow).LaunchButton.IsEnabled = true;
                ((MainWindow)Current.MainWindow).OpenModsButton.IsEnabled = true;
                ((MainWindow)Current.MainWindow).UpdateButton.IsEnabled = true;
                ((MainWindow)Current.MainWindow).GameBox.IsEnabled = true;
                ((MainWindow)Current.MainWindow).LoadoutBox.IsEnabled = true;
                ((MainWindow)Current.MainWindow).EditLoadoutsButton.IsEnabled = true;
                ((MainWindow)Current.MainWindow).DropBox.Visibility = Visibility.Collapsed;
            });
        }

        private static void App_AppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Excepción en un hilo que NO es el de UI (ej. Task.Run, async void, threadpool). El proceso va a morir igual,
            // pero al menos queda registrado el motivo antes de que termine.
            LogException("AppDomain.UnhandledException (hilo de fondo)", e.ExceptionObject as Exception);
        }

        private static void App_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("TaskScheduler.UnobservedTaskException (Task sin await)", e.Exception);
            e.SetObserved();
        }
    }
}
