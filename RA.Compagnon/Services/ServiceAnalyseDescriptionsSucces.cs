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
        IReadOnlyList<GameAchievementV2> succesJeu
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

        GroupeSuccesPotentiel? groupeBossReference = ConstruireGroupeBossDepuisReference(
            reference,
            descriptionsAnalysees
        );

        if (groupeBossReference is not null)
        {
            groupes.Add(groupeBossReference);
        }

        GroupeSuccesPotentiel? groupeNonRelie = ConstruireGroupeSuccesNonRelies(
            succesReference,
            succesJeu,
            analyseGlobale.Groupes
        );

        if (groupeNonRelie is not null)
        {
            groupes.Add(groupeNonRelie);
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

        groupes =
        [
            .. groupes
                .GroupBy(groupe => $"{groupe.TypeGroupe}|{NormaliserCle(groupe.Ancre)}")
                .Select(group => group.OrderByDescending(item => item.ScoreConfiance).First())
                .OrderByDescending(groupe => ObtenirPrioriteType(groupe.TypeGroupe))
                .ThenByDescending(groupe => groupe.ScoreConfiance)
                .ThenByDescending(groupe => groupe.IdentifiantsSucces.Count)
                .ThenBy(groupe => groupe.Ancre, StringComparer.OrdinalIgnoreCase),
        ];

        return new ResultatAnalyseDescriptionsSucces
        {
            IdentifiantSuccesReference = succesReference.Id,
            DescriptionReference = succesReference.Description?.Trim() ?? string.Empty,
            GroupePrincipal = groupes.FirstOrDefault(),
            Groupes = groupes,
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
                    .Indices.GroupBy(item => $"{item.TypeGroupe}|{NormaliserCle(item.Ancre)}")
                    .Select(group => group.OrderByDescending(item => item.PoidsBase).First())
            )
            {
                string cle = $"{indice.TypeGroupe}|{NormaliserCle(indice.Ancre)}";

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

        List<int> identifiantsSucces =
        [
            .. descriptionsAnalysees
                .Where(description => DescriptionMentionneBoss(description.Description, ancreBoss))
                .Select(description => description.Succes.Id)
                .Distinct()
                .OrderBy(identifiant => identifiant),
        ];

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
            description.Contains("or less", StringComparison.OrdinalIgnoreCase)
            || description.Contains("more than", StringComparison.OrdinalIgnoreCase)
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

        return false;
    }

    /*
     * Nettoie une ancre textuelle avant comparaison ou affichage.
     */
    private static string NettoyerAncre(string valeur)
    {
        return RegexEspacesMultiples().Replace((valeur ?? string.Empty).Trim(), " ");
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

        return descriptionNettoyee.Contains(bossNettoye, StringComparison.OrdinalIgnoreCase);
    }

    /*
     * Uniformise la clé de regroupement interne.
     */
    private static string NormaliserCle(string valeur)
    {
        return NettoyerAncre(valeur).ToLowerInvariant();
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

        public void Ajouter(DescriptionAnalysee description, IndiceStructurel indice)
        {
            IdentifiantsSucces.Add(description.Succes.Id);

            if (!string.IsNullOrWhiteSpace(description.ActionPrincipale))
            {
                Actions.Add(description.ActionPrincipale);
            }

            Regles.Add(indice.RegleSource);

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
}
