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
/// 22 jours, puis un démarrage à froid le 12/11. Une panne survenue PENDANT que le poste
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
