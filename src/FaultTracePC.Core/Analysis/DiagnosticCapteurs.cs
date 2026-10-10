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
///
/// ÉCRIT SANS LANGUE, LU DANS LA LANGUE DU RAPPORT. Constaté le 10/10/2026 à la
/// première installation de la 1.8.0 sur le poste de l'auteur : le service tourne
/// sous le compte SYSTEM, dont la langue n'est pas celle de l'utilisateur, et avait
/// écrit son diagnostic EN ANGLAIS — qu'un rapport en français aurait cité tel quel.
/// Le service écrit donc des faits bruts (<see cref="Encoder"/>), et le rapport les
/// met en phrases dans sa propre langue (<see cref="Lire"/>).
/// </summary>
public static class DiagnosticCapteurs
{
    private const string Version = "v1";

    /// <summary>
    /// Forme brute, sans langue, écrite dans le journal par le service. Exemple :
    /// <c>v1|cpu=Nom du processeur|charge=CPU Core #1,CPU Total</c>.
    /// </summary>
    public static string Encoder(string? erreurOuverture,
                                 IReadOnlyList<(string Nom, IReadOnlyList<string> SondesCharge)> processeurs,
                                 IReadOnlyList<string> autresMateriels)
    {
        static string Net(string v) => v.Replace('|', ' ').Replace(',', ' ').Replace('=', ' ').Trim();

        if (erreurOuverture is not null) return $"{Version}|erreur={Net(erreurOuverture)}";
        if (processeurs.Count == 0) return $"{Version}|materiel={string.Join(",", autresMateriels.Select(Net))}";
        return Version + string.Concat(processeurs.Select(p =>
            $"|cpu={Net(p.Nom)}|charge={string.Join(",", p.SondesCharge.Select(Net))}"));
    }

    /// <summary>
    /// Met la forme brute en phrase, dans la langue courante. Un texte qui n'est pas
    /// dans la forme brute (journal d'une version de développement) est rendu tel quel.
    /// </summary>
    public static string Lire(string brut)
    {
        if (!brut.StartsWith(Version + "|", StringComparison.Ordinal)) return brut;

        string? erreur = null;
        var autres = new List<string>();
        var processeurs = new List<(string Nom, IReadOnlyList<string> SondesCharge)>();
        string? cpu = null;

        static List<string> Liste(string v) => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        foreach (var champ in brut.Split('|').Skip(1))
        {
            var i = champ.IndexOf('=');
            if (i < 0) continue;
            var (cle, val) = (champ[..i], champ[(i + 1)..]);
            switch (cle)
            {
                case "erreur": erreur = val; break;
                case "materiel": autres = Liste(val); break;
                case "cpu": cpu = val; break;
                case "charge" when cpu is not null: processeurs.Add((cpu, Liste(val))); cpu = null; break;
            }
        }
        if (cpu is not null) processeurs.Add((cpu, new List<string>()));
        return Decrire(erreur, processeurs, autres);
    }

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

        return string.Join(Lang.T(" ; ", "; "), processeurs.Select(p =>
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
