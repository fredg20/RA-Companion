using System.Globalization;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Presentation;

/*
 * Transforme les données d'activité récente issues de RetroAchievements en un
 * modèle d'affichage simple pour la zone des succès récents.
 */
namespace RA.Compagnon.Services;

/*
 * Prépare les libellés, états et lignes visibles de la section d'activité
 * récente dans l'interface principale.
 */
public sealed class ServicePresentationActivite
{
    /*
     * Retourne un état neutre lorsque aucune activité récente n'est encore
     * disponible pour le compte courant.
     */
    public static ActiviteRecenteAffichee ConstruireEtatNeutre()
    {
        return new ActiviteRecenteAffichee
        {
            TexteEtat = "Les derniers succès de ce compte apparaîtront ici.",
            Lignes =
            [
                "Aucun succès récent à afficher.",
                "Aucun succès récent à afficher.",
                "Aucun succès récent à afficher.",
            ],
        };
    }

    /*
     * Retourne un état d'erreur prêt à être affiché lorsque le chargement de
     * l'activité récente a échoué.
     */
    public static ActiviteRecenteAffichee ConstruireErreur()
    {
        return new ActiviteRecenteAffichee
        {
            TexteEtat = "Impossible de charger les succès récents.",
            Lignes =
            [
                "Aucun succès récent à afficher.",
                "Aucun succès récent à afficher.",
                "Aucun succès récent à afficher.",
            ],
        };
    }

    /*
     * Construit la présentation de l'activité récente en priorisant les succès
     * du jeu courant lorsqu'ils sont disponibles.
     */
    public static ActiviteRecenteAffichee Construire(
        DonneesActiviteRecente activiteRecente,
        int identifiantJeuCourant
    )
    {
        IReadOnlyList<AchievementUnlockV2> succesRecents = activiteRecente.SuccesRecents;

        List<AchievementUnlockV2> succesTries =
        [
            .. succesRecents.OrderByDescending(succes => ConvertirDateSucces(succes.Date)),
        ];

        if (identifiantJeuCourant > 0)
        {
            List<AchievementUnlockV2> succesJeuEnCours =
            [
                .. succesTries.Where(succes => succes.GameId == identifiantJeuCourant),
            ];

            if (succesJeuEnCours.Count > 0)
            {
                List<AchievementUnlockV2> succesAffiches = [.. succesJeuEnCours.Take(3)];
                return ConstruireDepuisSucces(
                    succesAffiches,
                    $"Affichage des {Math.Min(3, succesJeuEnCours.Count)} derniers succès du jeu en cours."
                );
            }
        }

        List<AchievementUnlockV2> succesGlobaux = [.. succesTries.Take(3)];

        if (succesGlobaux.Count == 0)
        {
            return new ActiviteRecenteAffichee
            {
                TexteEtat = "Aucun succès récent sur les 7 derniers jours.",
                Lignes =
                [
                    "Aucun autre succès récent.",
                    "Aucun autre succès récent.",
                    "Aucun autre succès récent.",
                ],
            };
        }

        return ConstruireDepuisSucces(
            succesGlobaux,
            $"Affichage des {succesGlobaux.Count} derniers succès connus."
        );
    }

    /*
     * Assemble les lignes visibles à partir d'une liste de succès déjà triés.
     */
    private static ActiviteRecenteAffichee ConstruireDepuisSucces(
        List<AchievementUnlockV2> succesRecents,
        string texteEtat
    )
    {
        string[] lignes =
        [
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
            "Aucun autre succès récent.",
        ];

        for (int index = 0; index < Math.Min(3, succesRecents.Count); index++)
        {
            lignes[index] = ConstruireLigneSucces(succesRecents[index]);
        }

        return new ActiviteRecenteAffichee { TexteEtat = texteEtat, Lignes = lignes };
    }

    /*
     * Met en forme une ligne de succès récente avec son mode, son jeu et sa
     * date de déblocage lisible.
     */
    private static string ConstruireLigneSucces(AchievementUnlockV2 succes)
    {
        string mode = succes.HardcoreMode ? "Hardcore" : "Standard";
        DateTimeOffset dateDeblocage = ConvertirDateSucces(succes.Date);
        string dateFormatee =
            dateDeblocage == DateTimeOffset.MinValue
                ? "Date inconnue"
                : dateDeblocage.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        string titreJeu = string.IsNullOrWhiteSpace(succes.TitleJeu)
            ? "Jeu inconnu"
            : succes.TitleJeu;
        string description = string.IsNullOrWhiteSpace(succes.Description)
            ? string.Empty
            : $"\n{succes.Description}";

        return $"{succes.Title} - {succes.Points} pts - {mode}\n{titreJeu} - {dateFormatee}{description}";
    }

    /*
     * Convertit une date d'API en horodatage exploitable, avec plusieurs
     * formats de repli pour rester tolérant aux variations reçues.
     */
    private static DateTimeOffset ConvertirDateSucces(string dateApi)
    {
        if (string.IsNullOrWhiteSpace(dateApi))
        {
            return DateTimeOffset.MinValue;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset dateParsee
            )
        )
        {
            return dateParsee;
        }

        string[] formatsAcceptes =
        [
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.fffK",
        ];

        if (
            DateTimeOffset.TryParseExact(
                dateApi,
                formatsAcceptes,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        if (
            DateTimeOffset.TryParse(
                dateApi,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out dateParsee
            )
        )
        {
            return dateParsee;
        }

        return DateTimeOffset.MinValue;
    }
}
