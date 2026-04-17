using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using RA.Compagnon.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

/*
 * Définit le point d'entrée WPF de Compagnon, la politique d'instance unique
 * et les journaux de démarrage de l'application.
 */
namespace RA.Compagnon;

/*
 * Représente l'application WPF principale et orchestre son démarrage global.
 */
public partial class App : Application
{
    private const string NomMutexInstanceUnique = @"Local\RA.Compagnon.InstanceUnique";
    private const int SwRestore = 9;
    private static readonly string CheminJournalDemarrage =
        ServiceModeDiagnostic.ConstruireCheminJournal("journal-demarrage.log");
    private static Mutex? _mutexInstanceUnique;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    /*
     * Démarre l'application en imposant une instance unique, en préparant le
     * thème global et en réinitialisant les journaux de session.
     */
    protected override void OnStartup(StartupEventArgs e)
    {
        ServiceModeDiagnostic.MigrerJournauxExistants();
        JournaliserDemarrage("OnStartup debut");

        _mutexInstanceUnique = new Mutex(true, NomMutexInstanceUnique, out bool premiereInstance);

        if (!premiereInstance)
        {
            JournaliserDemarrage("Instance existante détectée");
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
                JournaliserDemarrage("Impossible de reprendre la main après nettoyage");
                Shutdown();
                return;
            }

            JournaliserDemarrage("Instance fantôme nettoyée, poursuite du démarrage");
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

    /*
     * Libère proprement le mutex d'instance unique à la fermeture.
     */
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

    /*
     * Tente de réactiver une instance déjà ouverte plutôt que d'en créer une
     * seconde.
     */
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

    /*
     * Nettoie d'éventuelles instances orphelines sans fenêtre principale.
     */
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

    /*
     * Écrit une ligne de diagnostic de démarrage lorsque le mode diagnostic
     * est actif.
     */
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
