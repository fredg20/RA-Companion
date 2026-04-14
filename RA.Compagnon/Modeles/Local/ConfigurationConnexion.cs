using System.Text.Json.Serialization;

/*
 * Représente la configuration locale persistée de l'application, incluant
 * la géométrie de fenêtre et les emplacements d'émulateurs.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke les préférences et états persistables nécessaires au prochain
 * démarrage de Compagnon.
 */
public sealed class ConfigurationConnexion
{
    [JsonIgnore]
    public string Pseudo { get; set; } = string.Empty;

    [JsonIgnore]
    public string CleApiWeb { get; set; } = string.Empty;

    public double? PositionGaucheFenetre { get; set; }

    public double? PositionHautFenetre { get; set; }

    public double LargeurFenetre { get; set; } = 1100;

    public double HauteurFenetre { get; set; } = 700;

    public string ModeAffichageSucces { get; set; } = "Normal";

    public bool HaloBoutonAidePremiereUtilisationDejaAffiche { get; set; }

    [JsonIgnore]
    public EtatJeuAfficheLocal? DernierJeuAffiche { get; set; }

    [JsonIgnore]
    public EtatSuccesAfficheLocal? DernierSuccesAffiche { get; set; }

    [JsonIgnore]
    public EtatListeSuccesAfficheeLocal? DerniereListeSuccesAffichee { get; set; }

    public Dictionary<string, string> EmplacementsEmulateursManuels { get; set; } = [];

    public Dictionary<string, string> EmplacementsEmulateursDetectes { get; set; } = [];
}
