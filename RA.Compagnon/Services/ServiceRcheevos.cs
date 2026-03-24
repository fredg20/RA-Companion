using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Orchestre les sources rcheevos et l'interrogation du bridge natif.
/// </summary>
public sealed class ServiceRcheevos
{
    // Le callback natif doit rester vivant tant que le bridge peut encore l'appeler.
    private static ServiceRcheevos? _instanceBridge;
    private static readonly DelegateLectureMemoire CallbackLectureMemoire = LireMemoireBridge;

    private readonly ServiceMemoireRetroArch _serviceMemoireRetroArch = new();
    private readonly ServiceRcheevosRalibretro _serviceRcheevosRalibretro = new();
    private ContexteJeuRcheevos? _jeuActif;
    private bool _bridgeIndisponible;
    private SourceMemoireRcheevos _sourceMemoireActive;
    private SourceMemoireRcheevos _sourceMemoirePont;
    private DiagnosticMemoireRetroArch? _diagnosticMemoireRetroArch;
    private string _nomEmulateurSource = string.Empty;
    private readonly Dictionary<int, string> _definitionsSucces = [];
    private readonly Dictionary<string, ValidationDefinitionRcheevos> _validationsDefinitions = [];
    private string _signatureDefinitionsSucces = string.Empty;

    public ServiceRcheevos()
    {
        _instanceBridge = this;
        ActualiserPontMemoire();
    }

    /// <summary>
    /// Indique si le bridge natif semble disponible pour le processus courant.
    /// </summary>
    public bool EstBridgeDisponible => !_bridgeIndisponible;

    /// <summary>
    /// Indique quelle source mémoire locale est actuellement préparée.
    /// </summary>
    public SourceMemoireRcheevos SourceMemoireActive => _sourceMemoireActive;

    /// <summary>
    /// Diagnostic le plus récent pour RetroArch, si disponible.
    /// </summary>
    public DiagnosticMemoireRetroArch? DiagnosticMemoireRetroArch => _diagnosticMemoireRetroArch;

    /// <summary>
    /// Mémorise le jeu actif pour les futures requêtes de progression mesurée.
    /// </summary>
    public void DefinirJeuActif(int identifiantJeu, int identifiantConsole, string titreJeu)
    {
        if (identifiantJeu <= 0)
        {
            _jeuActif = null;
            ReinitialiserDefinitionsSucces();
            return;
        }

        _jeuActif = new ContexteJeuRcheevos(identifiantJeu, identifiantConsole, titreJeu);
    }

    /// <summary>
    /// Enregistre les définitions MemAddr du jeu courant pour le bridge natif.
    /// </summary>
    public void DefinirDefinitionsSucces(
        int identifiantJeu,
        IEnumerable<SuccesJeuUtilisateurRetroAchievements> succes
    )
    {
        IReadOnlyDictionary<int, string> definitionsCacheesRalibretro =
            _serviceRcheevosRalibretro.ObtenirDefinitionsSuccesCachees(identifiantJeu);
        Dictionary<int, string> nouvellesDefinitions = [];

        foreach (SuccesJeuUtilisateurRetroAchievements succesJeu in succes)
        {
            if (succesJeu.IdentifiantSucces <= 0)
            {
                continue;
            }

            string definition = succesJeu.DefinitionMemoire;

            if (
                definitionsCacheesRalibretro.TryGetValue(
                    succesJeu.IdentifiantSucces,
                    out string? definitionCachee
                )
                && !string.IsNullOrWhiteSpace(definitionCachee)
                && (string.IsNullOrWhiteSpace(definition) || DefinitionSembleHashApi(definition))
            )
            {
                definition = definitionCachee;
            }

            if (string.IsNullOrWhiteSpace(definition))
            {
                continue;
            }

            nouvellesDefinitions[succesJeu.IdentifiantSucces] = definition;
        }

        string nouvelleSignature = ConstruireSignatureDefinitions(nouvellesDefinitions);

        if (string.Equals(_signatureDefinitionsSucces, nouvelleSignature, StringComparison.Ordinal))
        {
            return;
        }

        _definitionsSucces.Clear();
        _validationsDefinitions.Clear();

        foreach ((int identifiantSucces, string definition) in nouvellesDefinitions)
        {
            _definitionsSucces[identifiantSucces] = definition;
        }

        _signatureDefinitionsSucces = nouvelleSignature;
        ActualiserDefinitionsPont();
    }

    /// <summary>
    /// Mémorise une source mémoire locale exploitable pour le bridge rcheevos.
    /// </summary>
    public async Task DefinirSourceLocaleAsync(
        JeuDetecteLocalement? jeuLocal,
        CancellationToken cancellationToken = default
    )
    {
        if (jeuLocal is null || string.IsNullOrWhiteSpace(jeuLocal.NomEmulateur))
        {
            ReinitialiserSourceMemoire();
            return;
        }

        if (string.Equals(jeuLocal.NomEmulateur, "RALibRetro", StringComparison.OrdinalIgnoreCase))
        {
            // RALibRetro passe ici par une source passive, basée sur les savestates.
            bool sourcePassivePrete = await _serviceRcheevosRalibretro.DefinirProcessusAsync(
                jeuLocal,
                cancellationToken
            );
            _sourceMemoireActive = sourcePassivePrete
                ? SourceMemoireRcheevos.RalibretroEtatSauvegarde
                : SourceMemoireRcheevos.Aucune;
            _diagnosticMemoireRetroArch = null;
            _nomEmulateurSource = jeuLocal.NomEmulateur;
            ActualiserPontMemoire();
            return;
        }

        if (!_serviceMemoireRetroArch.EmulateurPrisEnCharge(jeuLocal.NomEmulateur))
        {
            ReinitialiserSourceMemoire();
            return;
        }

        _serviceRcheevosRalibretro.Reinitialiser();
        _nomEmulateurSource = jeuLocal.NomEmulateur;
        // RetroArch est la seule source "live" actuellement pilotée par lecture mémoire directe.
        _diagnosticMemoireRetroArch = await _serviceMemoireRetroArch.SonderAsync(
            jeuLocal.NomEmulateur,
            cancellationToken
        );
        _sourceMemoireActive = _diagnosticMemoireRetroArch is null
            ? SourceMemoireRcheevos.Aucune
            : SourceMemoireRcheevos.RetroArchReseau;
        ActualiserPontMemoire();
    }

    /// <summary>
    /// Lit un bloc mémoire brut depuis la source locale active, si disponible.
    /// </summary>
    public Task<byte[]?> LireMemoireBruteAsync(
        uint adresse,
        int nombreOctets,
        CancellationToken cancellationToken = default
    )
    {
        if (
            _sourceMemoireActive != SourceMemoireRcheevos.RetroArchReseau
            || string.IsNullOrWhiteSpace(_nomEmulateurSource)
        )
        {
            return Task.FromResult<byte[]?>(null);
        }

        return _serviceMemoireRetroArch.LireMemoireAsync(
            _nomEmulateurSource,
            adresse,
            nombreOctets,
            cancellationToken
        );
    }

    /// <summary>
    /// Fournit un résumé lisible de la source mémoire préparée.
    /// </summary>
    public string ObtenirResumeSourceMemoire()
    {
        return _sourceMemoireActive switch
        {
            SourceMemoireRcheevos.RetroArchReseau when _diagnosticMemoireRetroArch is not null =>
                $"Source mémoire : RetroArch réseau ({_diagnosticMemoireRetroArch.Hote}:{_diagnosticMemoireRetroArch.Port})",
            SourceMemoireRcheevos.RalibretroEtatSauvegarde =>
                _serviceRcheevosRalibretro.ObtenirResumeSource(),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Réinitialise le contexte du jeu actif.
    /// </summary>
    public void ReinitialiserJeuActif()
    {
        _jeuActif = null;
        ReinitialiserDefinitionsSucces();
    }

    /// <summary>
    /// Réinitialise la source mémoire locale.
    /// </summary>
    public void ReinitialiserSourceMemoire()
    {
        _sourceMemoireActive = SourceMemoireRcheevos.Aucune;
        _diagnosticMemoireRetroArch = null;
        _nomEmulateurSource = string.Empty;
        _serviceRcheevosRalibretro.Reinitialiser();
        ActualiserPontMemoire();
    }

    /// <summary>
    /// Efface les définitions de succès actuellement mémorisées.
    /// </summary>
    public void ReinitialiserDefinitionsSucces()
    {
        if (_definitionsSucces.Count == 0 && string.IsNullOrEmpty(_signatureDefinitionsSucces))
        {
            return;
        }

        _definitionsSucces.Clear();
        _validationsDefinitions.Clear();
        _signatureDefinitionsSucces = string.Empty;
        ActualiserDefinitionsPont();
    }

    /// <summary>
    /// Tente d'obtenir un Progress Indicator rcheevos pour le succès courant.
    /// </summary>
    public IndicateurProgressionRcheevos? ObtenirIndicateurProgression(int identifiantSucces)
    {
        if (_bridgeIndisponible || _jeuActif is null || identifiantSucces <= 0)
        {
            return null;
        }

        if (_sourceMemoireActive == SourceMemoireRcheevos.RalibretroEtatSauvegarde)
        {
            // La progression passive doit être rechargée juste avant l'appel natif.
            if (!_serviceRcheevosRalibretro.ActualiserProgressionPont())
            {
                return null;
            }
        }

        try
        {
            ProgressIndicatorRcheevosNatif indicateurNatif = new();
            int resultat = MethodesNatives.ObtenirIndicateurProgression(
                _jeuActif.IdentifiantJeu,
                _jeuActif.IdentifiantConsole,
                identifiantSucces,
                ref indicateurNatif
            );

            if (resultat != 0)
            {
                return null;
            }

            string texte = (indicateurNatif.Texte ?? string.Empty).Trim();
            double? pourcentage =
                indicateurNatif.PourcentageDisponible != 0 ? indicateurNatif.Pourcentage : null;

            if (string.IsNullOrWhiteSpace(texte) && !pourcentage.HasValue)
            {
                return null;
            }

            return new IndicateurProgressionRcheevos(texte, pourcentage);
        }
        catch (DllNotFoundException)
        {
            _bridgeIndisponible = true;
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            _bridgeIndisponible = true;
            return null;
        }
        catch (BadImageFormatException)
        {
            _bridgeIndisponible = true;
            return null;
        }
    }

    /// <summary>
    /// Fournit un diagnostic temporaire expliquant pourquoi aucun indicateur rcheevos n'est affichable.
    /// </summary>
    public string ObtenirDiagnosticIndicateurProgression(int identifiantSucces)
    {
        if (identifiantSucces <= 0)
        {
            return string.Empty;
        }

        if (_bridgeIndisponible)
        {
            return "Diagnostic rcheevos : bridge natif indisponible.";
        }

        if (_jeuActif is null)
        {
            return "Diagnostic rcheevos : aucun jeu actif.";
        }

        if (!_definitionsSucces.TryGetValue(identifiantSucces, out string? definition))
        {
            return "Diagnostic rcheevos : définition du succès absente.";
        }

        ValidationDefinitionRcheevos validation = ValiderDefinitionSucces(definition);
        if (!validation.EstValide)
        {
            return string.IsNullOrWhiteSpace(validation.Message)
                ? "Diagnostic rcheevos : définition du succès invalide."
                : $"Diagnostic rcheevos : {validation.Message}";
        }

        if (DefinitionSembleHashApi(definition))
        {
            return "Diagnostic rcheevos : l'API fournit ici un hash MemAddr, pas une définition mesurée exploitable.";
        }

        return _sourceMemoireActive switch
        {
            SourceMemoireRcheevos.Aucune => "Diagnostic rcheevos : aucune source mémoire active.",
            SourceMemoireRcheevos.RalibretroEtatSauvegarde =>
                "Diagnostic rcheevos : aucune progression mesurée trouvée dans l'état RALibRetro.",
            SourceMemoireRcheevos.RetroArchReseau =>
                "Diagnostic rcheevos : aucune progression mesurée pour ce succès.",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Valide une définition de succès avec le validateur rcheevos si le bridge natif le permet.
    /// </summary>
    public ValidationDefinitionRcheevos ValiderDefinitionSucces(int identifiantSucces)
    {
        if (!_definitionsSucces.TryGetValue(identifiantSucces, out string? definition))
        {
            return new ValidationDefinitionRcheevos(false, "Définition du succès absente.");
        }

        return ValiderDefinitionSucces(definition);
    }

    private static bool DefinitionSembleHashApi(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition) || definition.Length != 32)
        {
            return false;
        }

        foreach (char caractere in definition)
        {
            bool hexadecimal =
                (caractere >= '0' && caractere <= '9')
                || (caractere >= 'a' && caractere <= 'f')
                || (caractere >= 'A' && caractere <= 'F');

            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }

    private ValidationDefinitionRcheevos ValiderDefinitionSucces(string definition)
    {
        if (_bridgeIndisponible)
        {
            return new ValidationDefinitionRcheevos(
                false,
                "Validation indisponible : bridge natif absent."
            );
        }

        if (_jeuActif is null || _jeuActif.IdentifiantConsole <= 0)
        {
            return new ValidationDefinitionRcheevos(
                false,
                "Validation indisponible : console inconnue."
            );
        }

        string cle = $"{_jeuActif.IdentifiantConsole}:{definition}";
        if (_validationsDefinitions.TryGetValue(cle, out ValidationDefinitionRcheevos? validation))
        {
            return validation;
        }

        if (DefinitionSembleHashApi(definition))
        {
            validation = new ValidationDefinitionRcheevos(
                false,
                "l'API fournit ici un hash MemAddr, pas une définition mesurée exploitable."
            );
            _validationsDefinitions[cle] = validation;
            return validation;
        }

        try
        {
            StringBuilder message = new(256);
            int resultat = MethodesNatives.ValiderDefinitionSucces(
                _jeuActif.IdentifiantConsole,
                definition,
                message,
                message.Capacity
            );

            validation = resultat switch
            {
                0 => new ValidationDefinitionRcheevos(true, message.ToString().Trim()),
                1 => new ValidationDefinitionRcheevos(false, message.ToString().Trim()),
                3 => new ValidationDefinitionRcheevos(false, "validation native indisponible."),
                _ => new ValidationDefinitionRcheevos(
                    false,
                    string.IsNullOrWhiteSpace(message.ToString())
                        ? $"validation native impossible (code {resultat})."
                        : message.ToString().Trim()
                ),
            };
        }
        catch (DllNotFoundException)
        {
            _bridgeIndisponible = true;
            validation = new ValidationDefinitionRcheevos(
                false,
                "validation indisponible : bridge natif absent."
            );
        }
        catch (EntryPointNotFoundException)
        {
            _bridgeIndisponible = true;
            validation = new ValidationDefinitionRcheevos(
                false,
                "validation indisponible : bridge natif incomplet."
            );
        }
        catch (BadImageFormatException)
        {
            _bridgeIndisponible = true;
            validation = new ValidationDefinitionRcheevos(
                false,
                "validation indisponible : bridge natif incompatible."
            );
        }

        _validationsDefinitions[cle] = validation;
        return validation;
    }

    private void ActualiserPontMemoire()
    {
        if (_bridgeIndisponible)
        {
            return;
        }

        if (_sourceMemoirePont == _sourceMemoireActive)
        {
            return;
        }

        try
        {
            if (_sourceMemoireActive == SourceMemoireRcheevos.RetroArchReseau)
            {
                IntPtr callback = Marshal.GetFunctionPointerForDelegate(CallbackLectureMemoire);
                MethodesNatives.EnregistrerLecteurMemoire(callback, IntPtr.Zero);
                _sourceMemoirePont = _sourceMemoireActive;
                return;
            }

            MethodesNatives.EffacerLecteurMemoire();
            _sourceMemoirePont = SourceMemoireRcheevos.Aucune;
        }
        catch (DllNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (EntryPointNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (BadImageFormatException)
        {
            _bridgeIndisponible = true;
        }
    }

    private static string ConstruireSignatureDefinitions(
        IReadOnlyDictionary<int, string> definitionsSucces
    )
    {
        if (definitionsSucces.Count == 0)
        {
            return string.Empty;
        }

        List<int> identifiants = [.. definitionsSucces.Keys];
        identifiants.Sort();

        StringBuilder signature = new();

        foreach (int identifiant in identifiants)
        {
            signature.Append(identifiant);
            signature.Append('=');
            signature.Append(definitionsSucces[identifiant]);
            signature.Append('|');
        }

        return signature.ToString();
    }

    private void ActualiserDefinitionsPont()
    {
        if (_bridgeIndisponible)
        {
            return;
        }

        try
        {
            // On repart toujours d'une liste complète pour garder le runtime natif cohérent.
            MethodesNatives.EffacerDefinitionsSucces();

            foreach ((int identifiantSucces, string definition) in _definitionsSucces)
            {
                MethodesNatives.DefinirDefinitionSucces(identifiantSucces, definition);
            }
        }
        catch (DllNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (EntryPointNotFoundException)
        {
            _bridgeIndisponible = true;
        }
        catch (BadImageFormatException)
        {
            _bridgeIndisponible = true;
        }
    }

    private int LireMemoirePourBridge(
        uint adresse,
        uint nombreOctets,
        IntPtr tampon,
        uint tailleTampon
    )
    {
        if (
            _sourceMemoireActive != SourceMemoireRcheevos.RetroArchReseau
            || tailleTampon == 0
            || nombreOctets == 0
            || tampon == IntPtr.Zero
        )
        {
            return -1;
        }

        int nombreOctetsInt = checked((int)nombreOctets);
        byte[]? donnees = LireMemoireBruteAsync(adresse, nombreOctetsInt).GetAwaiter().GetResult();

        if (donnees is null || donnees.Length == 0)
        {
            return -1;
        }

        int tailleCopie = Math.Min((int)tailleTampon, donnees.Length);
        // Le bridge natif fournit le tampon de destination ; on y copie juste la lecture brute.
        Marshal.Copy(donnees, 0, tampon, tailleCopie);
        return tailleCopie;
    }

    private static int LireMemoireBridge(
        uint adresse,
        uint nombreOctets,
        IntPtr tampon,
        uint tailleTampon,
        IntPtr contexte
    )
    {
        _ = contexte;
        return _instanceBridge?.LireMemoirePourBridge(adresse, nombreOctets, tampon, tailleTampon)
            ?? -1;
    }

    private sealed record ContexteJeuRcheevos(
        int IdentifiantJeu,
        int IdentifiantConsole,
        string TitreJeu
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DelegateLectureMemoire(
        uint adresse,
        uint nombreOctets,
        IntPtr tampon,
        uint tailleTampon,
        IntPtr contexte
    );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct ProgressIndicatorRcheevosNatif
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string Texte;

        public float Pourcentage;
        public int PourcentageDisponible;
    }

    private static class MethodesNatives
    {
        private const string NomBibliotheque = "ra_compagnon_rcheevos_bridge";

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_register_memory_reader",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern int EnregistrerLecteurMemoire(IntPtr callback, IntPtr contexte);

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_clear_memory_reader",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern int EffacerLecteurMemoire();

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_clear_achievement_definitions",
            CallingConvention = CallingConvention.Cdecl
        )]
        public static extern int EffacerDefinitionsSucces();

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_set_achievement_definition",
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi
        )]
        public static extern int DefinirDefinitionSucces(int identifiantSucces, string definition);

        [DllImport(
            NomBibliotheque,
            EntryPoint = "ra_compagnon_rcheevos_validate_achievement_definition",
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi
        )]
        public static extern int ValiderDefinitionSucces(
            int identifiantConsole,
            string definition,
            StringBuilder message,
            int tailleMessage
        );

        [DllImport(NomBibliotheque, EntryPoint = "ra_compagnon_rcheevos_get_progress_indicator")]
        public static extern int ObtenirIndicateurProgression(
            int identifiantJeu,
            int identifiantConsole,
            int identifiantSucces,
            ref ProgressIndicatorRcheevosNatif indicateur
        );
    }
}

/// <summary>
/// Représente un Progress Indicator rcheevos prêt à être affiché.
/// </summary>
public sealed record IndicateurProgressionRcheevos(string Texte, double? Pourcentage);

/// <summary>
/// Résultat d'une validation native rcheevos sur une définition d'achievement.
/// </summary>
public sealed record ValidationDefinitionRcheevos(bool EstValide, string Message);

public enum SourceMemoireRcheevos
{
    Aucune = 0,
    RetroArchReseau = 1,
    RalibretroEtatSauvegarde = 2,
}
