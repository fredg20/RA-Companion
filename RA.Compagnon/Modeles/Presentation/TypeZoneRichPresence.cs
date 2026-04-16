/*
 * Énumère les grandes familles de zones que Compagnon peut reconnaître
 * dans un message de Rich Presence.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Qualifie la nature de la zone extraite pour permettre un affichage ou
 * un traitement plus précis par l'interface.
 */
public enum TypeZoneRichPresence
{
    Inconnue = 0,
    Niveau,
    Monde,
    Chapitre,
    Zone,
    Donjon,
    Boss,
    Activite,
}
