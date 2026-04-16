using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Presentation;

/*
 * Extrait une zone ou une étape plausible à partir d'un message de Rich
 * Presence afin de proposer un repère de progression réutilisable.
 */
namespace RA.Compagnon.Services;

/*
 * Applique des règles universelles et prudentes sur les messages de Rich
 * Presence pour identifier une zone courante sans inventer de faux niveau.
 */
public sealed partial class ServiceExtractionZoneRichPresence
{
    private static readonly HashSet<string> ZonesTropGeneriques =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "adventure mode",
            "arcade mode",
            "challenge mode",
            "demo",
            "game over",
            "hard mode",
            "hardcore mode",
            "loading",
            "main menu",
            "map",
            "menu",
            "paused",
            "softcore mode",
            "story mode",
            "title screen",
            "world map",
        };

    /*
     * Analyse le message de Rich Presence brut et retourne soit une zone
     * fiable, soit un simple contexte textuel condensé.
     */
    public static AnalyseZoneRichPresence Analyser(string messageRichPresence)
    {
        string message = messageRichPresence?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            return new AnalyseZoneRichPresence();
        }

        AnalyseZoneRichPresence? analyse =
            EssayerExtraireZoneExplicite(message)
            ?? EssayerExtraireZoneParPreposition(message)
            ?? EssayerExtraireZoneNommee(message);

        if (analyse is not null)
        {
            return analyse;
        }

        return ConstruireAnalyse(
            message,
            CondenserMessage(message),
            TypeZoneRichPresence.Activite,
            35,
            estFiable: false
        );
    }

    /*
     * Détecte les formes les plus explicites du type « World 3-2 » ou
     * « Chapter 4 - Prison ».
     */
    private static AnalyseZoneRichPresence? EssayerExtraireZoneExplicite(string message)
    {
        Match correspondance = ZoneExpliciteRegex().Match(message);

        if (!correspondance.Success)
        {
            return null;
        }

        string typeBrut = correspondance.Groups["type"].Value.Trim();
        string index = correspondance.Groups["index"].Value.Trim();
        string nom = correspondance.Groups["name"].Value.Trim();
        string zone = string.IsNullOrWhiteSpace(nom) ? $"{typeBrut} {index}" : $"{typeBrut} {index} - {nom}";

        zone = NettoyerZone(zone);

        if (ZoneEstTropGenerique(zone))
        {
            return null;
        }

        return ConstruireAnalyse(message, zone, MapperTypeZone(typeBrut, zone), 95, estFiable: true);
    }

    /*
     * Détecte les lieux annoncés après une préposition, par exemple
     * « in Forest Temple » ou « at Green Hill Zone ».
     */
    private static AnalyseZoneRichPresence? EssayerExtraireZoneParPreposition(string message)
    {
        Match correspondance = ZoneParPrepositionRegex().Match(message);

        if (!correspondance.Success)
        {
            return null;
        }

        string zone = NettoyerZone(correspondance.Groups["zone"].Value);

        if (string.IsNullOrWhiteSpace(zone) || ZoneEstTropGenerique(zone))
        {
            return null;
        }

        return ConstruireAnalyse(message, zone, MapperTypeZone(string.Empty, zone), 84, estFiable: true);
    }

    /*
     * Tente une dernière détection sur les lieux nommés terminés par un mot
     * structurant comme « Temple », « Zone » ou « Castle ».
     */
    private static AnalyseZoneRichPresence? EssayerExtraireZoneNommee(string message)
    {
        Match correspondance = ZoneNommeeRegex().Match(message);

        if (!correspondance.Success)
        {
            return null;
        }

        string zone = NettoyerZone(correspondance.Groups["zone"].Value);

        if (string.IsNullOrWhiteSpace(zone) || ZoneEstTropGenerique(zone))
        {
            return null;
        }

        return ConstruireAnalyse(message, zone, MapperTypeZone(string.Empty, zone), 78, estFiable: true);
    }

    /*
     * Construit une analyse normalisée afin que tous les appelants disposent
     * du même format, quel que soit le chemin de détection retenu.
     */
    private static AnalyseZoneRichPresence ConstruireAnalyse(
        string texteSource,
        string zoneDetectee,
        TypeZoneRichPresence typeZone,
        int scoreConfiance,
        bool estFiable
    )
    {
        return new AnalyseZoneRichPresence
        {
            TexteSource = texteSource,
            ResumeCourt = CondenserMessage(texteSource),
            ZoneDetectee = zoneDetectee,
            LibelleType = TraduireTypeZone(typeZone),
            TypeZone = typeZone,
            ScoreConfiance = scoreConfiance,
            EstFiable = estFiable,
        };
    }

    /*
     * Détermine le type de zone à partir du mot-clé détecté ou, à défaut,
     * du contenu même de la zone reconnue.
     */
    private static TypeZoneRichPresence MapperTypeZone(string typeBrut, string zone)
    {
        if (!string.IsNullOrWhiteSpace(typeBrut))
        {
            return typeBrut.Trim().ToLowerInvariant() switch
            {
                "world" => TypeZoneRichPresence.Monde,
                "chapter" => TypeZoneRichPresence.Chapitre,
                "boss" => TypeZoneRichPresence.Boss,
                "dungeon" => TypeZoneRichPresence.Donjon,
                "temple" => TypeZoneRichPresence.Donjon,
                "castle" => TypeZoneRichPresence.Donjon,
                "palace" => TypeZoneRichPresence.Donjon,
                "tower" => TypeZoneRichPresence.Donjon,
                "fortress" => TypeZoneRichPresence.Donjon,
                "level" => TypeZoneRichPresence.Niveau,
                "stage" => TypeZoneRichPresence.Niveau,
                "act" => TypeZoneRichPresence.Niveau,
                "round" => TypeZoneRichPresence.Niveau,
                "episode" => TypeZoneRichPresence.Niveau,
                "mission" => TypeZoneRichPresence.Niveau,
                "room" => TypeZoneRichPresence.Niveau,
                "floor" => TypeZoneRichPresence.Niveau,
                "area" => TypeZoneRichPresence.Zone,
                "zone" => TypeZoneRichPresence.Zone,
                _ => TypeZoneRichPresence.Zone,
            };
        }

        if (ZoneDonjonRegex().IsMatch(zone))
        {
            return TypeZoneRichPresence.Donjon;
        }

        if (ZoneMondeRegex().IsMatch(zone))
        {
            return TypeZoneRichPresence.Monde;
        }

        if (ZoneChapitreRegex().IsMatch(zone))
        {
            return TypeZoneRichPresence.Chapitre;
        }

        if (ZoneNiveauRegex().IsMatch(zone))
        {
            return TypeZoneRichPresence.Niveau;
        }

        return TypeZoneRichPresence.Zone;
    }

    /*
     * Traduit le type technique de zone en libellé court en français.
     */
    private static string TraduireTypeZone(TypeZoneRichPresence typeZone)
    {
        return typeZone switch
        {
            TypeZoneRichPresence.Niveau => "Niveau",
            TypeZoneRichPresence.Monde => "Monde",
            TypeZoneRichPresence.Chapitre => "Chapitre",
            TypeZoneRichPresence.Zone => "Zone",
            TypeZoneRichPresence.Donjon => "Donjon",
            TypeZoneRichPresence.Boss => "Boss",
            TypeZoneRichPresence.Activite => "Activité",
            _ => string.Empty,
        };
    }

    /*
     * Nettoie le texte extrait pour supprimer les ponctuations parasites et
     * conserver un libellé de zone stable.
     */
    private static string NettoyerZone(string zone)
    {
        if (string.IsNullOrWhiteSpace(zone))
        {
            return string.Empty;
        }

        return EspacesRegex()
            .Replace(zone.Replace(":", " : "), " ")
            .Trim(' ', '.', ',', ';', '-', ':');
    }

    /*
     * Empêche de traiter comme une zone réelle un contexte trop générique
     * tel qu'un menu, un mode de jeu ou un simple état système.
     */
    private static bool ZoneEstTropGenerique(string zone)
    {
        return string.IsNullOrWhiteSpace(zone)
            || ZonesTropGeneriques.Contains(zone.Trim())
            || !ContientCaractereUtile(zone);
    }

    /*
     * Condense le message de Rich Presence lorsqu'aucune zone exploitable
     * n'a pu être extraite avec une confiance suffisante.
     */
    private static string CondenserMessage(string message)
    {
        string texte = EspacesRegex().Replace(message.Trim(), " ");

        return texte.Length <= 80 ? texte : texte[..77].TrimEnd() + "...";
    }

    /*
     * Vérifie qu'une zone potentielle contient plus qu'un séparateur ou une
     * ponctuation purement décorative.
     */
    private static bool ContientCaractereUtile(string zone)
    {
        foreach (char caractere in zone)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(
        @"\b(?<type>world|level|stage|act|round|chapter|episode|mission|area|zone|floor|room)\s*(?<index>[A-Z]?\d+(?:[-.:]\d+)*(?:\s*[A-Z])?)(?:\s*[-:]\s*(?<name>[A-Z][A-Za-z0-9'&/.\-]*(?:\s+[A-Z][A-Za-z0-9'&/.\-]*){0,5}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex ZoneExpliciteRegex();

    [GeneratedRegex(
        @"\b(?:in|at|inside|on|into)\s+(?:the\s+)?(?<zone>(?:[A-Z][A-Za-z0-9'&/.\-]*|\d+(?:-\d+)+)(?:\s+(?:[A-Z][A-Za-z0-9'&/.\-]*|\d+(?:-\d+)+)){0,5})",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex ZoneParPrepositionRegex();

    [GeneratedRegex(
        @"\b(?<zone>[A-Z][A-Za-z0-9'&/.\-]*(?:\s+[A-Z][A-Za-z0-9'&/.\-]*){0,4}\s+(?:Temple|Castle|Dungeon|Tower|Palace|Fortress|Forest|Cave|Zone|Stage|Area|World))\b",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex ZoneNommeeRegex();

    [GeneratedRegex(@"\b(Temple|Castle|Dungeon|Tower|Palace|Fortress|Cave)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ZoneDonjonRegex();

    [GeneratedRegex(@"\bWorld\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ZoneMondeRegex();

    [GeneratedRegex(@"\bChapter\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ZoneChapitreRegex();

    [GeneratedRegex(@"\b(Level|Stage|Act|Round|Episode|Mission|Floor|Room)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ZoneNiveauRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspacesRegex();
}
