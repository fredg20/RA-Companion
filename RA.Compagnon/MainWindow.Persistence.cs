using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

public partial class MainWindow
{
    /// <summary>
    /// Réapplique le dernier jeu sauvegardé pour éviter une fenêtre vide au démarrage.
    /// </summary>
    private Task AppliquerDernierJeuSauvegardeAsync()
    {
        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Titre))
        {
            return Task.CompletedTask;
        }

        DefinirTitreZoneJeu(jeuSauvegarde.EstJeuEnCours);
        _dernierTitreJeuApi = jeuSauvegarde.Titre;
        _dernierIdentifiantJeuAvecInfos = jeuSauvegarde.IdentifiantJeu;
        _dernierIdentifiantJeuAvecProgression = jeuSauvegarde.IdentifiantJeu;
        DefinirTitreJeuEnCours(jeuSauvegarde.Titre);
        DefinirDetailsJeuEnCours(jeuSauvegarde.Details);
        DefinirTempsJeuSousImage(jeuSauvegarde.TempsJeuSousImage);
        DefinirEtatJeuDansProgression(jeuSauvegarde.EtatJeu);
        TexteResumeProgressionJeuEnCours.Text = string.IsNullOrWhiteSpace(
            jeuSauvegarde.ResumeProgression
        )
            ? "-- / --"
            : jeuSauvegarde.ResumeProgression;
        TextePourcentageJeuEnCours.Text = string.IsNullOrWhiteSpace(
            jeuSauvegarde.PourcentageProgression
        )
            ? "Progression du jeu indisponible"
            : jeuSauvegarde.PourcentageProgression;
        BarreProgressionJeuEnCours.Value = Math.Clamp(jeuSauvegarde.ValeurProgression, 0, 100);

        DefinirVisuelsJeuEnCours(
            string.IsNullOrWhiteSpace(jeuSauvegarde.CheminImageBoite)
                ? []
                : [new VisuelJeuEnCours("Jaquette", jeuSauvegarde.CheminImageBoite)]
        );

        JeuUtilisateurRetroAchievements jeuLocalReconstruit =
            ConstruireJeuUtilisateurDepuisEtatLocal(jeuSauvegarde);
        _identifiantSuccesGrilleTemporaire = null;
        _identifiantSuccesGrilleEpingle = null;
        AppliquerMetaConsoleJeuEnCoursInitiale(jeuLocalReconstruit);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeuLocalReconstruit);
        DemarrerRestaurationSuccesSauvegardesEnArrierePlan(jeuSauvegarde.IdentifiantJeu);
        return Task.CompletedTask;
    }

    private void DemarrerRestaurationSuccesSauvegardesEnArrierePlan(int identifiantJeu)
    {
        _ = RestaurerSuccesSauvegardesEnArrierePlanAsync(identifiantJeu);
    }

    private async Task RestaurerSuccesSauvegardesEnArrierePlanAsync(int identifiantJeu)
    {
        try
        {
            await AppliquerDernierSuccesSauvegardeAsync(identifiantJeu);
            await AppliquerDerniereListeSuccesSauvegardeeAsync(identifiantJeu);
        }
        catch { }
    }

    private async Task AppliquerDernierSuccesSauvegardeAsync(int identifiantJeu)
    {
        EtatSuccesAfficheLocal? succesSauvegarde = _configurationConnexion.DernierSuccesAffiche;

        if (
            succesSauvegarde is null
            || succesSauvegarde.IdentifiantJeu != identifiantJeu
            || string.IsNullOrWhiteSpace(succesSauvegarde.Titre)
        )
        {
            return;
        }

        _identifiantSuccesGrilleEpingle = succesSauvegarde.EstEpingleManuellement
            ? succesSauvegarde.IdentifiantSucces
            : null;
        TexteTitrePremierSuccesNonDebloque.Text = succesSauvegarde.Titre;
        TexteTitrePremierSuccesNonDebloque.Visibility = Visibility.Visible;
        TexteDescriptionPremierSuccesNonDebloque.Text = succesSauvegarde.Description;
        TexteDescriptionPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.Description
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        TextePointsPremierSuccesNonDebloque.Text = succesSauvegarde.DetailsPoints;
        TextePointsPremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.DetailsPoints
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(succesSauvegarde.CheminImageBadge))
        {
            ImageSource? imageSucces = await ChargerImageDistanteAsync(
                succesSauvegarde.CheminImageBadge
            );

            if (imageSucces is not null)
            {
                ImagePremierSuccesNonDebloque.Source = ConvertirImageEnNoirEtBlanc(imageSucces);
                ImagePremierSuccesNonDebloque.Visibility = Visibility.Visible;
                TexteImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
                AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
                return;
            }
        }

        ImagePremierSuccesNonDebloque.Source = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        ImagePremierSuccesNonDebloque.Visibility = Visibility.Collapsed;
        TexteImagePremierSuccesNonDebloque.Text = succesSauvegarde.TexteVisuel;
        TexteImagePremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.TexteVisuel
        )
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async Task AppliquerDerniereListeSuccesSauvegardeeAsync(int identifiantJeu)
    {
        EtatListeSuccesAfficheeLocal? listeSauvegardee =
            _configurationConnexion.DerniereListeSuccesAffichee;

        if (listeSauvegardee is null || listeSauvegardee.IdentifiantJeu != identifiantJeu)
        {
            return;
        }

        GrilleTousSuccesJeuEnCours.Children.Clear();

        SystemControls.Border[] badges = await Task.WhenAll(
            listeSauvegardee.Succes.Select(succesSauvegarde =>
                ConstruireBadgeGrilleSuccesAsync(
                    identifiantJeu,
                    new SuccesJeuUtilisateurRetroAchievements
                    {
                        IdentifiantSucces = succesSauvegarde.IdentifiantSucces,
                        Titre = succesSauvegarde.Titre,
                    },
                    succesSauvegarde.CheminImageBadge
                )
            )
        );

        foreach (SystemControls.Border badge in badges)
        {
            GrilleTousSuccesJeuEnCours.Children.Add(badge);
        }

        RafraichirStyleBadgesGrilleSucces();
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
    }

    private Task SauvegarderDernierJeuAfficheAsync(
        JeuUtilisateurRetroAchievements jeu,
        string detailsTempsJeu,
        string detailsEtatJeu
    )
    {
        _configurationConnexion.DernierJeuAffiche = new EtatJeuAfficheLocal
        {
            IdentifiantJeu = jeu.IdentifiantJeu,
            EstJeuEnCours = string.Equals(
                TitreZoneJeuEnCours.Text,
                "Jeu en cours",
                StringComparison.Ordinal
            ),
            Titre = jeu.Titre,
            Details = TexteDetailsJeuEnCours.Text,
            ResumeProgression = TexteResumeProgressionJeuEnCours.Text,
            PourcentageProgression = TextePourcentageJeuEnCours.Text,
            ValeurProgression = BarreProgressionJeuEnCours.Value,
            TempsJeuSousImage = detailsTempsJeu,
            EtatJeu = detailsEtatJeu,
            CheminImageBoite = jeu.CheminImageBoite,
            IdentifiantConsole = jeu.IdentifiantConsole,
            DateSortie = jeu.DateSortie,
            Genre = jeu.Genre,
            Developpeur = jeu.Developpeur,
        };
        GarantirCoherenceEtatPersistantJeu(jeu.IdentifiantJeu);
        _dernierJeuAfficheModifie = true;
        return Task.CompletedTask;
    }

    private void SauvegarderDernierSuccesAffiche(EtatSuccesAfficheLocal nouvelEtat)
    {
        if (EtatSuccesAfficheEquivalent(_configurationConnexion.DernierSuccesAffiche, nouvelEtat))
        {
            return;
        }

        _configurationConnexion.DernierSuccesAffiche = nouvelEtat;
        _dernierSuccesAfficheModifie = true;
    }

    private void GarantirCoherenceEtatPersistantJeu(int identifiantJeu)
    {
        if (
            _configurationConnexion.DernierSuccesAffiche is not null
            && _configurationConnexion.DernierSuccesAffiche.IdentifiantJeu != identifiantJeu
        )
        {
            _configurationConnexion.DernierSuccesAffiche = null;
            _dernierSuccesAfficheModifie = true;
        }

        if (
            _configurationConnexion.DerniereListeSuccesAffichee is not null
            && _configurationConnexion.DerniereListeSuccesAffichee.IdentifiantJeu != identifiantJeu
        )
        {
            _configurationConnexion.DerniereListeSuccesAffichee = null;
            _derniereListeSuccesAfficheeModifiee = true;
        }
    }

    private void SauvegarderDerniereListeSuccesAffichee(
        int identifiantJeu,
        List<ElementListeSuccesAfficheLocal> succes
    )
    {
        EtatListeSuccesAfficheeLocal nouvelEtat = new()
        {
            IdentifiantJeu = identifiantJeu,
            Succes = succes,
        };

        if (
            EtatListeSuccesAfficheeEquivalent(
                _configurationConnexion.DerniereListeSuccesAffichee,
                nouvelEtat
            )
        )
        {
            return;
        }

        _configurationConnexion.DerniereListeSuccesAffichee = nouvelEtat;
        _derniereListeSuccesAfficheeModifiee = true;
        _ = PersisterDernierJeuAfficheSiNecessaireAsync();
    }

    private static bool EtatSuccesAfficheEquivalent(
        EtatSuccesAfficheLocal? precedent,
        EtatSuccesAfficheLocal? courant
    )
    {
        if (precedent is null || courant is null)
        {
            return precedent is null && courant is null;
        }

        return precedent.IdentifiantJeu == courant.IdentifiantJeu
            && precedent.IdentifiantSucces == courant.IdentifiantSucces
            && string.Equals(precedent.Titre, courant.Titre, StringComparison.Ordinal)
            && string.Equals(precedent.Description, courant.Description, StringComparison.Ordinal)
            && string.Equals(
                precedent.DetailsPoints,
                courant.DetailsPoints,
                StringComparison.Ordinal
            )
            && precedent.EstEpingleManuellement == courant.EstEpingleManuellement
            && string.Equals(
                precedent.CheminImageBadge,
                courant.CheminImageBadge,
                StringComparison.Ordinal
            )
            && string.Equals(precedent.TexteVisuel, courant.TexteVisuel, StringComparison.Ordinal);
    }

    private static bool EtatListeSuccesAfficheeEquivalent(
        EtatListeSuccesAfficheeLocal? precedent,
        EtatListeSuccesAfficheeLocal? courant
    )
    {
        if (precedent is null || courant is null)
        {
            return precedent is null && courant is null;
        }

        if (
            precedent.IdentifiantJeu != courant.IdentifiantJeu
            || precedent.Succes.Count != courant.Succes.Count
        )
        {
            return false;
        }

        for (int index = 0; index < precedent.Succes.Count; index++)
        {
            ElementListeSuccesAfficheLocal succesPrecedent = precedent.Succes[index];
            ElementListeSuccesAfficheLocal succesCourant = courant.Succes[index];

            if (
                !string.Equals(succesPrecedent.Titre, succesCourant.Titre, StringComparison.Ordinal)
                || !string.Equals(
                    succesPrecedent.CheminImageBadge,
                    succesCourant.CheminImageBadge,
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool EtatJeuAfficheEquivalent(
        EtatJeuAfficheLocal? precedent,
        EtatJeuAfficheLocal courant
    )
    {
        if (precedent is null)
        {
            return false;
        }

        return precedent.SignatureLocale == courant.SignatureLocale
            && precedent.IdentifiantJeu == courant.IdentifiantJeu
            && precedent.EstJeuEnCours == courant.EstJeuEnCours
            && precedent.Titre == courant.Titre
            && precedent.Details == courant.Details
            && precedent.ResumeProgression == courant.ResumeProgression
            && precedent.PourcentageProgression == courant.PourcentageProgression
            && Math.Abs(precedent.ValeurProgression - courant.ValeurProgression) < 0.01
            && precedent.TempsJeuSousImage == courant.TempsJeuSousImage
            && precedent.EtatJeu == courant.EtatJeu
            && precedent.CheminImageBoite == courant.CheminImageBoite
            && precedent.IdentifiantConsole == courant.IdentifiantConsole
            && precedent.DateSortie == courant.DateSortie
            && precedent.Genre == courant.Genre
            && precedent.Developpeur == courant.Developpeur;
    }

    private static JeuUtilisateurRetroAchievements ConstruireJeuUtilisateurDepuisEtatLocal(
        EtatJeuAfficheLocal jeuSauvegarde
    )
    {
        return new JeuUtilisateurRetroAchievements
        {
            IdentifiantJeu = jeuSauvegarde.IdentifiantJeu,
            Titre = jeuSauvegarde.Titre,
            IdentifiantConsole = jeuSauvegarde.IdentifiantConsole,
            DateSortie = jeuSauvegarde.DateSortie,
            Genre = jeuSauvegarde.Genre,
            Developpeur = jeuSauvegarde.Developpeur,
            CheminImageBoite = jeuSauvegarde.CheminImageBoite,
        };
    }

    private async Task PersisterDernierJeuAfficheSiNecessaireAsync()
    {
        if (
            !_dernierJeuAfficheModifie
            && !_dernierSuccesAfficheModifie
            && !_derniereListeSuccesAfficheeModifiee
        )
        {
            return;
        }

        _dernierJeuAfficheModifie = false;
        _dernierSuccesAfficheModifie = false;
        _derniereListeSuccesAfficheeModifiee = false;

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
        }
        catch
        {
            _dernierJeuAfficheModifie = true;
            _dernierSuccesAfficheModifie = true;
            _derniereListeSuccesAfficheeModifiee = true;
        }
    }
}
