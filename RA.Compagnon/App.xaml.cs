using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Initialise l'application et applique le thème WPF-UI au démarrage.
/// </summary>
public partial class App : Application
{
    private const string NomMutexInstanceUnique = @"Local\RA.Compagnon.InstanceUnique";
    private const int SwRestore = 9;
    private static readonly bool ActiverJournalDemarrage = false;
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

    /// <summary>
    /// Applique le thème global avant l'affichage de la première fenêtre.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        JournaliserDemarrage("OnStartup début");

        _mutexInstanceUnique = new Mutex(true, NomMutexInstanceUnique, out bool premiereInstance);

        if (!premiereInstance)
        {
            JournaliserDemarrage("Instance existante détectée");
            ActiverInstanceExistanteSiPossible();
            Shutdown();
            return;
        }

        ApplicationThemeManager.Apply(
            ApplicationTheme.Dark,
            WindowBackdropType.Mica,
            updateAccent: true
        );

        base.OnStartup(e);
        JournaliserDemarrage("OnStartup fin");
    }

    /// <summary>
    /// Libère le verrou d'instance unique à la fermeture de l'application.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutexInstanceUnique?.ReleaseMutex();
        }
        catch
        {
            // Le mutex a pu être abandonné ou déjà libéré.
        }
        finally
        {
            _mutexInstanceUnique?.Dispose();
            _mutexInstanceUnique = null;
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Tente de remettre au premier plan l'instance déjà ouverte de Compagnon.
    /// </summary>
    private static void ActiverInstanceExistanteSiPossible()
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
                return;
            }

            ShowWindowAsync(processusExistant.MainWindowHandle, SwRestore);
            SetForegroundWindow(processusExistant.MainWindowHandle);
        }
        catch
        {
            // Ne jamais empêcher l'instance secondaire de se fermer proprement.
        }
    }

    internal static void JournaliserDemarrage(string message)
    {
        if (!ActiverJournalDemarrage)
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

            File.AppendAllText(
                CheminJournalDemarrage,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}"
            );
        }
        catch
        {
            // Ne jamais bloquer le démarrage pour un simple journal.
        }
    }
}
