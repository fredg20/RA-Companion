namespace RA.Compagnon.Modeles.Etat;

[Flags]
public enum EtapePipelineChargementJeu
{
    Aucune = 0,
    DonneesMinimales = 1 << 0,
    MetadonneesEnrichies = 1 << 1,
    SuccesCharges = 1 << 2,
    ImagesChargees = 1 << 3,
}
