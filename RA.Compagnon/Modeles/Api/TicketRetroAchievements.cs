using System.Text.Json.Serialization;

namespace RA.Compagnon.Modeles.Api;

/// <summary>
/// Représente un ticket ou des statistiques de ticket issues de l'API.
/// </summary>
public sealed class TicketRetroAchievements : ModeleApiRetroAchievementsBase
{
    [JsonPropertyName("ID")]
    public int IdentifiantTicket { get; set; }

    [JsonPropertyName("GameID")]
    public int IdentifiantJeu { get; set; }

    [JsonPropertyName("AchievementID")]
    public int IdentifiantSucces { get; set; }

    [JsonPropertyName("User")]
    public string Utilisateur { get; set; } = string.Empty;

    [JsonPropertyName("Status")]
    public string Statut { get; set; } = string.Empty;

    [JsonPropertyName("Created")]
    public string DateCreation { get; set; } = string.Empty;
}
