/*
 * Déclare les différents modes de déclenchement d'un scénario de test de
 * succès.
 */
namespace RA.Compagnon.Modeles.Debug;

/*
 * Représente la façon dont un test de succès simulé doit être déclenché.
 */
public enum ModeDeclenchementTestSuccesDebug
{
    InterneUi = 0,
    SourceLocale = 1,
    Session = 2,
}