using System.Reflection;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;
using RA.Compagnon.Services;

return TestRunner.Executer();

static class TestRunner
{
    private static int _echecs;

    public static int Executer()
    {
        ExecuterTest(
            "Orchestrateur ignore une detection locale faible apres un jeu valide recent",
            OrchestrateurIgnoreDetectionLocaleFaibleApresJeuValideRecent
        );
        ExecuterTest(
            "Orchestrateur conserve un jeu valide face a un aucun jeu tardif",
            OrchestrateurConserveJeuValideFaceAAucunJeuTardif
        );
        ExecuterTest(
            "Resolution rapide retrouve le bon GameID depuis les jeux recents",
            ResolutionRapideRetrouveLeBonGameId
        );
        ExecuterTest(
            "Resolution catalogue utilise aussi les titres alternatifs",
            ResolutionCatalogueUtiliseTitresAlternatifs
        );
        ExecuterTest(
            "Detection de succes local repere un nouveau hardcore",
            DetectionSuccesRepereNouveauHardcore
        );
        ExecuterTest(
            "Detection de succes local ignore un succes deja observe",
            DetectionSuccesIgnoreSuccesDejaObserve
        );
        ExecuterTest(
            "Le debloqueur virtuel couvre les familles log local",
            DebloqueurVirtuelCouvreLesFamillesLogLocal
        );
        ExecuterTest(
            "Fusion de succes catalogue ne regresse pas sur une liste longue",
            FusionSuccesCatalogueNeRegressePasSurListeLongue
        );
        ExecuterTest(
            "Comparaison de versions detecte une version plus recente",
            ComparaisonVersionsDetecteVersionPlusRecente
        );
        ExecuterTest(
            "Comparaison de versions traite 1.0.2 et 1.0.2.0 comme equivalentes",
            ComparaisonVersionsTraiteFormatsEquivalents
        );

        Console.WriteLine();
        Console.WriteLine(
            _echecs == 0 ? "Tous les tests sont passes." : $"{_echecs} test(s) en echec."
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
            Console.WriteLine($"[ECHEC] {nom}");
            Console.WriteLine($"        {exception.Message}");
        }
    }

    private static void OrchestrateurIgnoreDetectionLocaleFaibleApresJeuValideRecent()
    {
        ServiceOrchestrateurEtatJeu service = new();
        Assert.True(
            service.EnregistrerJeuAffiche(11423, "Ace Combat", "api"),
            "Le jeu affiche initial doit etre accepte."
        );

        bool transitionAcceptee = service.EnregistrerDetectionLocale(0, string.Empty, "local");

        Assert.False(
            transitionAcceptee,
            "Une detection locale faible recente ne doit pas ecraser un jeu valide."
        );
        Assert.Equal(
            11423,
            service.EtatCourant.IdentifiantJeu,
            "Le GameID affiche doit etre conserve."
        );
        Assert.Equal(
            "Ace Combat",
            service.EtatCourant.TitreJeu,
            "Le titre affiche doit etre conserve."
        );
    }

    private static void OrchestrateurConserveJeuValideFaceAAucunJeuTardif()
    {
        ServiceOrchestrateurEtatJeu service = new();
        Assert.True(
            service.EnregistrerJeuAffiche(18160, "Crash of the Titans", "api"),
            "Le jeu affiche initial doit etre accepte."
        );

        bool transitionAcceptee = service.EnregistrerAucunJeu("local");

        Assert.False(
            transitionAcceptee,
            "Un 'aucun jeu' tardif ne doit pas vider un jeu recent deja valide."
        );
        Assert.Equal(
            18160,
            service.EtatCourant.IdentifiantJeu,
            "Le GameID recent doit etre conserve."
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

        Assert.NotNull(resolution, "La resolution rapide devrait trouver un jeu recent identique.");
        Assert.Equal(18160, resolution!.IdentifiantJeu, "Le GameID recent doit etre retrouve.");
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
            "Le GameID du titre alternatif doit etre retrouve."
        );
        Assert.Equal(
            "catalogue_local",
            resolution.Source,
            "La source attendue est catalogue_local."
        );
    }

    private static void DetectionSuccesRepereNouveauHardcore()
    {
        IReadOnlyDictionary<int, EtatObservationSuccesLocal> etatPrecedent =
            new Dictionary<int, EtatObservationSuccesLocal>();
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

        Assert.Equal(1, resultat.Count, "Un nouveau succes hardcore doit etre detecte.");
        Assert.True(resultat[0].Hardcore, "Le succes detecte doit etre marque hardcore.");
        Assert.Equal(214818, resultat[0].IdentifiantSucces, "Le bon succes doit etre detecte.");
    }

    private static void DetectionSuccesIgnoreSuccesDejaObserve()
    {
        IReadOnlyDictionary<int, EtatObservationSuccesLocal> etatPrecedent = new Dictionary<
            int,
            EtatObservationSuccesLocal
        >
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

        Assert.Equal(0, resultat.Count, "Un succes deja observe ne doit pas etre re-detecte.");
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
                    Titre = $"Succes {index}",
                    Points = index,
                }),
        ];
        List<SuccesCatalogueLocal> succesRecus =
        [
            new SuccesCatalogueLocal
            {
                AchievementId = 1,
                Titre = "Succes 1 mis a jour",
                Points = 999,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 2,
                Titre = "Succes 2",
                Points = 2,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 3,
                Titre = "Succes 3",
                Points = 3,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 4,
                Titre = "Succes 4",
                Points = 4,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 5,
                Titre = "Succes 5",
                Points = 5,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 6,
                Titre = "Succes 6",
                Points = 6,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 7,
                Titre = "Succes 7",
                Points = 7,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 8,
                Titre = "Succes 8",
                Points = 8,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 9,
                Titre = "Succes 9",
                Points = 9,
            },
            new SuccesCatalogueLocal
            {
                AchievementId = 10,
                Titre = "Succes 10",
                Points = 10,
            },
        ];

        MethodInfo? methodeFusion = typeof(ServiceCatalogueJeuxLocal).GetMethod(
            "FusionnerSuccesSansRegresser",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(
            methodeFusion,
            "La methode de fusion des succes doit etre trouvable par reflection."
        );

        object? resultatBrut = methodeFusion!.Invoke(null, [succesExistants, succesRecus]);
        List<SuccesCatalogueLocal>? resultat = resultatBrut as List<SuccesCatalogueLocal>;

        Assert.NotNull(resultat, "La fusion doit retourner une liste de succes.");
        Assert.Equal(
            204,
            resultat!.Count,
            "Une mise a jour partielle ne doit pas reduire une longue liste existante."
        );
        Assert.Equal(
            999,
            resultat.First(item => item.AchievementId == 1).Points,
            "Les succes recents doivent quand meme mettre a jour les entrees recues."
        );
        Assert.NotNull(
            resultat.FirstOrDefault(item => item.AchievementId == 204),
            "Les succes absents de la mise a jour partielle doivent etre conserves."
        );
    }

    private static void ComparaisonVersionsDetecteVersionPlusRecente()
    {
        int resultat = ServiceMiseAJourApplication.ComparerVersions("1.0.3", "1.0.2");

        Assert.True(resultat > 0, "La version 1.0.3 devrait etre plus recente que 1.0.2.");
    }

    private static void ComparaisonVersionsTraiteFormatsEquivalents()
    {
        int resultat = ServiceMiseAJourApplication.ComparerVersions("1.0.2", "1.0.2.0");

        Assert.Equal(0, resultat, "1.0.2 et 1.0.2.0 devraient etre considerees equivalentes.");
    }

    private static void DebloqueurVirtuelCouvreLesFamillesLogLocal()
    {
        string[] emulateurs = ["RetroArch", "DuckStation", "PCSX2", "PPSSPP"];

        foreach (string nomEmulateur in emulateurs)
        {
            Assert.True(
                ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur),
                $"Le succes local direct devrait etre pris en charge pour {nomEmulateur}."
            );
            Assert.Equal(
                "logs",
                ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(nomEmulateur),
                $"Le type de source locale attendu pour {nomEmulateur} devrait etre logs."
            );
            Assert.True(
                ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
                    nomEmulateur,
                    "logs"
                ),
                $"Le type logs devrait porter un succes local direct pour {nomEmulateur}."
            );
        }

        string[] emulateursRACache = ["RALibretro", "RAP64"];

        foreach (string nomEmulateur in emulateursRACache)
        {
            Assert.True(
                ServiceCatalogueEmulateursLocaux.EstSuccesLocalDirectPrisEnCharge(nomEmulateur),
                $"Le succes local direct devrait etre pris en charge pour {nomEmulateur}."
            );
            Assert.Equal(
                "racache_log",
                ServiceCatalogueEmulateursLocaux.ObtenirTypeSourceJournalSuccesLocal(nomEmulateur),
                $"Le type de source locale attendu pour {nomEmulateur} devrait etre racache_log."
            );
            Assert.True(
                ServiceCatalogueEmulateursLocaux.TypeSourcePeutPorterSuccesDirect(
                    nomEmulateur,
                    "racache_log"
                ),
                $"Le type racache_log devrait porter un succes local direct pour {nomEmulateur}."
            );
        }
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
