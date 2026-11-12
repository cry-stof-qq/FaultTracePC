namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 125 (go de l'auteur le 12/11/2026) — les périodes où le poste n'a pas tourné,
/// et pour chacune : comment la session précédente s'était terminée, et comment le poste
/// a redémarré.
///
/// POURQUOI. Deux postes du parc ne se rallumaient plus et n'ont redémarré qu'après des
/// heures ou des jours sans courant. Le journal de l'un, relu à la main le 12/11/2026,
/// disait tout ce que le rapport taisait : redémarrage programmé à 20:30, arrêt
/// programmé à 21:00 le 21/10 (shutdown.exe, arrêt propre), puis plus rien pendant
/// 21 jours et demi, puis un démarrage à froid le 12/11. Une panne survenue PENDANT que le poste
/// est éteint ne s'écrit nulle part ; mais la façon dont il s'est éteint juste avant, et
/// la durée pendant laquelle il n'a pas tourné, se lisent — et c'est ce qu'on cherche.
///
/// Placé dans Core pour être testé à partir de relevés réels.
/// </summary>
public static class ArretsEtDemarrages
{
    /// <summary>Événements rattachés à un démarrage s'ils le suivent de moins de ce délai.</summary>
    private static readonly TimeSpan PresDuDemarrage = TimeSpan.FromMinutes(5);

    public static List<PeriodeEteinte> Periodes(IEnumerable<EvenementAlimentation> evenements)
    {
        var ev = evenements.OrderBy(e => e.Heure).ToList();
        var periodes = new List<PeriodeEteinte>();
        int debutSession = 0;

        for (int i = 0; i < ev.Count; i++)
        {
            var e = ev[i];

            // Veille : 42 (entrée) puis 107 (sortie), sans nouveau démarrage de Windows.
            if (Est(e, "Kernel-Power", 107))
            {
                var entree = ev.Take(i).LastOrDefault(x => Est(x, "Kernel-Power", 42) && x.Heure >= ev[debutSession].Heure);
                if (entree is not null)
                    periodes.Add(new PeriodeEteinte
                    {
                        Debut = entree.Heure, Fin = e.Heure, FinDeSession = FinDeSession.Veille, Reprise = Reprise.SortieDeVeille,
                    });
                continue;
            }

            if (!Est(e, "Kernel-General", 12)) continue;

            // La session qui précède ce démarrage : ses événements depuis le démarrage d'avant.
            var avant = ev.Skip(debutSession).Take(i - debutSession).ToList();
            var arret = avant.LastOrDefault(x => Est(x, "Kernel-General", 13));
            var demande = avant.LastOrDefault(x => Est(x, "User32", 1074));
            var apres = ev.Skip(i + 1).TakeWhile(x => x.Heure - e.Heure <= PresDuDemarrage && !Est(x, "Kernel-General", 12)).ToList();
            bool inattendu = apres.Any(x => Est(x, "Kernel-Power", 41) || Est(x, "EventLog", 6008));
            var type = apres.FirstOrDefault(x => Est(x, "Kernel-Boot", 27))?.TypeDemarrage;

            periodes.Add(new PeriodeEteinte
            {
                Debut = inattendu ? avant.LastOrDefault()?.Heure : arret?.Heure,
                Fin = e.Heure,
                FinDeSession = inattendu ? FinDeSession.Inattendue
                             : arret is null ? FinDeSession.Inconnue
                             : demande is null ? FinDeSession.ArretSansDemandeLue
                             : FinDeSession.ArretDemande,
                Processus = inattendu ? null : demande?.Processus,
                Action = inattendu ? null : demande?.Action,
                Compte = inattendu ? null : demande?.Compte,
                Reprise = type switch
                {
                    0 => Reprise.DemarrageAFroid,
                    1 => Reprise.DemarrageRapide,
                    2 => Reprise.SortieDeVeilleProlongee,
                    _ => Reprise.Inconnue,
                },
            });
            debutSession = i;
        }
        return periodes;
    }

    /// <summary>À partir de cette durée sans démarrage, la période fait l'objet d'une conclusion (un week-end n'en fait pas).</summary>
    public static readonly TimeSpan PeriodeLongue = TimeSpan.FromDays(5);

    /// <summary>Durée lisible : « 22 j 11 h », « 14 h 05 », « 18 s ».</summary>
    public static string Duree(TimeSpan d)
    {
        if (d < TimeSpan.FromMinutes(1)) return $"{(int)d.TotalSeconds} s";
        if (d < TimeSpan.FromHours(1)) return $"{(int)d.TotalMinutes} min";
        if (d < TimeSpan.FromDays(2)) return Lang.T($"{(int)d.TotalHours} h {d.Minutes:00}", $"{(int)d.TotalHours}h{d.Minutes:00}");
        return Lang.T($"{(int)d.TotalDays} j {d.Hours} h", $"{(int)d.TotalDays} d {d.Hours} h");
    }

    /// <summary>Comment la session s'est terminée, en clair.</summary>
    public static string FinLisible(PeriodeEteinte p)
    {
        var programme = Programme(p.Processus);
        var par = (programme, p.Compte) switch
        {
            ({ } prog, { Length: > 0 } compte) => Lang.T($" par {prog}, au nom de {compte}", $" by {prog}, on behalf of {compte}"),
            ({ } prog, _) => Lang.T($" par {prog}", $" by {prog}"),
            _ => "",
        };
        var action = string.IsNullOrWhiteSpace(p.Action) ? "" : Lang.T($" (« {p.Action!.TrimEnd('.')} »)", $" (“{p.Action!.TrimEnd('.')}”)");
        return p.FinDeSession switch
        {
            FinDeSession.ArretDemande => Lang.T($"arrêt propre, demandé{par}{action}", $"clean shutdown, requested{par}{action}"),
            FinDeSession.ArretSansDemandeLue => Lang.T("arrêt propre", "clean shutdown"),
            FinDeSession.Inattendue => Lang.T("arrêt INATTENDU : Windows a redémarré sans s'être arrêté proprement", "UNEXPECTED stop: Windows restarted without a clean shutdown"),
            FinDeSession.Veille => Lang.T("mise en veille", "sleep"),
            _ => Lang.T("inconnue (avant le début du journal lu)", "unknown (before the start of the log read)"),
        };
    }

    /// <summary>Comment le poste a repris, en clair.</summary>
    public static string RepriseLisible(PeriodeEteinte p) => p.Reprise switch
    {
        Analysis.Reprise.DemarrageAFroid => Lang.T("démarrage à froid", "cold boot"),
        Analysis.Reprise.DemarrageRapide => Lang.T("« démarrage rapide » (reprise d'un arrêt hybride)", "“fast startup” (resume from a hybrid shutdown)"),
        Analysis.Reprise.SortieDeVeilleProlongee => Lang.T("sortie de veille prolongée", "resume from hibernation"),
        Analysis.Reprise.SortieDeVeille => Lang.T("sortie de veille", "resume from sleep"),
        _ => Lang.T("non précisé par Windows", "not stated by Windows"),
    };

    /// <summary>« C:\Windows\System32\shutdown.exe (POSTE) » devient « shutdown.exe ».</summary>
    internal static string? Programme(string? processus)
    {
        if (string.IsNullOrWhiteSpace(processus)) return null;
        var chemin = processus.Split(" (", 2)[0].Trim();
        var nom = chemin[(chemin.LastIndexOfAny(['\\', '/']) + 1)..];
        return nom.Length > 0 ? nom : chemin;
    }

    private static bool Est(EvenementAlimentation e, string fournisseur, int id) =>
        e.Id == id && e.Source.EndsWith(fournisseur, StringComparison.OrdinalIgnoreCase);
}

public enum FinDeSession
{
    /// <summary>Arrêt de Windows enregistré, et la demande d'arrêt (programme, compte) lue.</summary>
    ArretDemande,
    /// <summary>Arrêt de Windows enregistré, sans demande lue (bouton Démarrer, mise à jour…).</summary>
    ArretSansDemandeLue,
    /// <summary>Redémarrage sans arrêt propre (Kernel-Power 41 ou EventLog 6008 au démarrage).</summary>
    Inattendue,
    /// <summary>Mise en veille.</summary>
    Veille,
    /// <summary>Rien dans la période lue (début du journal).</summary>
    Inconnue,
}

public enum Reprise { DemarrageAFroid, DemarrageRapide, SortieDeVeilleProlongee, SortieDeVeille, Inconnue }

public sealed class PeriodeEteinte
{
    /// <summary>Dernier signe de vie avant la période ; null si inconnu.</summary>
    public DateTime? Debut { get; set; }
    public DateTime Fin { get; set; }
    public TimeSpan? Duree => Debut is { } d ? Fin - d : null;
    public FinDeSession FinDeSession { get; set; }
    public string? Processus { get; set; }
    public string? Action { get; set; }
    public string? Compte { get; set; }
    public Reprise Reprise { get; set; }
}
