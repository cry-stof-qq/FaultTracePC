namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 86 — dire POURQUOI la charge processeur manque dans la boîte noire.
///
/// Constaté le 24/09/2026 : sur une machine, tous les relevés portaient « — » dans la
/// colonne CPU %, alors que la température du GPU et la mémoire étaient bien
/// enregistrées. Cause non établie. Plutôt que de deviner, le service écrit à chaque
/// démarrage ce que la bibliothèque de capteurs (LibreHardwareMonitor) a vu :
/// a-t-elle pu s'ouvrir, quel processeur, quelles sondes de charge. Le rapport le
/// montre quand la charge manque. La prochaine machine concernée dira la cause.
///
/// Placé dans Core pour être testé : le service, lui, n'est pas chargé par les tests.
/// </summary>
public static class DiagnosticCapteurs
{
    /// <summary>Nom de la sonde de charge que le service lit (LibreHardwareMonitor).</summary>
    public const string SondeChargeAttendue = "Total";

    /// <param name="erreurOuverture">Message si la bibliothèque n'a pas pu s'ouvrir, sinon null.</param>
    /// <param name="processeurs">Processeurs vus, avec le nom de leurs sondes de charge.</param>
    /// <param name="autresMateriels">Autres matériels vus (type et nom), pour le cas où aucun processeur ne l'est.</param>
    public static string Decrire(string? erreurOuverture,
                                 IReadOnlyList<(string Nom, IReadOnlyList<string> SondesCharge)> processeurs,
                                 IReadOnlyList<string> autresMateriels)
    {
        if (erreurOuverture is not null)
            return Lang.T($"bibliothèque de capteurs non ouverte : {erreurOuverture}",
                          $"sensor library not opened: {erreurOuverture}");

        if (processeurs.Count == 0)
        {
            var vus = autresMateriels.Count == 0 ? Lang.T("aucun", "none") : string.Join(", ", autresMateriels);
            return Lang.T($"aucun processeur vu par la bibliothèque de capteurs (matériel vu : {vus})",
                          $"no processor seen by the sensor library (hardware seen: {vus})");
        }

        return string.Join(" ; ", processeurs.Select(p =>
        {
            var aLaSonde = p.SondesCharge.Any(n => n.Contains(SondeChargeAttendue, StringComparison.OrdinalIgnoreCase));
            var sondes = p.SondesCharge.Count == 0
                ? Lang.T("aucune sonde de charge", "no load sensor")
                : Lang.T($"sondes de charge : {string.Join(", ", p.SondesCharge)}", $"load sensors: {string.Join(", ", p.SondesCharge)}");
            var verdict = aLaSonde
                ? ""
                : Lang.T($" — pas de sonde « {SondeChargeAttendue} », celle que le service lit", $" — no “{SondeChargeAttendue}” sensor, the one the service reads");
            return Lang.T($"processeur « {p.Nom} » ; {sondes}{verdict}", $"processor “{p.Nom}”; {sondes}{verdict}");
        }));
    }
}
