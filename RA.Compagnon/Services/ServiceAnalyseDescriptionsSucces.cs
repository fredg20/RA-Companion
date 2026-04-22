using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Presentation;

/*
 * Analyse globalement les descriptions d'un set de succès pour détecter des
 * familles stables, en privilégiant les niveaux probables avant les autres
 * types de regroupement.
 */
namespace RA.Compagnon.Services;

/*
 * Produit des regroupements potentiels réutilisables par l'interface à partir
 * d'un succès courant et de l'ensemble des descriptions du jeu.
 */
public sealed partial class ServiceAnalyseDescriptionsSucces
{
    private const int SeuilFrequenceMotTropCommun = 45;
    private static readonly HashSet<string> ActionsConnues = new(StringComparer.OrdinalIgnoreCase)
    {
        "activate",
        "beat",
        "clear",
        "collect",
        "complete",
        "defeat",
        "destroy",
        "enter",
        "explore",
        "find",
        "finish",
        "gain",
        "light",
        "open",
        "reach",
        "rescue",
        "ring",
        "survive",
        "unlock",
        "use",
        "win",
    };
    private static readonly HashSet<string> ConnecteursTitre = new(StringComparer.OrdinalIgnoreCase)
    {
        "and",
        "de",
        "des",
        "du",
        "l",
        "la",
        "le",
        "of",
        "the",
        "to",
    };
    private static readonly HashSet<string> TermesGeneriquesAliasNommes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "act",
        "area",
        "chapter",
        "course",
        "episode",
        "floor",
        "lap",
        "level",
        "map",
        "mission",
        "part",
        "room",
        "round",
        "stage",
        "world",
        "zone",
    };
    private static readonly HashSet<string> AncresNonNiveau = new(StringComparer.OrdinalIgnoreCase)
    {
        "constrainte d'actions",
        "extra life",
        "hard mode",
        "hardcore",
        "hyper mode",
        "softcore",
        "sans dégât",
        "sans perdre de vie",
        "time attack",
    };
    private static readonly HashSet<string> TermesCollectionNonNiveau = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "badge",
        "coin",
        "coins",
        "icon",
        "icons",
        "jar",
        "jars",
        "letter",
        "letters",
        "medal",
        "medals",
        "piece",
        "pieces",
        "token",
        "tokens",
        "toy",
        "toys",
    };
    private static readonly HashSet<string> MotsVides = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "all",
        "an",
        "and",
        "any",
        "by",
        "collect",
        "complete",
        "defeat",
        "first",
        "for",
        "gain",
        "higher",
        "in",
        "inside",
        "level",
        "life",
        "losing",
        "mode",
        "of",
        "on",
        "or",
        "outfit",
        "presses",
        "reset",
        "stage",
        "stars",
        "the",
        "then",
        "time",
        "to",
        "unlock",
        "without",
        "within",
    };

    /*
     * Analyse un succès de référence en s'appuyant d'abord sur une lecture
     * globale du set, puis sur un repli lexical plus prudent si nécessaire.
     */
    public static ResultatAnalyseDescriptionsSucces Analyser(
        GameAchievementV2 succesReference,
        IReadOnlyList<GameAchievementV2> succesJeu,
        AnalyseZoneRichPresence? analyseZoneCourante = null
    )
    {
        if (succesReference is null)
        {
            return new ResultatAnalyseDescriptionsSucces();
        }

        List<DescriptionAnalysee> descriptionsAnalysees =
        [
            .. succesJeu
                .Where(succes => !string.IsNullOrWhiteSpace(succes.Description))
                .Select(AnalyserDescription),
        ];

        DescriptionAnalysee reference =
            descriptionsAnalysees.FirstOrDefault(item => item.Succes.Id == succesReference.Id)
            ?? AnalyserDescription(succesReference);

        AnalyseGlobaleSet analyseGlobale = ConstruireAnalyseGlobale(descriptionsAnalysees);
        List<GroupeSuccesPotentiel> groupes =
        [
            .. analyseGlobale.Groupes.Where(groupe =>
                groupe.IdentifiantsSucces.Contains(reference.Succes.Id)
            ),
        ];

        GroupeSuccesPotentiel? groupeNiveauReference = ConstruireGroupeNiveauDepuisReference(
            reference,
            descriptionsAnalysees
        );

        if (groupeNiveauReference is not null)
        {
            groupes.Add(groupeNiveauReference);
        }

        GroupeSuccesPotentiel? groupeMondeReference = ConstruireGroupeMondeDepuisReference(
            reference,
            descriptionsAnalysees
        );

        if (groupeMondeReference is not null)
        {
            groupes.Add(groupeMondeReference);
        }

        GroupeSuccesPotentiel? groupeCollectionReference =
            ConstruireGroupeCollectionDepuisReference(reference, descriptionsAnalysees);

        if (groupeCollectionReference is not null)
        {
            groupes.Add(groupeCollectionReference);
        }

        GroupeSuccesPotentiel? groupeModeReference = ConstruireGroupeModeDepuisReference(
            reference,
            descriptionsAnalysees
        );

        if (groupeModeReference is not null)
        {
            groupes.Add(groupeModeReference);
        }

        GroupeSuccesPotentiel? groupeBossReference = ConstruireGroupeBossDepuisReference(
            reference,
            descriptionsAnalysees
        );

        if (groupeBossReference is not null)
        {
            groupes.Add(groupeBossReference);
        }

        if (!groupes.Any(groupe => groupe.TypeGroupe == TypeGroupeSuccesPotentiel.Niveau))
        {
            GroupeSuccesPotentiel? groupeLexical = ConstruireGroupeLexical(
                reference,
                descriptionsAnalysees,
                analyseGlobale.FrequencesTokens
            );

            if (groupeLexical is not null)
            {
                groupes.Add(groupeLexical);
            }
        }

        List<GroupeSuccesPotentiel> groupesCouvertsPourNonRelie =
        [
            .. analyseGlobale.Groupes,
            .. groupes,
        ];

        GroupeSuccesPotentiel? groupeNonRelie = ConstruireGroupeSuccesNonRelies(
            succesReference,
            succesJeu,
            groupesCouvertsPourNonRelie
        );

        if (groupeNonRelie is not null)
        {
            groupes.Add(groupeNonRelie);
        }

        groupes =
        [
            .. groupes
                .GroupBy(groupe =>
                    $"{groupe.TypeGroupe}|{NormaliserCleAlias(groupe.TypeGroupe, groupe.Ancre)}"
                )
                .Select(group => group.OrderByDescending(item => item.ScoreConfiance).First())
                .Select(groupe => EnrichirScoreSelection(groupe, analyseZoneCourante, reference))
                .OrderByDescending(groupe => groupe.ScoreSelection)
                .ThenByDescending(groupe => groupe.ScoreConfiance)
                .ThenByDescending(groupe => ObtenirPrioriteType(groupe.TypeGroupe))
                .ThenByDescending(groupe => groupe.IdentifiantsSucces.Count)
                .ThenBy(groupe => groupe.Ancre, StringComparer.OrdinalIgnoreCase),
        ];

        GroupeSuccesPotentiel? groupePrincipal = groupes.FirstOrDefault();

        return new ResultatAnalyseDescriptionsSucces
        {
            IdentifiantSuccesReference = succesReference.Id,
            DescriptionReference = succesReference.Description?.Trim() ?? string.Empty,
            GroupePrincipal = groupePrincipal,
            Groupes = groupePrincipal is null ? [] : groupes,
            DiagnosticsGroupes = ConstruireDiagnosticsGroupes(groupes),
        };
    }

    /*
     * Analyse une description isolée en extrayant à la fois son action
     * principale, ses ancres structurelles et ses signaux lexicaux.
     */
    private static DescriptionAnalysee AnalyserDescription(GameAchievementV2 succes)
    {
        string description = (succes.Description ?? string.Empty).Trim();
        string descriptionNettoyee = NettoyerDescription(description);
        List<string> tokensBruts = Tokeniser(descriptionNettoyee);
        string actionPrincipale = DeterminerActionPrincipale(tokensBruts);
        List<IndiceStructurel> indices = [];

        AjouterIndicesStructurels(description, actionPrincipale, indices);
        AjouterIndicesDefis(description, indices);
        AjouterIndicesNiveauUniversels(descriptionNettoyee, actionPrincipale, indices);

        return new DescriptionAnalysee(
            succes,
            description,
            actionPrincipale,
            ConstruireTokensSignificatifs(tokensBruts),
            ConstruireBigrams(tokensBruts),
            indices
        );
    }

    /*
     * Construit l'index global du set pour identifier quelles ancres sont
     * réellement stables à l'échelle du jeu.
     */
    private static AnalyseGlobaleSet ConstruireAnalyseGlobale(
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        Dictionary<string, FamilleCandidate> familles = new(StringComparer.Ordinal);
        Dictionary<string, int> frequencesTokens = ConstruireFrequencesTokens(
            descriptionsAnalysees
        );

        foreach (DescriptionAnalysee description in descriptionsAnalysees)
        {
            foreach (
                IndiceStructurel indice in description
                    .Indices.GroupBy(item =>
                        $"{item.TypeGroupe}|{NormaliserCleAlias(item.TypeGroupe, item.Ancre)}"
                    )
                    .Select(group => group.OrderByDescending(item => item.PoidsBase).First())
            )
            {
                string cle =
                    $"{indice.TypeGroupe}|{NormaliserCleAlias(indice.TypeGroupe, indice.Ancre)}";

                if (!familles.TryGetValue(cle, out FamilleCandidate? famille))
                {
                    famille = new FamilleCandidate(indice.TypeGroupe, indice.Ancre);
                    familles[cle] = famille;
                }

                famille.Ajouter(description, indice);
            }
        }

        List<GroupeSuccesPotentiel> groupes =
        [
            .. familles
                .Values.Select(EvaluerFamilleCandidate)
                .Where(groupe => groupe is not null)
                .Cast<GroupeSuccesPotentiel>(),
        ];

        return new AnalyseGlobaleSet(groupes, frequencesTokens);
    }

    /*
     * Construit un groupe niveau à partir des ancres détectées dans le succès
     * courant, puis rassemble tous les succès qui mentionnent explicitement ce
     * même niveau, même avec une formulation plus longue.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeNiveauDepuisReference(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        List<GroupeSuccesPotentiel> groupesCandidats = [];

        foreach (
            string ancreNiveau in reference
                .Indices.Where(indice => indice.TypeGroupe == TypeGroupeSuccesPotentiel.Niveau)
                .Select(indice => NettoyerAncre(indice.Ancre))
                .Where(AncreSembleValidePourNiveau)
                .GroupBy(ancre => NormaliserCleAlias(TypeGroupeSuccesPotentiel.Niveau, ancre))
                .Select(group => group.OrderByDescending(item => item.Length).First())
                .OrderByDescending(ancre =>
                    ancre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                )
                .ThenByDescending(ancre => ancre.Length)
        )
        {
            List<int> identifiantsSucces = ConstruireIdentifiantsSuccesPourAncre(
                TypeGroupeSuccesPotentiel.Niveau,
                descriptionsAnalysees,
                ancreNiveau
            );

            if (identifiantsSucces.Count < 2)
            {
                continue;
            }

            int scoreConfiance = 56;
            scoreConfiance += identifiantsSucces.Count switch
            {
                >= 5 => 16,
                4 => 12,
                3 => 8,
                _ => 4,
            };
            scoreConfiance += RegexMotCleNiveau().IsMatch(ancreNiveau) ? 6 : 0;
            scoreConfiance +=
                ancreNiveau.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2 ? 4 : 0;

            groupesCandidats.Add(
                new GroupeSuccesPotentiel
                {
                    TypeGroupe = TypeGroupeSuccesPotentiel.Niveau,
                    Ancre = ancreNiveau,
                    RegleSource = "NiveauReferenceTexte",
                    ScoreConfiance = Math.Clamp(scoreConfiance, 0, 96),
                    LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
                    IdentifiantsSucces = identifiantsSucces,
                }
            );
        }

        return groupesCandidats
            .OrderByDescending(groupe => groupe.IdentifiantsSucces.Count)
            .ThenByDescending(groupe => groupe.ScoreConfiance)
            .ThenByDescending(groupe => groupe.Ancre.Length)
            .FirstOrDefault();
    }

    /*
     * Construit un groupe monde à partir de l'ancre détectée dans le succès
     * courant, puis rassemble les succès qui mentionnent explicitement ce même
     * monde avec des formulations différentes.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeMondeDepuisReference(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        List<GroupeSuccesPotentiel> groupesCandidats = [];

        foreach (
            string ancreMonde in reference
                .Indices.Where(indice => indice.TypeGroupe == TypeGroupeSuccesPotentiel.Monde)
                .Select(indice => NettoyerAncre(indice.Ancre))
                .Where(ancre => !string.IsNullOrWhiteSpace(ancre))
                .GroupBy(ancre => NormaliserCleAlias(TypeGroupeSuccesPotentiel.Monde, ancre))
                .Select(group => group.OrderByDescending(item => item.Length).First())
                .OrderByDescending(ancre => ancre.Length)
        )
        {
            List<int> identifiantsSucces = ConstruireIdentifiantsSuccesPourAncre(
                TypeGroupeSuccesPotentiel.Monde,
                descriptionsAnalysees,
                ancreMonde
            );

            if (identifiantsSucces.Count < 2)
            {
                continue;
            }

            int scoreConfiance = 68;
            scoreConfiance += identifiantsSucces.Count switch
            {
                >= 4 => 10,
                3 => 6,
                _ => 2,
            };
            scoreConfiance +=
                ancreMonde.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2 ? 4 : 0;

            groupesCandidats.Add(
                new GroupeSuccesPotentiel
                {
                    TypeGroupe = TypeGroupeSuccesPotentiel.Monde,
                    Ancre = ancreMonde,
                    RegleSource = "MondeReferenceTexte",
                    ScoreConfiance = Math.Clamp(scoreConfiance, 0, 90),
                    LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
                    IdentifiantsSucces = identifiantsSucces,
                }
            );
        }

        return groupesCandidats
            .OrderByDescending(groupe => groupe.IdentifiantsSucces.Count)
            .ThenByDescending(groupe => groupe.ScoreConfiance)
            .ThenByDescending(groupe => groupe.Ancre.Length)
            .FirstOrDefault();
    }

    /*
     * Construit un groupe collection à partir de l'objet détecté dans le succès
     * courant, puis rassemble les succès qui mentionnent explicitement ce même
     * objet de collection.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeCollectionDepuisReference(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        List<GroupeSuccesPotentiel> groupesCandidats = [];

        foreach (
            string ancreCollection in reference
                .Indices.Where(indice => indice.TypeGroupe == TypeGroupeSuccesPotentiel.Collection)
                .Select(indice => NettoyerAncre(indice.Ancre))
                .Where(ancre => !string.IsNullOrWhiteSpace(ancre))
                .GroupBy(ancre => NormaliserCleAlias(TypeGroupeSuccesPotentiel.Collection, ancre))
                .Select(group => group.OrderByDescending(item => item.Length).First())
                .OrderByDescending(ancre => ancre.Length)
        )
        {
            List<int> identifiantsSucces = ConstruireIdentifiantsSuccesPourAncre(
                TypeGroupeSuccesPotentiel.Collection,
                descriptionsAnalysees,
                ancreCollection
            );

            if (identifiantsSucces.Count < 2)
            {
                continue;
            }

            int scoreConfiance = 58;
            scoreConfiance += identifiantsSucces.Count switch
            {
                >= 4 => 10,
                3 => 6,
                _ => 2,
            };
            scoreConfiance +=
                ancreCollection.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
                    ? 4
                    : 0;

            groupesCandidats.Add(
                new GroupeSuccesPotentiel
                {
                    TypeGroupe = TypeGroupeSuccesPotentiel.Collection,
                    Ancre = ancreCollection,
                    RegleSource = "CollectionReferenceTexte",
                    ScoreConfiance = Math.Clamp(scoreConfiance, 0, 84),
                    LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
                    IdentifiantsSucces = identifiantsSucces,
                }
            );
        }

        return groupesCandidats
            .OrderByDescending(groupe => groupe.IdentifiantsSucces.Count)
            .ThenByDescending(groupe => groupe.ScoreConfiance)
            .ThenByDescending(groupe => groupe.Ancre.Length)
            .FirstOrDefault();
    }

    /*
     * Construit un groupe mode à partir du mode détecté dans le succès
     * courant, puis rattache les autres succès qui mentionnent ce même mode.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeModeDepuisReference(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        List<GroupeSuccesPotentiel> groupesCandidats = [];

        foreach (
            string ancreMode in reference
                .Indices.Where(indice => indice.TypeGroupe == TypeGroupeSuccesPotentiel.Mode)
                .Select(indice => NettoyerAncre(indice.Ancre))
                .Where(ancre => !string.IsNullOrWhiteSpace(ancre))
                .GroupBy(ancre => NormaliserCleAlias(TypeGroupeSuccesPotentiel.Mode, ancre))
                .Select(group => group.OrderByDescending(item => item.Length).First())
                .OrderByDescending(ancre => ancre.Length)
        )
        {
            List<int> identifiantsSucces = ConstruireIdentifiantsSuccesPourAncre(
                TypeGroupeSuccesPotentiel.Mode,
                descriptionsAnalysees,
                ancreMode
            );

            if (identifiantsSucces.Count < 2)
            {
                continue;
            }

            int scoreConfiance = 54;
            scoreConfiance += identifiantsSucces.Count switch
            {
                >= 4 => 10,
                3 => 6,
                _ => 2,
            };
            scoreConfiance +=
                ancreMode.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2 ? 4 : 0;

            groupesCandidats.Add(
                new GroupeSuccesPotentiel
                {
                    TypeGroupe = TypeGroupeSuccesPotentiel.Mode,
                    Ancre = ancreMode,
                    RegleSource = "ModeReferenceTexte",
                    ScoreConfiance = Math.Clamp(scoreConfiance, 0, 80),
                    LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
                    IdentifiantsSucces = identifiantsSucces,
                }
            );
        }

        return groupesCandidats
            .OrderByDescending(groupe => groupe.IdentifiantsSucces.Count)
            .ThenByDescending(groupe => groupe.ScoreConfiance)
            .ThenByDescending(groupe => groupe.Ancre.Length)
            .FirstOrDefault();
    }

    /*
     * Construit un groupe boss à partir du nom détecté dans le succès courant,
     * puis rassemble tous les succès qui mentionnent ce même boss, même si le
     * verbe d'ouverture de leur description diffère.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeBossDepuisReference(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        string ancreBoss =
            reference
                .Indices.Where(indice => indice.TypeGroupe == TypeGroupeSuccesPotentiel.Boss)
                .Select(indice => NettoyerAncre(indice.Ancre))
                .FirstOrDefault(ancre => !string.IsNullOrWhiteSpace(ancre))
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ancreBoss))
        {
            return null;
        }

        List<int> identifiantsSucces = ConstruireIdentifiantsSuccesPourAncre(
            TypeGroupeSuccesPotentiel.Boss,
            descriptionsAnalysees,
            ancreBoss
        );

        if (identifiantsSucces.Count < 2)
        {
            return null;
        }

        int scoreConfiance = Math.Min(94, 76 + (identifiantsSucces.Count - 2) * 4);

        return new GroupeSuccesPotentiel
        {
            TypeGroupe = TypeGroupeSuccesPotentiel.Boss,
            Ancre = ancreBoss,
            RegleSource = "BossReferenceNom",
            ScoreConfiance = scoreConfiance,
            LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
            IdentifiantsSucces = identifiantsSucces,
        };
    }

    /*
     * Construit un groupe de repli rassemblant les succès qui ne sont reliés
     * à aucune famille structurée suffisamment forte dans le set.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeSuccesNonRelies(
        GameAchievementV2 succesReference,
        IReadOnlyList<GameAchievementV2> succesJeu,
        IReadOnlyList<GroupeSuccesPotentiel> groupesGlobaux
    )
    {
        HashSet<int> identifiantsCouverts =
        [
            .. groupesGlobaux.SelectMany(groupe => groupe.IdentifiantsSucces),
        ];
        List<int> identifiantsNonRelies =
        [
            .. succesJeu
                .Select(succes => succes.Id)
                .Where(identifiant => !identifiantsCouverts.Contains(identifiant))
                .Distinct()
                .OrderBy(identifiant => identifiant),
        ];

        if (identifiantsNonRelies.Count < 2 || !identifiantsNonRelies.Contains(succesReference.Id))
        {
            return null;
        }

        const int scoreConfiance = 72;

        return new GroupeSuccesPotentiel
        {
            TypeGroupe = TypeGroupeSuccesPotentiel.NonRelie,
            Ancre = "Succès non reliés",
            RegleSource = "RepliNonRelie",
            ScoreConfiance = scoreConfiance,
            LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
            IdentifiantsSucces = identifiantsNonRelies,
        };
    }

    /*
     * Convertit une famille candidate globale en groupe exploitable quand son
     * score est suffisant pour être utile à l'interface.
     */
    private static GroupeSuccesPotentiel? EvaluerFamilleCandidate(FamilleCandidate famille)
    {
        int score = famille.TypeGroupe switch
        {
            TypeGroupeSuccesPotentiel.Niveau => EvaluerScoreNiveau(famille),
            TypeGroupeSuccesPotentiel.Monde => EvaluerScoreMonde(famille),
            TypeGroupeSuccesPotentiel.Boss => EvaluerScoreBoss(famille),
            TypeGroupeSuccesPotentiel.Collection => EvaluerScoreCollection(famille),
            TypeGroupeSuccesPotentiel.Mode => EvaluerScoreMode(famille),
            TypeGroupeSuccesPotentiel.Objet => EvaluerScoreObjet(famille),
            TypeGroupeSuccesPotentiel.DefiTechnique => EvaluerScoreDefi(famille),
            _ => 0,
        };

        if (score <= 0)
        {
            return null;
        }

        return new GroupeSuccesPotentiel
        {
            TypeGroupe = famille.TypeGroupe,
            Ancre = famille.Ancre,
            RegleSource = string.Join(" + ", famille.Regles.OrderBy(item => item)),
            ScoreConfiance = score,
            LibelleConfiance = DeterminerLibelleConfiance(score),
            IdentifiantsSucces = [.. famille.IdentifiantsSucces.OrderBy(item => item)],
        };
    }

    /*
     * Calcule la confiance d'un niveau probable en privilégiant la récurrence,
     * la diversité d'actions et la stabilité de l'ancre.
     */
    private static int EvaluerScoreNiveau(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2 || EstAncreManifestementNonNiveau(famille.Ancre))
        {
            return 0;
        }

        int score = 20;
        score += famille.IdentifiantsSucces.Count switch
        {
            >= 5 => 28,
            4 => 24,
            3 => 18,
            _ => 12,
        };
        score += famille.Actions.Count switch
        {
            >= 4 => 28,
            3 => 22,
            2 => 14,
            _ => 0,
        };
        score += famille.Regles.Count switch
        {
            >= 3 => 16,
            2 => 10,
            _ => 0,
        };

        if (famille.AncreSembleStructuree)
        {
            score += 10;
        }

        if (famille.AncreContientMotCleNiveau || famille.Regles.Contains("NiveauSuffixe"))
        {
            score += 8;
        }

        if (famille.Regles.Contains("NiveauPreposition") || famille.Regles.Contains("NiveauTitre"))
        {
            score += 6;
        }

        if (famille.Actions.Count == 1 && famille.Regles.Count == 1)
        {
            score -= 14;
        }

        score -= CalculerPenaliteFauxPositifDefi(famille, TypeGroupeSuccesPotentiel.Niveau);

        return Math.Clamp(score, 0, 98);
    }

    /*
     * Calcule la confiance d'un monde complet, généralement plus large qu'un
     * niveau mais tout de même structurant.
     */
    private static int EvaluerScoreMonde(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 1)
        {
            return 0;
        }

        int score = 46;
        score += famille.Regles.Contains("PatronMonde") ? 12 : 0;
        score += famille.IdentifiantsSucces.Count >= 2 ? 8 : 0;
        score -= CalculerPenaliteFauxPositifDefi(famille, TypeGroupeSuccesPotentiel.Monde);
        return Math.Clamp(score, 0, 92);
    }

    /*
     * Calcule la confiance d'un boss partagé entre plusieurs variantes de
     * succès, comme une victoire standard et un défi sans dégâts.
     */
    private static int EvaluerScoreBoss(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2)
        {
            return 0;
        }

        int score = 38;
        score += famille.Actions.Count >= 2 ? 10 : 0;
        score += famille.Regles.Count >= 2 ? 8 : 0;
        score += famille.IdentifiantsSucces.Count >= 3 ? 6 : 0;
        score -= CalculerPenaliteFauxPositifDefi(famille, TypeGroupeSuccesPotentiel.Boss);
        return Math.Clamp(score, 0, 88);
    }

    /*
     * Calcule la confiance d'un groupe de collection récurrent.
     */
    private static int EvaluerScoreCollection(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2)
        {
            return 0;
        }

        int score = 34;
        score += famille.Actions.Count >= 2 ? 6 : 0;
        score += famille.Regles.Count >= 2 ? 6 : 0;
        score -= CalculerPenaliteFauxPositifDefi(famille, TypeGroupeSuccesPotentiel.Collection);
        return Math.Clamp(score, 0, 80);
    }

    /*
     * Calcule la confiance d'un mode partagé entre plusieurs descriptions.
     */
    private static int EvaluerScoreMode(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2 || EstAncreManifestementNonNiveau(famille.Ancre))
        {
            return 0;
        }

        int score = 28;
        score += famille.Regles.Contains("PatronModeTimeAttack") ? 14 : 0;
        score += famille.Regles.Contains("PatronActivationMode") ? 10 : 0;
        score -= CalculerPenaliteFauxPositifDefi(famille, TypeGroupeSuccesPotentiel.Mode);
        return Math.Clamp(score, 0, 74);
    }

    /*
     * Calcule la confiance d'un objet ou d'une récompense récurrente.
     */
    private static int EvaluerScoreObjet(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2)
        {
            return 0;
        }

        return Math.Clamp(30 + famille.IdentifiantsSucces.Count * 4, 0, 72);
    }

    /*
     * Calcule la confiance d'un défi technique partagé entre plusieurs succès.
     */
    private static int EvaluerScoreDefi(FamilleCandidate famille)
    {
        if (famille.IdentifiantsSucces.Count < 2)
        {
            return 0;
        }

        int score = 26;
        score += famille.IdentifiantsSucces.Count >= 3 ? 8 : 0;
        return Math.Clamp(score, 0, 68);
    }

    /*
     * Ajoute les ancres détectées par patrons forts classiques.
     */
    private static void AjouterIndicesStructurels(
        string description,
        string actionPrincipale,
        ICollection<IndiceStructurel> indices
    )
    {
        Match correspondanceMonde = RegexCompleteMonde().Match(description);

        if (correspondanceMonde.Success)
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Monde,
                    NettoyerAncre(correspondanceMonde.Groups["monde"].Value),
                    "PatronMonde",
                    56
                )
            );
        }

        Match correspondanceTimeAttack = RegexCompleteTimeAttack().Match(description);

        if (correspondanceTimeAttack.Success)
        {
            string niveau = NettoyerAncre(correspondanceTimeAttack.Groups["zone"].Value);
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Niveau,
                    niveau,
                    "PatronTimeAttackZone",
                    46
                )
            );
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Mode,
                    "Time Attack",
                    "PatronModeTimeAttack",
                    28
                )
            );
        }

        Match correspondanceCollecte = RegexCollecteZone().Match(description);

        if (correspondanceCollecte.Success)
        {
            string zone = NettoyerAncre(correspondanceCollecte.Groups["zone"].Value);
            string objet = NormaliserCollection(correspondanceCollecte.Groups["objet"].Value);

            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Niveau,
                    zone,
                    "PatronCollecteZone",
                    44
                )
            );

            if (!string.IsNullOrWhiteSpace(objet))
            {
                indices.Add(
                    new IndiceStructurel(
                        TypeGroupeSuccesPotentiel.Collection,
                        objet,
                        "PatronCollection",
                        30
                    )
                );
            }
        }

        Match correspondanceBoss = RegexDefaiteBoss().Match(description);

        if (correspondanceBoss.Success)
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Boss,
                    NettoyerAncre(correspondanceBoss.Groups["boss"].Value),
                    "PatronBoss",
                    42
                )
            );
        }

        Match correspondanceTenue = RegexTenue().Match(description);

        if (correspondanceTenue.Success)
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Objet,
                    $"{NettoyerAncre(correspondanceTenue.Groups["tenue"].Value)} Outfit",
                    "PatronTenue",
                    34
                )
            );
        }

        Match correspondanceActivation = RegexActivationMode().Match(description);

        if (correspondanceActivation.Success)
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Mode,
                    NettoyerAncre(correspondanceActivation.Groups["mode"].Value),
                    "PatronActivationMode",
                    28
                )
            );
        }

        Match correspondanceGain = RegexGainCollection().Match(description);

        if (correspondanceGain.Success)
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.Collection,
                    NormaliserCollection(correspondanceGain.Groups["objet"].Value),
                    "PatronGainCollection",
                    24
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(actionPrincipale))
        {
            Match correspondanceActionTitre = RegexActionTitre().Match(description);

            if (correspondanceActionTitre.Success)
            {
                string ancre = NettoyerAncre(correspondanceActionTitre.Groups["titre"].Value);

                if (
                    ancre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
                    && !EstAncreManifestementNonNiveau(ancre)
                )
                {
                    indices.Add(
                        new IndiceStructurel(
                            TypeGroupeSuccesPotentiel.Niveau,
                            ancre,
                            "ActionTitre",
                            18
                        )
                    );
                }
            }
        }
    }

    /*
     * Ajoute les ancres de niveau universelles extraites sans dépendre d'un
     * patron trop spécifique à un jeu.
     */
    private static void AjouterIndicesNiveauUniversels(
        string descriptionNettoyee,
        string actionPrincipale,
        ICollection<IndiceStructurel> indices
    )
    {
        foreach (Match correspondance in RegexNiveauPreposition().Matches(descriptionNettoyee))
        {
            string ancre = NettoyerAncre(correspondance.Groups["ancre"].Value);

            if (AncreSembleValidePourNiveau(ancre))
            {
                indices.Add(
                    new IndiceStructurel(
                        TypeGroupeSuccesPotentiel.Niveau,
                        ancre,
                        "NiveauPreposition",
                        34
                    )
                );
            }
        }

        foreach (Match correspondance in RegexNiveauCle().Matches(descriptionNettoyee))
        {
            string ancre = NettoyerAncre(correspondance.Groups["ancre"].Value);

            if (AncreSembleValidePourNiveau(ancre))
            {
                indices.Add(
                    new IndiceStructurel(TypeGroupeSuccesPotentiel.Niveau, ancre, "NiveauCle", 30)
                );
            }
        }

        foreach (
            string ancre in ExtrairePhrasesTitreCandidates(descriptionNettoyee, actionPrincipale)
        )
        {
            if (AncreSembleValidePourNiveau(ancre))
            {
                indices.Add(
                    new IndiceStructurel(TypeGroupeSuccesPotentiel.Niveau, ancre, "NiveauTitre", 24)
                );
            }
        }

        foreach (Match correspondance in RegexSuffixeNiveau().Matches(descriptionNettoyee))
        {
            string ancre = NettoyerAncre(correspondance.Groups["ancre"].Value);

            if (AncreSembleValidePourNiveau(ancre))
            {
                indices.Add(
                    new IndiceStructurel(
                        TypeGroupeSuccesPotentiel.Niveau,
                        ancre,
                        "NiveauSuffixe",
                        26
                    )
                );
            }
        }
    }

    /*
     * Ajoute les ancres de défi générique, utiles mais de priorité inférieure
     * à un vrai regroupement par niveau.
     */
    private static void AjouterIndicesDefis(
        string description,
        ICollection<IndiceStructurel> indices
    )
    {
        if (description.Contains("without taking any damage", StringComparison.OrdinalIgnoreCase))
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.DefiTechnique,
                    "Sans dégât",
                    "DefiSansDegat",
                    24
                )
            );
        }

        if (description.Contains("losing a life", StringComparison.OrdinalIgnoreCase))
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.DefiTechnique,
                    "Sans perdre de vie",
                    "DefiSansPerdreVie",
                    22
                )
            );
        }

        if (
            description.Contains("single life", StringComparison.OrdinalIgnoreCase)
            || description.Contains("one life", StringComparison.OrdinalIgnoreCase)
            || description.Contains("without dying", StringComparison.OrdinalIgnoreCase)
        )
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.DefiTechnique,
                    "Contrainte de survie",
                    "DefiSurvie",
                    18
                )
            );
        }

        if (
            description.Contains("or less", StringComparison.OrdinalIgnoreCase)
            || description.Contains("less than", StringComparison.OrdinalIgnoreCase)
            || description.Contains("more than", StringComparison.OrdinalIgnoreCase)
            || description.Contains("under ", StringComparison.OrdinalIgnoreCase)
        )
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.DefiTechnique,
                    "Contrainte d'actions",
                    "DefiContrainteActions",
                    18
                )
            );
        }

        if (
            description.Contains("or higher", StringComparison.OrdinalIgnoreCase)
            || RegexSignalDefiDifficulte().IsMatch(description)
        )
        {
            indices.Add(
                new IndiceStructurel(
                    TypeGroupeSuccesPotentiel.DefiTechnique,
                    "Contrainte de difficulté",
                    "DefiDifficulte",
                    18
                )
            );
        }
    }

    /*
     * Produit un groupe de secours basé sur la proximité lexicale lorsque
     * l'analyse structurelle ne parvient pas à proposer un niveau fiable.
     */
    private static GroupeSuccesPotentiel? ConstruireGroupeLexical(
        DescriptionAnalysee reference,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees,
        IReadOnlyDictionary<string, int> frequencesGlobales
    )
    {
        List<SimilariteLexicale> similarites = [];

        foreach (DescriptionAnalysee candidate in descriptionsAnalysees)
        {
            if (candidate.Succes.Id == reference.Succes.Id)
            {
                continue;
            }

            HashSet<string> tokensCommuns =
            [
                .. reference
                    .TokensSignificatifs.Intersect(candidate.TokensSignificatifs)
                    .Where(token =>
                        frequencesGlobales.GetValueOrDefault(token) <= 2
                        || frequencesGlobales.GetValueOrDefault(token)
                            * 100
                            / Math.Max(1, descriptionsAnalysees.Count)
                            <= SeuilFrequenceMotTropCommun
                    ),
            ];
            HashSet<string> bigramsCommuns =
            [
                .. reference.BigramsSignificatifs.Intersect(candidate.BigramsSignificatifs),
            ];

            int poidsTokens = tokensCommuns.Sum(token =>
                Math.Max(1, 6 - frequencesGlobales.GetValueOrDefault(token, 6))
            );
            int score = poidsTokens + bigramsCommuns.Count * 7;

            if (score < 7)
            {
                continue;
            }

            similarites.Add(
                new SimilariteLexicale(candidate.Succes.Id, score, tokensCommuns, bigramsCommuns)
            );
        }

        if (similarites.Count == 0)
        {
            return null;
        }

        string ancre = DeterminerAncreLexicale(similarites);

        if (string.IsNullOrWhiteSpace(ancre))
        {
            return null;
        }

        int scoreConfiance = Math.Min(
            74,
            34
                + Math.Min(similarites.Count * 6, 24)
                + Math.Min(similarites.Max(item => item.Score), 16)
        );

        return new GroupeSuccesPotentiel
        {
            TypeGroupe = TypeGroupeSuccesPotentiel.Lexical,
            Ancre = ancre,
            RegleSource = "RepliLexical",
            ScoreConfiance = scoreConfiance,
            LibelleConfiance = DeterminerLibelleConfiance(scoreConfiance),
            IdentifiantsSucces =
            [
                reference.Succes.Id,
                .. similarites
                    .Select(item => item.IdentifiantSucces)
                    .Distinct()
                    .OrderBy(item => item),
            ],
        };
    }

    /*
     * Calcule la fréquence globale des mots significatifs dans le set.
     */
    private static Dictionary<string, int> ConstruireFrequencesTokens(
        IEnumerable<DescriptionAnalysee> descriptionsAnalysees
    )
    {
        Dictionary<string, int> frequences = new(StringComparer.OrdinalIgnoreCase);

        foreach (DescriptionAnalysee description in descriptionsAnalysees)
        {
            foreach (string token in description.TokensSignificatifs)
            {
                frequences[token] = frequences.GetValueOrDefault(token) + 1;
            }
        }

        return frequences;
    }

    /*
     * Choisit l'ancre lexicale la plus explicite parmi les mots communs.
     */
    private static string DeterminerAncreLexicale(IReadOnlyList<SimilariteLexicale> similarites)
    {
        string? meilleurBigram = similarites
            .SelectMany(item => item.BigramsCommuns)
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(meilleurBigram))
        {
            return meilleurBigram;
        }

        string[] tokens =
        [
            .. similarites
                .SelectMany(item => item.TokensCommuns)
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .Select(group => group.Key),
        ];

        return string.Join(" ", tokens).Trim();
    }

    /*
     * Détermine l'action principale d'une description à partir de son premier
     * mot normalisé.
     */
    private static string DeterminerActionPrincipale(List<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        string premier = tokens[0];
        return ActionsConnues.Contains(premier) ? premier : string.Empty;
    }

    /*
     * Nettoie une description en supprimant les notes entre crochets qui
     * polluent souvent les ancres structurelles.
     */
    private static string NettoyerDescription(string description)
    {
        string sansCrochets = MyRegex().Replace(description, string.Empty).Trim();
        return RegexEspacesMultiples().Replace(sansCrochets, " ");
    }

    /*
     * Découpe une description en mots normalisés.
     */
    private static List<string> Tokeniser(string description)
    {
        return
        [
            .. RegexTokenisation()
                .Matches(description.ToLowerInvariant())
                .Select(item => item.Value),
        ];
    }

    /*
     * Extrait les mots conservant une casse exploitable pour repérer des titres
     * ou noms de niveaux potentiels dans une description.
     */
    private static List<string> ConstruireMotsTitre(string description)
    {
        return [.. RegexExtractionTitre().Matches(description).Select(item => item.Value)];
    }

    /*
     * Conserve uniquement les mots suffisamment discriminants pour un repli
     * lexical léger.
     */
    private static HashSet<string> ConstruireTokensSignificatifs(List<string> tokens)
    {
        return
        [
            .. tokens.Where(token =>
                token.Length >= 3
                && !int.TryParse(token, out _)
                && !MotsVides.Contains(token)
                && !token.All(char.IsDigit)
            ),
        ];
    }

    /*
     * Construit des groupes de deux mots significatifs adjacents.
     */
    private static HashSet<string> ConstruireBigrams(List<string> tokens)
    {
        HashSet<string> bigrams = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < tokens.Count - 1; index++)
        {
            string premier = tokens[index];
            string second = tokens[index + 1];

            if (
                premier.Length < 3
                || second.Length < 3
                || MotsVides.Contains(premier)
                || MotsVides.Contains(second)
            )
            {
                continue;
            }

            bigrams.Add($"{premier} {second}");
        }

        return bigrams;
    }

    /*
     * Extrait des groupes de mots à casse significative susceptibles de
     * représenter un niveau nommé, même sans préposition explicite.
     */
    private static IEnumerable<string> ExtrairePhrasesTitreCandidates(
        string description,
        string actionPrincipale
    )
    {
        List<string> mots = ConstruireMotsTitre(description);
        /*
            .. RegexExtractionTitre()
                .Matches(description, @"[A-Za-z0-9'’-]+")
                .Select(item => item.Value),
        */

        if (mots.Count == 0)
        {
            yield break;
        }

        int index = 0;

        while (index < mots.Count)
        {
            if (!MotPeutAppartenirATitre(mots[index], debutAutorise: true))
            {
                index++;
                continue;
            }

            int debut = index;
            int fin = index;

            while (
                fin + 1 < mots.Count && MotPeutAppartenirATitre(mots[fin + 1], debutAutorise: false)
            )
            {
                fin++;
            }

            List<string> sequence = [.. mots.Skip(debut).Take(fin - debut + 1)];

            if (
                !string.IsNullOrWhiteSpace(actionPrincipale)
                && sequence.Count > 0
                && string.Equals(sequence[0], actionPrincipale, StringComparison.OrdinalIgnoreCase)
            )
            {
                sequence.RemoveAt(0);
            }

            sequence = NettoyerSequenceTitre(sequence);

            if (sequence.Count >= 2 && CompterMotsMajuscules(sequence) >= 2)
            {
                yield return string.Join(" ", sequence);
            }

            index = fin + 1;
        }
    }

    /*
     * Nettoie une séquence titre en retirant les suffixes génériques qui ne
     * décrivent pas le niveau lui-même.
     */
    private static List<string> NettoyerSequenceTitre(List<string> sequence)
    {
        while (sequence.Count > 0)
        {
            string dernier = sequence[^1];

            if (
                string.Equals(dernier, "Time", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dernier, "Attack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dernier, "Boss", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dernier, "Mode", StringComparison.OrdinalIgnoreCase)
            )
            {
                sequence.RemoveAt(sequence.Count - 1);
                continue;
            }

            break;
        }

        return sequence;
    }

    /*
     * Indique si un mot peut appartenir à une phrase de titre significative.
     */
    private static bool MotPeutAppartenirATitre(string mot, bool debutAutorise)
    {
        return CommenceParMajusculeOuNombre(mot)
            || (!debutAutorise && ConnecteursTitre.Contains(mot));
    }

    /*
     * Compte les mots réellement porteurs d'une casse significative.
     */
    private static int CompterMotsMajuscules(IEnumerable<string> mots)
    {
        return mots.Count(CommenceParMajusculeOuNombre);
    }

    /*
     * Détermine si un mot commence par une majuscule ou un nombre.
     */
    private static bool CommenceParMajusculeOuNombre(string mot)
    {
        if (string.IsNullOrWhiteSpace(mot))
        {
            return false;
        }

        char premier = mot[0];
        return char.IsUpper(premier) || char.IsDigit(premier);
    }

    /*
     * Vérifie si une ancre mérite d'être traitée comme un niveau probable.
     */
    private static bool AncreSembleValidePourNiveau(string ancre)
    {
        string ancreNettoyee = NettoyerAncre(ancre);

        if (
            string.IsNullOrWhiteSpace(ancreNettoyee)
            || EstAncreManifestementNonNiveau(ancreNettoyee)
        )
        {
            return false;
        }

        string[] mots = ancreNettoyee.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (mots.Length == 1)
        {
            string mot = mots[0];
            return CommenceParMajusculeOuNombre(mot) || mot.Contains('\'') || mot.Contains('-');
        }

        return mots.Any(CommenceParMajusculeOuNombre) || RegexMotCleNiveau().IsMatch(ancreNettoyee);
    }

    /*
     * Filtre les ancres trop génériques ou manifestement non liées à un niveau.
     */
    private static bool EstAncreManifestementNonNiveau(string ancre)
    {
        string cle = NormaliserCle(ancre);

        if (AncresNonNiveau.Contains(cle))
        {
            return true;
        }

        if (cle.EndsWith(" outfit", StringComparison.Ordinal))
        {
            return true;
        }

        string[] mots = cle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (mots.Length > 0 && TermesCollectionNonNiveau.Contains(mots[^1]))
        {
            return true;
        }

        return false;
    }

    /*
     * Nettoie une ancre textuelle avant comparaison ou affichage.
     */
    private static string NettoyerAncre(string valeur)
    {
        string ancre = (valeur ?? string.Empty).Replace('’', '\'').Replace('`', '\'').Trim();

        return RegexEspacesMultiples().Replace(ancre, " ").Trim(' ', '.', ',', ';', ':', '-');
    }

    /*
     * Normalise une collection en retirant le compteur initial lorsqu'il n'est
     * pas indispensable à son identité.
     */
    private static string NormaliserCollection(string valeur)
    {
        string ancre = NettoyerAncre(valeur);
        return RegexCompteurInitial().Replace(ancre, string.Empty);
    }

    /*
     * Construit la liste triée des succès qui mentionnent explicitement une
     * ancre textuelle complète.
     */
    private static List<int> ConstruireIdentifiantsSuccesPourAncre(
        TypeGroupeSuccesPotentiel typeGroupe,
        IReadOnlyList<DescriptionAnalysee> descriptionsAnalysees,
        string ancre
    )
    {
        string cleAlias = NormaliserCleAlias(typeGroupe, ancre);

        return
        [
            .. descriptionsAnalysees
                .Where(description =>
                    DescriptionEstLieeAAncre(typeGroupe, description, ancre, cleAlias)
                )
                .Select(description => description.Succes.Id)
                .Distinct()
                .OrderBy(identifiant => identifiant),
        ];
    }

    /*
     * Détermine si une description appartient à une ancre donnée soit par une
     * détection structurelle aliasée, soit par une mention textuelle complète.
     */
    private static bool DescriptionEstLieeAAncre(
        TypeGroupeSuccesPotentiel typeGroupe,
        DescriptionAnalysee description,
        string ancre,
        string cleAlias
    )
    {
        if (
            !string.IsNullOrWhiteSpace(cleAlias)
            && description.Indices.Any(indice =>
                indice.TypeGroupe == typeGroupe
                && string.Equals(
                    NormaliserCleAlias(typeGroupe, indice.Ancre),
                    cleAlias,
                    StringComparison.Ordinal
                )
            )
        )
        {
            return true;
        }

        return DescriptionMentionneAncreComplete(description.Description, ancre);
    }

    /*
     * Vérifie si une description mentionne explicitement le boss de référence,
     * indépendamment du verbe qui introduit la phrase.
     */
    private static bool DescriptionMentionneBoss(string description, string ancreBoss)
    {
        string descriptionNettoyee = NettoyerDescription(description);
        string bossNettoye = NettoyerAncre(ancreBoss);

        if (
            string.IsNullOrWhiteSpace(descriptionNettoyee) || string.IsNullOrWhiteSpace(bossNettoye)
        )
        {
            return false;
        }

        return DescriptionMentionneAncreComplete(descriptionNettoyee, bossNettoye);
    }

    /*
     * Vérifie qu'une ancre apparaît comme segment complet dans une description,
     * afin d'éviter les faux positifs du type Act 1 dans Act 10.
     */
    private static bool DescriptionMentionneAncreComplete(string description, string ancre)
    {
        string descriptionNormalisee = NettoyerDescription(description).ToLowerInvariant();
        string ancreNormalisee = NettoyerAncre(ancre).ToLowerInvariant();

        if (
            string.IsNullOrWhiteSpace(descriptionNormalisee)
            || string.IsNullOrWhiteSpace(ancreNormalisee)
        )
        {
            return false;
        }

        int indexRecherche = 0;

        while (indexRecherche < descriptionNormalisee.Length)
        {
            int position = descriptionNormalisee.IndexOf(
                ancreNormalisee,
                indexRecherche,
                StringComparison.Ordinal
            );

            if (position < 0)
            {
                return false;
            }

            int fin = position + ancreNormalisee.Length;
            bool borneGaucheValide =
                position == 0 || !char.IsLetterOrDigit(descriptionNormalisee[position - 1]);
            bool borneDroiteValide =
                fin >= descriptionNormalisee.Length
                || !char.IsLetterOrDigit(descriptionNormalisee[fin]);

            if (borneGaucheValide && borneDroiteValide)
            {
                return true;
            }

            indexRecherche = fin;
        }

        string descriptionComparable = NormaliserTexteComparaison(description);
        string ancreComparable = NormaliserTexteComparaison(ancre);

        return ContientSuiteMots(descriptionComparable, ancreComparable);
    }

    /*
     * Uniformise la clé de regroupement interne.
     */
    private static string NormaliserCle(string valeur)
    {
        return NormaliserTexteComparaison(valeur);
    }

    /*
     * Uniformise une clé de groupe avec une logique d'alias adaptée au type
     * d'ancre afin de faire converger les formulations proches.
     */
    private static string NormaliserCleAlias(TypeGroupeSuccesPotentiel typeGroupe, string valeur)
    {
        string normalisee = NormaliserTexteComparaison(valeur);

        if (string.IsNullOrWhiteSpace(normalisee))
        {
            return string.Empty;
        }

        return typeGroupe switch
        {
            TypeGroupeSuccesPotentiel.Niveau => NormaliserAliasZone(normalisee),
            TypeGroupeSuccesPotentiel.Monde => NormaliserAliasZone(normalisee),
            TypeGroupeSuccesPotentiel.Boss => NormaliserAliasBoss(normalisee),
            _ => normalisee,
        };
    }

    /*
     * Rapproche les alias de zones et de niveaux en retirant les préfixes
     * génériques puis en stabilisant les suites numériques.
     */
    private static string NormaliserAliasZone(string valeur)
    {
        string normalisee = RegexPrefixeAliasZone().Replace(valeur, string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalisee))
        {
            return string.Empty;
        }

        normalisee = RegexEspacesMultiples().Replace(normalisee, " ").Trim();
        normalisee = UniformiserSuiteNumerique(normalisee);

        string signatureNommee = ConstruireSignatureAliasNommee(normalisee);
        return string.IsNullOrWhiteSpace(signatureNommee) ? normalisee : signatureNommee;
    }

    /*
     * Retire les mots d'introduction trop génériques autour d'un boss afin de
     * mieux fusionner les descriptions portant sur le même adversaire.
     */
    private static string NormaliserAliasBoss(string valeur)
    {
        string normalisee = RegexPrefixeAliasBoss().Replace(valeur, string.Empty).Trim();
        normalisee = RegexEspacesMultiples().Replace(normalisee, " ").Trim();

        string signatureNommee = ConstruireSignatureAliasNommee(normalisee);
        return string.IsNullOrWhiteSpace(signatureNommee) ? normalisee : signatureNommee;
    }

    /*
     * Stabilise les suites numériques pour rapprocher des variantes comme
     * 01, 1, 1 1 et 1-1 sur une même représentation interne.
     */
    private static string UniformiserSuiteNumerique(string valeur)
    {
        string[] segments = valeur.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !segments.All(segment => segment.All(char.IsDigit)))
        {
            return valeur;
        }

        return string.Join("-", segments.Select(NormaliserSegmentNumerique));
    }

    /*
     * Supprime les zéros initiaux d'un segment numérique sans perdre le cas 0.
     */
    private static string NormaliserSegmentNumerique(string valeur)
    {
        string normalisee = valeur.TrimStart('0');
        return string.IsNullOrWhiteSpace(normalisee) ? "0" : normalisee;
    }

    /*
     * Réduit la confiance d'une famille lorsque plusieurs descriptions
     * ressemblent davantage à des défis qu'à une vraie zone ou un vrai groupe.
     */
    private static int CalculerPenaliteFauxPositifDefi(
        FamilleCandidate famille,
        TypeGroupeSuccesPotentiel typeCible
    )
    {
        if (typeCible == TypeGroupeSuccesPotentiel.DefiTechnique)
        {
            return 0;
        }

        int penalite = 0;

        if (EstAncreManifestementDeDefi(famille.Ancre))
        {
            penalite += 18;
        }

        if (famille.DescriptionsAvecSignalDefi >= 2)
        {
            penalite += 8;
        }

        if (
            famille.DescriptionsAvecSignalDefi == famille.IdentifiantsSucces.Count
            && famille.IdentifiantsSucces.Count >= 2
        )
        {
            penalite += 8;
        }

        if (famille.OccurrencesSignauxDefi >= Math.Max(3, famille.IdentifiantsSucces.Count * 2))
        {
            penalite += 6;
        }

        if (
            famille.DescriptionsAvecSignalDefi > 0
            && famille.Actions.Count <= 1
            && famille.Regles.Count <= 1
        )
        {
            penalite += 6;
        }

        if (
            typeCible == TypeGroupeSuccesPotentiel.Niveau
            && !famille.AncreContientMotCleNiveau
            && !famille.AncreSembleStructuree
            && famille.DescriptionsAvecSignalDefi > 0
        )
        {
            penalite += 8;
        }

        return penalite;
    }

    /*
     * Détecte si une ancre elle-même ressemble davantage à un défi qu'à une
     * zone, un monde ou un boss.
     */
    private static bool EstAncreManifestementDeDefi(string ancre)
    {
        string normalisee = NormaliserTexteComparaison(ancre);

        if (string.IsNullOrWhiteSpace(normalisee))
        {
            return false;
        }

        return normalisee.Contains("sans degat", StringComparison.Ordinal)
            || normalisee.Contains("sans perdre de vie", StringComparison.Ordinal)
            || normalisee.Contains("time attack", StringComparison.Ordinal)
            || normalisee.Contains("hard mode", StringComparison.Ordinal)
            || normalisee.Contains("softcore", StringComparison.Ordinal)
            || normalisee.Contains("hardcore", StringComparison.Ordinal)
            || normalisee.Contains("difficulty", StringComparison.Ordinal)
            || normalisee.Contains("contrainte", StringComparison.Ordinal);
    }

    /*
     * Produit un résumé interne des meilleurs groupes pour faciliter les
     * futurs diagnostics sans encore modifier l'interface.
     */
    private static List<string> ConstruireDiagnosticsGroupes(
        IReadOnlyList<GroupeSuccesPotentiel> groupes
    )
    {
        return
        [
            .. groupes
                .Take(3)
                .Select(groupe =>
                    $"type={groupe.TypeGroupe};ancre={groupe.Ancre};score={groupe.ScoreConfiance};bonusType={groupe.BonusSelectionType};bonusZone={groupe.BonusAlignementZone};bonusContexte={groupe.BonusContexteReference};selection={groupe.ScoreSelection};source={groupe.RegleSource};taille={groupe.IdentifiantsSucces.Count}"
                ),
        ];
    }

    /*
     * Compte les marqueurs de descriptions qui signalent plus probablement un
     * défi qu'un emplacement ou un regroupement structurel.
     */
    private static int CompterSignauxDefiDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return 0;
        }

        int score = 0;

        if (description.Contains("without", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (
            description.Contains("without taking any damage", StringComparison.OrdinalIgnoreCase)
            || description.Contains("no damage", StringComparison.OrdinalIgnoreCase)
        )
        {
            score += 3;
        }

        if (
            description.Contains("losing a life", StringComparison.OrdinalIgnoreCase)
            || description.Contains("single life", StringComparison.OrdinalIgnoreCase)
            || description.Contains("one life", StringComparison.OrdinalIgnoreCase)
            || description.Contains("without dying", StringComparison.OrdinalIgnoreCase)
        )
        {
            score += 2;
        }

        if (
            description.Contains("or less", StringComparison.OrdinalIgnoreCase)
            || description.Contains("less than", StringComparison.OrdinalIgnoreCase)
            || description.Contains("more than", StringComparison.OrdinalIgnoreCase)
            || description.Contains("under ", StringComparison.OrdinalIgnoreCase)
        )
        {
            score += 2;
        }

        if (
            description.Contains("or higher", StringComparison.OrdinalIgnoreCase)
            || RegexSignalDefiDifficulte().IsMatch(description)
        )
        {
            score += 2;
        }

        return score;
    }

    /*
     * Construit une signature stable pour les ancres nommées en retirant les
     * connecteurs et les termes génériques qui n'apportent pas d'identité.
     */
    private static string ConstruireSignatureAliasNommee(string valeur)
    {
        string[] tokens = valeur.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 2 || tokens.Any(token => token.Any(char.IsDigit)))
        {
            return string.Empty;
        }

        List<string> tokensSignificatifs =
        [
            .. tokens
                .Select(NettoyerTokenAliasNommee)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Where(token => !ConnecteursTitre.Contains(token))
                .Where(token => !TermesGeneriquesAliasNommes.Contains(token)),
        ];

        if (tokensSignificatifs.Count < 2)
        {
            return string.Empty;
        }

        return string.Join(" ", tokensSignificatifs);
    }

    /*
     * Nettoie un mot d'ancre nommée pour rapprocher les variantes possessives
     * et supprimer les ponctuations résiduelles.
     */
    private static string NettoyerTokenAliasNommee(string valeur)
    {
        string token = valeur.Trim('\'', '"', '.', ',', ';', ':', '-', '_');

        if (token.EndsWith("'s", StringComparison.Ordinal))
        {
            token = token[..^2];
        }
        else if (token.EndsWith("s'", StringComparison.Ordinal))
        {
            token = token[..^1];
        }

        return token.Trim('\'', '"', '.', ',', ';', ':', '-', '_');
    }

    /*
     * Définit l'ordre de priorité des types de groupes pour l'affichage.
     */
    private static int ObtenirPrioriteType(TypeGroupeSuccesPotentiel type)
    {
        return type switch
        {
            TypeGroupeSuccesPotentiel.Niveau => 90,
            TypeGroupeSuccesPotentiel.Boss => 80,
            TypeGroupeSuccesPotentiel.Monde => 70,
            TypeGroupeSuccesPotentiel.Collection => 60,
            TypeGroupeSuccesPotentiel.Mode => 50,
            TypeGroupeSuccesPotentiel.Objet => 40,
            TypeGroupeSuccesPotentiel.DefiTechnique => 30,
            TypeGroupeSuccesPotentiel.NonRelie => 20,
            TypeGroupeSuccesPotentiel.Lexical => 10,
            _ => 0,
        };
    }

    /*
     * Produit un score de sélection plus souple que la simple priorité de type
     * afin qu'un groupe très solide puisse dépasser un groupe seulement
     * prioritaire par défaut.
     */
    private static int CalculerScoreSelection(
        GroupeSuccesPotentiel groupe,
        AnalyseZoneRichPresence? analyseZoneCourante,
        DescriptionAnalysee reference
    )
    {
        int bonusType = ObtenirBonusSelectionType(groupe.TypeGroupe);
        int bonusZone = ObtenirBonusAlignementZone(groupe, analyseZoneCourante);
        int bonusContexte = ObtenirBonusContexteReference(groupe, reference);
        return groupe.ScoreConfiance + bonusType + bonusZone + bonusContexte;
    }

    /*
     * Fige le score de sélection sur chaque groupe pour éviter de recalculer
     * plusieurs fois les mêmes comparaisons pendant l'affichage.
     */
    private static GroupeSuccesPotentiel EnrichirScoreSelection(
        GroupeSuccesPotentiel groupe,
        AnalyseZoneRichPresence? analyseZoneCourante,
        DescriptionAnalysee reference
    )
    {
        int bonusType = ObtenirBonusSelectionType(groupe.TypeGroupe);
        int bonusZone = ObtenirBonusAlignementZone(groupe, analyseZoneCourante);
        int bonusContexte = ObtenirBonusContexteReference(groupe, reference);

        return new GroupeSuccesPotentiel
        {
            TypeGroupe = groupe.TypeGroupe,
            Ancre = groupe.Ancre,
            RegleSource = groupe.RegleSource,
            ScoreConfiance = groupe.ScoreConfiance,
            ScoreSelection = groupe.ScoreConfiance + bonusType + bonusZone + bonusContexte,
            BonusSelectionType = bonusType,
            BonusAlignementZone = bonusZone,
            BonusContexteReference = bonusContexte,
            LibelleConfiance = groupe.LibelleConfiance,
            IdentifiantsSucces = [.. groupe.IdentifiantsSucces],
        };
    }

    /*
     * Favorise le niveau comme contexte lorsque le succès courant combine un
     * objectif de collection et un emplacement explicite.
     */
    private static int ObtenirBonusContexteReference(
        GroupeSuccesPotentiel groupe,
        DescriptionAnalysee reference
    )
    {
        bool referenceContientNiveau = reference.Indices.Any(indice =>
            indice.TypeGroupe == TypeGroupeSuccesPotentiel.Niveau
        );
        bool referenceContientCollection = reference.Indices.Any(indice =>
            indice.TypeGroupe == TypeGroupeSuccesPotentiel.Collection
        );

        if (!referenceContientNiveau || !referenceContientCollection)
        {
            return 0;
        }

        if (
            groupe.TypeGroupe == TypeGroupeSuccesPotentiel.Niveau
            && groupe.IdentifiantsSucces.Count >= 3
        )
        {
            return 5;
        }

        if (
            groupe.TypeGroupe == TypeGroupeSuccesPotentiel.Collection
            && groupe.IdentifiantsSucces.Count <= 3
        )
        {
            return -1;
        }

        return 0;
    }

    /*
     * Ajoute un bonus de sélection lorsqu'un groupe décrit manifestement la
     * même zone que celle déjà détectée par le Rich Presence courant.
     */
    private static int ObtenirBonusAlignementZone(
        GroupeSuccesPotentiel groupe,
        AnalyseZoneRichPresence? analyseZoneCourante
    )
    {
        if (
            analyseZoneCourante is null
            || !analyseZoneCourante.EstFiable
            || string.IsNullOrWhiteSpace(analyseZoneCourante.ZoneDetectee)
        )
        {
            return 0;
        }

        string ancreGroupe = NormaliserTexteComparaison(groupe.Ancre);
        string ancreZone = NormaliserTexteComparaison(analyseZoneCourante.ZoneDetectee);

        if (string.IsNullOrWhiteSpace(ancreGroupe) || string.IsNullOrWhiteSpace(ancreZone))
        {
            return 0;
        }

        bool typeCompatible = TypeZoneCompatibleAvecGroupe(
            analyseZoneCourante.TypeZone,
            groupe.TypeGroupe
        );

        if (string.Equals(ancreGroupe, ancreZone, StringComparison.OrdinalIgnoreCase))
        {
            return typeCompatible ? 18 : 12;
        }

        bool inclusionForte =
            ancreGroupe.Length >= 5
            && ancreZone.Length >= 5
            && (
                ContientSuiteMots(ancreGroupe, ancreZone)
                || ContientSuiteMots(ancreZone, ancreGroupe)
            );

        if (inclusionForte)
        {
            return typeCompatible ? 12 : 7;
        }

        return 0;
    }

    /*
     * Indique si le type de zone issu du Rich Presence est cohérent avec la
     * famille de groupe proposée par l'analyse de descriptions.
     */
    private static bool TypeZoneCompatibleAvecGroupe(
        TypeZoneRichPresence typeZone,
        TypeGroupeSuccesPotentiel typeGroupe
    )
    {
        return (typeZone, typeGroupe) switch
        {
            (TypeZoneRichPresence.Niveau, TypeGroupeSuccesPotentiel.Niveau) => true,
            (TypeZoneRichPresence.Chapitre, TypeGroupeSuccesPotentiel.Niveau) => true,
            (TypeZoneRichPresence.Zone, TypeGroupeSuccesPotentiel.Niveau) => true,
            (TypeZoneRichPresence.Donjon, TypeGroupeSuccesPotentiel.Niveau) => true,
            (TypeZoneRichPresence.Monde, TypeGroupeSuccesPotentiel.Monde) => true,
            (TypeZoneRichPresence.Boss, TypeGroupeSuccesPotentiel.Boss) => true,
            _ => false,
        };
    }

    /*
     * Normalise un texte d'ancre pour rapprocher des formulations proches sans
     * perdre complètement leur structure utile.
     */
    private static string NormaliserTexteComparaison(string valeur)
    {
        string normalisee = NettoyerAncre(valeur).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalisee))
        {
            return string.Empty;
        }

        normalisee = RegexSeparateursAncre().Replace(normalisee, " ");
        normalisee = RegexArticlesInitiaux().Replace(normalisee, string.Empty);
        normalisee = RegexZerosInitiauxNombres().Replace(normalisee, "${nombre}");
        normalisee = RegexSuffixeGeneriqueAncre().Replace(normalisee, string.Empty);

        return RegexEspacesMultiples().Replace(normalisee, " ").Trim();
    }

    /*
     * Vérifie si une suite de mots normalisés est contenue telle quelle dans
     * un autre texte lui aussi déjà normalisé.
     */
    private static bool ContientSuiteMots(string texte, string ancre)
    {
        if (string.IsNullOrWhiteSpace(texte) || string.IsNullOrWhiteSpace(ancre))
        {
            return false;
        }

        string texteBorne = $" {texte.Trim()} ";
        string ancreBorne = $" {ancre.Trim()} ";
        return texteBorne.Contains(ancreBorne, StringComparison.Ordinal);
    }

    /*
     * Donne un léger bonus aux familles naturellement plus structurantes sans
     * les rendre automatiquement gagnantes face à un score de confiance plus fort.
     */
    private static int ObtenirBonusSelectionType(TypeGroupeSuccesPotentiel type)
    {
        return type switch
        {
            TypeGroupeSuccesPotentiel.Niveau => 6,
            TypeGroupeSuccesPotentiel.Boss => 5,
            TypeGroupeSuccesPotentiel.Monde => 4,
            TypeGroupeSuccesPotentiel.Collection => 3,
            TypeGroupeSuccesPotentiel.Mode => 2,
            TypeGroupeSuccesPotentiel.Objet => 1,
            TypeGroupeSuccesPotentiel.DefiTechnique => 0,
            TypeGroupeSuccesPotentiel.NonRelie => -2,
            TypeGroupeSuccesPotentiel.Lexical => -4,
            _ => 0,
        };
    }

    /*
     * Traduit un score numérique en libellé de confiance lisible.
     */
    private static string DeterminerLibelleConfiance(int score)
    {
        return score switch
        {
            >= 85 => "Très forte",
            >= 70 => "Forte",
            >= 55 => "Moyenne",
            _ => "Faible",
        };
    }

    [GeneratedRegex(
        @"^Collect the (?<objet>.+?) in (?<zone>.+?)(?:, then complete the stage)?\.?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexCollecteZone();

    [GeneratedRegex(@"^Complete (?<zone>.+?) Time Attack\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCompleteTimeAttack();

    [GeneratedRegex(@"^Complete the \d+ stages of (?<monde>.+?)\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCompleteMonde();

    [GeneratedRegex(@"^Defeat (?<boss>.+?)(?= without |\.| \[|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexDefaiteBoss();

    [GeneratedRegex(@"^Unlock the (?<tenue>.+?) Outfit for Donald\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexTenue();

    [GeneratedRegex(@"^Activate (?<mode>.+?) for the first time\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexActivationMode();

    [GeneratedRegex(
        @"^Gain an extra life by collecting (?<objet>.+?)\.?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexGainCollection();

    [GeneratedRegex(
        @"^(?:Complete|Finish|Clear|Beat|Reach|Enter|Explore|Open|Unlock|Collect|Find|Rescue|Destroy|Survive|Win|Use)\s+(?<titre>(?:[A-Z0-9][A-Za-z0-9'’-]*)(?:\s+(?:[A-Z0-9][A-Za-z0-9'’-]*|the|of|and|to|de|du|des|la|le))*)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexActionTitre();

    [GeneratedRegex(
        @"\b(?:in|at|inside|within|during|on)\s+(?<ancre>(?:[A-Z0-9][A-Za-z0-9'’-]*|the|of|and|to|de|du|des|la|le)(?:\s+(?:[A-Z0-9][A-Za-z0-9'’-]*|the|of|and|to|de|du|des|la|le)){0,5})",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexNiveauPreposition();

    [GeneratedRegex(
        @"\b(?<ancre>(?:Level|Stage|Act|Mission|Chapter|Episode|World|Zone|Area|Round|Course|Lap)\s+[A-Z0-9][A-Za-z0-9'’-]*)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexNiveauCle();

    [GeneratedRegex(
        @"^(?:Complete|Finish|Clear|Beat|Reach|Enter|Explore|Open|Unlock|Collect|Find|Rescue|Destroy|Survive|Win|Use)\s+(?<ancre>.+?)\s+(?:Time Attack|Act \d+|Stage \d+|Mission \d+|Chapter \d+|Episode \d+|Round \d+|Area \d+|Zone \d+|Course \d+|Lap \d+)\.?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexSuffixeNiveau();

    private sealed record DescriptionAnalysee(
        GameAchievementV2 Succes,
        string Description,
        string ActionPrincipale,
        HashSet<string> TokensSignificatifs,
        HashSet<string> BigramsSignificatifs,
        List<IndiceStructurel> Indices
    );

    private sealed record IndiceStructurel(
        TypeGroupeSuccesPotentiel TypeGroupe,
        string Ancre,
        string RegleSource,
        int PoidsBase
    );

    private sealed record SimilariteLexicale(
        int IdentifiantSucces,
        int Score,
        HashSet<string> TokensCommuns,
        HashSet<string> BigramsCommuns
    );

    private sealed record AnalyseGlobaleSet(
        List<GroupeSuccesPotentiel> Groupes,
        Dictionary<string, int> FrequencesTokens
    );

    private sealed class FamilleCandidate(TypeGroupeSuccesPotentiel typeGroupe, string ancre)
    {
        public TypeGroupeSuccesPotentiel TypeGroupe { get; } = typeGroupe;

        public string Ancre { get; } = ancre;

        public HashSet<int> IdentifiantsSucces { get; } = [];

        public HashSet<string> Actions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Regles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool AncreSembleStructuree { get; private set; }

        public bool AncreContientMotCleNiveau { get; private set; }

        public int OccurrencesSignauxDefi { get; private set; }

        public int DescriptionsAvecSignalDefi { get; private set; }

        public void Ajouter(DescriptionAnalysee description, IndiceStructurel indice)
        {
            IdentifiantsSucces.Add(description.Succes.Id);

            if (!string.IsNullOrWhiteSpace(description.ActionPrincipale))
            {
                Actions.Add(description.ActionPrincipale);
            }

            Regles.Add(indice.RegleSource);

            int signauxDefi = CompterSignauxDefiDescription(description.Description);

            if (signauxDefi > 0)
            {
                OccurrencesSignauxDefi += signauxDefi;
                DescriptionsAvecSignalDefi += 1;
            }

            string[] mots = indice.Ancre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            AncreSembleStructuree |=
                mots.Length >= 2
                || indice.Ancre.Any(char.IsDigit)
                || indice.Ancre.Contains('\'')
                || indice.Ancre.Contains('-');
            AncreContientMotCleNiveau |= RegexMotCleNiveau().IsMatch(indice.Ancre);
        }
    }

    [GeneratedRegex(@"\[[^\]]+\]")]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacesMultiples();

    [GeneratedRegex(@"[a-z0-9'-]+")]
    private static partial Regex RegexTokenisation();

    [GeneratedRegex(@"[A-Za-z0-9'â€™-]+")]
    private static partial Regex RegexExtractionTitre();

    [GeneratedRegex(
        @"\b(Level|Stage|Act|Mission|Chapter|Episode|World|Zone|Area|Round|Course|Lap)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexMotCleNiveau();

    [GeneratedRegex(@"^\d+\s+")]
    private static partial Regex RegexCompteurInitial();

    [GeneratedRegex(@"[\-:/,;()\[\]]+")]
    private static partial Regex RegexSeparateursAncre();

    [GeneratedRegex(
        @"^(?:(?:world|level|stage|act|mission|chapter|episode|round|course|lap|room|floor|area|zone|map|part)\s+)+",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexPrefixeAliasZone();

    [GeneratedRegex(@"^(?:boss|the boss)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexPrefixeAliasBoss();

    [GeneratedRegex(@"^(?:the|a|an)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexArticlesInitiaux();

    [GeneratedRegex(@"\b0+(?<nombre>\d+)\b")]
    private static partial Regex RegexZerosInitiauxNombres();

    [GeneratedRegex(
        @"\s+(?:zone|area|stage|level|mission|chapter|episode|room|floor)\s*$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexSuffixeGeneriqueAncre();

    [GeneratedRegex(
        @"\b(?:easy|medium|hard|expert|nightmare)\s+difficulty\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RegexSignalDefiDifficulte();
}
