namespace RA.Compagnon.Modeles.Etat;

public sealed record EtatPipelineChargementJeu(
    int IdentifiantJeu,
    string TitreJeu,
    int VersionChargement,
    EtapePipelineChargementJeu EtapesChargees,
    DateTimeOffset HorodatageDerniereMiseAJourUtc
)
{
    public static EtatPipelineChargementJeu Vide { get; } =
        new(0, string.Empty, 0, EtapePipelineChargementJeu.Aucune, DateTimeOffset.MinValue);
}
