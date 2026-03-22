using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente une demande de set utilisateur.
/// </summary>
public sealed class DemandeSetUtilisateurRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantDemande { get; set; }

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("GameTitle")]
    public string TitreJeu { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Statut { get; set; } = string.Empty;

    [JsonPropertyName("RequestedAt")]
    public string DateDemande { get; set; } = string.Empty;
}
