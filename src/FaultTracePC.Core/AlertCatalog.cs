namespace FaultTracePC.Core;

/// <summary>
/// Texte des alertes préventives, reconstruit à partir de ce qui a été OBSERVÉ
/// plutôt que relu depuis le fichier.
///
/// LE PROBLÈME QU'ELLE RÉSOUT
/// Le service écrivait la phrase française dans <c>alerts.json</c> au moment de
/// l'émission. Le lendemain, l'application en anglais relisait ce fichier et
/// affichait la phrase française : elle n'avait rien d'autre sous la main.
///
/// Or le fait, lui, était déjà là : l'identifiant de règle (<c>cpu_temp</c>) et
/// la valeur mesurée (92). « La règle température processeur s'est déclenchée à
/// 92 » suffit à refabriquer la phrase, dans la langue du moment.
///
/// LA MÊME TABLE DES DEUX CÔTÉS
/// Le service appelle <see cref="Localize"/> avant d'écrire, le lecteur l'appelle
/// après avoir lu. Une seule source de texte : deux copies finiraient par
/// diverger, et la divergence ne se verrait que sur les vieux fichiers.
///
/// CE QU'ON NE PEUT PAS REFABRIQUER
/// Deux règles citent un extrait du message de Windows. Il est conservé à part
/// (<see cref="PreventiveAlert.Extract"/>) parce qu'il n'est pas déductible.
/// Pour une alerte écrite par une version antérieure, ce champ est absent :
/// <see cref="Localize"/> renonce alors et laisse le texte d'origine. Mieux vaut
/// une phrase dans la mauvaise langue qu'une phrase amputée de son fait.
/// </summary>
public static class AlertCatalog
{
    private const string DiskPrefix = "disk_health_";

    /// <summary>
    /// Réécrit Titre, Détail et Recommandation dans la langue en cours.
    /// Renvoie false — sans rien modifier — si la règle est inconnue ou si la
    /// donnée nécessaire manque.
    /// </summary>
    public static bool Localize(PreventiveAlert a)
    {
        if (a.RuleId.StartsWith(DiskPrefix, StringComparison.Ordinal))
            return LocalizeDisk(a);

        switch (a.RuleId)
        {
            case "cpu_temp" when a.Value is { } v:
                a.Title = Lang.T($"Température du processeur élevée : {v:0} °C", $"Processor temperature high: {v:0} °C");
                a.Details = Lang.T($"Le CPU dépasse {v:0} °C de façon soutenue. Au-delà de ~95 °C, le processeur se bride puis la machine peut s'éteindre brutalement pour se protéger.", $"The CPU is sustaining more than {v:0} °C. Above ~95 °C the processor throttles, then the machine may shut down abruptly to protect itself.");
                a.Recommendation = Lang.T("Vérifier la ventilation : dépoussiérer radiateur et ventilateurs, contrôler leur rotation, renouveler la pâte thermique si la machine a plus de 3-4 ans. Fermer les applications qui chargent le CPU pour tester.", "Check the cooling: clear the dust from the heatsink and fans, check that they spin, renew the thermal paste if the machine is more than 3-4 years old. Close the applications loading the CPU to test.");
                return true;

            case "gpu_temp" when a.Value is { } v:
                a.Title = Lang.T($"Température de la carte graphique élevée : {v:0} °C", $"Graphics card temperature high: {v:0} °C");
                a.Details = Lang.T($"Le GPU dépasse {v:0} °C de façon soutenue — risque d'écran noir, de réinitialisation du pilote (TDR) ou d'arrêt brutal.", $"The GPU is sustaining more than {v:0} °C — risk of a black screen, a driver reset (TDR) or an abrupt shutdown.");
                a.Recommendation = Lang.T("Dépoussiérer la carte et le flux d'air du boîtier ; vérifier la courbe de ventilation ; retirer tout overclocking.", "Clear the dust from the card and the case airflow; check the fan curve; remove any overclocking.");
                return true;

            case "commit" when a.Value is { } v:
                a.Title = Lang.T($"Mémoire virtuelle presque saturée : {v:0} %", $"Virtual memory nearly exhausted: {v:0}%");
                a.Details = Lang.T($"La mémoire engagée (RAM + fichier d'échange) atteint {v:0} %. À saturation, Windows gèle, les applications plantent et des écrans bleus mémoire peuvent survenir.", $"Committed memory (RAM + page file) has reached {v:0}%. At saturation Windows freezes, applications crash and memory blue screens can occur.")
                          + (a.Extract is { Length: > 0 } p
                                ? Lang.T($" Processus dominants : {p}.", $" Dominant processes: {p}.")
                                : "");
                a.Recommendation = Lang.T("Fermer les applications les plus gourmandes ; si la virtualisation (vmmem/WSL/Docker) est en tête, lui fixer une limite via %USERPROFILE%\\.wslconfig ([wsl2] puis memory=8GB), puis « wsl --shutdown ».", "Close the most demanding applications; if virtualisation (vmmem/WSL/Docker) is at the top, cap it through %USERPROFILE%\\.wslconfig ([wsl2] then memory=8GB), then “wsl --shutdown”.");
                return true;

            case "whea":
                a.Title = Lang.T("Erreur matérielle signalée par le processeur (WHEA)", "Hardware error reported by the processor (WHEA)");
                a.Details = Lang.T("Le matériel vient de signaler une erreur corrigée ou fatale. Répétées, ces erreurs annoncent une défaillance CPU, mémoire, carte mère ou alimentation.", "The hardware has just reported a corrected or fatal error. Repeated, these errors announce a failure of the CPU, memory, motherboard or power supply.");
                a.Recommendation = Lang.T("Vérifier températures et alimentation, retirer tout overclocking/XMP, mettre à jour le BIOS. Si les erreurs persistent, faire tester le matériel.", "Check temperatures and power supply, remove any overclocking/XMP, update the BIOS. If the errors persist, have the hardware tested.");
                return true;

            case "power41":
                a.Title = Lang.T("Arrêt brutal détecté (coupure sans arrêt propre)", "Abrupt shutdown detected (power loss without a clean stop)");
                a.Details = Lang.T("Le système s'est éteint ou a redémarré sans arrêt propre. Causes typiques : alimentation défaillante, surchauffe déclenchant la protection, ou blocage matériel complet.", "The system switched off or restarted without a clean shutdown. Typical causes: a failing power supply, overheating tripping the protection, or a complete hardware freeze.");
                a.Recommendation = Lang.T("Vérifier les températures en charge et le branchement électrique ; si cela se répète, tester une autre alimentation. Le journal de la boîte noire montre les relevés juste avant la coupure.", "Check temperatures under load and the power connections; if it happens again, test another power supply. The flight recorder log shows the readings just before the loss.");
                return true;

            // Les deux suivantes citent Windows : sans l'extrait conservé, la
            // phrase perdrait le fait qu'elle rapporte. On préfère alors ne rien
            // toucher.
            case "exhaustion" when a.Extract is { Length: > 0 } m:
                a.Title = Lang.T("Mémoire épuisée — Windows a manqué de mémoire virtuelle", "Memory exhausted — Windows ran out of virtual memory");
                a.Details = Lang.T("Windows signale l'épuisement de la mémoire virtuelle : ", "Windows reports virtual memory exhaustion: ") + m;
                a.Recommendation = Lang.T("Fermer le programme le plus gourmand cité ci-dessus. Si c'est la virtualisation (vmmem/WSL/Docker), lui fixer une limite via %USERPROFILE%\\.wslconfig ([wsl2] puis memory=8GB) et exécuter « wsl --shutdown ». Vérifier aussi que le fichier d'échange est géré automatiquement.", "Close the most demanding program named above. If it is virtualisation (vmmem/WSL/Docker), cap it through %USERPROFILE%\\.wslconfig ([wsl2] then memory=8GB) and run “wsl --shutdown”. Also check that the page file is managed automatically.");
                return true;

            case "disk_event" when a.Extract is { Length: > 0 } m:
                a.Title = Lang.T("Erreur disque signalée par Windows", "Disk error reported by Windows");
                a.Details = Lang.T("Windows vient d'enregistrer une erreur d'entrée/sortie sur un disque : ", "Windows has just recorded an input/output error on a drive: ") + m;
                a.Recommendation = Lang.T("Sauvegarder les données importantes sans attendre, vérifier la santé SMART du disque et ses câbles, mettre à jour le firmware du SSD.", "Back up the important data without delay, check the drive's SMART health and its cables, update the SSD firmware.");
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Santé d'un disque. Le modèle est dans l'identifiant de règle, et l'état
    /// se déduit du niveau : c'est ainsi que la règle a été émise, « crit » pour
    /// un disque défaillant et « warn » pour un disque à surveiller.
    /// </summary>
    private static bool LocalizeDisk(PreventiveAlert a)
    {
        var nom = a.RuleId[DiskPrefix.Length..];
        if (nom.Length == 0) return false;

        var etat = a.Level == "crit" ? DiskHealth.Failing : DiskHealth.Warning;

        a.Title = Lang.T($"Disque en mauvaise santé : {nom}", $"Drive in poor health: {nom}");
        a.Details = Lang.T($"Windows signale l'état « {etat.Label()} » pour ce disque. Une panne de disque fait perdre les données ET rend la machine non démarrable.", $"Windows reports the state “{etat.Label()}” for this drive. A drive failure loses the data AND makes the machine unbootable.");
        a.Recommendation = Lang.T("SAUVEGARDER immédiatement les données, puis prévoir le remplacement du disque. Vérifier le rapport SMART complet pour confirmation.", "BACK UP the data immediately, then plan to replace the drive. Check the full SMART report for confirmation.");
        return true;
    }

    /// <summary>Réécrit toute une liste ; les alertes non refabricables gardent leur texte.</summary>
    public static void LocalizeAll(IEnumerable<PreventiveAlert> alertes)
    {
        foreach (var a in alertes) Localize(a);
    }
}
