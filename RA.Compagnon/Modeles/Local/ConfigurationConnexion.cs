using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Local;

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

    [JsonIgnore]
    public EtatJeuAfficheLocal? DernierJeuAffiche { get; set; }

    [JsonIgnore]
    public EtatSuccesAfficheLocal? DernierSuccesAffiche { get; set; }

    [JsonIgnore]
    public EtatListeSuccesAfficheeLocal? DerniereListeSuccesAffichee { get; set; }

    public Dictionary<string, string> EmplacementsEmulateursManuels { get; set; } = [];

    public Dictionary<string, string> EmplacementsEmulateursDetectes { get; set; } = [];
}
