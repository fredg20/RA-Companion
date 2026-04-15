using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

/*
 * Traduit des chaînes libres vers le français tout en protégeant certains
 * segments sensibles comme les citations.
 */
namespace RA.Compagnon.Services;

/*
 * Fournit une traduction opportuniste avec cache mémoire léger pour enrichir
 * certains textes d'interface sans re-solliciter inutilement le service distant.
 */
public sealed partial class ServiceTraductionTexte
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Regex RegexSegmentsEntreGuillemets = MyRegex();
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
            string texteATraduire = ProtegerSegmentsEntreGuillemets(texteNettoye, segmentsProteges);
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
     * Remplace temporairement les segments entre guillemets par des jetons
     * afin d'éviter qu'ils soient modifiés pendant la traduction.
     */
    private static string ProtegerSegmentsEntreGuillemets(
        string texte,
        Dictionary<string, string> segmentsProteges
    )
    {
        int index = 0;

        return RegexSegmentsEntreGuillemets.Replace(
            texte,
            correspondance =>
            {
                string jeton = $"[[RA_CITATION_{index++}]]";
                segmentsProteges[jeton] = correspondance.Value;
                return jeton;
            }
        );
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
    private static partial Regex MyRegex();
}
