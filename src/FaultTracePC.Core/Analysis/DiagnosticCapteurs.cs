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
    /// Point 120 (1.11.0) — comme <see cref="Encoder"/>, avec en plus le nom des sondes
    /// de fréquence (« freq= ») et de consommation (« puiss= ») de chaque processeur,
    /// même vides : sur la prochaine machine sans fréquence enregistrée, on saura si la
    /// bibliothèque n'en voyait aucune. <see cref="Lire"/> ignore ces champs : une
    /// version antérieure qui relit ce journal n'en est pas gênée.
    /// </summary>
    /// <param name="pilotePawnIO">
    /// Point 120 (1.11.0, essai du 08/11/2026) — état du pilote PawnIO tel que le service
    /// l'a relevé : <see cref="PawnIOAbsent"/>, ou l'état donné par Windows (« Running »,
    /// « Stopped »…). Null si le relevé a échoué : le champ n'est alors pas écrit.
    /// </param>
    public static string EncoderComplet(string? erreurOuverture,
                                        IReadOnlyList<(string Nom, IReadOnlyList<string> SondesCharge, IReadOnlyList<string> SondesFrequence, IReadOnlyList<string> SondesPuissance)> processeurs,
                                        IReadOnlyList<string> autresMateriels,
                                        string? pilotePawnIO = null)
    {
        static string Net(string v) => v.Replace('|', ' ').Replace(',', ' ').Replace('=', ' ').Trim();

        var pawnio = pilotePawnIO is null ? "" : $"|pawnio={Net(pilotePawnIO)}";
        if (erreurOuverture is not null || processeurs.Count == 0)
            return Encoder(erreurOuverture, new List<(string, IReadOnlyList<string>)>(), autresMateriels) + pawnio;
        return Version + string.Concat(processeurs.Select(p =>
            $"|cpu={Net(p.Nom)}|charge={string.Join(",", p.SondesCharge.Select(Net))}"
            + $"|freq={string.Join(",", p.SondesFrequence.Select(Net))}|puiss={string.Join(",", p.SondesPuissance.Select(Net))}"))
            + pawnio;
    }

    /// <summary>
    /// Point 120 — valeur écrite par le service quand aucun pilote PawnIO n'est inscrit
    /// dans Windows. Mesuré le 08/11/2026 sur le poste de l'auteur : sans ce pilote, la
    /// bibliothèque de capteurs (LibreHardwareMonitor 0.9.6) voit les sondes de
    /// température, de fréquence et de consommation du processeur, mais elles restent
    /// vides ; une fois le pilote installé et le service redémarré, elles sont lues.
    /// </summary>
    public const string PawnIOAbsent = "absent";

    /// <summary>État relevé par Windows pour un pilote en marche.</summary>
    public const string PawnIOActif = "Running";

    /// <summary>
    /// Point 120 — l'état du pilote PawnIO écrit par le service (<see cref="EncoderComplet"/>).
    /// Null quand la ligne n'en porte pas : service antérieur, ou relevé impossible.
    /// </summary>
    public static string? EtatPawnIO(string? brut)
    {
        if (brut is null || !brut.StartsWith(Version + "|", StringComparison.Ordinal)) return null;
        foreach (var champ in brut.Split('|').Skip(1))
            if (champ.StartsWith("pawnio=", StringComparison.Ordinal))
                return champ["pawnio=".Length..].Trim() is { Length: > 0 } v ? v : null;
        return null;
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

    /// <summary>
    /// Point 120 — les noms des sondes de fréquence et de consommation écrits par le
    /// service (<see cref="EncoderComplet"/>), tous processeurs confondus. Null quand la
    /// ligne n'en porte pas : service antérieur à la 1.11.0, ou bibliothèque non ouverte.
    /// </summary>
    public static (IReadOnlyList<string> Frequence, IReadOnlyList<string> Puissance)? Sondes(string? brut)
    {
        if (brut is null || !brut.StartsWith(Version + "|", StringComparison.Ordinal)) return null;
        var freq = new List<string>(); var puiss = new List<string>(); bool vu = false;
        foreach (var champ in brut.Split('|').Skip(1))
        {
            var i = champ.IndexOf('=');
            if (i < 0) continue;
            var (cle, val) = (champ[..i], champ[(i + 1)..]);
            var liste = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (cle == "freq") { vu = true; freq.AddRange(liste); }
            else if (cle == "puiss") { vu = true; puiss.AddRange(liste); }
        }
        return vu ? (freq, puiss) : null;
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
