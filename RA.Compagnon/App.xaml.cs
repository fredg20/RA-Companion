using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using RA.Compagnon.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RA.Compagnon;

public partial class App : Application
{
    private const string NomMutexInstanceUnique = @"Local\RA.Compagnon.InstanceUnique";
    private const int SwRestore = 9;
    private static readonly string CheminJournalDemarrage = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-demarrage.log"
    );
    private static Mutex? _mutexInstanceUnique;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        JournaliserDemarrage("OnStartup debut");

        _mutexInstanceUnique = new Mutex(true, NomMutexInstanceUnique, out bool premiereInstance);

        if (!premiereInstance)
        {
            JournaliserDemarrage("Instance existante detectee");
            if (ActiverInstanceExistanteSiPossible())
            {
                Shutdown();
                return;
            }

            NettoyerInstancesFantomes();
            _mutexInstanceUnique.Dispose();
            _mutexInstanceUnique = null;
            _mutexInstanceUnique = new Mutex(true, NomMutexInstanceUnique, out premiereInstance);

            if (!premiereInstance)
            {
                JournaliserDemarrage("Impossible de reprendre la main apres nettoyage");
                Shutdown();
                return;
            }

            JournaliserDemarrage("Instance fantome nettoyee, poursuite du demarrage");
        }

        ApplicationThemeManager.Apply(
            ApplicationTheme.Dark,
            WindowBackdropType.Mica,
            updateAccent: true
        );

        ServiceSondeLocaleEmulateurs.ReinitialiserJournalSession();
        ServiceSondeRichPresence.ReinitialiserJournalSession();
        ServiceResolutionJeuLocal.ReinitialiserJournalSession();
        ServiceDetectionSuccesJeu.ReinitialiserJournalSession();
        RA.Compagnon.MainWindow.ReinitialiserJournalDiagnosticPerformance();

        base.OnStartup(e);
        JournaliserDemarrage("OnStartup fin");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutexInstanceUnique?.ReleaseMutex();
        }
        catch { }
        finally
        {
            _mutexInstanceUnique?.Dispose();
            _mutexInstanceUnique = null;
        }

        base.OnExit(e);
    }

    private static bool ActiverInstanceExistanteSiPossible()
    {
        try
        {
            Process processusCourant = Process.GetCurrentProcess();
            Process? processusExistant = Process
                .GetProcessesByName(processusCourant.ProcessName)
                .FirstOrDefault(processus =>
                    processus.Id != processusCourant.Id && processus.MainWindowHandle != IntPtr.Zero
                );

            if (processusExistant is null)
            {
                return false;
            }

            ShowWindowAsync(processusExistant.MainWindowHandle, SwRestore);
            SetForegroundWindow(processusExistant.MainWindowHandle);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void NettoyerInstancesFantomes()
    {
        try
        {
            Process processusCourant = Process.GetCurrentProcess();
            Process[] processusCompagnon = Process.GetProcessesByName(processusCourant.ProcessName);

            foreach (Process processus in processusCompagnon)
            {
                try
                {
                    if (processus.Id == processusCourant.Id)
                    {
                        continue;
                    }

                    if (processus.MainWindowHandle != IntPtr.Zero)
                    {
                        continue;
                    }

                    processus.Kill();
                    processus.WaitForExit(2000);
                }
                catch { }
            }
        }
        catch { }
    }

    internal static void JournaliserDemarrage(string message)
    {
        if (!ServiceModeDiagnostic.EstActif)
        {
            return;
        }

        try
        {
            string? repertoire = Path.GetDirectoryName(CheminJournalDemarrage);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            ServiceModeDiagnostic.JournaliserLigne(
                CheminJournalDemarrage,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}"
            );
        }
        catch { }
    }
}
