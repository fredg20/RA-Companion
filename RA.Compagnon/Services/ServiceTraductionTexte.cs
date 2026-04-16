using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

/*
 * Traduit des chaînes libres vers le français tout en protégeant certains
 * segments sensibles comme les citations et les noms propres probables.
 */
namespace RA.Compagnon.Services;

/*
 * Fournit une traduction opportuniste avec cache mémoire léger pour enrichir
 * certains textes d'interface sans re-solliciter inutilement le service distant.
 */
public sealed partial class ServiceTraductionTexte
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Regex RegexSegmentsEntreGuillemets =
        RegexSegmentsEntreGuillemetsFactory();
    private static readonly Regex RegexSuitesNomsPropres = RegexSuitesNomsPropresFactory();
    private static readonly Regex RegexJetonsNomsPropres = RegexJetonsNomsPropresFactory();
    private static readonly Regex RegexNomPropreContextuel = RegexNomPropreContextuelFactory();
    private static readonly HashSet<string> MotsNonProtegesPremierePosition = new(
        [
            "Activate",
            "Beat",
            "Clear",
            "Collect",
            "Complete",
            "Defeat",
            "Destroy",
            "Earn",
            "Easy",
            "Enter",
            "Expert",
            "Find",
            "Finish",
            "Get",
            "Hard",
            "Hardcore",
            "Hold",
            "Kill",
            "Medium",
            "Normal",
            "Obtain",
            "Open",
            "Play",
            "Reach",
            "Rescue",
            "Save",
            "Score",
            "Softcore",
            "Start",
            "Story",
            "Talk",
            "Time",
            "Trigger",
            "Unlock",
            "Use",
            "Visit",
            "Watch",
            "Win",
        ],
        StringComparer.OrdinalIgnoreCase
    );
    private static readonly HashSet<string> MotsTitresGeneriques = new(
        [
            "Act",
            "Area",
            "Boss",
            "Chapter",
            "Difficulty",
            "Episode",
            "Level",
            "Mission",
            "Mode",
            "Room",
            "Stage",
            "World",
            "Zone",
        ],
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, string> _cacheTraductions = new(
        StringComparer.OrdinalIgnoreCase
    );

    /*
     * Traduit un texte vers le français en réutilisant le cache local et en
     * restaurant les segments protégés après la requête.
     */
    public async Task<string> TraduireVersFrancaisAsync(
        string texte,
        CancellationToken jetonAnnulation = default
    )
    {
        string texteNettoye = texte?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(texteNettoye))
        {
            return string.Empty;
        }

        if (_cacheTraductions.TryGetValue(texteNettoye, out string? traductionCachee))
        {
            return traductionCachee;
        }

        try
        {
            Dictionary<string, string> segmentsProteges = [];
            string texteATraduire = ProtegerSegmentsSensibles(texteNettoye, segmentsProteges);
            string url =
                "https://translate.googleapis.com/translate_a/single"
                + $"?client=gtx&sl=auto&tl=fr&dt=t&q={Uri.EscapeDataString(texteATraduire)}";

            using HttpResponseMessage reponse = await HttpClient.GetAsync(url, jetonAnnulation);
            reponse.EnsureSuccessStatusCode();
            await using Stream flux = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
            using JsonDocument document = await JsonDocument.ParseAsync(
                flux,
                cancellationToken: jetonAnnulation
            );

            if (
                document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() == 0
            )
            {
                return MemoriserTraduction(texteNettoye, texteNettoye);
            }

            JsonElement segments = document.RootElement[0];

            if (segments.ValueKind != JsonValueKind.Array)
            {
                return MemoriserTraduction(texteNettoye, texteNettoye);
            }

            List<string> morceaux = [];

            foreach (JsonElement segment in segments.EnumerateArray())
            {
                if (
                    segment.ValueKind == JsonValueKind.Array
                    && segment.GetArrayLength() > 0
                    && segment[0].ValueKind == JsonValueKind.String
                )
                {
                    string? morceau = segment[0].GetString();

                    if (!string.IsNullOrWhiteSpace(morceau))
                    {
                        morceaux.Add(morceau.Trim());
                    }
                }
            }

            string traduction = string.Join(" ", morceaux).Trim();
            traduction = RestaurerSegmentsProteges(traduction, segmentsProteges);

            return MemoriserTraduction(
                texteNettoye,
                string.IsNullOrWhiteSpace(traduction) ? texteNettoye : traduction
            );
        }
        catch
        {
            return MemoriserTraduction(texteNettoye, texteNettoye);
        }
    }

    /*
     * Mémorise une traduction dans le cache local puis retourne la valeur
     * enregistrée.
     */
    private string MemoriserTraduction(string source, string traduction)
    {
        _cacheTraductions[source] = traduction;
        return traduction;
    }

    /*
     * Protège les segments les plus sensibles avant traduction, en combinant
     * citations, noms propres contextuels, suites nominatives et jetons mixtes.
     */
    private static string ProtegerSegmentsSensibles(
        string texte,
        Dictionary<string, string> segmentsProteges
    )
    {
        string resultat = ProtegerSegmentsEntreGuillemets(texte, segmentsProteges);
        resultat = ProtegerNomsPropresContextuels(resultat, segmentsProteges);
        resultat = ProtegerSegmentsParExpression(
            resultat,
            RegexSuitesNomsPropres,
            segmentsProteges,
            EstNomPropreProbable
        );
        resultat = ProtegerSegmentsParExpression(
            resultat,
            RegexJetonsNomsPropres,
            segmentsProteges,
            EstJetonNomPropreProbable
        );
        return resultat;
    }

    /*
     * Remplace temporairement les segments entre guillemets par des jetons
     * afin d'éviter qu'ils soient modifiés pendant la traduction.
     */
    private static string ProtegerSegmentsEntreGuillemets(
        string texte,
        Dictionary<string, string> segmentsProteges
    )
    {
        return RegexSegmentsEntreGuillemets.Replace(
            texte,
            correspondance =>
            {
                string jeton = CreerJetonSegmentProtege(segmentsProteges);
                segmentsProteges[jeton] = correspondance.Value;
                return jeton;
            }
        );
    }

    /*
     * Protège les noms propres probables lorsqu'ils apparaissent après une
     * préposition forte comme in, at ou inside.
     */
    private static string ProtegerNomsPropresContextuels(
        string texte,
        Dictionary<string, string> segmentsProteges
    )
    {
        return RegexNomPropreContextuel.Replace(
            texte,
            correspondance =>
            {
                string segment = correspondance.Groups["nom"].Value.Trim();

                if (!EstNomPropreContextuelProbable(segment))
                {
                    return correspondance.Value;
                }

                string jeton = CreerJetonSegmentProtege(segmentsProteges);
                segmentsProteges[jeton] = segment;
                return correspondance.Value.Replace(segment, jeton, StringComparison.Ordinal);
            }
        );
    }

    /*
     * Protège les segments qui correspondent à une expression régulière donnée
     * lorsqu'ils passent le filtre heuristique associé.
     */
    private static string ProtegerSegmentsParExpression(
        string texte,
        Regex expression,
        Dictionary<string, string> segmentsProteges,
        Func<string, bool> estEligible
    )
    {
        return expression.Replace(
            texte,
            correspondance =>
            {
                string segment = correspondance.Value.Trim();

                if (!estEligible(segment))
                {
                    return correspondance.Value;
                }

                string jeton = CreerJetonSegmentProtege(segmentsProteges);
                segmentsProteges[jeton] = segment;
                return jeton;
            }
        );
    }

    /*
     * Détermine si un segment ressemble suffisamment à un nom propre composé
     * pour mériter une protection avant traduction.
     */
    private static bool EstNomPropreProbable(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains("RA_", StringComparison.Ordinal))
        {
            return false;
        }

        string premierMot = ExtrairePremierMotNettoye(segment);

        if (string.IsNullOrWhiteSpace(premierMot))
        {
            return false;
        }

        if (MotsNonProtegesPremierePosition.Contains(premierMot))
        {
            return false;
        }

        string[] mots =
        [
            .. segment
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(ExtraireMotNettoye)
                .Where(mot => !string.IsNullOrWhiteSpace(mot)),
        ];

        if (mots.Length == 0)
        {
            return false;
        }

        if (mots.Length == 1)
        {
            return EstJetonNomPropreProbable(mots[0]);
        }

        return true;
    }

    /*
     * Détermine si un nom propre repéré via le contexte mérite d'être protégé,
     * y compris lorsqu'il ne comporte qu'un seul mot en casse titre.
     */
    private static bool EstNomPropreContextuelProbable(string segment)
    {
        if (!EstNomPropreProbable(segment))
        {
            string mot = ExtrairePremierMotNettoye(segment);

            if (
                string.IsNullOrWhiteSpace(mot)
                || MotsNonProtegesPremierePosition.Contains(mot)
                || MotsTitresGeneriques.Contains(mot)
            )
            {
                return false;
            }

            return EstMotTitreProbable(mot);
        }

        return true;
    }

    /*
     * Détermine si un jeton isolé ressemble à un identifiant, un acronyme ou
     * un nom de produit qu'il vaut mieux laisser intact.
     */
    private static bool EstJetonNomPropreProbable(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains("RA_", StringComparison.Ordinal))
        {
            return false;
        }

        string mot = ExtraireMotNettoye(segment);

        if (string.IsNullOrWhiteSpace(mot) || MotsNonProtegesPremierePosition.Contains(mot))
        {
            return false;
        }

        bool contientChiffre = mot.Any(char.IsDigit);
        bool contientMajuscule = mot.Any(char.IsUpper);
        bool contientMinuscule = mot.Any(char.IsLower);
        int nombreLettres = mot.Count(char.IsLetter);

        if (contientChiffre)
        {
            return nombreLettres > 0;
        }

        if (contientMajuscule && contientMinuscule)
        {
            return true;
        }

        return nombreLettres >= 2
            && mot.All(caractere => !char.IsLetter(caractere) || char.IsUpper(caractere));
    }

    /*
     * Vérifie si un mot en casse titre ressemble à un nom propre simple
     * plutôt qu'à une catégorie générique.
     */
    private static bool EstMotTitreProbable(string mot)
    {
        if (string.IsNullOrWhiteSpace(mot) || mot.Length < 3)
        {
            return false;
        }

        if (!char.IsUpper(mot[0]) || mot.Skip(1).Any(char.IsUpper))
        {
            return false;
        }

        return !MotsTitresGeneriques.Contains(mot);
    }

    /*
     * Extrait le premier mot significatif d'un segment pour appliquer les
     * règles d'exclusion au début des phrases d'action.
     */
    private static string ExtrairePremierMotNettoye(string segment)
    {
        string premierBloc =
            segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? string.Empty;
        return ExtraireMotNettoye(premierBloc);
    }

    /*
     * Nettoie un mot isolé en retirant les signes périphériques qui ne doivent
     * pas influencer l'heuristique de détection.
     */
    private static string ExtraireMotNettoye(string mot)
    {
        return mot.Trim()
            .Trim(',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '«', '»');
    }

    /*
     * Construit un jeton interne unique servant à masquer temporairement un
     * segment protégé avant traduction.
     */
    private static string CreerJetonSegmentProtege(
        IReadOnlyDictionary<string, string> segmentsProteges
    )
    {
        return $"[[RA_SEGMENT_{segmentsProteges.Count}]]";
    }

    /*
     * Restaure les segments protégés dans le texte traduit final.
     */
    private static string RestaurerSegmentsProteges(
        string texteTraduit,
        IReadOnlyDictionary<string, string> segmentsProteges
    )
    {
        string resultat = texteTraduit;

        foreach ((string jeton, string valeurOriginale) in segmentsProteges)
        {
            resultat = resultat.Replace(jeton, valeurOriginale, StringComparison.Ordinal);
        }

        return resultat;
    }

    [GeneratedRegex("\"[^\"]+\"|\\u00AB\\s*[^\\u00BB]+\\s*\\u00BB", RegexOptions.Compiled)]
    /*
     * Déclare l'expression régulière utilisée pour repérer les segments
     * encadrés de guillemets à préserver.
     */
    private static partial Regex RegexSegmentsEntreGuillemetsFactory();

    [GeneratedRegex(
        "\\b[A-Z][\\p{L}\\p{M}'’-]+(?:\\s+(?:(?:of|the|and|in|on|at|to|for|de|du|des|la|le|les)\\s+)?[A-Z][\\p{L}\\p{M}'’-]+)+\\b",
        RegexOptions.Compiled
    )]
    /*
     * Déclare l'expression régulière utilisée pour repérer les suites de mots
     * en casse titre qui ressemblent à des noms de lieux, niveaux ou objets.
     */
    private static partial Regex RegexSuitesNomsPropresFactory();

    [GeneratedRegex(
        "\\b(?:(?=[A-Za-z0-9]*[A-Za-z])[A-Za-z0-9]*\\d+[A-Za-z0-9]*|[a-z]+[A-Z][A-Za-z0-9]*|[A-Z][a-z0-9]+[A-Z][A-Za-z0-9]*|[A-Z]{2,}[A-Za-z0-9]*)\\b",
        RegexOptions.Compiled
    )]
    /*
     * Déclare l'expression régulière utilisée pour repérer les jetons mixtes
     * comme RetroArch, PCSX2, mGBA ou tout identifiant contenant des chiffres.
     */
    private static partial Regex RegexJetonsNomsPropresFactory();

    [GeneratedRegex(
        "(?i:\\b(?:in|at|inside|into|from|near|against|versus|vs\\.?)\\b)\\s+(?<nom>[A-Z][\\p{L}\\p{M}'’-]+(?:\\s+(?:(?:of|the|and|de|du|des|la|le|les)\\s+)?[A-Z][\\p{L}\\p{M}'’-]+)*)",
        RegexOptions.Compiled
    )]
    /*
     * Déclare l'expression régulière utilisée pour repérer un nom propre
     * plausible lorsqu'il suit une préposition contextuelle forte.
     */
    private static partial Regex RegexNomPropreContextuelFactory();
}
