using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Etat;
using SystemControls = System.Windows.Controls;
using UiControls = Wpf.Ui.Controls;

/*
 * Regroupe la vérification, le téléchargement et l'installation de mises à
 * jour applicatives depuis la fenêtre principale.
 */
namespace RA.Compagnon;

/*
 * Porte la logique d'interface et d'action autour des mises à jour de
 * l'application Compagnon.
 */
public partial class MainWindow
{
    /*
     * Vérifie si une mise à jour doit être recherchée maintenant puis aligne
     * l'état de l'interface avec le résultat.
     */
    private async Task VerifierMiseAJourApplicationSiNecessaireAsync(bool forcer = false)
    {
        if (_verificationMiseAJourApplicationEnCours)
        {
            return;
        }

        if (
            !forcer
            && _etatMiseAJourApplication.Statut != StatutMiseAJourApplication.EnAttente
            && DateTimeOffset.UtcNow - _horodatageDerniereVerificationMiseAJourUtc
                < IntervalleRafraichissementMiseAJourApplication
        )
        {
            return;
        }

        _verificationMiseAJourApplicationEnCours = true;

        try
        {
            _etatMiseAJourApplication = await Services.ServiceMiseAJourApplication.VerifierAsync();
            _horodatageDerniereVerificationMiseAJourUtc = DateTimeOffset.UtcNow;

            string? versionDistante = _etatMiseAJourApplication.VersionDistante;
            string? cheminPackageExistant =
                Services.ServiceMiseAJourApplication.TrouverPackageTelechargeExistant(
                    _etatMiseAJourApplication
                );

            if (!string.IsNullOrWhiteSpace(cheminPackageExistant))
            {
                _versionMiseAJourTelechargee = versionDistante ?? string.Empty;
                _cheminFichierMiseAJourTelechargee = cheminPackageExistant;
            }
            else if (_versionMiseAJourTelechargee != (versionDistante ?? string.Empty))
            {
                _versionMiseAJourTelechargee = string.Empty;
                _cheminFichierMiseAJourTelechargee = null;
                _messageTelechargementMiseAJourApplication = string.Empty;
            }

            AppliquerEtatMiseAJourApplicationInterface();
        }
        catch
        {
            _etatMiseAJourApplication = new EtatMiseAJourApplication(
                Services.ServiceMiseAJourApplication.ObtenirVersionLocale(),
                null,
                null,
                null,
                null,
                StatutMiseAJourApplication.VerificationImpossible,
                "Impossible de v\u00E9rifier les mises \u00E0 jour pour le moment."
            );
            _horodatageDerniereVerificationMiseAJourUtc = DateTimeOffset.UtcNow;
            AppliquerEtatMiseAJourApplicationInterface();
        }
        finally
        {
            _verificationMiseAJourApplicationEnCours = false;
        }
    }

    /*
     * Répercute dans le ViewModel l'état courant de la mise à jour
     * applicative et des éventuels téléchargements en cours.
     */
    private void AppliquerEtatMiseAJourApplicationInterface()
    {
        if (_telechargementMiseAJourApplicationEnCours)
        {
            _vueModele.VisibiliteMiseAJourApplication = Visibility.Visible;
            _vueModele.MiseAJourApplicationActivee = false;
            _vueModele.LibelleMiseAJourApplication = "Téléchargement...";
            _vueModele.ToolTipMiseAJourApplication = "Téléchargement de la mise à jour en cours.";
            return;
        }

        if (_etatMiseAJourApplication.PeutTelecharger)
        {
            string versionDistante = string.IsNullOrWhiteSpace(
                _etatMiseAJourApplication.VersionDistante
            )
                ? "disponible"
                : _etatMiseAJourApplication.VersionDistante;

            _vueModele.VisibiliteMiseAJourApplication = Visibility.Visible;
            _vueModele.MiseAJourApplicationActivee = true;

            if (PaquetMiseAJourTelechargeDisponible())
            {
                _vueModele.LibelleMiseAJourApplication = "Installer";
                _vueModele.ToolTipMiseAJourApplication =
                    $"La version {versionDistante} est prête à être installée.";
                return;
            }

            _vueModele.LibelleMiseAJourApplication = "Mise à jour";
            _vueModele.ToolTipMiseAJourApplication = $"Télécharger la version {versionDistante}";
            return;
        }

        _vueModele.VisibiliteMiseAJourApplication = Visibility.Collapsed;
        _vueModele.MiseAJourApplicationActivee = false;
        _vueModele.ToolTipMiseAJourApplication = string.Empty;
    }

    /*
     * Exécute l'action principale de mise à jour, soit installation si le
     * paquet est déjà prêt, soit téléchargement sinon.
     */
    private async Task ExecuterActionMiseAJourApplicationAsync()
    {
        if (PaquetMiseAJourTelechargeDisponible())
        {
            InstallerMiseAJourTelechargee();
            return;
        }

        await TelechargerMiseAJourApplicationAsync();
    }

    /*
     * Construit le bloc d'aide dédié à la mise à jour applicative dans la
     * fenêtre d'assistance.
     */
    private SystemControls.Border ConstruireBlocAideMiseAJourApplication(
        IList<SystemControls.Expander>? sectionsAide = null
    )
    {
        SystemControls.TextBlock texteVersionLocale = new()
        {
            Margin = new Thickness(0, 0, 0, 4),
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.TextBlock texteEtat = new()
        {
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.TextBlock texteDate = new()
        {
            Margin = new Thickness(0, 0, 0, 4),
            Opacity = 0.72,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.TextBlock texteNotes = new()
        {
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0.76,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };

        SystemControls.TextBlock textePackage = new()
        {
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0.76,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };

        UiControls.Button boutonVerification = new()
        {
            Content = "V\u00E9rifier maintenant",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };

        UiControls.Button boutonAction = new()
        {
            Content = "T\u00E9l\u00E9charger la mise \u00E0 jour",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
            Visibility = Visibility.Collapsed,
        };

        UiControls.Button boutonZip = new()
        {
            Content = "Ouvrir le zip",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
            Visibility = Visibility.Collapsed,
        };

        UiControls.Button boutonDossier = new()
        {
            Content = "Ouvrir le dossier",
            Padding = _modaleAideCompacteCourante
                ? ConstantesDesign.PaddingBoutonActionCompact
                : new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 8),
            Visibility = Visibility.Collapsed,
        };

        void AppliquerEtat(EtatMiseAJourApplication etat)
        {
            texteVersionLocale.Text = $"Version actuelle : {etat.VersionLocale}";
            texteEtat.Text = string.IsNullOrWhiteSpace(_messageTelechargementMiseAJourApplication)
                ? etat.Message
                : $"{etat.Message}\n{_messageTelechargementMiseAJourApplication}";

            if (!string.IsNullOrWhiteSpace(etat.DatePublication))
            {
                texteDate.Text = $"Publication : {etat.DatePublication}";
                texteDate.Visibility = Visibility.Visible;
            }
            else
            {
                texteDate.Text = string.Empty;
                texteDate.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(etat.Notes))
            {
                texteNotes.Text = $"Notes de version :\n{etat.Notes}";
                texteNotes.Visibility = Visibility.Visible;
            }
            else
            {
                texteNotes.Text = string.Empty;
                texteNotes.Visibility = Visibility.Collapsed;
            }

            if (PaquetMiseAJourTelechargeDisponible())
            {
                textePackage.Text =
                    $"Package t\u00E9l\u00E9charg\u00E9 : {Path.GetFileName(_cheminFichierMiseAJourTelechargee)}\n{_cheminFichierMiseAJourTelechargee}";
                textePackage.Visibility = Visibility.Visible;
                boutonAction.Visibility = Visibility.Visible;
                boutonZip.Visibility = Visibility.Visible;
                boutonDossier.Visibility = Visibility.Visible;
                boutonAction.IsEnabled = !_telechargementMiseAJourApplicationEnCours;
                boutonZip.IsEnabled = !_telechargementMiseAJourApplicationEnCours;
                boutonDossier.IsEnabled = !_telechargementMiseAJourApplicationEnCours;
                boutonAction.Content = "Installer la mise \u00E0 jour";
            }
            else if (etat.PeutTelecharger)
            {
                textePackage.Text =
                    "La mise \u00E0 jour sera t\u00E9l\u00E9charg\u00E9e dans un dossier local temporaire.";
                textePackage.Visibility = Visibility.Visible;
                boutonAction.Visibility = Visibility.Visible;
                boutonZip.Visibility = Visibility.Collapsed;
                boutonDossier.Visibility = Visibility.Collapsed;
                boutonAction.IsEnabled = !_telechargementMiseAJourApplicationEnCours;
                boutonAction.Content = _telechargementMiseAJourApplicationEnCours
                    ? "T\u00E9l\u00E9chargement..."
                    : "T\u00E9l\u00E9charger la mise \u00E0 jour";
            }
            else
            {
                textePackage.Text = string.Empty;
                textePackage.Visibility = Visibility.Collapsed;
                boutonAction.Visibility = Visibility.Collapsed;
                boutonZip.Visibility = Visibility.Collapsed;
                boutonDossier.Visibility = Visibility.Collapsed;
            }

            AppliquerEtatMiseAJourApplicationInterface();
        }

        boutonVerification.Click += async (_, _) =>
        {
            boutonVerification.IsEnabled = false;
            boutonVerification.Content = "V\u00E9rification...";
            texteEtat.Text = "V\u00E9rification en cours...";

            try
            {
                await VerifierMiseAJourApplicationSiNecessaireAsync(true);
                AppliquerEtat(_etatMiseAJourApplication);
            }
            finally
            {
                boutonVerification.IsEnabled = true;
                boutonVerification.Content = "V\u00E9rifier maintenant";
            }
        };

        boutonAction.Click += async (_, _) =>
        {
            if (PaquetMiseAJourTelechargeDisponible())
            {
                InstallerMiseAJourTelechargee();
                return;
            }

            if (
                _telechargementMiseAJourApplicationEnCours
                || !_etatMiseAJourApplication.PeutTelecharger
            )
            {
                return;
            }

            boutonAction.IsEnabled = false;
            boutonAction.Content = "T\u00E9l\u00E9chargement...";

            try
            {
                await TelechargerMiseAJourApplicationAsync();
            }
            finally
            {
                AppliquerEtat(_etatMiseAJourApplication);
            }
        };

        boutonZip.Click += (_, _) =>
        {
            if (PaquetMiseAJourTelechargeDisponible())
            {
                OuvrirFichierExterne(_cheminFichierMiseAJourTelechargee);
            }
        };

        boutonDossier.Click += (_, _) =>
        {
            if (PaquetMiseAJourTelechargeDisponible())
            {
                OuvrirDossierContenant(_cheminFichierMiseAJourTelechargee);
            }
        };

        AppliquerEtat(_etatMiseAJourApplication);

        SystemControls.StackPanel pile = new()
        {
            Margin = new Thickness(0),
            Children =
            {
                new SystemControls.TextBlock
                {
                    Margin = new Thickness(0, 0, 0, _modaleAideCompacteCourante ? 6 : 8),
                    Opacity = 0.78,
                    Text =
                        "Cette section sert à vérifier la version actuelle, lire les notes publiées et ouvrir directement le package déjà téléchargé.",
                    TextWrapping = TextWrapping.Wrap,
                },
                texteVersionLocale,
                texteEtat,
                texteDate,
                texteNotes,
                textePackage,
                new SystemControls.WrapPanel
                {
                    Orientation = SystemControls.Orientation.Horizontal,
                    Children = { boutonVerification, boutonAction, boutonZip, boutonDossier },
                },
            },
        };

        return ConstruireSectionAideRabattable(
            "Mise à jour",
            pile,
            "Version actuelle, notes et actions de téléchargement.",
            false,
            null,
            sectionsAide,
            _modaleAideCompacteCourante
        );
    }

    /*
     * Télécharge le paquet de mise à jour depuis la source distante puis met
     * à jour l'interface et les messages de suivi.
     */
    private async Task TelechargerMiseAJourApplicationAsync()
    {
        if (
            _telechargementMiseAJourApplicationEnCours || !_etatMiseAJourApplication.PeutTelecharger
        )
        {
            return;
        }

        _telechargementMiseAJourApplicationEnCours = true;
        _messageTelechargementMiseAJourApplication = "T\u00E9l\u00E9chargement en cours...";
        AppliquerEtatMiseAJourApplicationInterface();

        try
        {
            ResultatTelechargementMiseAJourApplication resultat =
                await Services.ServiceMiseAJourApplication.TelechargerPackageAsync(
                    _etatMiseAJourApplication
                );

            _messageTelechargementMiseAJourApplication = resultat.Message;

            if (resultat.Reussi && !string.IsNullOrWhiteSpace(resultat.CheminFichier))
            {
                _cheminFichierMiseAJourTelechargee = resultat.CheminFichier;
                _versionMiseAJourTelechargee =
                    _etatMiseAJourApplication.VersionDistante ?? string.Empty;
                _messageTelechargementMiseAJourApplication =
                    $"{resultat.Message}\nTu peux maintenant l'installer, ouvrir le zip ou ouvrir le dossier quand tu veux.";
            }
            else if (!resultat.Reussi)
            {
                MessageBox.Show(
                    resultat.Message,
                    "Mise \u00E0 jour",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }
        catch
        {
            _messageTelechargementMiseAJourApplication =
                "Le t\u00E9l\u00E9chargement de la mise \u00E0 jour a \u00E9chou\u00E9.";
            MessageBox.Show(
                "Le t\u00E9l\u00E9chargement de la mise \u00E0 jour a \u00E9chou\u00E9.",
                "Mise \u00E0 jour",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        finally
        {
            _telechargementMiseAJourApplicationEnCours = false;
            AppliquerEtatMiseAJourApplicationInterface();
        }
    }

    /*
     * Indique si un paquet de mise à jour déjà téléchargé est toujours valide
     * pour la version distante actuellement connue.
     */
    private bool PaquetMiseAJourTelechargeDisponible()
    {
        return !string.IsNullOrWhiteSpace(_cheminFichierMiseAJourTelechargee)
            && File.Exists(_cheminFichierMiseAJourTelechargee)
            && _versionMiseAJourTelechargee
                == (_etatMiseAJourApplication.VersionDistante ?? string.Empty);
    }

    /*
     * Lance l'installation d'un paquet de mise à jour déjà téléchargé après
     * confirmation explicite de l'utilisateur.
     */
    private void InstallerMiseAJourTelechargee()
    {
        if (!PaquetMiseAJourTelechargeDisponible())
        {
            return;
        }

        string versionDistante = string.IsNullOrWhiteSpace(
            _etatMiseAJourApplication.VersionDistante
        )
            ? "disponible"
            : _etatMiseAJourApplication.VersionDistante;

        MessageBoxResult confirmation = MessageBox.Show(
            $"Compagnon va se fermer pour installer la version {versionDistante}, puis redémarrer automatiquement.\n\nContinuer ?",
            "Installer la mise à jour",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        ResultatLancementInstallationMiseAJourApplication resultat =
            Services.ServiceMiseAJourApplication.LancerInstallationPackage(
                _cheminFichierMiseAJourTelechargee!,
                _etatMiseAJourApplication.VersionDistante,
                Environment.ProcessId,
                Path.Combine(AppContext.BaseDirectory, "RA.Compagnon.exe")
            );

        if (!resultat.Reussi)
        {
            MessageBox.Show(
                resultat.Message,
                "Installer la mise à jour",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        _messageTelechargementMiseAJourApplication = resultat.Message;
        Application.Current.Shutdown();
    }

    /*
     * Ouvre l'explorateur sur le dossier contenant le paquet téléchargé.
     */
    private static void OuvrirDossierContenant(string? cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{cheminFichier}\"",
                    UseShellExecute = true,
                }
            );
        }
        catch
        {
            try
            {
                string? dossier = Path.GetDirectoryName(cheminFichier);

                if (!string.IsNullOrWhiteSpace(dossier))
                {
                    Process.Start(
                        new ProcessStartInfo { FileName = dossier, UseShellExecute = true }
                    );
                }
            }
            catch { }
        }
    }

    /*
     * Ouvre directement un fichier externe si possible, sinon son dossier.
     */
    private static void OuvrirFichierExterne(string? cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier) || !File.Exists(cheminFichier))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo { FileName = cheminFichier, UseShellExecute = true }
            );
        }
        catch
        {
            OuvrirDossierContenant(cheminFichier);
        }
    }
}
