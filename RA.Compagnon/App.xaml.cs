using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RA.Compagnon;

/// <summary>
/// Initialise l'application et applique le theme WPF-UI au demarrage.
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
    /// Applique le theme global avant l'affichage de la premiere fenetre.
    /// </summary>
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

        base.OnStartup(e);
        JournaliserDemarrage("OnStartup fin");
    }

    /// <summary>
    /// Libere le verrou d'instance unique a la fermeture de l'application.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutexInstanceUnique?.ReleaseMutex();
        }
        catch
        {
            // Le mutex a pu etre abandonne ou deja libere.
        }
        finally
        {
            _mutexInstanceUnique?.Dispose();
            _mutexInstanceUnique = null;
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Tente de remettre au premier plan l'instance deja ouverte de Compagnon.
    /// </summary>
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

    /// <summary>
    /// Ferme les anciennes instances sans fenetre qui bloquent le mutex sans proposer d'interface utilisable.
    /// </summary>
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
                catch
                {
                    // Ignore une ancienne instance recalcitrante.
                }
            }
        }
        catch
        {
            // Ne jamais empecher le demarrage pour un nettoyage opportuniste.
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
            // Ne jamais bloquer le demarrage pour un simple journal.
        }
    }
}

