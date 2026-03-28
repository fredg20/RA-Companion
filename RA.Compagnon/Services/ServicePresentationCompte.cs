using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Transforme les données de compte en contenu prêt à afficher dans l'interface.
/// </summary>
public sealed class ServicePresentationCompte
{
    private readonly ServiceSondeRichPresence _serviceSondeRichPresence = new();

    public CompteAffiche Construire(DonneesCompteUtilisateur donnees, string pseudoParDefaut)
    {
        string nomUtilisateur = donnees.Profil?.User?.Trim() ?? pseudoParDefaut.Trim();
        string titre = string.IsNullOrWhiteSpace(nomUtilisateur) ? "Compte" : nomUtilisateur;
        string devise = donnees.Profil?.Motto?.Trim() ?? string.Empty;
        string urlAvatar = ConstruireUrlAvatar(donnees.Profil?.UserPic);
        EtatRichPresence etatRichPresence = _serviceSondeRichPresence.Sonder(donnees);

        List<SectionInformationsAffichee> sections = [];

        List<LigneInformationAffichee> lignesCompte = [];
        AjouterLigneSiValeurUtile(
            lignesCompte,
            "Membre depuis",
            FormaterDateProfil(donnees.Profil?.MemberSince)
        );
        AjouterLigneSiValeurUtile(
            lignesCompte,
            "Dernier jeu joué",
            DeterminerDernierJeuAffiche(donnees)
        );

        if (lignesCompte.Count > 0)
        {
            sections.Add(
                new SectionInformationsAffichee { Titre = "Compte", Lignes = lignesCompte }
            );
        }

        List<LigneInformationAffichee> lignesProgression = [];
        AjouterLigneSiValeurUtile(
            lignesProgression,
            "Points hardcore",
            FormaterNombre(
                donnees.Points?.Points ?? donnees.Resume?.TotalPoints ?? donnees.Profil?.TotalPoints
            )
        );
        AjouterLigneSiValeurUtile(
            lignesProgression,
            "Points softcore",
            FormaterNombre(
                donnees.Points?.SoftcorePoints
                    ?? donnees.Resume?.TotalSoftcorePoints
                    ?? donnees.Profil?.TotalSoftcorePoints
            )
        );
        AjouterLigneSiValeurUtile(
            lignesProgression,
            "TruePoints",
            FormaterNombre(donnees.Resume?.TotalTruePoints ?? donnees.Profil?.TotalTruePoints)
        );
        AjouterLigneSiValeurUtile(
            lignesProgression,
            "Classement",
            ConstruireResumePositionCompte(donnees.Resume)
        );

        if (donnees.Progression is not null && donnees.Progression.Count > 0)
        {
            AjouterLigneSiValeurUtile(
                lignesProgression,
                "Jeux suivis",
                donnees.Progression.Count.ToString(CultureInfo.CurrentCulture)
            );
        }

        if (lignesProgression.Count > 0)
        {
            sections.Add(
                new SectionInformationsAffichee
                {
                    Titre = "Progression",
                    Lignes = lignesProgression,
                }
            );
        }

        List<LigneInformationAffichee> lignesRecompenses = [];

        if (donnees.Recompenses is not null)
        {
            int recompensesVisibles = Math.Max(
                donnees.Recompenses.VisibleUserAwards.Count,
                donnees.Recompenses.TotalAwardsCount - donnees.Recompenses.HiddenAwardsCount
            );

            AjouterLigneSiValeurUtile(
                lignesRecompenses,
                "Récompenses visibles",
                recompensesVisibles > 0
                    ? recompensesVisibles.ToString(CultureInfo.CurrentCulture)
                    : string.Empty
            );
            AjouterLigneSiValeurUtile(
                lignesRecompenses,
                "Maîtrises",
                donnees.Recompenses.MasteryAwardsCount > 0
                    ? donnees.Recompenses.MasteryAwardsCount.ToString(CultureInfo.CurrentCulture)
                    : string.Empty
            );
            AjouterLigneSiValeurUtile(
                lignesRecompenses,
                "Complétions",
                donnees.Recompenses.CompletionAwardsCount > 0
                    ? donnees.Recompenses.CompletionAwardsCount.ToString(CultureInfo.CurrentCulture)
                    : string.Empty
            );
        }

        if (lignesRecompenses.Count > 0)
        {
            sections.Add(
                new SectionInformationsAffichee
                {
                    Titre = "Récompenses",
                    Lignes = lignesRecompenses,
                }
            );
        }

        List<LigneInformationAffichee> lignesActivite = [];
        AjouterLigneSiValeurUtile(
            lignesActivite,
            "Rich Presence",
            etatRichPresence.MessageRichPresence
        );
        AjouterLigneSiValeurUtile(
            lignesActivite,
            "Succès récents",
            FormaterResumeSuccesRecents(donnees.Resume)
        );

        if (lignesActivite.Count > 0)
        {
            sections.Add(
                new SectionInformationsAffichee { Titre = "Activité", Lignes = lignesActivite }
            );
        }

        List<JeuRecentAffiche> jeuxRecents =
        [
            .. (donnees.Resume?.RecentlyPlayed ?? [])
                .Take(3)
                .Select(jeu => new JeuRecentAffiche
                {
                    Titre = jeu.Titre,
                    SousTitre = ConstruireSousTitreJeuRecent(jeu),
                }),
        ];

        return new CompteAffiche
        {
            NomUtilisateur = nomUtilisateur,
            Titre = titre,
            Statut = etatRichPresence.StatutAffiche,
            SousStatut = etatRichPresence.SousStatutAffiche,
            Devise = devise,
            Introduction =
                "Retrouve ici les informations principales de ton compte et gère ta connexion en toute simplicité.",
            UrlAvatar = urlAvatar,
            Sections = sections,
            JeuxRecemmentJoues = jeuxRecents,
        };
    }

    private static string ConstruireUrlAvatar(string? cheminImage)
    {
        if (string.IsNullOrWhiteSpace(cheminImage))
        {
            return string.Empty;
        }

        return cheminImage.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? cheminImage
            : $"https://retroachievements.org{cheminImage}";
    }

    private static string FormaterDateProfil(string? dateProfil)
    {
        if (string.IsNullOrWhiteSpace(dateProfil))
        {
            return string.Empty;
        }

        if (
            DateTime.TryParseExact(
                dateProfil,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date
            )
        )
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        if (
            DateTime.TryParse(
                dateProfil,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date
            )
        )
        {
            return date.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-CA"));
        }

        return dateProfil.Trim();
    }

    private static string FormaterNombre(int? valeur)
    {
        return valeur.HasValue ? valeur.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;
    }

    private static string ConstruireResumePositionCompte(UserSummaryV2? resume)
    {
        if (resume is null || resume.Rank <= 0 || resume.TotalRanked <= 0)
        {
            return string.Empty;
        }

        return $"{resume.Rank.ToString(CultureInfo.CurrentCulture)} sur {resume.TotalRanked.ToString(CultureInfo.CurrentCulture)}";
    }

    private static string FormaterResumeSuccesRecents(UserSummaryV2? resume)
    {
        if (resume is null)
        {
            return string.Empty;
        }

        int nombreSucces = resume.RecentAchievements.Sum(item => item.Value?.Count ?? 0);

        if (nombreSucces <= 0)
        {
            return string.Empty;
        }

        return nombreSucces == 1
            ? "1 succès récent"
            : $"{nombreSucces.ToString(CultureInfo.CurrentCulture)} succès récents";
    }

    private static string DeterminerDernierJeuAffiche(DonneesCompteUtilisateur donnees)
    {
        if (!string.IsNullOrWhiteSpace(donnees.Resume?.LastGame?.Titre))
        {
            return donnees.Resume.LastGame.Titre;
        }

        if (!string.IsNullOrWhiteSpace(donnees.Profil?.LastGame))
        {
            return donnees.Profil.LastGame.Trim();
        }

        return string.Empty;
    }

    private static string ConstruireSousTitreJeuRecent(RecentlyPlayedGameV2 jeu)
    {
        List<string> segments = [];

        if (!string.IsNullOrWhiteSpace(jeu.NomConsole))
        {
            segments.Add(jeu.NomConsole);
        }

        if (!string.IsNullOrWhiteSpace(jeu.DernierePartie))
        {
            segments.Add(jeu.DernierePartie);
        }

        return segments.Count == 0 ? "Activité récente" : string.Join(" • ", segments);
    }

    private static void AjouterLigneSiValeurUtile(
        ICollection<LigneInformationAffichee> lignes,
        string libelle,
        string? valeur
    )
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return;
        }

        lignes.Add(new LigneInformationAffichee { Libelle = libelle, Valeur = valeur.Trim() });
    }
}
