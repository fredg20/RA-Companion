using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Detecte localement les principaux emulateurs connus a partir des processus et titres de fenetre.
/// </summary>
public sealed partial class ServiceSondeLocaleEmulateurs
{
    private sealed record RenseignementJeuRA(int IdentifiantJeu, string TitreJeu);

    public sealed record RenseignementSuccesRA(int IdentifiantSucces, string TitreSucces);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int ProcessCommandLineInformation = 60;
    private static readonly Lock VerrouCacheDuckStation = new();
    private static readonly Lock VerrouCacheRALibretro = new();
    private static readonly Lock VerrouCachePPSSPP = new();
    private static readonly Lock VerrouCacheRetroArch = new();
    private static readonly Lock VerrouCacheFlycast = new();
    private static string _dernierRepertoireDuckStation = string.Empty;
    private static DateTime _dernierHorodatageCacheGamelistUtc = DateTime.MinValue;
    private static Dictionary<string, string> _cacheSerialVersCheminDuckStation = [];
    private static RenseignementJeuRA? _dernierRenseignementRALibretro;
    private static DateTime _dernierHorodatageRenseignementRALibretroUtc = DateTime.MinValue;
    private static RenseignementJeuRA? _dernierRenseignementPPSSPP;
    private static DateTime _dernierHorodatageRenseignementPPSSPPUtc = DateTime.MinValue;
    private static RenseignementJeuRA? _dernierRenseignementRetroArch;
    private static DateTime _dernierHorodatageRenseignementRetroArchUtc = DateTime.MinValue;
    private static string _dernierRepertoireContenuFlycast = string.Empty;
    private static DateTime _dernierHorodatageCacheContenuFlycastUtc = DateTime.MinValue;
    private static List<string> _cacheFichiersJeuFlycast = [];

    private static readonly IReadOnlyList<DefinitionEmulateurLocal> Definitions =
        ServiceCatalogueEmulateursLocaux.Definitions;

    private static readonly string CheminJournalSondeLocale = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-sonde-locale.log"
    );
    private string _derniereSignatureJournalisee = "\u0000";

    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalSondeLocale);
    }

    public static string ObtenirCheminJournal() => CheminJournalSondeLocale;

    public static bool EssayerObtenirContexteRejouerDepuisSources(
        string nomEmulateur,
        out int identifiantJeu,
        out string titreJeu,
        out string cheminExecutable,
        out string cheminJeu
    )
    {
        identifiantJeu = 0;
        titreJeu = string.Empty;
        cheminExecutable = string.Empty;
        cheminJeu = string.Empty;

        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return false;
        }

        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null || !ServiceCatalogueEmulateursLocaux.EstEmulateurValide(definition))
        {
            return false;
        }

        cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
            definition.NomEmulateur
        );

        if (string.IsNullOrWhiteSpace(cheminExecutable) || !File.Exists(cheminExecutable))
        {
            return false;
        }

        RenseignementJeuRA? renseignementJeu = definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireRenseignementJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireRenseignementJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                LireRenseignementJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                LireRenseignementJeuPPSSPPDepuisLog(string.Empty),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireRenseignementJeuFlycastDepuisSourcesRejouer(),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireRenseignementJeuProject64DepuisRACache(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireRenseignementJeuRALibretroDepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireRenseignementJeuRANesDepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireRenseignementJeuRAVBADepuisRACache(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireRenseignementJeuRASnes9xDepuisRACache(),
            _ => null,
        };

        identifiantJeu = renseignementJeu?.IdentifiantJeu ?? 0;
        titreJeu = renseignementJeu?.TitreJeu ?? string.Empty;

        cheminJeu = definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireCheminJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireCheminJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => LireCheminJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => LireCheminJeuPPSSPPDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireCheminJeuFlycastDepuisLogOuConfiguration(titreJeu),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireCheminJeuProject64DepuisConfiguration(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireCheminJeuRALibretroDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireCheminJeuRANesDepuisConfiguration(titreJeu),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireCheminJeuRAVBADepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireCheminJeuRASnes9xDepuisConfiguration(),
            _ => string.Empty,
        };

        return identifiantJeu > 0
            && !string.IsNullOrWhiteSpace(cheminJeu)
            && File.Exists(cheminJeu);
    }

    public static void JournaliserEvenement(string evenement, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalSondeLocale,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={NettoyerPourJournal(evenement)};details={NettoyerPourJournal(details)}{Environment.NewLine}"
            )
        );
    }

    public EtatSondeLocaleEmulateur Sonder(bool journaliser = true)
    {
        try
        {
            Process[] processus = Process.GetProcesses();

            foreach (DefinitionEmulateurLocal definition in Definitions)
            {
                EtatSondeLocaleEmulateur? etat = SonderPourDefinition(definition, processus);

                if (etat is not null)
                {
                    if (journaliser)
                    {
                        JournaliserSiChangement(etat);
                    }

                    return etat;
                }
            }
        }
        catch
        {
            // Une erreur ponctuelle de lecture des processus ne doit pas casser l'application.
        }

        EtatSondeLocaleEmulateur etatAucun = new()
        {
            EmulateurDetecte = false,
            Signature = string.Empty,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };

        if (journaliser)
        {
            JournaliserSiChangement(etatAucun);
        }

        return etatAucun;
    }

    public static bool SonderPresenceEmulateur()
    {
        try
        {
            Process[] processus = Process.GetProcesses();
            return Definitions.Any(definition =>
                processus.Any(processusCourant =>
                    CorrespondNomProcessus(processusCourant, definition)
                )
            );
        }
        catch
        {
            return false;
        }
    }

    private static EtatSondeLocaleEmulateur? SonderPourDefinition(
        DefinitionEmulateurLocal definition,
        IEnumerable<Process> processus
    )
    {
        Process? processusCible = processus
            .Where(processusCourant => Correspond(processusCourant, definition))
            .OrderByDescending(ProcessusPossedeUneFenetreVisible)
            .ThenByDescending(processusCourant => LireTitreFenetre(processusCourant).Length)
            .FirstOrDefault();

        if (processusCible is null)
        {
            return null;
        }

        bool correspondAuNomProcessus = CorrespondNomProcessus(processusCible, definition);
        bool correspondAuxMetadonneesExecutable = CorrespondMetadonneesExecutable(
            processusCible,
            definition
        );
        IReadOnlyList<string> titresFenetres = LireTitresFenetresVisibles(processusCible);
        string titreFenetre = ChoisirTitreFenetre(definition, processusCible, titresFenetres);
        string cheminExecutable =
            correspondAuNomProcessus || correspondAuxMetadonneesExecutable
                ? LireCheminExecutableProcessus(processusCible)
                : string.Empty;

        if (
            string.IsNullOrWhiteSpace(cheminExecutable)
            && (correspondAuNomProcessus || correspondAuxMetadonneesExecutable)
        )
        {
            cheminExecutable = ServiceSourcesLocalesEmulateurs.TrouverEmplacementEmulateur(
                definition.NomEmulateur
            );
        }

        string titreJeuProbable = ExtraireTitreJeuPourDefinition(
            definition,
            processusCible,
            titreFenetre
        );
        string cheminJeuProbable = ExtraireCheminJeuPourDefinition(
            definition,
            processusCible,
            titreJeuProbable
        );
        int identifiantJeuProbable = 0;
        string informationsDiagnostic = string.Empty;

        if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.Project64RACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuProject64DepuisRACache(
                definition.NomEmulateur
            );

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RALibretroRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }

            if (identifiantJeuProbable <= 0)
            {
                titreJeuProbable = string.Empty;
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RANesRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRANesDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RAVBARACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRAVBADepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRASnes9xDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.FlycastConfig
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuFlycastDepuisLog(
                processusCible,
                titreJeuProbable
            );

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.RetroArchLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRetroArchDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.DuckStationLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuDuckStationDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu == StrategieRenseignementJeuEmulateurLocal.PCSX2Log
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuPCSX2DepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }
        else if (
            definition.StrategieRenseignementJeu
            == StrategieRenseignementJeuEmulateurLocal.PPSSPPLog
        )
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuPPSSPPDepuisLog(
                titreJeuProbable
            );

            renseignementJeu ??= LireRenseignementJeuPPSSPPDepuisCache(titreJeuProbable);

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic = ConstruireDiagnosticSourceJeu(
                    definition.StrategieRenseignementJeu,
                    identifiantJeuProbable
                );
            }
        }

        if (
            definition.StrategieRenseignementJeu
                == StrategieRenseignementJeuEmulateurLocal.DuckStationLog
            && string.IsNullOrWhiteSpace(titreJeuProbable)
        )
        {
            informationsDiagnostic = ConstruireDiagnosticDuckStation(
                processusCible,
                titresFenetres
            );
        }

        string cheminJeuRecalcule = ExtraireCheminJeuPourDefinition(
            definition,
            processusCible,
            titreJeuProbable
        );

        if (!string.IsNullOrWhiteSpace(cheminJeuRecalcule))
        {
            cheminJeuProbable = cheminJeuRecalcule;
        }

        string signature =
            $"{definition.NomEmulateur}|{processusCible.ProcessName}|{titreFenetre}|{titreJeuProbable}|{cheminJeuProbable}|{identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}|{informationsDiagnostic}";

        return new EtatSondeLocaleEmulateur
        {
            EmulateurDetecte = true,
            NomEmulateur = definition.NomEmulateur,
            NomProcessus = processusCible.ProcessName,
            CheminExecutable = cheminExecutable,
            TitreFenetre = titreFenetre,
            TitreJeuProbable = titreJeuProbable,
            CheminJeuProbable = cheminJeuProbable,
            IdentifiantJeuProbable = identifiantJeuProbable,
            InformationsDiagnostic = informationsDiagnostic,
            Signature = signature,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    private static string ExtraireCheminJeuPourDefinition(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreJeuProbable = ""
    )
    {
        string cheminDepuisCommande = NormaliserCheminJeuProbable(
            ExtraireCheminJeuDepuisLigneCommande(LireLigneCommandeProcessus(processus))
        );

        if (!string.IsNullOrWhiteSpace(cheminDepuisCommande))
        {
            string cheminExecutable = LireCheminExecutableProcessus(processus);

            if (
                !string.IsNullOrWhiteSpace(cheminExecutable)
                && string.Equals(
                    cheminDepuisCommande,
                    cheminExecutable,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                cheminDepuisCommande = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(cheminDepuisCommande))
        {
            return cheminDepuisCommande;
        }

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                LireCheminJeuRetroArchDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                LireCheminJeuDuckStationDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => LireCheminJeuPCSX2DepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => LireCheminJeuPPSSPPDepuisLog(),
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                LireCheminJeuFlycastDepuisLogOuConfiguration(titreJeuProbable),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache =>
                LireCheminJeuProject64DepuisConfiguration(definition.NomEmulateur),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache =>
                LireCheminJeuRALibretroDepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache =>
                LireCheminJeuRANesDepuisConfiguration(titreJeuProbable),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache =>
                LireCheminJeuRAVBADepuisConfiguration(),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache =>
                LireCheminJeuRASnes9xDepuisConfiguration(),
            _ => string.Empty,
        };
    }

    private static string ExtraireTitreJeuPourDefinition(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreFenetre
    )
    {
        return definition.StrategieExtractionTitre switch
        {
            StrategieExtractionTitreEmulateurLocal.SeparateursRetroArch =>
                ExtraireTitreAvecSeparateurs(titreFenetre, "RetroArch", "RetroArch "),
            StrategieExtractionTitreEmulateurLocal.RALibretro => ExtraireTitreRALibretro(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.DuckStation => ExtraireTitreDuckStation(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.PCSX2 => ExtraireTitrePCSX2(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.PPSSPP => ExtraireTitrePPSSPP(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.RANes => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RANes"
            ),
            StrategieExtractionTitreEmulateurLocal.RAVBA => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RAVBA",
                "RAVBA-M",
                "RAVisualBoyAdvance",
                "RAVisualBoyAdvance-M"
            ),
            StrategieExtractionTitreEmulateurLocal.RASnes9x => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "RASnes9x"
            ),
            StrategieExtractionTitreEmulateurLocal.Dolphin => ExtraireTitreDolphin(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.Project64 => ExtraireTitreProject64(
                processus,
                titreFenetre
            ),
            StrategieExtractionTitreEmulateurLocal.Flycast => ExtraireTitreAvecSeparateurs(
                titreFenetre,
                "Flycast"
            ),
            _ => string.Empty,
        };
    }

    private static bool Correspond(Process processus, DefinitionEmulateurLocal definition)
    {
        bool correspondAuNomProcessus = CorrespondNomProcessus(processus, definition);

        if (correspondAuNomProcessus)
        {
            return true;
        }

        bool correspondAuxMetadonneesExecutable = CorrespondMetadonneesExecutable(
            processus,
            definition
        );

        if (correspondAuxMetadonneesExecutable)
        {
            return true;
        }

        // RetroArch, DuckStation et PCSX2 ont des variantes de fenetres/outils qui rendent
        // le fallback par titre trop bruyant (explorer, navigateurs, installateur, dialogues internes, etc.).
        if (!definition.AutoriserDetectionParTitreFenetre)
        {
            return false;
        }

        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;

        if (
            string.Equals(nomProcessus, "explorer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nomProcessus, "openwith", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nomProcessus, "dllhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        string titreFenetre = LireTitreFenetre(processus);

        return !string.IsNullOrWhiteSpace(titreFenetre)
            && titreFenetre.StartsWith(definition.NomEmulateur, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CorrespondNomProcessus(
        Process processus,
        DefinitionEmulateurLocal definition
    )
    {
        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;

        if (
            string.Equals(definition.NomEmulateur, "DuckStation", StringComparison.Ordinal)
            && nomProcessus.Contains("duckstation", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return definition.NomsProcessus.Any(nom =>
            string.Equals(nomProcessus, nom, StringComparison.OrdinalIgnoreCase)
            || nomProcessus.StartsWith(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool CorrespondMetadonneesExecutable(
        Process processus,
        DefinitionEmulateurLocal definition
    )
    {
        try
        {
            string cheminExecutable = processus.MainModule?.FileName?.Trim() ?? string.Empty;

            if (
                ServiceSourcesLocalesEmulateurs.CorrespondAuCheminEmulateurManuel(
                    definition.NomEmulateur,
                    cheminExecutable
                )
            )
            {
                return true;
            }

            FileVersionInfo? version = processus.MainModule?.FileVersionInfo;

            string[] valeurs =
            [
                cheminExecutable,
                version?.ProductName ?? string.Empty,
                version?.FileDescription ?? string.Empty,
                version?.OriginalFilename ?? string.Empty,
                version?.InternalName ?? string.Empty,
            ];

            string[] jetons = ObtenirJetonsCorrespondanceEmulateur(definition);

            return valeurs.Any(valeur => CorrespondValeurEmpreinteEmulateur(valeur, jetons));
        }
        catch
        {
            return false;
        }
    }

    private static bool CorrespondValeurEmpreinteEmulateur(
        string valeur,
        IReadOnlyList<string> jetons
    )
    {
        string valeurNormalisee = NormaliserEmpreinteExecutable(valeur);

        if (string.IsNullOrWhiteSpace(valeurNormalisee))
        {
            return false;
        }

        return jetons.Any(jeton =>
        {
            string jetonNormalise = NormaliserEmpreinteExecutable(jeton);
            return !string.IsNullOrWhiteSpace(jetonNormalise)
                && valeurNormalisee.Contains(jetonNormalise, StringComparison.Ordinal);
        });
    }

    private static string LireCheminExecutableProcessus(Process processus)
    {
        try
        {
            return processus.MainModule?.FileName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string[] ObtenirJetonsCorrespondanceEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        List<string> jetons = [definition.NomEmulateur, .. definition.NomsProcessus];

        if (string.Equals(definition.NomEmulateur, "RAVBA", StringComparison.Ordinal))
        {
            jetons.Add("VisualBoyAdvance");
            jetons.Add("VisualBoyAdvance-M");
        }
        else if (string.Equals(definition.NomEmulateur, "RASnes9x", StringComparison.Ordinal))
        {
            jetons.Add("Snes9x");
        }
        else if (string.Equals(definition.NomEmulateur, "LunaProject64", StringComparison.Ordinal))
        {
            jetons.Add("Project64");
            jetons.Add("Luna Project64");
        }
        else if (string.Equals(definition.NomEmulateur, "RAP64", StringComparison.Ordinal))
        {
            jetons.Add("RAProject64");
            jetons.Add("RA Project64");
            jetons.Add("RAP64");
        }

        return [.. jetons.Where(jeton => !string.IsNullOrWhiteSpace(jeton)).Distinct()];
    }

    private static string NormaliserEmpreinteExecutable(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        StringBuilder builder = new(valeur.Length);

        foreach (char caractere in valeur)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                builder.Append(char.ToLowerInvariant(caractere));
            }
        }

        return builder.ToString();
    }

    private static int ProcessusPossedeUneFenetreVisible(Process processus)
    {
        try
        {
            return LireTitresFenetresVisibles(processus).Count;
        }
        catch
        {
            return 0;
        }
    }

    private static string LireTitreFenetre(Process processus)
    {
        try
        {
            return ChoisirTitreFenetre(
                new DefinitionEmulateurLocal(
                    string.Empty,
                    [],
                    [],
                    StrategieExtractionTitreEmulateurLocal.Flycast,
                    StrategieRenseignementJeuEmulateurLocal.Aucune,
                    StrategieSurveillanceSuccesLocale.Aucune,
                    true,
                    false,
                    []
                ),
                processus,
                LireTitresFenetresVisibles(processus)
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtraireTitreAvecSeparateurs(
        string titreFenetre,
        params string[] jetonsEmulateur
    )
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string[] separateurs = [" - ", " | ", " \u2014 ", " \u2013 ", " :: ", " / "];

        foreach (string separateur in separateurs)
        {
            string[] morceaux = titre.Split(separateur, StringSplitOptions.RemoveEmptyEntries);

            if (morceaux.Length < 2)
            {
                continue;
            }

            string premier = morceaux[0].Trim();
            string dernier = morceaux[^1].Trim();

            if (ContientJetonEmulateur(dernier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(premier, jetonsEmulateur);
            }

            if (ContientJetonEmulateur(premier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(dernier, jetonsEmulateur);
            }
        }

        if (ContientJetonEmulateur(titre, jetonsEmulateur))
        {
            return string.Empty;
        }

        return NettoyerTitreJeu(titre, jetonsEmulateur);
    }

    private static bool ContientJetonEmulateur(string valeur, IEnumerable<string> jetonsEmulateur)
    {
        return jetonsEmulateur.Any(jeton =>
            valeur.Contains(jeton, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string NettoyerTitreJeu(string titre, IEnumerable<string> jetonsEmulateur)
    {
        string resultat = titre.Trim();

        foreach (string jeton in jetonsEmulateur)
        {
            resultat = Regex.Replace(
                resultat,
                $@"\b{Regex.Escape(jeton)}\b",
                string.Empty,
                RegexOptions.IgnoreCase
            );
        }

        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    private static string ExtraireTitreDuckStation(Process processus, string titreFenetre)
    {
        string titreFenetreExtrait = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "DuckStation",
            "DuckStation "
        );

        if (!string.IsNullOrWhiteSpace(titreFenetreExtrait))
        {
            return titreFenetreExtrait;
        }

        string titreAutomatisation = ExtraireTitreDuckStationDepuisAutomatisation(processus);

        if (!string.IsNullOrWhiteSpace(titreAutomatisation))
        {
            return titreAutomatisation;
        }

        string ligneCommande = LireLigneCommandeProcessus(processus);
        string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

        if (!string.IsNullOrWhiteSpace(cheminJeu))
        {
            return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
        }

        string titreDepuisMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

        if (!string.IsNullOrWhiteSpace(titreDepuisMemcard))
        {
            return titreDepuisMemcard;
        }

        return string.Empty;
    }

    private static string ExtraireTitrePCSX2(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PCSX2");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialoguePCSX2(titreNettoye))
        {
            return string.Empty;
        }

        return titreNettoye;
    }

    private static string ExtraireTitrePPSSPP(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PPSSPP");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        // PPSSPP affiche souvent le serial PSP devant le vrai titre.
        titreNettoye = PrefixeSerialPpssppRegex().Replace(titreNettoye, string.Empty).Trim();

        titreNettoye = titreNettoye.Replace("\u00AE", string.Empty, StringComparison.Ordinal);
        titreNettoye = EspacesMultiplesRegex().Replace(titreNettoye, " ").Trim();

        return titreNettoye;
    }

    private static string ExtraireTitreDolphin(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "Dolphin",
            "Dolphin Emulator",
            "Slippi Dolphin",
            "Slippi Dolphin Launcher"
        );

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialogueDolphin(titreNettoye))
        {
            return string.Empty;
        }

        titreNettoye = SuffixeCodeJeuDolphinRegex().Replace(titreNettoye, string.Empty).Trim();

        return titreNettoye;
    }

    private static string ExtraireTitreProject64(Process _, string titreFenetre)
    {
        return ExtraireTitreProject64DepuisFenetre(titreFenetre);
    }

    private static string ExtraireTitreRALibretro(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreRALibretroDepuisFenetre(titreFenetre);

        if (!string.IsNullOrWhiteSpace(titre))
        {
            return titre;
        }

        RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

        if (renseignementJeu is not null && !string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
        {
            return renseignementJeu.TitreJeu;
        }

        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recent)
                && recent.ValueKind == JsonValueKind.Array
                && recent.GetArrayLength() > 0
                && recent[0].TryGetProperty("path", out JsonElement path)
                && path.ValueKind == JsonValueKind.String
            )
            {
                string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }
        }
        catch
        {
            // Le JSON local reste un fallback opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuRALibretroDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recent)
                && recent.ValueKind == JsonValueKind.Array
                && recent.GetArrayLength() > 0
                && recent[0].TryGetProperty("path", out JsonElement path)
                && path.ValueKind == JsonValueKind.String
            )
            {
                return NormaliserCheminJeuProbable(path.GetString()?.Trim() ?? string.Empty);
            }
        }
        catch
        {
            // Le JSON local reste un fallback opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuProject64DepuisConfiguration(string nomEmulateur)
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationProject64(nomEmulateur);

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Recent Rom 0=";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[prefixe.Length..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le fichier local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuRANesDepuisConfiguration(string titreJeuProbable)
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRANes();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            List<string> cheminsCandidats = [];

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                if (ligne.StartsWith("ResumeROM ", StringComparison.OrdinalIgnoreCase))
                {
                    string cheminResume = NormaliserCheminJeuProbable(
                        ligne["ResumeROM ".Length..].Trim()
                    );

                    if (!string.IsNullOrWhiteSpace(cheminResume))
                    {
                        cheminsCandidats.Add(cheminResume);
                    }
                }

                if (!ligne.StartsWith("recent_files[", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf(' ');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[(indexSeparateur + 1)..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    cheminsCandidats.Add(cheminNormalise);
                }
            }

            cheminsCandidats = [.. cheminsCandidats.Distinct(StringComparer.OrdinalIgnoreCase)];

            if (cheminsCandidats.Count == 0)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(titreJeuProbable))
            {
                return cheminsCandidats[0];
            }

            string meilleurChemin = string.Empty;
            int meilleurScore = int.MinValue;

            foreach (string cheminCandidat in cheminsCandidats)
            {
                string nomFichier = NettoyerNomFichierJeu(
                    Path.GetFileNameWithoutExtension(cheminCandidat)
                );
                string nomDossier = NettoyerNomFichierJeu(
                    Path.GetFileName(Path.GetDirectoryName(cheminCandidat) ?? string.Empty)
                );

                int score = 0;

                if (TitresSemblables(nomFichier, titreJeuProbable))
                {
                    score += 120;
                }
                else
                {
                    score +=
                        CalculerScoreProximiteTitreSouple(
                            NormaliserTitreComparaisonSouple(titreJeuProbable),
                            nomFichier
                        ) * 10;
                }

                if (TitresSemblables(nomDossier, titreJeuProbable))
                {
                    score += 80;
                }
                else
                {
                    score +=
                        CalculerScoreProximiteTitreSouple(
                            NormaliserTitreComparaisonSouple(titreJeuProbable),
                            nomDossier
                        ) * 6;
                }

                if (score <= meilleurScore)
                {
                    continue;
                }

                meilleurScore = score;
                meilleurChemin = cheminCandidat;
            }

            return !string.IsNullOrWhiteSpace(meilleurChemin)
                ? meilleurChemin
                : cheminsCandidats[0];
        }
        catch
        {
            // Le fichier local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuRASnes9xDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRASnes9x();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Rom:RecentGame1";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf('=');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne[(indexSeparateur + 1)..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le fichier local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuRAVBADepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRAVBA();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                if (!ligne.StartsWith("file1=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    ligne["file1=".Length..].Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le fichier local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuPCSX2DepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPCSX2();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIso = PCSX2IsoOuverteRegex().Match(ligne);

                if (!correspondanceIso.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceIso.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le log local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuFlycastDepuisConfiguration(string titreJeuProbable)
    {
        try
        {
            string cheminConfiguration =
                ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationFlycast();

            if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
            {
                return string.Empty;
            }

            string repertoireContenu = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                    cheminConfiguration
                )
            )
            {
                const string prefixe = "Dreamcast.ContentPath";

                if (!ligne.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int indexSeparateur = ligne.IndexOf('=');

                if (indexSeparateur < 0 || indexSeparateur == ligne.Length - 1)
                {
                    continue;
                }

                repertoireContenu = ligne[(indexSeparateur + 1)..].Trim();
                break;
            }

            if (
                string.IsNullOrWhiteSpace(repertoireContenu) || !Directory.Exists(repertoireContenu)
            )
            {
                return string.Empty;
            }

            return TrouverCheminJeuFlycastDepuisRepertoireContenu(
                repertoireContenu,
                titreJeuProbable
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string LireCheminJeuFlycastDepuisLogOuConfiguration(string titreJeuProbable)
    {
        string cheminDepuisLog = LireCheminJeuFlycastDepuisLog();

        if (!string.IsNullOrWhiteSpace(cheminDepuisLog))
        {
            return cheminDepuisLog;
        }

        return LireCheminJeuFlycastDepuisConfiguration(titreJeuProbable);
    }

    private static string TrouverCheminJeuFlycastDepuisRepertoireContenu(
        string repertoireContenu,
        string titreJeuProbable
    )
    {
        if (
            string.IsNullOrWhiteSpace(repertoireContenu)
            || string.IsNullOrWhiteSpace(titreJeuProbable)
        )
        {
            return string.Empty;
        }

        string repertoireNormalise;

        try
        {
            repertoireNormalise = Path.GetFullPath(repertoireContenu.Trim());
        }
        catch
        {
            return string.Empty;
        }

        if (!Directory.Exists(repertoireNormalise))
        {
            return string.Empty;
        }

        string titreNormalise = NormaliserTitreComparaisonSouple(titreJeuProbable);

        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            return string.Empty;
        }

        List<string> fichiersJeu = ObtenirFichiersJeuFlycast(repertoireNormalise);
        string meilleurChemin = string.Empty;
        int meilleurScore = int.MinValue;

        foreach (string cheminFichier in fichiersJeu)
        {
            string nomFichier = NettoyerNomFichierJeu(
                Path.GetFileNameWithoutExtension(cheminFichier)
            );
            string nomDossier = NettoyerNomFichierJeu(
                Path.GetFileName(Path.GetDirectoryName(cheminFichier) ?? string.Empty)
            );

            int score = 0;

            if (TitresSemblables(nomFichier, titreJeuProbable))
            {
                score += 120;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomFichier) * 10;
            }

            if (TitresSemblables(nomDossier, titreJeuProbable))
            {
                score += 80;
            }
            else
            {
                score += CalculerScoreProximiteTitreSouple(titreNormalise, nomDossier) * 6;
            }

            if (score <= meilleurScore)
            {
                continue;
            }

            meilleurScore = score;
            meilleurChemin = cheminFichier;
        }

        return meilleurScore >= 30 ? meilleurChemin : string.Empty;
    }

    private static List<string> ObtenirFichiersJeuFlycast(string repertoireContenu)
    {
        lock (VerrouCacheFlycast)
        {
            if (
                string.Equals(
                    _dernierRepertoireContenuFlycast,
                    repertoireContenu,
                    StringComparison.OrdinalIgnoreCase
                )
                && DateTime.UtcNow - _dernierHorodatageCacheContenuFlycastUtc
                    < TimeSpan.FromMinutes(5)
                && _cacheFichiersJeuFlycast.Count > 0
            )
            {
                return [.. _cacheFichiersJeuFlycast];
            }

            string[] extensions = [".cue", ".cdi", ".gdi", ".chd"];

            _cacheFichiersJeuFlycast =
            [
                .. new DirectoryInfo(repertoireContenu)
                    .EnumerateFiles("*.*", SearchOption.AllDirectories)
                    .Where(fichier =>
                        extensions.Contains(fichier.Extension, StringComparer.OrdinalIgnoreCase)
                    )
                    .Select(fichier => fichier.FullName),
            ];
            _dernierRepertoireContenuFlycast = repertoireContenu;
            _dernierHorodatageCacheContenuFlycastUtc = DateTime.UtcNow;
            return [.. _cacheFichiersJeuFlycast];
        }
    }

    private static int CalculerScoreProximiteTitreSouple(
        string titreReferenceNormalise,
        string titreCandidat
    )
    {
        string candidatNormalise = NormaliserTitreComparaisonSouple(titreCandidat);

        if (
            string.IsNullOrWhiteSpace(titreReferenceNormalise)
            || string.IsNullOrWhiteSpace(candidatNormalise)
        )
        {
            return 0;
        }

        HashSet<string> jetonsReference =
        [
            .. titreReferenceNormalise
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(jeton => jeton.Length >= 3),
        ];
        HashSet<string> jetonsCandidats =
        [
            .. candidatNormalise
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(jeton => jeton.Length >= 3),
        ];

        if (jetonsReference.Count == 0 || jetonsCandidats.Count == 0)
        {
            return 0;
        }

        int communs = jetonsReference.Intersect(jetonsCandidats, StringComparer.Ordinal).Count();

        if (communs <= 0)
        {
            return 0;
        }

        if (
            candidatNormalise.Contains(titreReferenceNormalise, StringComparison.Ordinal)
            || titreReferenceNormalise.Contains(candidatNormalise, StringComparison.Ordinal)
        )
        {
            communs += 3;
        }

        return communs;
    }

    private static string ExtraireTitreRALibretroDepuisFenetre(string titreFenetre)
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        if (
            titre.StartsWith("RALibretro", StringComparison.OrdinalIgnoreCase)
            || titre.StartsWith("RALibRetro", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Les fenetres RALibretro exposent surtout version/core/system/profil.
            // Le nom du jeu fiable vient du RACache ou du JSON recent.
            return string.Empty;
        }

        return NettoyerNomFichierJeu(titre);
    }

    private static bool EstDialogueDolphin(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = titre.Trim().ToLowerInvariant();

        return titreNormalise
            is "selectionner un dossier"
                or "select a folder"
                or "select a directory"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    private static bool EstDialoguePCSX2(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = NormaliserTexteComparaison(titre);

        if (titreNormalise.StartsWith("pcsx2 v", StringComparison.Ordinal))
        {
            return true;
        }

        if (titreNormalise.StartsWith("pcsx2 update installer", StringComparison.Ordinal))
        {
            return true;
        }

        return titreNormalise
            is "mise a jour automatique"
                or "lancer un disque"
                or "telecharger des jaquettes"
                or "parametres pcsx2"
                or "scanner les sous-dossiers ?"
                or "confirmer l'extinction"
                or "attention : memory card occupee"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    private static string ExtraireTitreProject64DepuisFenetre(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string[] morceaux = titreFenetre.Split(
            " - ",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

        if (
            morceaux.Length >= 3
            && morceaux[0].Contains("Project64", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (EstBlocVersionProject64(morceaux[1]))
            {
                if (morceaux.Length == 3)
                {
                    // Format "LunaProject64 / RAP64 - 3.6 - Profil" : aucun jeu exploitable n'est encore visible.
                    return string.Empty;
                }

                string candidat = morceaux.Length >= 4 ? morceaux[^2] : morceaux[^1];
                return NettoyerTitreJeu(
                    candidat,
                    ["Project64", "LunaProject64", "RAP64", "RAProject64"]
                );
            }

            return NettoyerTitreJeu(
                morceaux[^1],
                ["Project64", "LunaProject64", "RAP64", "RAProject64"]
            );
        }

        return ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "Project64",
            "LunaProject64",
            "RAP64",
            "RAProject64"
        );
    }

    private static bool EstBlocVersionProject64(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        return BlocVersionProject64Regex().IsMatch(valeur.Trim());
    }

    private static string ExtraireTitreDuckStationDepuisAutomatisation(Process processus)
    {
        try
        {
            processus.Refresh();

            if (processus.MainWindowHandle == IntPtr.Zero)
            {
                return string.Empty;
            }

            AutomationElement fenetre = AutomationElement.FromHandle(processus.MainWindowHandle);
            AutomationElementCollection elements = fenetre.FindAll(
                TreeScope.Descendants,
                Condition.TrueCondition
            );

            string meilleurNom = string.Empty;
            double meilleurScore = 0;

            foreach (AutomationElement element in elements.Cast<AutomationElement>())
            {
                string nom = element.Current.Name?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(nom))
                {
                    continue;
                }

                double score = EvaluerNomAutomatisationDuckStation(nom);

                if (score > meilleurScore)
                {
                    meilleurScore = score;
                    meilleurNom = nom;
                }
            }

            return meilleurScore >= 1.2 ? meilleurNom : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static double EvaluerNomAutomatisationDuckStation(string nom)
    {
        string valeur = nom.Trim();

        if (string.IsNullOrWhiteSpace(valeur))
        {
            return 0;
        }

        string valeurMinuscule = valeur.ToLowerInvariant();

        string[] termesExclus =
        [
            "duckstation",
            "file",
            "emulation",
            "system",
            "settings",
            "tools",
            "view",
            "help",
            "debug",
            "fullscreen",
            "pause",
            "resume",
            "controller",
            "memory card",
            "game list",
            "toolbar",
            "status bar",
            "qt",
        ];

        if (termesExclus.Any(terme => valeurMinuscule.Contains(terme, StringComparison.Ordinal)))
        {
            return 0;
        }

        if (ValeurNumeriqueSeuleRegex().IsMatch(valeurMinuscule))
        {
            return 0;
        }

        double score = 0;

        if (valeur.Length >= 4)
        {
            score += 0.4;
        }

        if (valeur.Contains(' '))
        {
            score += 0.4;
        }

        if (ContientDeuxLettresRegex().IsMatch(valeur))
        {
            score += 0.4;
        }

        if (valeur.Length >= 8)
        {
            score += 0.2;
        }

        return score;
    }

    private void JournaliserSiChangement(EtatSondeLocaleEmulateur etat)
    {
        if (string.Equals(_derniereSignatureJournalisee, etat.Signature, StringComparison.Ordinal))
        {
            return;
        }

        _derniereSignatureJournalisee = etat.Signature;
        Journaliser(etat);
    }

    private static void Journaliser(EtatSondeLocaleEmulateur etat)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalSondeLocale,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] detecte={etat.EmulateurDetecte};emulateur={NettoyerPourJournal(etat.NomEmulateur)};processus={NettoyerPourJournal(etat.NomProcessus)};cheminExecutable={NettoyerPourJournal(etat.CheminExecutable)};titreFenetre={NettoyerPourJournal(etat.TitreFenetre)};titreJeu={NettoyerPourJournal(etat.TitreJeuProbable)};gameId={etat.IdentifiantJeuProbable.ToString(CultureInfo.InvariantCulture)};diagnostic={NettoyerPourJournal(etat.InformationsDiagnostic)};signature={NettoyerPourJournal(etat.Signature)}{Environment.NewLine}"
            )
        );
    }

    private static string NettoyerPourJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static IReadOnlyList<string> LireTitresFenetresVisibles(Process processus)
    {
        List<string> titres = [];

        try
        {
            EnumWindows(
                (handle, _) =>
                {
                    if (!IsWindowVisible(handle))
                    {
                        return true;
                    }

                    if (GetWindowThreadProcessId(handle, out uint identifiantProcessusFenetre) == 0)
                    {
                        return true;
                    }

                    if (identifiantProcessusFenetre != unchecked((uint)processus.Id))
                    {
                        return true;
                    }

                    int longueur = GetWindowTextLength(handle);

                    if (longueur <= 0)
                    {
                        return true;
                    }

                    StringBuilder constructeur = new(longueur + 1);
                    _ = GetWindowText(handle, constructeur, constructeur.Capacity);
                    string titre = constructeur.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(titre))
                    {
                        titres.Add(titre);
                    }

                    return true;
                },
                IntPtr.Zero
            );
        }
        catch
        {
            return [];
        }

        return [.. titres.Distinct(StringComparer.Ordinal)];
    }

    private static string ChoisirTitreFenetre(
        DefinitionEmulateurLocal definition,
        Process processus,
        IReadOnlyList<string> titres
    )
    {
        string titrePrincipal = processus.MainWindowTitle?.Trim() ?? string.Empty;

        if (PeutRetenirTitreFenetrePourEmulateur(definition, processus, titrePrincipal))
        {
            return titrePrincipal;
        }

        if (titres.Count > 0)
        {
            return titres
                    .Where(titre =>
                        PeutRetenirTitreFenetrePourEmulateur(definition, processus, titre)
                    )
                    .OrderByDescending(titre =>
                        CalculerPrioriteTitreFenetre(definition, processus, titre, titrePrincipal)
                    )
                    .ThenByDescending(titre => titre.Length)
                    .ThenByDescending(titre => titre.Contains(" - ", StringComparison.Ordinal))
                    .FirstOrDefault()
                ?? string.Empty;
        }

        processus.Refresh();
        titrePrincipal = processus.MainWindowTitle?.Trim() ?? string.Empty;
        return PeutRetenirTitreFenetrePourEmulateur(definition, processus, titrePrincipal)
            ? titrePrincipal
            : string.Empty;
    }

    private static bool PeutRetenirTitreFenetrePourEmulateur(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titreFenetre
    )
    {
        string titreNettoye = titreFenetre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreNettoye))
        {
            return false;
        }

        if (TitreFenetreSembleParasite(titreNettoye))
        {
            return false;
        }

        if (TitreFenetreReferenceEmulateur(definition, titreNettoye))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(
            ExtraireTitreJeuPourDefinition(definition, processus, titreNettoye)
        );
    }

    private static int CalculerPrioriteTitreFenetre(
        DefinitionEmulateurLocal definition,
        Process processus,
        string titre,
        string titrePrincipal
    )
    {
        int score = 0;
        string titreNettoye = titre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(titreNettoye))
        {
            return int.MinValue;
        }

        string titreJeu = ExtraireTitreJeuPourDefinition(definition, processus, titreNettoye);

        if (!string.IsNullOrWhiteSpace(titreJeu))
        {
            score += 1000;
            score += Math.Min(titreJeu.Length, 120);
        }

        if (
            !string.IsNullOrWhiteSpace(titrePrincipal)
            && string.Equals(titreNettoye, titrePrincipal, StringComparison.Ordinal)
        )
        {
            score += 180;
        }

        if (TitreFenetreReferenceEmulateur(definition, titreNettoye))
        {
            score += 80;
        }

        if (TitreFenetreSembleParasite(titreNettoye))
        {
            score -= 220;
        }
        else
        {
            score += 25;
        }

        if (titreNettoye.Contains(" - ", StringComparison.Ordinal))
        {
            score += 10;
        }

        return score;
    }

    private static bool TitreFenetreReferenceEmulateur(
        DefinitionEmulateurLocal definition,
        string titreFenetre
    )
    {
        if (titreFenetre.Contains(definition.NomEmulateur, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return definition.NomsProcessus.Any(nom =>
            !string.IsNullOrWhiteSpace(nom)
            && titreFenetre.Contains(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool TitreFenetreSembleParasite(string titreFenetre)
    {
        string titreNormalise = NormaliserTexteComparaison(titreFenetre);

        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            return true;
        }

        string[] fragmentsParasites =
        [
            "explorateur de fichiers",
            "file explorer",
            "ouvrir",
            "open",
            "enregistrer",
            "save as",
            "proprietes",
            "properties",
            "parametres",
            "settings",
            "options",
            "configuration",
            "a propos",
            "about",
            "outils",
            "tools",
            "debug",
            "debugger",
            "console",
            "plugins",
            "plugin",
            "controleur",
            "controller",
            "input config",
            "input settings",
            "cheats",
            "retroachievements",
        ];

        return fragmentsParasites.Any(fragment =>
            titreNormalise.Contains(fragment, StringComparison.Ordinal)
        );
    }

    private static string NormaliserTexteComparaison(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        string normalise = valeur.Normalize(NormalizationForm.FormD);
        StringBuilder constructeur = new(normalise.Length);

        foreach (char caractere in normalise)
        {
            UnicodeCategory categorie = CharUnicodeInfo.GetUnicodeCategory(caractere);

            if (categorie != UnicodeCategory.NonSpacingMark)
            {
                constructeur.Append(caractere);
            }
        }

        return constructeur.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }

    private static string NormaliserTitreComparaisonSouple(string valeur)
    {
        string normalise = NormaliserTexteComparaison(valeur);

        if (string.IsNullOrWhiteSpace(normalise))
        {
            return string.Empty;
        }

        normalise = TexteEntreParenthesesRegex().Replace(normalise, " ");
        normalise = CaracteresNonAlphaNumeriquesRegex().Replace(normalise, " ");
        return EspacesMultiplesRegex().Replace(normalise, " ").Trim();
    }

    private static bool TitresSemblables(string titreA, string titreB)
    {
        string normaliseA = NormaliserTitreComparaisonSouple(titreA);
        string normaliseB = NormaliserTitreComparaisonSouple(titreB);

        if (string.IsNullOrWhiteSpace(normaliseA) || string.IsNullOrWhiteSpace(normaliseB))
        {
            return false;
        }

        return string.Equals(normaliseA, normaliseB, StringComparison.Ordinal)
            || normaliseA.Contains(normaliseB, StringComparison.Ordinal)
            || normaliseB.Contains(normaliseA, StringComparison.Ordinal);
    }

    private static string ConstruireDiagnosticSourceJeu(
        StrategieRenseignementJeuEmulateurLocal strategie,
        int identifiantJeu
    )
    {
        string source = strategie switch
        {
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => "project64_racache",
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => "ralibretro_racache",
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => "ranes_racache",
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => "ravba_racache",
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => "rasnes9x_racache",
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig => "flycast_config",
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => "retroarch_log",
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => "duckstation_log",
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => "pcsx2_log",
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => "ppsspp_log",
            _ => "inconnue",
        };

        return $"source={source};gameId={identifiantJeu.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string ConstruireDiagnosticDuckStation(
        Process processus,
        IReadOnlyList<string> titresFenetres
    )
    {
        try
        {
            List<string> morceaux = [];

            if (titresFenetres.Count > 0)
            {
                morceaux.Add($"titres=[{string.Join(" | ", titresFenetres)}]");
            }

            if (processus.MainWindowHandle != IntPtr.Zero)
            {
                AutomationElement fenetre = AutomationElement.FromHandle(
                    processus.MainWindowHandle
                );
                AutomationElementCollection elements = fenetre.FindAll(
                    TreeScope.Descendants,
                    Condition.TrueCondition
                );

                string[] noms =
                [
                    .. elements
                        .Cast<AutomationElement>()
                        .Select(element => element.Current.Name?.Trim() ?? string.Empty)
                        .Where(nom => !string.IsNullOrWhiteSpace(nom))
                        .Distinct(StringComparer.Ordinal)
                        .Take(12),
                ];

                if (noms.Length > 0)
                {
                    morceaux.Add($"ui=[{string.Join(" | ", noms)}]");
                }
            }

            string ligneCommande = LireLigneCommandeProcessus(processus);

            if (!string.IsNullOrWhiteSpace(ligneCommande))
            {
                string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

                morceaux.Add(
                    string.IsNullOrWhiteSpace(cheminJeu)
                        ? $"cmd=[{ligneCommande}]"
                        : $"cmdJeu=[{cheminJeu}]"
                );
            }

            string titreMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

            if (!string.IsNullOrWhiteSpace(titreMemcard))
            {
                morceaux.Add($"memcardJeu=[{titreMemcard}]");
            }

            string details = string.Join("; ", morceaux);

            return string.IsNullOrWhiteSpace(details)
                ? "source=duckstation_fallback"
                : $"source=duckstation_fallback; {details}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string LireLigneCommandeProcessus(Process processus)
    {
        try
        {
            int statut = NtQueryInformationProcess(
                processus.Handle,
                ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out int tailleRetour
            );

            if (tailleRetour <= 0 && statut != unchecked((int)0xC0000004))
            {
                return string.Empty;
            }

            IntPtr buffer = Marshal.AllocHGlobal(tailleRetour);

            try
            {
                statut = NtQueryInformationProcess(
                    processus.Handle,
                    ProcessCommandLineInformation,
                    buffer,
                    tailleRetour,
                    out tailleRetour
                );

                if (statut < 0)
                {
                    return string.Empty;
                }

                UnicodeString commande = Marshal.PtrToStructure<UnicodeString>(buffer);

                if (commande.Length <= 0 || commande.Buffer == IntPtr.Zero)
                {
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(commande.Buffer, commande.Length / 2)?.Trim()
                    ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtraireCheminJeuDepuisLigneCommande(string ligneCommande)
    {
        if (string.IsNullOrWhiteSpace(ligneCommande))
        {
            return string.Empty;
        }

        MatchCollection correspondances = JetonsLigneCommandeRegex().Matches(ligneCommande);

        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".cdi",
            ".gdi",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".zip",
            ".exe",
            ".nes",
            ".fds",
            ".unf",
            ".unif",
            ".smc",
            ".sfc",
            ".fig",
            ".gb",
            ".gbc",
            ".gba",
            ".nds",
            ".n64",
            ".z64",
            ".v64",
            ".gen",
            ".md",
            ".smd",
            ".sms",
            ".gg",
            ".sg",
            ".32x",
            ".a26",
            ".lnx",
            ".pce",
            ".sgx",
            ".ws",
            ".wsc",
            ".col",
            ".int",
            ".rom",
            ".prg",
            ".d64",
            ".g64",
            ".crt",
            ".tap",
        ];

        foreach (Match correspondance in correspondances.Cast<Match>().Reverse())
        {
            string valeur = correspondance.Groups[1].Success
                ? correspondance.Groups[1].Value
                : correspondance.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(valeur))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(valeur) || valeur.StartsWith('-'))
            {
                continue;
            }

            string extension = Path.GetExtension(valeur);

            if (
                !string.IsNullOrWhiteSpace(extension)
                && extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase)
            )
            {
                return valeur.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormaliserCheminJeuProbable(string cheminJeu)
    {
        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return string.Empty;
        }

        try
        {
            string cheminNormalise = Path.GetFullPath(cheminJeu.Trim());
            return File.Exists(cheminNormalise) ? cheminNormalise : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuFlycastDepuisLog(
        Process processus,
        string titreJeuFenetre
    )
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceCharge = FlycastGameLoadedRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    string titreJeu = correspondanceCharge.Groups[2].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuCharge, titreJeu);
                }

                Match correspondanceId = JournalGameIdRegex().Match(ligne);

                if (
                    correspondanceId.Success
                    && int.TryParse(
                        correspondanceId.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    return ConstruireRenseignementJeuFlycastDepuisCommande(
                        processus,
                        titreJeuFenetre,
                        identifiantJeu
                    );
                }
            }
        }
        catch
        {
            // Le journal Flycast reste une aide locale facultative.
        }

        return ConstruireRenseignementJeuFlycastDepuisCommande(processus, titreJeuFenetre);
    }

    private static RenseignementJeuRA? LireRenseignementJeuFlycastDepuisSourcesRejouer()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            int identifiantJeu = 0;
            string titreJeu = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                if (string.IsNullOrWhiteSpace(titreJeu))
                {
                    Match correspondanceCharge = FlycastGameLoadedRegex().Match(ligne);

                    if (correspondanceCharge.Success)
                    {
                        if (
                            int.TryParse(
                                correspondanceCharge.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantCharge
                            )
                        )
                        {
                            identifiantJeu = identifiantCharge;
                        }

                        titreJeu = correspondanceCharge.Groups[2].Value.Trim();
                    }
                }

                if (identifiantJeu <= 0)
                {
                    Match correspondanceId = JournalGameIdRegex().Match(ligne);

                    if (
                        correspondanceId.Success
                        && int.TryParse(
                            correspondanceId.Groups[1].Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int identifiantTrouve
                        )
                    )
                    {
                        identifiantJeu = identifiantTrouve;
                    }
                }

                if (identifiantJeu > 0 && !string.IsNullOrWhiteSpace(titreJeu))
                {
                    return new RenseignementJeuRA(identifiantJeu, titreJeu);
                }
            }

            return identifiantJeu > 0 || !string.IsNullOrWhiteSpace(titreJeu)
                ? new RenseignementJeuRA(identifiantJeu, titreJeu)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string LireCheminJeuRetroArchDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverDernierCheminJournalRetroArch();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceContenu = RetroArchContenuChargeRegex().Match(ligne);

                if (!correspondanceContenu.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceContenu.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le log local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static string LireCheminJeuFlycastDepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalFlycast();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceContenu = FlycastContenuChargeRegex().Match(ligne);

                if (!correspondanceContenu.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceContenu.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le log local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static RenseignementJeuRA? ConstruireRenseignementJeuFlycastDepuisCommande(
        Process processus,
        string titreJeuFenetre,
        int identifiantJeu = 0
    )
    {
        try
        {
            string ligneCommande = LireLigneCommandeProcessus(processus);
            string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

            string titreDepuisCommande = string.IsNullOrWhiteSpace(cheminJeu)
                ? string.Empty
                : NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            string titreRetenu = !string.IsNullOrWhiteSpace(titreDepuisCommande)
                ? titreDepuisCommande
                : titreJeuFenetre;

            if (identifiantJeu <= 0 && string.IsNullOrWhiteSpace(titreRetenu))
            {
                return null;
            }

            return new RenseignementJeuRA(identifiantJeu, titreRetenu);
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverDernierCheminJournalRetroArch();

            if (string.IsNullOrWhiteSpace(cheminJournal))
            {
                JournaliserEvenement("retroarch_log_absent", "chemin=");
                return null;
            }

            if (
                DateTime.UtcNow - File.GetLastWriteTimeUtc(cheminJournal)
                > TimeSpan.FromMinutes(15)
            )
            {
                JournaliserEvenement("retroarch_log_trop_ancien", $"chemin={cheminJournal}");
                return LireRenseignementJeuRetroArchDepuisCache();
            }

            int? identifiantJeuSessionObserve = null;
            string titreJeuObserve = string.Empty;

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifie = RetroArchGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifie.Success
                    && int.TryParse(
                        correspondanceIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                )
                {
                    string titreJeu = correspondanceIdentifie.Groups[2].Value.Trim();
                    JournaliserEvenement(
                        "retroarch_log_identified",
                        $"chemin={cheminJournal};gameId={identifiantJeuIdentifie.ToString(CultureInfo.InvariantCulture)};titre={titreJeu}"
                    );
                    return MemoriserRenseignementRetroArch(
                        new RenseignementJeuRA(identifiantJeuIdentifie, titreJeu)
                    );
                }

                Match correspondanceCharge = RetroArchGameChargeRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                )
                {
                    identifiantJeuSessionObserve ??= identifiantJeuCharge;
                }

                Match correspondanceSession = RetroArchSessionJeuRegex().Match(ligne);

                if (
                    correspondanceSession.Success
                    && int.TryParse(
                        correspondanceSession.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuSession
                    )
                )
                {
                    identifiantJeuSessionObserve ??= identifiantJeuSession;
                }

                if (string.IsNullOrWhiteSpace(titreJeuObserve))
                {
                    Match correspondanceContenu = RetroArchContenuChargeRegex().Match(ligne);

                    if (correspondanceContenu.Success)
                    {
                        string cheminJeu = correspondanceContenu.Groups[1].Value.Trim();

                        if (!string.IsNullOrWhiteSpace(cheminJeu))
                        {
                            titreJeuObserve = NettoyerNomFichierJeu(
                                Path.GetFileNameWithoutExtension(cheminJeu)
                            );
                        }
                    }
                }
            }

            if (identifiantJeuSessionObserve.HasValue)
            {
                JournaliserEvenement(
                    "retroarch_log_fallback",
                    $"chemin={cheminJournal};gameId={identifiantJeuSessionObserve.Value.ToString(CultureInfo.InvariantCulture)};titre={titreJeuObserve}"
                );
                return MemoriserRenseignementRetroArch(
                    new RenseignementJeuRA(identifiantJeuSessionObserve.Value, titreJeuObserve)
                );
            }

            JournaliserEvenement("retroarch_log_aucune_correspondance", $"chemin={cheminJournal}");
        }
        catch
        {
            // Le log RetroArch reste une aide locale facultative.
        }

        return LireRenseignementJeuRetroArchDepuisCache();
    }

    private static RenseignementJeuRA? LireRenseignementJeuDuckStationDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverCheminJournalDuckStation();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceChargement = DuckStationGameLoadedRegex().Match(ligne);

                if (
                    correspondanceChargement.Success
                    && int.TryParse(
                        correspondanceChargement.Groups[2].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    string titreJeu = correspondanceChargement.Groups[1].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuCharge, titreJeu);
                }

                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    return new RenseignementJeuRA(identifiantJeu, string.Empty);
                }
            }
        }
        catch
        {
            // Le log DuckStation reste une aide locale facultative.
        }

        return null;
    }

    private static RenseignementJeuRA? LireRenseignementJeuPCSX2DepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPCSX2();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifie = PCSX2GameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifie.Success
                    && int.TryParse(
                        correspondanceIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                    && identifiantJeuIdentifie > 0
                )
                {
                    string titreJeu = correspondanceIdentifie.Groups[2].Value.Trim();
                    return new RenseignementJeuRA(identifiantJeuIdentifie, titreJeu);
                }

                Match correspondanceCharge = PCSX2GameChargeRegex().Match(ligne);

                if (
                    correspondanceCharge.Success
                    && int.TryParse(
                        correspondanceCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    return new RenseignementJeuRA(identifiantJeuCharge, string.Empty);
                }
            }
        }
        catch
        {
            // Le log PCSX2 reste une aide locale facultative.
        }

        return null;
    }

    private static RenseignementJeuRA? LireRenseignementJeuPPSSPPDepuisLog(string titreJeuAttendu)
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPPSSPP();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return null;
            }

            bool exigerCorrespondanceTitre = !string.IsNullOrWhiteSpace(titreJeuAttendu);
            List<string> lignes =
            [
                .. ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(cheminJournal),
            ];

            if (exigerCorrespondanceTitre)
            {
                for (int index = lignes.Count - 1; index >= 0; index--)
                {
                    Match correspondanceDemarrage = PPSSPPJeuDemarreRegex().Match(lignes[index]);

                    if (!correspondanceDemarrage.Success)
                    {
                        continue;
                    }

                    string titreDemarre = ExtraireTitrePPSSPPDepuisCheminJeu(
                        correspondanceDemarrage.Groups[1].Value
                    );

                    if (!TitresSemblables(titreJeuAttendu, titreDemarre))
                    {
                        continue;
                    }

                    for (
                        int indexRecherche = index;
                        indexRecherche < lignes.Count && indexRecherche <= index + 40;
                        indexRecherche++
                    )
                    {
                        string ligneRecherche = lignes[indexRecherche];
                        Match correspondanceJeuIdentifie = PPSSPPGameIdentifieRegex()
                            .Match(ligneRecherche);

                        if (
                            correspondanceJeuIdentifie.Success
                            && int.TryParse(
                                correspondanceJeuIdentifie.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantJeuIdentifie
                            )
                            && identifiantJeuIdentifie > 0
                        )
                        {
                            return MemoriserRenseignementPPSSPP(
                                new RenseignementJeuRA(
                                    identifiantJeuIdentifie,
                                    correspondanceJeuIdentifie.Groups[2].Value.Trim()
                                )
                            );
                        }

                        Match correspondanceJeuCharge = PPSSPPGameChargeRegex()
                            .Match(ligneRecherche);

                        if (
                            correspondanceJeuCharge.Success
                            && int.TryParse(
                                correspondanceJeuCharge.Groups[1].Value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int identifiantJeuCharge
                            )
                            && identifiantJeuCharge > 0
                        )
                        {
                            return MemoriserRenseignementPPSSPP(
                                new RenseignementJeuRA(identifiantJeuCharge, titreJeuAttendu)
                            );
                        }
                    }
                }
            }

            RenseignementJeuRA? renseignementSecours = null;

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondanceJeuIdentifie = PPSSPPGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceJeuIdentifie.Success
                    && int.TryParse(
                        correspondanceJeuIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                    && identifiantJeuIdentifie > 0
                )
                {
                    RenseignementJeuRA renseignementIdentifie = new(
                        identifiantJeuIdentifie,
                        correspondanceJeuIdentifie.Groups[2].Value.Trim()
                    );

                    if (
                        !exigerCorrespondanceTitre
                        || TitresSemblables(titreJeuAttendu, renseignementIdentifie.TitreJeu)
                    )
                    {
                        return MemoriserRenseignementPPSSPP(renseignementIdentifie);
                    }

                    continue;
                }

                Match correspondanceJeuCharge = PPSSPPGameChargeRegex().Match(ligne);

                if (
                    correspondanceJeuCharge.Success
                    && int.TryParse(
                        correspondanceJeuCharge.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCharge
                    )
                    && identifiantJeuCharge > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuCharge,
                        string.Empty
                    );
                    continue;
                }

                Match correspondanceLoadCallback = PPSSPPLoadCallbackRegex().Match(ligne);

                if (
                    correspondanceLoadCallback.Success
                    && int.TryParse(
                        correspondanceLoadCallback.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuCallback
                    )
                    && identifiantJeuCallback > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    string titreJeu = correspondanceLoadCallback.Groups[2].Value.Trim();

                    if (string.Equals(titreJeu, "(null)", StringComparison.OrdinalIgnoreCase))
                    {
                        titreJeu = string.Empty;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(
                        identifiantJeuCallback,
                        titreJeu
                    );
                    continue;
                }

                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                    && identifiantJeu > 0
                )
                {
                    if (exigerCorrespondanceTitre)
                    {
                        continue;
                    }

                    renseignementSecours ??= new RenseignementJeuRA(identifiantJeu, string.Empty);
                }
            }

            return renseignementSecours is null
                ? null
                : MemoriserRenseignementPPSSPP(renseignementSecours);
        }
        catch
        {
            // Le log PPSSPP reste une aide locale facultative.
        }

        return null;
    }

    private static string ExtraireTitrePPSSPPDepuisCheminJeu(string cheminJeu)
    {
        if (string.IsNullOrWhiteSpace(cheminJeu))
        {
            return string.Empty;
        }

        string titre = Path.GetFileNameWithoutExtension(cheminJeu).Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        titre = TexteEntreParenthesesRegex().Replace(titre, " ");
        titre = titre.Replace("\u00AE", string.Empty, StringComparison.Ordinal);
        return EspacesMultiplesRegex().Replace(titre, " ").Trim();
    }

    private static string LireCheminJeuPPSSPPDepuisLog()
    {
        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalPPSSPP();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return string.Empty;
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceDemarrage = PPSSPPJeuDemarreRegex().Match(ligne);

                if (!correspondanceDemarrage.Success)
                {
                    continue;
                }

                string cheminNormalise = NormaliserCheminJeuProbable(
                    correspondanceDemarrage.Groups[1].Value.Trim()
                );

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le log local reste un secours opportuniste.
        }

        return string.Empty;
    }

    private static RenseignementJeuRA? MemoriserRenseignementPPSSPP(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCachePPSSPP)
        {
            _dernierRenseignementPPSSPP = renseignement;
            _dernierHorodatageRenseignementPPSSPPUtc = DateTime.UtcNow;
            return _dernierRenseignementPPSSPP;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuPPSSPPDepuisCache(string titreJeuAttendu)
    {
        lock (VerrouCachePPSSPP)
        {
            if (
                _dernierRenseignementPPSSPP is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementPPSSPPUtc
                    > TimeSpan.FromSeconds(10)
            )
            {
                return null;
            }

            if (
                string.IsNullOrWhiteSpace(titreJeuAttendu)
                || string.IsNullOrWhiteSpace(_dernierRenseignementPPSSPP.TitreJeu)
                || !TitresSemblables(titreJeuAttendu, _dernierRenseignementPPSSPP.TitreJeu)
            )
            {
                return null;
            }

            return _dernierRenseignementPPSSPP;
        }
    }

    private static RenseignementJeuRA? MemoriserRenseignementRetroArch(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRetroArch)
        {
            _dernierRenseignementRetroArch = renseignement;
            _dernierHorodatageRenseignementRetroArchUtc = DateTime.UtcNow;
            return _dernierRenseignementRetroArch;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisCache()
    {
        lock (VerrouCacheRetroArch)
        {
            if (
                _dernierRenseignementRetroArch is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRetroArchUtc
                    > TimeSpan.FromSeconds(10)
            )
            {
                return null;
            }

            return _dernierRenseignementRetroArch;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisRACache(
        string nomEmulateur
    )
    {
        try
        {
            string repertoireRACache =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheProject64(nomEmulateur);

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return null;
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");
            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        return LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);
                    }
                }
            }

            return LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRANesDepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRANes()
        );
    }

    private static RenseignementJeuRA? LireRenseignementJeuRAVBADepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRAVBA()
        );
    }

    private static RenseignementJeuRA? LireRenseignementJeuRASnes9xDepuisRACache()
    {
        return LireRenseignementJeuDepuisRACache(
            ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRASnes9x()
        );
    }

    private static RenseignementJeuRA? LireRenseignementJeuDepuisRACache(string repertoireRACache)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return null;
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");

            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        return LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);
                    }
                }
            }

            return LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisRACache()
    {
        try
        {
            string repertoireRACache =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRALibretro();
            RenseignementJeuRA? renseignementConfiguration =
                LireRenseignementJeuRALibretroDepuisConfiguration();

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
            }

            // Pour RALibretro, le fichier Data/<GameId>.json le plus recent est le signal
            // le plus fiable du jeu courant pendant les transitions de session.
            RenseignementJeuRA? dernierRenseignement =
                LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);

            if (dernierRenseignement is not null)
            {
                if (
                    string.IsNullOrWhiteSpace(dernierRenseignement.TitreJeu)
                    && renseignementConfiguration is not null
                    && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                )
                {
                    dernierRenseignement = dernierRenseignement with
                    {
                        TitreJeu = renseignementConfiguration.TitreJeu,
                    };
                }

                return MemoriserRenseignementRALibretro(dernierRenseignement);
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");

            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        RenseignementJeuRA renseignement =
                            LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);

                        if (
                            renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                            && !string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && !TitresSemblables(
                                renseignementConfiguration.TitreJeu,
                                renseignement.TitreJeu
                            )
                        )
                        {
                            return renseignementConfiguration;
                        }

                        if (
                            string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                        )
                        {
                            renseignement = renseignement with
                            {
                                TitreJeu = renseignementConfiguration.TitreJeu,
                            };
                        }

                        return MemoriserRenseignementRALibretro(renseignement);
                    }
                }
            }

            return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
        }
        catch
        {
            return LireRenseignementJeuRALibretroDepuisCache();
        }
    }

    private static RenseignementJeuRA? MemoriserRenseignementRALibretro(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRALibretro)
        {
            _dernierRenseignementRALibretro = renseignement;
            _dernierHorodatageRenseignementRALibretroUtc = DateTime.UtcNow;
            return _dernierRenseignementRALibretro;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisCache()
    {
        lock (VerrouCacheRALibretro)
        {
            if (
                _dernierRenseignementRALibretro is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRALibretroUtc
                    > TimeSpan.FromSeconds(5)
            )
            {
                return null;
            }

            return _dernierRenseignementRALibretro;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisConfiguration()
    {
        string cheminConfiguration =
            ServiceSourcesLocalesEmulateurs.TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                !document.RootElement.TryGetProperty("recent", out JsonElement recent)
                || recent.ValueKind != JsonValueKind.Array
                || recent.GetArrayLength() == 0
            )
            {
                return null;
            }

            JsonElement premierRecent = recent[0];

            if (
                !premierRecent.TryGetProperty("path", out JsonElement path)
                || path.ValueKind != JsonValueKind.String
            )
            {
                return null;
            }

            string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cheminJeu))
            {
                return null;
            }

            string titreJeu = NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            return string.IsNullOrWhiteSpace(titreJeu) ? null : new RenseignementJeuRA(0, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueDepuisSourceLocale(
        string nomEmulateur,
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        if (identifiantJeu <= 0)
        {
            return null;
        }

        try
        {
            string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
                nomEmulateur
            );

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            RenseignementSuccesRA? renseignement = LireDernierSuccesDepuisJournalRA(cheminJournal);

            if (renseignement is null)
            {
                return null;
            }

            GameAchievementV2? succes = succesConnus.FirstOrDefault(item =>
                item.Id == renseignement.IdentifiantSucces
            );

            return new SuccesDebloqueDetecte
            {
                IdentifiantJeu = identifiantJeu,
                TitreJeu = titreJeu?.Trim() ?? string.Empty,
                IdentifiantSucces = renseignement.IdentifiantSucces,
                TitreSucces = succes?.Title?.Trim() ?? renseignement.TitreSucces,
                Points = succes?.Points ?? 0,
                Hardcore = true,
                DateObtention = DateTimeOffset.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                ),
            };
        }
        catch
        {
            return null;
        }
    }

    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueRALibretro(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        return LireDernierSuccesDebloqueDepuisSourceLocale(
            "RALibretro",
            identifiantJeu,
            titreJeu,
            succesConnus
        );
    }

    private static int LireDernierIdentifiantJeuDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                cheminJournal
            );

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                )
                {
                    return identifiantJeu;
                }
            }
        }
        catch
        {
            // Le journal RA reste une aide locale facultative.
        }

        return 0;
    }

    private static RenseignementSuccesRA? LireDernierSuccesDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(
                cheminJournal
            );

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondanceAttribue = JournalSuccesAttribueRegex().Match(ligne);

                if (
                    correspondanceAttribue.Success
                    && int.TryParse(
                        correspondanceAttribue.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribue
                    )
                )
                {
                    return new RenseignementSuccesRA(identifiantSuccesAttribue, string.Empty);
                }

                Match correspondanceAttribution = JournalSuccesAttributionRegex().Match(ligne);

                if (
                    correspondanceAttribution.Success
                    && int.TryParse(
                        correspondanceAttribution.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribution
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesAttribution,
                        correspondanceAttribution.Groups[2].Value.Trim()
                    );
                }

                Match correspondanceDeblocageDuckStation = JournalSuccesDebloqueDuckStationRegex()
                    .Match(ligne);

                if (
                    correspondanceDeblocageDuckStation.Success
                    && int.TryParse(
                        correspondanceDeblocageDuckStation.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesDebloqueDuckStation
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesDebloqueDuckStation,
                        correspondanceDeblocageDuckStation.Groups[2].Value.Trim()
                    );
                }

                Match correspondanceDeblocageFlycast = JournalSuccesDebloqueFlycastRegex()
                    .Match(ligne);

                if (
                    correspondanceDeblocageFlycast.Success
                    && int.TryParse(
                        correspondanceDeblocageFlycast.Groups[2].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesDebloqueFlycast
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesDebloqueFlycast,
                        correspondanceDeblocageFlycast.Groups[1].Value.Trim()
                    );
                }
            }
        }
        catch
        {
            // Le journal RA reste une aide locale facultative.
        }

        return null;
    }

    private static RenseignementJeuRA? LireDernierRenseignementJeuProject64DepuisData(
        string repertoireRACache
    )
    {
        try
        {
            string repertoireData = Path.Combine(repertoireRACache, "Data");

            if (!Directory.Exists(repertoireData))
            {
                return null;
            }

            FileInfo? fichierJeuRecent = new DirectoryInfo(repertoireData)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .Where(fichier => FichierDonneesJeuRegex().IsMatch(fichier.Name))
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (fichierJeuRecent is null)
            {
                return null;
            }

            if (DateTime.UtcNow - fichierJeuRecent.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return null;
            }

            string nomBase = Path.GetFileNameWithoutExtension(fichierJeuRecent.Name);

            if (
                !int.TryParse(
                    nomBase,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int identifiantJeu
                )
            )
            {
                return null;
            }

            return LireRenseignementJeuProject64DepuisFichierData(
                repertoireRACache,
                identifiantJeu
            );
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisFichierData(
        string repertoireRACache,
        int identifiantJeu
    )
    {
        try
        {
            string cheminDonneesJeu = Path.Combine(
                repertoireRACache,
                "Data",
                $"{identifiantJeu}.json"
            );

            if (!File.Exists(cheminDonneesJeu))
            {
                return null;
            }

            string contenu = string.Join(
                Environment.NewLine,
                ServiceSourcesLocalesEmulateurs.LireToutesLesLignesAvecPartage(cheminDonneesJeu)
            );

            if (string.IsNullOrWhiteSpace(contenu))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(contenu);
            string titreJeu = string.Empty;

            if (
                document.RootElement.TryGetProperty("Title", out JsonElement titre)
                && titre.ValueKind == JsonValueKind.String
            )
            {
                titreJeu = titre.GetString()?.Trim() ?? string.Empty;
            }

            return new RenseignementJeuRA(identifiantJeu, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    private static string NettoyerNomFichierJeu(string nomFichier)
    {
        if (string.IsNullOrWhiteSpace(nomFichier))
        {
            return string.Empty;
        }

        string resultat = nomFichier.Trim();
        resultat = resultat.Replace('_', ' ');
        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    private static string ExtraireTitreDuckStationDepuisMemcardRecente()
    {
        try
        {
            string repertoireDuckStation =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireDuckStation();

            if (string.IsNullOrWhiteSpace(repertoireDuckStation))
            {
                return string.Empty;
            }

            string repertoireMemcards = Path.Combine(repertoireDuckStation, "memcards");

            if (!Directory.Exists(repertoireMemcards))
            {
                return string.Empty;
            }

            FileInfo? memcardRecente = new DirectoryInfo(repertoireMemcards)
                .EnumerateFiles("*.mcd", SearchOption.TopDirectoryOnly)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (memcardRecente is null)
            {
                return string.Empty;
            }

            if (DateTime.UtcNow - memcardRecente.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return string.Empty;
            }

            string nomBase = Path.GetFileNameWithoutExtension(memcardRecente.Name);
            nomBase = SuffixeSlotCarteMemoireRegex().Replace(nomBase, string.Empty);

            if (SerialJeuPlayStationRegex().IsMatch(nomBase))
            {
                string cheminJeu = ResoudreCheminJeuDuckStationDepuisSerial(
                    repertoireDuckStation,
                    nomBase
                );

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }

            return NettoyerNomFichierJeu(nomBase);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TrouverCheminJeuDuckStationDepuisMemcardRecente()
    {
        try
        {
            string repertoireDuckStation =
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireDuckStation();

            if (string.IsNullOrWhiteSpace(repertoireDuckStation))
            {
                return string.Empty;
            }

            string repertoireMemcards = Path.Combine(repertoireDuckStation, "memcards");

            if (!Directory.Exists(repertoireMemcards))
            {
                return string.Empty;
            }

            FileInfo? memcardRecente = new DirectoryInfo(repertoireMemcards)
                .EnumerateFiles("*.mcd", SearchOption.TopDirectoryOnly)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (memcardRecente is null)
            {
                return string.Empty;
            }

            if (DateTime.UtcNow - memcardRecente.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return string.Empty;
            }

            string nomBase = Path.GetFileNameWithoutExtension(memcardRecente.Name);
            nomBase = SuffixeSlotCarteMemoireRegex().Replace(nomBase, string.Empty);

            if (!SerialJeuPlayStationRegex().IsMatch(nomBase))
            {
                return string.Empty;
            }

            return NormaliserCheminJeuProbable(
                ResoudreCheminJeuDuckStationDepuisSerial(repertoireDuckStation, nomBase)
            );
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string LireCheminJeuDuckStationDepuisLog()
    {
        try
        {
            string cheminJournal =
                ServiceSourcesLocalesEmulateurs.TrouverCheminJournalDuckStation();

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return TrouverCheminJeuDuckStationDepuisMemcardRecente();
            }

            FileInfo fichierJournal = new(cheminJournal);

            if (
                fichierJournal.Length <= 0
                || DateTime.UtcNow - fichierJournal.LastWriteTimeUtc > TimeSpan.FromMinutes(15)
            )
            {
                return TrouverCheminJeuDuckStationDepuisMemcardRecente();
            }

            foreach (
                string ligne in ServiceSourcesLocalesEmulateurs
                    .LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceBootPath = DuckStationBootPathRegex().Match(ligne);

                if (!correspondanceBootPath.Success)
                {
                    continue;
                }

                string cheminJeu = correspondanceBootPath.Groups[1].Value.Trim();
                string cheminNormalise = NormaliserCheminJeuProbable(cheminJeu);

                if (!string.IsNullOrWhiteSpace(cheminNormalise))
                {
                    return cheminNormalise;
                }
            }
        }
        catch
        {
            // Le log DuckStation reste une aide locale facultative.
        }

        return TrouverCheminJeuDuckStationDepuisMemcardRecente();
    }

    private static string ResoudreCheminJeuDuckStationDepuisSerial(
        string repertoireDuckStation,
        string serial
    )
    {
        try
        {
            string cheminGamelist = Path.Combine(repertoireDuckStation, "cache", "gamelist.cache");

            if (!File.Exists(cheminGamelist))
            {
                return string.Empty;
            }

            DateTime horodatage = File.GetLastWriteTimeUtc(cheminGamelist);
            Dictionary<string, string> cache;

            lock (VerrouCacheDuckStation)
            {
                if (
                    !string.Equals(
                        _dernierRepertoireDuckStation,
                        repertoireDuckStation,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || _dernierHorodatageCacheGamelistUtc != horodatage
                    || _cacheSerialVersCheminDuckStation.Count == 0
                )
                {
                    _cacheSerialVersCheminDuckStation = ConstruireCacheSerialDuckStation(
                        cheminGamelist
                    );
                    _dernierRepertoireDuckStation = repertoireDuckStation;
                    _dernierHorodatageCacheGamelistUtc = horodatage;
                }

                cache = _cacheSerialVersCheminDuckStation;
            }

            return cache.TryGetValue(serial, out string? cheminJeu) ? cheminJeu : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, string> ConstruireCacheSerialDuckStation(
        string cheminGamelist
    )
    {
        string[] chaines = ExtraireChainesLisiblesDepuisBinaire(cheminGamelist, 4);
        Dictionary<string, string> correspondances = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < chaines.Length - 1; i++)
        {
            string chaine = chaines[i].Trim();
            string suivante = chaines[i + 1].Trim();

            if (!EstCheminJeuDuckStation(chaine) || !EstSerialJeuPlayStation(suivante))
            {
                continue;
            }

            correspondances[suivante] = chaine;
        }

        return correspondances;
    }

    private static string[] ExtraireChainesLisiblesDepuisBinaire(
        string cheminFichier,
        int longueurMin
    )
    {
        byte[] bytes = File.ReadAllBytes(cheminFichier);
        List<string> chaines = [];
        StringBuilder constructeur = new();

        foreach (byte valeur in bytes)
        {
            if ((valeur >= 32 && valeur <= 126) || valeur == 9)
            {
                constructeur.Append((char)valeur);
            }
            else
            {
                if (constructeur.Length >= longueurMin)
                {
                    chaines.Add(constructeur.ToString());
                }

                constructeur.Clear();
            }
        }

        if (constructeur.Length >= longueurMin)
        {
            chaines.Add(constructeur.ToString());
        }

        return [.. chaines];
    }

    private static bool EstCheminJeuDuckStation(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        string extension = Path.GetExtension(valeur);
        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".exe",
        ];

        return extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool EstSerialJeuPlayStation(string valeur)
    {
        return !string.IsNullOrWhiteSpace(valeur) && SerialJeuPlayStationRegex().IsMatch(valeur);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspacesMultiplesRegex();

    [GeneratedRegex(@"^v?\d+(\.\d+)+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BlocVersionProject64Regex();

    [GeneratedRegex(@"^\d+(\.\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValeurNumeriqueSeuleRegex();

    [GeneratedRegex(@"[A-Za-z].*[A-Za-z]", RegexOptions.CultureInvariant)]
    private static partial Regex ContientDeuxLettresRegex();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex TexteEntreParenthesesRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex CaracteresNonAlphaNumeriquesRegex();

    [GeneratedRegex("\"([^\"]+)\"|([^\\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex JetonsLigneCommandeRegex();

    [GeneratedRegex(
        @"RA:\s*game\s+(\d+)\s+loaded:\s*(.+?),\s*achievements",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex FlycastGameLoadedRegex();

    [GeneratedRegex(
        @"RA:\s*cdreader_open_track\s+(.+?)\s+track\s+\d+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex FlycastContenuChargeRegex();

    [GeneratedRegex(
        @"(?:Identified game:\s*|Loading game\s+|Starting (?:new )?session for game\s+)(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalGameIdRegex();

    [GeneratedRegex(
        @"Awarding achievement\s+(\d+)\s*:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesAttributionRegex();

    [GeneratedRegex(
        @"Game loaded:\s*'(.+?)'\s*\(ID:\s*(\d+)\s*,",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex DuckStationGameLoadedRegex();

    [GeneratedRegex(@"Boot Path:\s*(.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DuckStationBootPathRegex();

    [GeneratedRegex(
        @"Achievements:\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PCSX2GameIdentifieRegex();

    [GeneratedRegex(
        @"Achievements:\s+Game\s+(\d+)\s+loaded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PCSX2GameChargeRegex();

    [GeneratedRegex(
        @"isoFile open ok:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PCSX2IsoOuverteRegex();

    [GeneratedRegex(
        @"Load callback:\s*(\d+)\s*\(([^)]*)\)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PPSSPPLoadCallbackRegex();

    [GeneratedRegex(
        @"RetroAchievements:\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PPSSPPGameIdentifieRegex();

    [GeneratedRegex(
        @"RetroAchievements:\s+Game\s+(\d+)\s+loaded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PPSSPPGameChargeRegex();

    [GeneratedRegex(
        @"Booted\s+(.+?)\.\.\.$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PPSSPPJeuDemarreRegex();

    [GeneratedRegex(
        @"Achievement\s+(\d+)\s+awarded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesAttribueRegex();

    [GeneratedRegex(
        @"Achievement\s+(\d+)\s+\((.+?)\)\s+for game\s+\d+\s+unlocked\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesDebloqueDuckStationRegex();

    [GeneratedRegex(
        @"Achievement\s+(.+?)\s+\((\d+)\)\s+for game\s+.+?\s+unlocked\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesDebloqueFlycastRegex();

    [GeneratedRegex(@"^\d+\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex FichierDonneesJeuRegex();

    [GeneratedRegex(
        @"(?:^|[_\-\s])(?:slot|card)\s*\d+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SuffixeSlotCarteMemoireRegex();

    [GeneratedRegex(
        @"^(?:S[CLN][A-Z]{2}|PBPX)[-_ ]?\d{3,5}(?:[-_ ]?\d{2,3})?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SerialJeuPlayStationRegex();

    [GeneratedRegex(
        @"^[A-Z]{4}\d{5}\s*:\s*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PrefixeSerialPpssppRegex();

    [GeneratedRegex(
        @"\s*\(([A-Z0-9]{4,8})\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SuffixeCodeJeuDolphinRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchGameIdentifieRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Starting session for game\s+(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchSessionJeuRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[RCHEEVOS\]\s+Game\s+(\d+)\s+loaded",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchGameChargeRegex();

    [GeneratedRegex(
        @"(?:\[[^\]]+\]\s+)*\[(?:Core|Content)\]\s+(?:Using content|Chargement du fichier de contenu):\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchContenuChargeRegex();

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength
    );

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UnicodeString
    {
        public readonly ushort Length;
        public readonly ushort MaximumLength;
        public readonly IntPtr Buffer;
    }
}
