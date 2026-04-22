using System.Reflection;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Modeles.Presentation;
using RA.Compagnon.Services;

return TestRunner.Executer();

static class TestRunner
{
    private static int _echecs;

    public static int Executer()
    {
        ExecuterTest(
            "Orchestrateur ignore une détection locale faible après un jeu valide récent",
            OrchestrateurIgnoreDetectionLocaleFaibleApresJeuValideRecent
        );
        ExecuterTest(
            "Orchestrateur conserve un jeu valide face à un signal \"aucun jeu\" tardif",
            OrchestrateurConserveJeuValideFaceAAucunJeuTardif
        );
        ExecuterTest(
            "Résolution rapide retrouve le bon GameID depuis les jeux récents",
            ResolutionRapideRetrouveLeBonGameId
        );
        ExecuterTest(
            "Résolution catalogue utilise aussi les titres alternatifs",
            ResolutionCatalogueUtiliseTitresAlternatifs
        );
        ExecuterTest(
            "Détection de succès local repère un nouveau hardcore",
            DetectionSuccesRepereNouveauHardcore
        );
        ExecuterTest(
            "Détection de succès local ignore un succès déjà observé",
            DetectionSuccesIgnoreSuccesDejaObserve
        );
        ExecuterTest(
            "Le débloqueur virtuel couvre les familles de journaux locaux",
            DebloqueurVirtuelCouvreLesFamillesLogLocal
        );
        ExecuterTest(
            "Fusion de succès catalogue ne régresse pas sur une liste longue",
            FusionSuccesCatalogueNeRegressePasSurListeLongue
        );
        ExecuterTest(
            "Comparaison de versions détecte une version plus récente",
            ComparaisonVersionsDetecteVersionPlusRecente
        );
        ExecuterTest(
            "Comparaison de versions traite 1.0.2 et 1.0.2.0 comme équivalentes",
            ComparaisonVersionsTraiteFormatsEquivalents
        );
        ExecuterTest(
            "Analyse hybride regroupe un niveau partagé par plusieurs familles",
            AnalyseHybrideRegroupeUnNiveauPartage
        );
        ExecuterTest(
            "Analyse hybride détecte un boss partagé entre version standard et défi",
            AnalyseHybrideDetecteUnBossPartage
        );
        ExecuterTest(
            "Analyse hybride conserve un repli lexical quand aucun patron fort n'existe",
            AnalyseHybrideConserveRepliLexical
        );
        ExecuterTest(
            "Analyse hybride retrouve un niveau nommé sans dépendre de in",
            AnalyseHybrideRetrouveNiveauNommeSansPrepositionForte
        );
        ExecuterTest(
            "Analyse hybride retrouve un niveau numéroté",
            AnalyseHybrideRetrouveNiveauNumerote
        );
        ExecuterTest(
            "Analyse hybride rattache un niveau de reference mentionne plus largement",
            AnalyseHybrideRattacheNiveauReferenceMentionEtendue
        );
        ExecuterTest(
            "Analyse hybride ne confond pas Act 1 et Act 10",
            AnalyseHybrideNeConfondPasAct1EtAct10
        );
        ExecuterTest(
            "Analyse hybride rattache un monde depuis la reference",
            AnalyseHybrideRattacheMondeDepuisReference
        );
        ExecuterTest(
            "Analyse hybride rattache un mode depuis la reference",
            AnalyseHybrideRattacheModeDepuisReference
        );
        ExecuterTest(
            "Analyse hybride laisse une collection forte depasser un niveau faible",
            AnalyseHybrideCollectionForteDepasseNiveauFaible
        );
        ExecuterTest(
            "Analyse hybride priorise le contexte de niveau sur une collection partagee",
            AnalyseHybridePrioriseContexteNiveauSurCollectionPartagee
        );
        ExecuterTest(
            "Analyse hybride priorise le niveau sur un mode recurrent",
            AnalyseHybridePrioriseNiveauSurModeRecurrent
        );
        ExecuterTest(
            "Traduction protège les noms propres probables avant traduction",
            TraductionProtegeNomsPropresProbables
        );
        ExecuterTest(
            "Traduction évite de protéger un début d'instruction banal",
            TraductionIgnoreDebutInstructionBanal
        );
        ExecuterTest(
            "Analyse hybride priorise boss sur monde après niveau",
            AnalyseHybridePrioriseBossSurMonde
        );
        ExecuterTest(
            "Analyse hybride regroupe ensemble les succès non reliés",
            AnalyseHybrideRegroupeSuccesNonRelies
        );
        Console.WriteLine();
        Console.WriteLine(
            _echecs == 0 ? "Tous les tests sont passés." : $"{_echecs} test(s) en échec."
        );
        return _echecs == 0 ? 0 : 1;
    }

    private static void ExecuterTest(string nom, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[OK] {nom}");
        }
        catch (Exception exception)
        {
            _echecs++;
            Console.WriteLine($"[ÉCHEC] {nom}");
            Console.WriteLine($"        {exception.Message}");
        }
    }

    private static void OrchestrateurIgnoreDetectionLocaleFaibleApresJeuValideRecent()
    {
        ServiceOrchestrateurEtatJeu service = new();
        Assert.True(
            service.EnregistrerJeuAffiche(11423, "Ace Combat", "api"),
            "Le jeu affiché initial doit être accepté."
        );

        bool transitionAcceptee = service.EnregistrerDetectionLocale(0, string.Empty, "local");

        Assert.False(
            transitionAcceptee,
            "Une détection locale faible récente ne doit pas écraser un jeu valide."
        );
        Assert.Equal(
            11423,
            service.EtatCourant.IdentifiantJeu,
            "Le GameID affiché doit être conservé."
        );
        Assert.Equal(
            "Ace Combat",
            service.EtatCourant.TitreJeu,
            "Le titre affiché doit être conservé."
        );
    }

    private static void OrchestrateurConserveJeuValideFaceAAucunJeuTardif()
    {
        ServiceOrchestrateurEtatJeu service = new();
        Assert.True(
            service.EnregistrerJeuAffiche(18160, "Crash of the Titans", "api"),
            "Le jeu affiché initial doit être accepté."
        );

        bool transitionAcceptee = service.EnregistrerAucunJeu("local");

        Assert.False(
            transitionAcceptee,
            "Un 'aucun jeu' tardif ne doit pas vider un jeu récent déjà valide."
        );
        Assert.Equal(
            18160,
            service.EtatCourant.IdentifiantJeu,
            "Le GameID récent doit être conservé."
        );
    }

    private static void ResolutionRapideRetrouveLeBonGameId()
    {
        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents =
        [
            new RecentlyPlayedGameV2
            {
                IdentifiantJeu = 18160,
                IdentifiantConsole = 41,
                Titre = "Crash of the Titans",
            },
        ];

        JeuLocalResolut? resolution = ServiceResolutionJeuLocal.ResoudreDepuisJeuxRecents(
            "Crash of the Titans",
            jeuxRecents
        );

        Assert.NotNull(resolution, "La résolution rapide devrait trouver un jeu récent identique.");
        Assert.Equal(18160, resolution!.IdentifiantJeu, "Le GameID récent doit être retrouvé.");
        Assert.Equal("jeux_recents", resolution.Source, "La source attendue est jeux_recents.");
    }

    private static void ResolutionCatalogueUtiliseTitresAlternatifs()
    {
        IReadOnlyList<JeuCatalogueLocal> catalogue =
        [
            new JeuCatalogueLocal
            {
                GameId = 19924,
                ConsoleId = 41,
                Titre = "Dead or Alive: Paradise",
                TitresAlternatifs = ["DOA Paradise"],
            },
        ];

        JeuLocalResolut? resolution = ServiceResolutionJeuLocal.ResoudreDepuisCatalogueLocal(
            "DOA Paradise",
            catalogue,
            [41]
        );

        Assert.NotNull(resolution, "Le catalogue local devrait accepter un titre alternatif.");
        Assert.Equal(
            19924,
            resolution!.IdentifiantJeu,
            "Le GameID du titre alternatif doit être retrouvé."
        );
        Assert.Equal(
            "catalogue_local",
            resolution.Source,
            "La source attendue est catalogue_local."
        );
    }

    private static void DetectionSuccesRepereNouveauHardcore()
    {
        Dictionary<int, EtatObservationSuccesLocal> etatPrecedent = [];
        IReadOnlyCollection<GameAchievementV2> succesCourants =
        [
            new GameAchievementV2
            {
                Id = 214818,
                Title = "Use mGBA!",
                Points = 5,
                DateEarnedHardcore = "2026-03-30 12:34:56",
            },
        ];

        IReadOnlyList<SuccesDebloqueDetecte> resultat =
            ServiceDetectionSuccesJeu.DetecterNouveauxSucces(
                17408,
                "Crash Bandicoot Blast!",
                etatPrecedent,
                succesCourants
            );

        Assert.Equal(1, resultat.Count, "Un nouveau succès hardcore doit être détecté.");
        Assert.True(resultat[0].Hardcore, "Le succès détecté doit être marqué hardcore.");
        Assert.Equal(214818, resultat[0].IdentifiantSucces, "Le bon succès doit être détecté.");
    }

    private static void DetectionSuccesIgnoreSuccesDejaObserve()
    {
        Dictionary<int, EtatObservationSuccesLocal> etatPrecedent = new()
        {
            [214818] = new EtatObservationSuccesLocal
            {
                IdentifiantSucces = 214818,
                DateObtention = "2026-03-30 12:34:56",
                DateObtentionHardcore = string.Empty,
            },
        };
        IReadOnlyCollection<GameAchievementV2> succesCourants =
        [
            new GameAchievementV2
            {
                Id = 214818,
                Title = "Use mGBA!",
                Points = 5,
                DateEarned = "2026-03-30 12:34:56",
            },
        ];

        IReadOnlyList<SuccesDebloqueDetecte> resultat =
            ServiceDetectionSuccesJeu.DetecterNouveauxSucces(
                17408,
                "Crash Bandicoot Blast!",
                etatPrecedent,
                succesCourants
            );

        Assert.Equal(0, resultat.Count, "Un succès déjà observé ne doit pas être re-détecté.");
    }

    private static void FusionSuccesCatalogueNeRegressePasSurListeLongue()
    {
        List<SuccesCatalogueLocal> succesExistants =
        [
            .. Enumerable
                .Range(1, 204)
                .Select(index => new SuccesCatalogueLocal
                {
                    AchievementId = index,
                    Titre = $"Succès {index}",
                    Points = index,
                }),
        ];
        List<SuccesCatalogueLocal> succesRecus =
        [
            new SuccesCatalogueLocal
            {
                AchievementId = 1,
                Titre = "Succès 1 mis à jour",
                Points = 999,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 2,
                Titre = "Succès 2",
                Points = 2,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 3,
                Titre = "Succès 3",
                Points = 3,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 4,
                Titre = "Succès 4",
                Points = 4,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 5,
                Titre = "Succès 5",
                Points = 5,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 6,
                Titre = "Succès 6",
                Points = 6,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 7,
                Titre = "Succès 7",
                Points = 7,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 8,
                Titre = "Succès 8",
                Points = 8,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 9,
                Titre = "Succès 9",
                Points = 9,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 10,
                Titre = "Succès 10",
                Points = 10,
            },
        ];

        MethodInfo? methodeFusion = typeof(ServiceCatalogueJeuxLocal).GetMethod(
            "FusionnerSuccesSansRegresser",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(
            methodeFusion,
            "La méthode de fusion des succès doit être trouvable par réflexion."
        );

        object? resultatBrut = methodeFusion!.Invoke(null, [succesExistants, succesRecus]);
        List<SuccesCatalogueLocal>? resultat = resultatBrut as List<SuccesCatalogueLocal>;

        Assert.NotNull(resultat, "La fusion doit retourner une liste de succès.");
        Assert.Equal(
            204,
            resultat!.Count,
            "Une mise à jour partielle ne doit pas réduire une longue liste existante."
        );
        Assert.Equal(
            999,
            resultat.First(item => item.AchievementId == 1).Points,
            "Les succès récents doivent quand même mettre à jour les entrées reçues."
        );
        Assert.NotNull(
            resultat.FirstOrDefault(item => item.AchievementId == 204),
            "Les succès absents de la mise à jour partielle doivent être conservés."
        );
    }

    private static void ComparaisonVersionsDetecteVersionPlusRecente()
    {
        int resultat = ServiceMiseAJourApplication.ComparerVersions("1.0.3", "1.0.2");

        Assert.True(resultat > 0, "La version 1.0.3 devrait être plus récente que 1.0.2.");
    }

    private static void ComparaisonVersionsTraiteFormatsEquivalents()
    {
        int resultat = ServiceMiseAJourApplication.ComparerVersions("1.0.2", "1.0.2.0");

        Assert.Equal(0, resultat, "1.0.2 et 1.0.2.0 devraient être considérées équivalentes.");
    }

    private static void DebloqueurVirtuelCouvreLesFamillesLogLocal()
    {
        string[] emulateurs = ["RetroArch", "DuckStation", "PCSX2", "PPSSPP"];

        foreach (string nomEmulateur in emulateurs)
        {
            Assert.True(
                ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur),
                $"Le succès local direct devrait être pris en charge pour {nomEmulateur}."
            );
            Assert.Equal(
                "logs",
                ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(nomEmulateur),
                $"Le type de source locale attendu pour {nomEmulateur} devrait être logs."
            );
            Assert.True(
                ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
                    nomEmulateur,
                    "logs"
                ),
                $"Le type logs devrait porter un succès local direct pour {nomEmulateur}."
            );
        }

        string[] emulateursRACache = ["RALibretro", "RAP64"];

        foreach (string nomEmulateur in emulateursRACache)
        {
            Assert.True(
                ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur),
                $"Le succès local direct devrait être pris en charge pour {nomEmulateur}."
            );
            Assert.Equal(
                "racache_log",
                ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(nomEmulateur),
                $"Le type de source locale attendu pour {nomEmulateur} devrait être racache_log."
            );
            Assert.True(
                ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
                    nomEmulateur,
                    "racache_log"
                ),
                $"Le type racache_log devrait porter un succès local direct pour {nomEmulateur}."
            );
        }
    }

    private static void AnalyseHybrideRegroupeUnNiveauPartage()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(1, "Collect the 3 plane toys in Ancient Fate, then complete the stage."),
            CreerSucces(2, "Collect the Warp Boss Icon in Ancient Fate."),
            CreerSucces(3, "Complete Ancient Fate Time Attack."),
            CreerSucces(4, "Complete Artifact Way Time Attack."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Un groupe principal devrait être produit pour un niveau clairement partagé."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le regroupement principal devrait être le niveau."
        );
        Assert.Equal(
            "Ancient Fate",
            resultat.GroupePrincipal.Ancre,
            "L'ancre de niveau devrait être retrouvée."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les trois succès du même niveau devraient être regroupés."
        );
    }

    private static void AnalyseHybrideDetecteUnBossPartage()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(10, "Defeat Merlock."),
            CreerSucces(
                11,
                "Defeat Merlock without taking any damage, losing a life and in 30 B/Y presses or less. [Exit Stage to Reset]"
            ),
            CreerSucces(12, "Survive Merlock's final phase."),
            CreerSucces(13, "Collect the Warp Boss Icon in Dangerous Cliff."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(resultat.GroupePrincipal, "Le boss devrait produire un groupe principal.");
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Boss,
            resultat.GroupePrincipal!.TypeGroupe,
            "La structure boss doit être reconnue."
        );
        Assert.Equal(
            "Merlock",
            resultat.GroupePrincipal.Ancre,
            "Le nom du boss doit être extrait proprement."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les succès mentionnant le même boss devraient être rapprochés même avec un autre verbe."
        );
    }

    private static void AnalyseHybrideConserveRepliLexical()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(20, "Ring every bell in the frozen chapel."),
            CreerSucces(21, "Light every candle in the frozen chapel."),
            CreerSucces(22, "Defeat the guardian in the desert arena."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        GroupeSuccesPotentiel? groupeLexical = resultat.Groupes.FirstOrDefault(groupe =>
            groupe.TypeGroupe == TypeGroupeSuccesPotentiel.Lexical
        );

        Assert.NotNull(
            groupeLexical,
            "Le repli lexical devrait survivre lorsqu'aucun patron fort ne s'applique."
        );
        Assert.True(
            groupeLexical!.IdentifiantsSucces.Contains(21),
            "Le second succès lexicalement proche devrait être rapproché."
        );
    }

    private static void AnalyseHybrideRetrouveNiveauNommeSansPrepositionForte()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(30, "Finish Sky Fortress without taking any damage."),
            CreerSucces(31, "Open every cage in Sky Fortress."),
            CreerSucces(32, "Collect the hidden emblem in Sky Fortress."),
            CreerSucces(33, "Defeat Iron Whale."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(resultat.GroupePrincipal, "Un niveau nommé devrait être détecté.");
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le groupe principal devrait rester un niveau."
        );
        Assert.Equal(
            "Sky Fortress",
            resultat.GroupePrincipal.Ancre,
            "Le nom du niveau devrait être conservé."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les trois succès du même niveau devraient être regroupés."
        );
    }

    private static void AnalyseHybrideRetrouveNiveauNumerote()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(40, "Collect all crystals in Level 1."),
            CreerSucces(41, "Finish Level 1 in under 2 minutes."),
            CreerSucces(42, "Defeat the mini-boss in Level 1."),
            CreerSucces(43, "Finish Level 2."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(resultat.GroupePrincipal, "Un niveau numéroté devrait être détecté.");
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le groupe principal devrait être le niveau numéroté."
        );
        Assert.Equal(
            "Level 1",
            resultat.GroupePrincipal.Ancre,
            "L'ancre Level 1 devrait être retrouvée."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les succès liés à Level 1 devraient être regroupés."
        );
    }

    private static void AnalyseHybrideRattacheNiveauReferenceMentionEtendue()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(44, "Collect the 5 seals in Temple of Dawn."),
            CreerSucces(45, "Reach Temple of Dawn's secret exit."),
            CreerSucces(46, "Finish Iron Ruins."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le niveau de reference devrait pouvoir rassembler les mentions etendues."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le groupe principal devrait rester un niveau."
        );
        Assert.Equal(
            "Temple of Dawn",
            resultat.GroupePrincipal.Ancre,
            "L'ancre retenue devrait etre le niveau de base."
        );
        Assert.True(
            resultat.GroupePrincipal.IdentifiantsSucces.Contains(45),
            "La mention etendue du meme niveau devrait etre rattachee au groupe."
        );
    }

    private static void AnalyseHybrideNeConfondPasAct1EtAct10()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(47, "Collect all medals in Act 1."),
            CreerSucces(48, "Find the hidden room in Act 1."),
            CreerSucces(49, "Finish Act 10."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le niveau de reference devrait produire un groupe principal."
        );
        Assert.Equal(
            "Act 1",
            resultat.GroupePrincipal!.Ancre,
            "L'ancre devrait rester exactement Act 1."
        );
        Assert.Equal(
            2,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Act 10 ne doit pas etre inclus par erreur."
        );
        Assert.False(
            resultat.GroupePrincipal.IdentifiantsSucces.Contains(49),
            "Le groupe Act 1 ne doit jamais absorber Act 10."
        );
    }

    private static void AnalyseHybrideRattacheMondeDepuisReference()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(80, "Complete the 3 stages of Olympus."),
            CreerSucces(81, "Find every hidden relic in Olympus."),
            CreerSucces(82, "Reach the summit in Olympus."),
            CreerSucces(83, "Defeat Hydra."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le monde de reference devrait produire un groupe principal."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Monde,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le groupe principal devrait etre le monde detecte depuis la reference."
        );
        Assert.Equal(
            "Olympus",
            resultat.GroupePrincipal.Ancre,
            "L'ancre du monde devrait etre conservee."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les succes mentionnant le meme monde devraient etre rattaches."
        );
    }

    private static void AnalyseHybrideRattacheModeDepuisReference()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(84, "Activate Hyper Mode for the first time."),
            CreerSucces(85, "Win the desert race in Hyper Mode."),
            CreerSucces(86, "Clear the arena in Hyper Mode."),
            CreerSucces(87, "Collect all medals in Ancient Fate."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le mode de reference devrait produire un groupe principal."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Mode,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le groupe principal devrait etre le mode detecte depuis la reference."
        );
        Assert.Equal(
            "Hyper Mode",
            resultat.GroupePrincipal.Ancre,
            "L'ancre du mode devrait etre conservee."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les succes mentionnant le meme mode devraient etre rattaches."
        );
    }

    private static void AnalyseHybrideCollectionForteDepasseNiveauFaible()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(88, "Collect the Warp Boss Icon in Ancient Fate."),
            CreerSucces(89, "Collect the Warp Boss Icon in Dangerous Cliff."),
            CreerSucces(90, "Gain an extra life by collecting Warp Boss Icon."),
            CreerSucces(91, "Collect all rings in Ancient Fate."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Une collection plus solide devrait pouvoir devenir le groupe principal."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Collection,
            resultat.GroupePrincipal!.TypeGroupe,
            "La collection devrait depasser le niveau plus faible sur ce cas."
        );
        Assert.Equal(
            "Warp Boss Icon",
            resultat.GroupePrincipal.Ancre,
            "L'ancre retenue devrait etre l'objet de collection partage."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Le groupe principal devrait contenir les trois succes lies a cet objet."
        );
    }

    private static void AnalyseHybridePrioriseContexteNiveauSurCollectionPartagee()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(92, "Complete Level 2 on normal mode."),
            CreerSucces(93, "Collect all chests in Level 2."),
            CreerSucces(94, "Defeat the guardian in Level 2."),
            CreerSucces(95, "Collect all chests in Level 3."),
            CreerSucces(96, "Collect all chests in Level 4."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[1],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Un succes de collection dans un niveau clair devrait produire un groupe."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le contexte de niveau devrait rester prioritaire sur l'objet partage."
        );
        Assert.Equal(
            "Level 2",
            resultat.GroupePrincipal.Ancre,
            "L'ancre retenue devrait etre le niveau courant."
        );
        Assert.Equal(
            3,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Les succes du meme niveau devraient etre regroupes."
        );
    }

    private static void AnalyseHybridePrioriseNiveauSurModeRecurrent()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(50, "Finish Level 1 on Hard Mode."),
            CreerSucces(51, "Collect all red gems in Level 1."),
            CreerSucces(52, "Defeat the guardian in Level 1."),
            CreerSucces(53, "Activate Hyper Mode for the first time."),
            CreerSucces(54, "Win a race in Hard Mode."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le succès devrait être rattaché à un groupe principal."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Niveau,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le niveau doit rester prioritaire sur un mode récurrent."
        );
        Assert.Equal(
            "Level 1",
            resultat.GroupePrincipal.Ancre,
            "Le groupe principal doit pointer vers le niveau."
        );
    }

    private static void AnalyseHybridePrioriseBossSurMonde()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(60, "Defeat Hydra."),
            CreerSucces(61, "Defeat Hydra without taking damage."),
            CreerSucces(62, "Complete the 3 stages of Olympus."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[0],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Le succès devrait être rattaché à un groupe principal."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.Boss,
            resultat.GroupePrincipal!.TypeGroupe,
            "Le boss doit devenir prioritaire devant le monde quand aucun niveau n'est détecté."
        );
        Assert.Equal(
            "Hydra",
            resultat.GroupePrincipal.Ancre,
            "L'ancre prioritaire doit être celle du boss partagé."
        );
    }

    private static void AnalyseHybrideRegroupeSuccesNonRelies()
    {
        List<GameAchievementV2> succes =
        [
            CreerSucces(70, "Defeat Hydra."),
            CreerSucces(71, "Defeat Hydra without taking damage."),
            CreerSucces(72, "Press Start on the title screen."),
            CreerSucces(73, "Reset the game after viewing the ending."),
        ];

        ResultatAnalyseDescriptionsSucces resultat = ServiceAnalyseDescriptionsSucces.Analyser(
            succes[2],
            succes
        );

        Assert.NotNull(
            resultat.GroupePrincipal,
            "Un succès orphelin devrait rejoindre le groupe de repli commun."
        );
        Assert.Equal(
            TypeGroupeSuccesPotentiel.NonRelie,
            resultat.GroupePrincipal!.TypeGroupe,
            "Les succès non reliés doivent être regroupés ensemble."
        );
        Assert.Equal(
            2,
            resultat.GroupePrincipal.IdentifiantsSucces.Count,
            "Le groupe de repli doit contenir les succès non reliés."
        );
        Assert.True(
            resultat.GroupePrincipal.IdentifiantsSucces.Contains(72)
                && resultat.GroupePrincipal.IdentifiantsSucces.Contains(73),
            "Le groupe de repli doit contenir les deux succès orphelins."
        );
    }

    private static void TraductionProtegeNomsPropresProbables()
    {
        MethodInfo? methode = typeof(ServiceTraductionTexte).GetMethod(
            "ProtegerSegmentsSensibles",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(methode, "La méthode interne de protection devrait exister.");

        Dictionary<string, string> segmentsProteges = [];
        string resultat =
            (string?)
                methode!.Invoke(
                    null,
                    ["Collect all emblems in Green Hill Zone with RetroArch", segmentsProteges]
                )
            ?? string.Empty;

        Assert.True(
            segmentsProteges.Values.Contains("Green Hill Zone"),
            "Le nom du niveau devrait être protégé."
        );
        Assert.True(
            segmentsProteges.Values.Contains("RetroArch"),
            "Le nom du produit devrait être protégé."
        );
        Assert.False(
            resultat.Contains("Green Hill Zone", StringComparison.Ordinal),
            "Le segment protégé ne devrait plus apparaître tel quel avant traduction."
        );
    }

    private static void TraductionIgnoreDebutInstructionBanal()
    {
        MethodInfo? methode = typeof(ServiceTraductionTexte).GetMethod(
            "ProtegerSegmentsSensibles",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        Assert.NotNull(methode, "La méthode interne de protection devrait exister.");

        Dictionary<string, string> segmentsProteges = [];
        string resultat =
            (string?)methode!.Invoke(null, ["Finish Level 1 on Hard Mode", segmentsProteges])
            ?? string.Empty;

        Assert.Equal(
            "Finish Level 1 on Hard Mode",
            resultat,
            "Une instruction simple ne devrait pas être surprotégée."
        );
        Assert.Equal(
            0,
            segmentsProteges.Count,
            "Aucun segment ne devrait être protégé dans cette phrase générique."
        );
    }

    private static GameAchievementV2 CreerSucces(int id, string description)
    {
        return new GameAchievementV2
        {
            Id = id,
            Title = $"Succès {id}",
            Description = description,
        };
    }
}

static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T attendu, T actuel, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(attendu, actuel))
        {
            throw new InvalidOperationException($"{message} Attendu={attendu}; Actuel={actuel}");
        }
    }

    public static void NotNull(object? valeur, string message)
    {
        if (valeur is null)
        {
            throw new InvalidOperationException(message);
        }
    }
}
