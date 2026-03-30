using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
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

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Title))
        {
            return Task.CompletedTask;
        }

        DefinirTitreZoneJeu();
        _dernierTitreJeuApi = jeuSauvegarde.Title;
        _dernierIdentifiantJeuAvecInfos = jeuSauvegarde.Id;
        _dernierIdentifiantJeuAvecProgression = jeuSauvegarde.Id;
        DefinirTitreJeuEnCours(jeuSauvegarde.Title);
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
            string.IsNullOrWhiteSpace(jeuSauvegarde.ImageBoxArt)
                ? []
                : [new VisuelJeuEnCours("Jaquette", jeuSauvegarde.ImageBoxArt)]
        );

        GameInfoAndUserProgressV2 jeuLocalReconstruit = ConstruireJeuUtilisateurDepuisEtatLocal(
            jeuSauvegarde
        );
        _identifiantSuccesGrilleTemporaire = null;
        _identifiantSuccesGrilleEpingle = null;
        AppliquerMetaConsoleJeuEnCoursInitiale(jeuLocalReconstruit);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeuLocalReconstruit);
        DemarrerRestaurationSuccesSauvegardesEnArrierePlan(jeuSauvegarde.Id);
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
            || succesSauvegarde.Id != identifiantJeu
            || string.IsNullOrWhiteSpace(succesSauvegarde.Title)
        )
        {
            return;
        }

        _identifiantSuccesGrilleEpingle = succesSauvegarde.EstEpingleManuellement
            ? succesSauvegarde.AchievementId
            : null;
        TexteTitrePremierSuccesNonDebloque.Text = succesSauvegarde.Title;
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
        TexteFaisabilitePremierSuccesNonDebloque.Text = succesSauvegarde.DetailsFaisabilite;
        TexteFaisabilitePremierSuccesNonDebloque.Visibility = string.IsNullOrWhiteSpace(
            succesSauvegarde.DetailsFaisabilite
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

    private Task AppliquerDerniereListeSuccesSauvegardeeAsync(int identifiantJeu)
    {
        EtatListeSuccesAfficheeLocal? listeSauvegardee =
            _configurationConnexion.DerniereListeSuccesAffichee;

        if (listeSauvegardee is null || listeSauvegardee.Id != identifiantJeu)
        {
            return Task.CompletedTask;
        }

        GrilleTousSuccesJeuEnCours.Children.Clear();

        foreach (ElementListeSuccesAfficheLocal succesSauvegarde in listeSauvegardee.Achievements)
        {
            GrilleTousSuccesJeuEnCours.Children.Add(
                ConstruireBadgeGrilleSucces(
                    identifiantJeu,
                    new SuccesGrilleAffiche
                    {
                        IdentifiantSucces = succesSauvegarde.AchievementId,
                        Titre = succesSauvegarde.Title,
                        UrlBadge = succesSauvegarde.CheminImageBadge,
                        EstDebloque = !succesSauvegarde.CheminImageBadge.Contains(
                            "_lock",
                            StringComparison.OrdinalIgnoreCase
                        ),
                    }
                )
            );
        }

        RafraichirStyleBadgesGrilleSucces();
        MettreAJourDispositionGrilleTousSucces();
        PlanifierMiseAJourAnimationGrilleTousSucces();
        return Task.CompletedTask;
    }

    private Task SauvegarderDernierJeuAfficheAsync(
        GameInfoAndUserProgressV2 jeu,
        string detailsTempsJeu,
        string detailsEtatJeu
    )
    {
        _configurationConnexion.DernierJeuAffiche = new EtatJeuAfficheLocal
        {
            IdentifiantJeu = jeu.Id,
            EstJeuEnCours = string.Equals(
                TitreZoneJeuEnCours.Text,
                "Jeu en cours",
                StringComparison.Ordinal
            ),
            Titre = jeu.Title,
            Details = TexteDetailsJeuEnCours.Text,
            ResumeProgression = TexteResumeProgressionJeuEnCours.Text,
            PourcentageProgression = TextePourcentageJeuEnCours.Text,
            ValeurProgression = BarreProgressionJeuEnCours.Value,
            TempsJeuSousImage = detailsTempsJeu,
            EtatJeu = detailsEtatJeu,
            CheminImageBoite = jeu.ImageBoxArt,
            IdentifiantConsole = jeu.ConsoleId,
            DateSortie = jeu.Released,
            Genre = jeu.Genre,
            Developpeur = jeu.Developer,
        };
        GarantirCoherenceEtatPersistantJeu(jeu.Id);
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
            && _configurationConnexion.DernierSuccesAffiche.Id != identifiantJeu
        )
        {
            _configurationConnexion.DernierSuccesAffiche = null;
            _dernierSuccesAfficheModifie = true;
        }

        if (
            _configurationConnexion.DerniereListeSuccesAffichee is not null
            && _configurationConnexion.DerniereListeSuccesAffichee.Id != identifiantJeu
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

        return precedent.Id == courant.Id
            && precedent.AchievementId == courant.AchievementId
            && string.Equals(precedent.Title, courant.Title, StringComparison.Ordinal)
            && string.Equals(precedent.Description, courant.Description, StringComparison.Ordinal)
            && string.Equals(
                precedent.DetailsPoints,
                courant.DetailsPoints,
                StringComparison.Ordinal
            )
            && string.Equals(
                precedent.DetailsFaisabilite,
                courant.DetailsFaisabilite,
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
            precedent.Id != courant.Id
            || precedent.Achievements.Count != courant.Achievements.Count
        )
        {
            return false;
        }

        for (int index = 0; index < precedent.Achievements.Count; index++)
        {
            ElementListeSuccesAfficheLocal succesPrecedent = precedent.Achievements[index];
            ElementListeSuccesAfficheLocal succesCourant = courant.Achievements[index];

            if (
                !string.Equals(succesPrecedent.Title, succesCourant.Title, StringComparison.Ordinal)
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
            && precedent.Id == courant.Id
            && precedent.EstJeuEnCours == courant.EstJeuEnCours
            && precedent.Title == courant.Title
            && precedent.Details == courant.Details
            && precedent.ResumeProgression == courant.ResumeProgression
            && precedent.PourcentageProgression == courant.PourcentageProgression
            && Math.Abs(precedent.ValeurProgression - courant.ValeurProgression) < 0.01
            && precedent.TempsJeuSousImage == courant.TempsJeuSousImage
            && precedent.EtatJeu == courant.EtatJeu
            && precedent.ImageBoxArt == courant.ImageBoxArt
            && precedent.ConsoleId == courant.ConsoleId
            && precedent.Released == courant.Released
            && precedent.Genre == courant.Genre
            && precedent.Developer == courant.Developer;
    }

    private static GameInfoAndUserProgressV2 ConstruireJeuUtilisateurDepuisEtatLocal(
        EtatJeuAfficheLocal jeuSauvegarde
    )
    {
        return new GameInfoAndUserProgressV2
        {
            IdentifiantJeu = jeuSauvegarde.Id,
            Titre = jeuSauvegarde.Title,
            IdentifiantConsole = jeuSauvegarde.ConsoleId,
            DateSortie = jeuSauvegarde.Released,
            Genre = jeuSauvegarde.Genre,
            Developpeur = jeuSauvegarde.Developer,
            CheminImageBoite = jeuSauvegarde.ImageBoxArt,
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
