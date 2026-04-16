/*
 * Décrit les grandes familles de regroupement que l'analyse hybride peut
 * proposer à partir des descriptions de succès.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Liste les types de cohérence détectables sans dépendre d'un jeu précis.
 */
public enum TypeGroupeSuccesPotentiel
{
    Inconnu,
    Niveau,
    Boss,
    Monde,
    Collection,
    Mode,
    Objet,
    DefiTechnique,
    NonRelie,
    Lexical,
}
