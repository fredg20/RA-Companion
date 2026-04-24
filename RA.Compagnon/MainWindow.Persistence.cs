/*
 * Regroupe la restauration et la persistance de l'état visible du jeu courant,
 * du succès mis en avant et de la grille des succès afin de retrouver une
 * interface cohérente au démarrage comme après chaque mise à jour importante.
 */
using System.Windows;
using System.Windows.Media;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using SystemControls = System.Windows.Controls;

namespace RA.Compagnon;

/*
 * Porte la partie persistance de la fenêtre principale.
 * Ce partial restaure les derniers états connus et les resauvegarde
 * quand l'interface évolue.
 */
public partial class MainWindow
{
    /*
     * Réapplique le dernier jeu sauvegardé lorsqu'un état local valable
     * est disponible au démarrage.
     */
    private Task AppliquerDernierJeuSauvegardeAsync()
    {
        EtatJeuAfficheLocal? jeuSauvegarde = _configurationConnexion.DernierJeuAffiche;

        if (jeuSauvegarde is null || string.IsNullOrWhiteSpace(jeuSauvegarde.Title))
        {
            return Task.CompletedTask;
        }

        AppliquerEtatJeuSauvegarde(jeuSauvegarde);
        return Task.CompletedTask;
    }

    /*
     * Réinjecte dans l'interface l'état d'un jeu précédemment sauvegardé,
     * avec ses informations, son visuel et son contexte de succès.
     */
    private void AppliquerEtatJeuSauvegarde(EtatJeuAfficheLocal jeuSauvegarde)
    {
        DefinirTitreZoneJeu();
        _dernierTitreJeuApi = jeuSauvegarde.Title;
        _dernierIdentifiantJeuApi = jeuSauvegarde.Id;
        _dernierIdentifiantJeuAvecInfos = jeuSauvegarde.Id;
        _dernierIdentifiantJeuAvecProgression = jeuSauvegarde.Id;
        DefinirTitreJeuEnCours(jeuSauvegarde.Title);
        DefinirDetailsJeuEnCours(jeuSauvegarde.Details);
        DefinirTempsJeuSousImage(jeuSauvegarde.TempsJeuSousImage);
        DefinirEtatJeuDansProgression(jeuSauvegarde.EtatJeu);
        _vueModele.JeuCourant.Progression = NormaliserResumeProgressionSauvegarde(
            jeuSauvegarde.ResumeProgression
        );
        _vueModele.JeuCourant.Pourcentage = string.IsNullOrWhiteSpace(
            jeuSauvegarde.PourcentageProgression
        )
            ? "Progression du jeu indisponible"
            : jeuSauvegarde.PourcentageProgression;
        _vueModele.JeuCourant.ProgressionValeur = Math.Clamp(
            jeuSauvegarde.ValeurProgression,
            0,
            100
        );

        DefinirVisuelsJeuEnCours(
            string.IsNullOrWhiteSpace(jeuSauvegarde.ImageBoxArt)
                ? []
                : [new VisuelJeuEnCours("Jaquette", jeuSauvegarde.ImageBoxArt)]
        );
        MettreAJourActionRejouerJeuEnCours(jeuSauvegarde);

        GameInfoAndUserProgressV2 jeuLocalReconstruit = ConstruireJeuUtilisateurDepuisEtatLocal(
            jeuSauvegarde
        );
        _etatListeSuccesUi.IdentifiantSuccesTemporaire = null;
        _etatListeSuccesUi.IdentifiantSuccesEpingle = null;
        AppliquerMetaConsoleJeuEnCoursInitiale(jeuLocalReconstruit);
        DemarrerEnrichissementMetaConsoleJeuEnCours(jeuLocalReconstruit);
        DemarrerRestaurationSuccesSauvegardesEnArrierePlan(jeuSauvegarde.Id);
    }

    /*
     * Harmonise le résumé de progression restauré depuis le stockage local
     * pour qu'il reste lisible dans l'interface.
     */
    private static string NormaliserResumeProgressionSauvegarde(string? resumeProgression)
    {
        if (string.IsNullOrWhiteSpace(resumeProgression))
        {
            return "-- / -- succès";
        }

        string resumeNettoye = resumeProgression.Trim();

        return resumeNettoye.Contains("succès", StringComparison.CurrentCultureIgnoreCase)
            ? resumeNettoye
            : $"{resumeNettoye} succès";
    }

    /*
     * Lance en arrière-plan la restauration des succès sauvegardés afin
     * de ne pas bloquer le rendu principal du jeu.
     */
    private void DemarrerRestaurationSuccesSauvegardesEnArrierePlan(int identifiantJeu)
    {
        _ = RestaurerSuccesSauvegardesEnArrierePlanAsync(identifiantJeu);
    }

    /*
     * Restaure successivement le succès affiché, la grille sauvegardée puis,
     * si besoin, le premier succès encore non débloqué.
     */
    private async Task RestaurerSuccesSauvegardesEnArrierePlanAsync(int identifiantJeu)
    {
        try
        {
            bool succesRestaure = await AppliquerDernierSuccesSauvegardeAsync(identifiantJeu);
            await AppliquerDerniereListeSuccesSauvegardeeAsync(identifiantJeu);

            if (!succesRestaure)
            {
                await AppliquerPremierSuccesNonDebloqueSauvegardeAsync(identifiantJeu);
            }
        }
        catch (Exception exception)
        {
            JournaliserExceptionNonBloquante("restauration_succes_sauvegardes", exception);
        }
    }

    /*
     * Réapplique le dernier succès sauvegardé lorsqu'il correspond encore
     * au jeu courant et qu'il reste pertinent à afficher.
     */
    private async Task<bool> AppliquerDernierSuccesSauvegardeAsync(int identifiantJeu)
    {
        EtatSuccesAfficheLocal? succesSauvegarde = _configurationConnexion.DernierSuccesAffiche;

        if (
            succesSauvegarde is null
            || succesSauvegarde.Id != identifiantJeu
            || string.IsNullOrWhiteSpace(succesSauvegarde.Title)
        )
        {
            return false;
        }

        if (succesSauvegarde.EstEpingleManuellement)
        {
            ReinitialiserSelectionSuccesGrille();
            _configurationConnexion.DernierSuccesAffiche = null;
            _dernierSuccesAfficheModifie = true;
            return false;
        }

        ReinitialiserSelectionSuccesGrille();
        _vueModele.SuccesEnCours.Titre = succesSauvegarde.Title;
        _vueModele.SuccesEnCours.TitreVisible = true;
        _vueModele.SuccesEnCours.Description = succesSauvegarde.Description;
        _vueModele.SuccesEnCours.DescriptionVisible = !string.IsNullOrWhiteSpace(
            succesSauvegarde.Description
        );
        _vueModele.SuccesEnCours.DetailsPoints = succesSauvegarde.DetailsPoints;
        AppliquerStyleBadgeSuccesEnCours(
            DetailsPointsIndiquentHardcore(succesSauvegarde.DetailsPoints)
        );
        _vueModele.SuccesEnCours.DetailsPointsVisible = !string.IsNullOrWhiteSpace(
            succesSauvegarde.DetailsPoints
        );
        _vueModele.SuccesEnCours.DetailsFaisabilite = succesSauvegarde.DetailsFaisabilite;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = !string.IsNullOrWhiteSpace(
            succesSauvegarde.DetailsFaisabilite
        );
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = NormaliserToolTipFaisabilite(
            succesSauvegarde.ExplicationFaisabilite
        );
        _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
        _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
        _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
        if (!string.IsNullOrWhiteSpace(succesSauvegarde.CheminImageBadge))
        {
            ImageSource? imageSucces = await ChargerImageDistanteAsync(
                succesSauvegarde.CheminImageBadge
            );

            if (imageSucces is not null)
            {
                _vueModele.SuccesEnCours.Image = ConvertirImageEnNoirEtBlanc(imageSucces);
                _vueModele.SuccesEnCours.ImageOpacity = 0.58;
                _vueModele.SuccesEnCours.ImageVisible = true;
                _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
                _vueModele.SuccesEnCours.TexteVisuelVisible = false;
                AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
                return true;
            }
        }

        _vueModele.SuccesEnCours.Image = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        _vueModele.SuccesEnCours.ImageOpacity = 0.58;
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.TexteVisuel = succesSauvegarde.TexteVisuel;
        _vueModele.SuccesEnCours.TexteVisuelVisible = !string.IsNullOrWhiteSpace(
            succesSauvegarde.TexteVisuel
        );
        return true;
    }

    /*
     * Normalise les anciennes variantes d'infobulle de faisabilité pour
     * conserver un texte cohérent après restauration locale.
     */
    private static string NormaliserToolTipFaisabilite(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        const string prefixeAncien = "Succès obtenu par ";
        const string prefixeHardcore = "Succès obtenu hardcore : ";

        if (valeur.StartsWith(prefixeAncien, StringComparison.Ordinal))
        {
            string suiteAncienne = valeur[prefixeAncien.Length..].Trim();
            if (suiteAncienne.EndsWith('.'))
            {
                suiteAncienne = suiteAncienne[..^1];
            }

            return $"Succès obtenu : {suiteAncienne}";
        }

        if (valeur.StartsWith(prefixeHardcore, StringComparison.Ordinal))
        {
            string suiteHardcore = valeur[prefixeHardcore.Length..].Trim();
            if (suiteHardcore.EndsWith('.'))
            {
                suiteHardcore = suiteHardcore[..^1];
            }

            return $"Succès obtenu : {suiteHardcore}";
        }

        return valeur;
    }

    /*
     * Recharge la dernière grille de succès sauvegardée pour le jeu courant
     * afin de retrouver le même ordre et le même état visuel.
     */
    private Task AppliquerDerniereListeSuccesSauvegardeeAsync(int identifiantJeu)
    {
        EtatListeSuccesAfficheeLocal? listeSauvegardee =
            _configurationConnexion.DerniereListeSuccesAffichee;

        if (listeSauvegardee is null || listeSauvegardee.Id != identifiantJeu)
        {
            return Task.CompletedTask;
        }

        GrilleTousSuccesJeuEnCours.Children.Clear();
        _etatListeSuccesUi.SuccesPasses.Clear();
        _etatListeSuccesUi.SuccesPasses.AddRange(listeSauvegardee.SuccesPasses);

        foreach (ElementListeSuccesAfficheLocal succesSauvegarde in listeSauvegardee.Achievements)
        {
            GrilleTousSuccesJeuEnCours.Children.Add(
                ConstruireBadgeGrilleSucces(
                    identifiantJeu,
                    new SuccesGrilleAffiche
                    {
                        IdentifiantSucces = succesSauvegarde.AchievementId,
                        Titre = succesSauvegarde.Title,
                        ToolTip = succesSauvegarde.Title,
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
        _ = ExporterEtatObsAsync();
        return Task.CompletedTask;
    }

    /*
     * Affiche le premier succès encore verrouillé lorsque rien de plus précis
     * n'a pu être restauré depuis l'état local.
     */
    private async Task AppliquerPremierSuccesNonDebloqueSauvegardeAsync(int identifiantJeu)
    {
        EtatListeSuccesAfficheeLocal? listeSauvegardee =
            _configurationConnexion.DerniereListeSuccesAffichee;

        if (listeSauvegardee is null || listeSauvegardee.Id != identifiantJeu)
        {
            return;
        }

        ReinitialiserSelectionSuccesGrille();

        ElementListeSuccesAfficheLocal? premierSuccesNonDebloque = null;

        foreach (ElementListeSuccesAfficheLocal succes in listeSauvegardee.Achievements)
        {
            if (succes.CheminImageBadge.Contains("_lock", StringComparison.OrdinalIgnoreCase))
            {
                premierSuccesNonDebloque = succes;
                break;
            }
        }

        if (premierSuccesNonDebloque is null)
        {
            await AppliquerSuccesEnCoursAsync(identifiantJeu, null, false, false);
            return;
        }

        _versionAffichageSuccesEnCours++;
        _vueModele.SuccesEnCours.Titre = premierSuccesNonDebloque.Title;
        _vueModele.SuccesEnCours.TitreVisible = true;
        _vueModele.SuccesEnCours.Description = string.Empty;
        _vueModele.SuccesEnCours.DescriptionVisible = false;
        _vueModele.SuccesEnCours.DetailsPoints = string.Empty;
        _vueModele.SuccesEnCours.DetailsPointsVisible = false;
        _vueModele.SuccesEnCours.DetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.DetailsFaisabiliteVisible = false;
        _vueModele.SuccesEnCours.ToolTipDetailsFaisabilite = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteType = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteAncre = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteQuantite = string.Empty;
        _vueModele.SuccesEnCours.ToolTipGroupeDetecte = string.Empty;
        _vueModele.SuccesEnCours.GroupeDetecteVisible = false;
        _vueModele.SuccesEnCours.BadgesGroupeDetecte.Clear();
        MettreAJourNavigationSuccesEnCours(null);

        if (!string.IsNullOrWhiteSpace(premierSuccesNonDebloque.CheminImageBadge))
        {
            ImageSource? imageSucces = await ChargerImageDistanteAsync(
                premierSuccesNonDebloque.CheminImageBadge
            );

            if (imageSucces is not null)
            {
                _vueModele.SuccesEnCours.Image = ConvertirImageEnNoirEtBlanc(imageSucces);
                _vueModele.SuccesEnCours.ImageOpacity = 0.58;
                _vueModele.SuccesEnCours.ImageVisible = true;
                _vueModele.SuccesEnCours.TexteVisuel = string.Empty;
                _vueModele.SuccesEnCours.TexteVisuelVisible = false;
                AppliquerCoinsArrondisImagePremierSuccesNonDebloque();
                return;
            }
        }

        _vueModele.SuccesEnCours.Image = null;
        ImagePremierSuccesNonDebloque.Clip = null;
        _vueModele.SuccesEnCours.ImageOpacity = 0.58;
        _vueModele.SuccesEnCours.ImageVisible = false;
        _vueModele.SuccesEnCours.TexteVisuel = premierSuccesNonDebloque.Title;
        _vueModele.SuccesEnCours.TexteVisuelVisible = true;
    }

    /*
     * Sauvegarde l'état visible du dernier jeu affiché, y compris le contexte
     * nécessaire pour pouvoir relancer ce jeu plus tard.
     */
    private Task SauvegarderDernierJeuAfficheAsync(
        GameInfoAndUserProgressV2 jeu,
        string detailsTempsJeu,
        string detailsEtatJeu
    )
    {
        EtatJeuAfficheLocal? precedent = _configurationConnexion.DernierJeuAffiche;
        bool contexteRelanceCourantValide =
            _identifiantJeuRejouableCourant == jeu.Id
            && !string.IsNullOrWhiteSpace(_nomEmulateurRejouableCourant)
            && !string.IsNullOrWhiteSpace(_cheminEmulateurRejouableCourant)
            && !string.IsNullOrWhiteSpace(_cheminJeuRejouableCourant);
        _configurationConnexion.DernierJeuAffiche = new EtatJeuAfficheLocal
        {
            IdentifiantJeu = jeu.Id,
            EstJeuEnCours = EtatLocalJeuEstActif(),
            Titre = jeu.Title,
            Details = _vueModele.JeuCourant.Details,
            ResumeProgression = _vueModele.JeuCourant.Progression,
            PourcentageProgression = _vueModele.JeuCourant.Pourcentage,
            ValeurProgression = _vueModele.JeuCourant.ProgressionValeur,
            TempsJeuSousImage = detailsTempsJeu,
            EtatJeu = detailsEtatJeu,
            CheminImageBoite = jeu.ImageBoxArt,
            CheminImageIcone = ObtenirCheminImageIconePourEtatLocal(jeu.Id, precedent?.ImageIcon),
            CheminImageConsole = ObtenirCheminImageConsolePourEtatLocal(
                jeu.ConsoleId,
                precedent?.ImageConsole
            ),
            IdentifiantConsole = jeu.ConsoleId,
            DateSortie = jeu.Released,
            Genre = jeu.Genre,
            Developpeur = jeu.Developer,
            NomEmulateurRelance =
                contexteRelanceCourantValide ? _nomEmulateurRejouableCourant
                : precedent?.Id == jeu.Id ? precedent.NomEmulateurRelance
                : string.Empty,
            CheminExecutableEmulateur =
                contexteRelanceCourantValide ? _cheminEmulateurRejouableCourant
                : precedent?.Id == jeu.Id ? precedent.CheminExecutableEmulateur
                : string.Empty,
            CheminJeuLocal =
                contexteRelanceCourantValide ? _cheminJeuRejouableCourant
                : precedent?.Id == jeu.Id ? precedent.CheminJeuLocal
                : string.Empty,
        };

        HydraterActionRejouerDepuisSourcesLocalesActifRecemment(
            _configurationConnexion.DernierJeuAffiche
        );

        MettreAJourActionRejouerJeuEnCours(_configurationConnexion.DernierJeuAffiche);
        GarantirCoherenceEtatPersistantJeu(jeu.Id);
        _dernierJeuAfficheModifie = true;
        return Task.CompletedTask;
    }

    /*
     * Mémorise l'icône du jeu lorsqu'elle est disponible afin de la restaurer
     * plus tard pour l'overlay OBS et l'état local.
     */
    private string ObtenirCheminImageIconePourEtatLocal(
        int identifiantJeu,
        string? cheminIconePrecedent
    )
    {
        if (_dernieresDonneesJeuAffichees?.Jeu.Id == identifiantJeu)
        {
            if (!string.IsNullOrWhiteSpace(_dernieresDonneesJeuAffichees.DetailsEtendus?.ImageIcon))
            {
                return _dernieresDonneesJeuAffichees.DetailsEtendus.ImageIcon.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_dernieresDonneesJeuAffichees.Progression?.ImageIcon))
            {
                return _dernieresDonneesJeuAffichees.Progression.ImageIcon.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(cheminIconePrecedent)
            ? string.Empty
            : cheminIconePrecedent.Trim();
    }

    /*
     * Mémorise l'icône de la console à partir du cache de catalogue lorsqu'il
     * est déjà disponible, avec repli sur la valeur persistée précédente.
     */
    private string ObtenirCheminImageConsolePourEtatLocal(
        int identifiantConsole,
        string? cheminIconePrecedent
    )
    {
        string imageConsole = _serviceCatalogueRetroAchievements.ObtenirUrlIconeConsoleDepuisCache(
            identifiantConsole
        );

        if (!string.IsNullOrWhiteSpace(imageConsole))
        {
            return imageConsole.Trim();
        }

        return string.IsNullOrWhiteSpace(cheminIconePrecedent)
            ? string.Empty
            : cheminIconePrecedent.Trim();
    }

    /*
     * Sauvegarde le dernier succès affiché uniquement si son contenu a
     * réellement changé depuis le dernier état persistant connu.
     */
    private void SauvegarderDernierSuccesAffiche(EtatSuccesAfficheLocal nouvelEtat)
    {
        if (EtatSuccesAfficheEquivalent(_configurationConnexion.DernierSuccesAffiche, nouvelEtat))
        {
            return;
        }

        _configurationConnexion.DernierSuccesAffiche = nouvelEtat;
        _dernierSuccesAfficheModifie = true;
    }

    /*
     * Nettoie les états persistants qui ne correspondent plus au jeu courant
     * afin d'éviter les mélanges entre deux jeux différents.
     */
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

    /*
     * Sauvegarde la dernière grille affichée avec son ordre courant et les
     * succès passés, puis planifie sa persistance.
     */
    private void SauvegarderDerniereListeSuccesAffichee(
        int identifiantJeu,
        List<ElementListeSuccesAfficheLocal> succes
    )
    {
        EtatListeSuccesAfficheeLocal nouvelEtat = new()
        {
            IdentifiantJeu = identifiantJeu,
            Succes = succes,
            SuccesPasses = [.. _etatListeSuccesUi.SuccesPasses],
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

    /*
     * Persiste l'ordre des succès volontairement passés pour conserver
     * la même progression de lecture à la prochaine ouverture.
     */
    private void SauvegarderOrdreSuccesPasses(int identifiantJeu, List<int> succesPasses)
    {
        EtatListeSuccesAfficheeLocal etat =
            _configurationConnexion.DerniereListeSuccesAffichee is { } etatCourant
            && etatCourant.Id == identifiantJeu
                ? etatCourant
                : new EtatListeSuccesAfficheeLocal { IdentifiantJeu = identifiantJeu, Succes = [] };

        etat.SuccesPasses = [.. succesPasses];
        _configurationConnexion.DerniereListeSuccesAffichee = etat;
        _derniereListeSuccesAfficheeModifiee = true;
        _ = PersisterDernierJeuAfficheSiNecessaireAsync();
    }

    /*
     * Compare deux états de succès affiché afin d'éviter une écriture locale
     * inutile lorsque rien d'important n'a changé.
     */
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

    /*
     * Compare deux listes de succès persistées pour savoir si leur contenu
     * ou leur ordre ont réellement changé.
     */
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
            || precedent.SuccesPasses.Count != courant.SuccesPasses.Count
        )
        {
            return false;
        }

        for (int index = 0; index < precedent.SuccesPasses.Count; index++)
        {
            if (precedent.SuccesPasses[index] != courant.SuccesPasses[index])
            {
                return false;
            }
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

    /*
     * Reconstruit un objet jeu minimal à partir de l'état local sauvegardé
     * afin de réhydrater rapidement la carte du jeu courant.
     */
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

    /*
     * Écrit la configuration locale seulement si un des états suivis a changé,
     * puis réarme les drapeaux si l'écriture échoue.
     */
    private async Task PersisterDernierJeuAfficheSiNecessaireAsync()
    {
        if (
            !_dernierJeuAfficheModifie
            && !_dernierSuccesAfficheModifie
            && !_derniereListeSuccesAfficheeModifiee
            && !_modeAffichageSuccesModifie
        )
        {
            return;
        }

        _dernierJeuAfficheModifie = false;
        _dernierSuccesAfficheModifie = false;
        _derniereListeSuccesAfficheeModifiee = false;
        _modeAffichageSuccesModifie = false;

        try
        {
            await _serviceConfigurationLocale.SauvegarderEtatApplicationAsync(
                _configurationConnexion
            );
            await ExporterEtatObsAsync();
        }
        catch
        {
            _dernierJeuAfficheModifie = true;
            _dernierSuccesAfficheModifie = true;
            _derniereListeSuccesAfficheeModifiee = true;
            _modeAffichageSuccesModifie = true;
        }
    }
}
