using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 88 — le compte rendu d'un réveil depuis la console. Séquences de lignes
/// écrites par le script (`Write-Json`), reprises de journaux réels : poste déjà
/// allumé, poste éteint réveillé (16/11/2026, six postes d'une salle réveillés depuis
/// la console), adresse MAC introuvable, signal sans réponse.
/// </summary>
[Collection("Langue")]
public class ReveilTests
{
    private static string L(string etape, string etat, string detail = "") =>
        detail.Length == 0
            ? $$"""{"horodatage":"2026-11-16T08:38:00+01:00","poste":"POSTE-01","etape":"{{etape}}","etat":"{{etat}}"}"""
            : $$"""{"horodatage":"2026-11-16T08:38:00+01:00","poste":"POSTE-01","etape":"{{etape}}","etat":"{{etat}}","detail":"{{detail}}"}""";

    private static (string Code, string Phrase) Verdict(params string[] lignes)
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.French);
            return ParkDeployment.VerdictDuReveil(ParkDeployment.LireLignes(lignes), "POSTE-01");
        }
        finally { Lang.Apply(initial); }
    }

    [Fact]
    public void Un_poste_qui_repond_est_deja_allume()
    {
        var (code, phrase) = Verdict(L("compte", "ok"), L("reponse", "ok"));
        Assert.Equal("deja", code);
        Assert.Equal("déjà allumé", phrase);
    }

    [Fact]
    public void Un_poste_reveille_dit_d_ou_vient_son_adresse_MAC()
    {
        var (code, phrase) = Verdict(L("compte", "ok"), L("reponse", "echec", "ni ping, ni port 445"),
                                     L("mac", "ok", "dhcp"), L("reveil", "ok", "réveillé puis joignable"));
        Assert.Equal("reveille", code);
        Assert.Equal("réveillé (adresse MAC : serveur DHCP)", phrase);
    }

    [Fact]
    public void Un_journal_ecrit_par_un_script_plus_ancien_sans_la_source_reste_lisible()
    {
        var (code, phrase) = Verdict(L("reponse", "echec"), L("reveil", "ok", "réveillé puis joignable"));
        Assert.Equal("reveille", code);
        Assert.Equal("réveillé", phrase);
    }

    [Fact]
    public void Une_adresse_MAC_introuvable_nomme_les_sources_qui_ont_manque()
    {
        var (code, phrase) = Verdict(L("reponse", "echec"), L("mac", "info", "dhcp:sans-bail,fichier:sans-ligne,arp:rien"),
                                     L("reveil", "echec", "adresse MAC inconnue"));
        Assert.Equal("echec", code);
        Assert.Equal("pas réveillé : adresse MAC introuvable (aucun bail DHCP pour ce poste ; poste absent du fichier d'adresses ; rien dans le cache ARP)", phrase);
    }

    [Fact]
    public void Un_signal_sans_reponse_oriente_vers_le_BIOS_ou_le_reseau()
    {
        var (code, phrase) = Verdict(L("reponse", "echec"), L("mac", "ok", "fichier"), L("reveil", "echec", "pas de réponse après 90 s"));
        Assert.Equal("echec", code);
        Assert.Contains("BIOS", phrase);
        Assert.Contains("routeurs", phrase);
    }

    [Fact]
    public void Un_nom_inconnu_de_l_annuaire_est_dit_tel_quel()
    {
        Assert.Equal("inconnu", Verdict(L("compte", "echec", "inconnu de l'annuaire")).Code);
    }

    [Fact]
    public void Un_poste_sans_trace_n_est_pas_dit_reveille()
    {
        Assert.Equal("absent", Verdict().Code);
    }

    [Fact]
    public void La_ligne_de_la_source_n_est_jamais_un_echec_decisif()
    {
        var lecture = ParkDeployment.LireLignes([L("reponse", "echec"), L("mac", "info", "dhcp:non-renseigne"), L("reveil", "echec", "adresse MAC inconnue")]);
        var echec = Assert.Single(lecture.EchecsDecisifs("POSTE-01"), e => e.Etape != "reponse");
        Assert.Equal(ParkDeployment.EtapeReveil, echec.Etape);
    }

    [Fact]
    public void En_anglais_la_phrase_est_en_anglais()
    {
        var initial = Lang.Current;
        try
        {
            Lang.Apply(AppLanguage.English);
            var (_, phrase) = ParkDeployment.VerdictDuReveil(
                ParkDeployment.LireLignes([L("reponse", "echec"), L("mac", "ok", "arp"), L("reveil", "ok")]), "POSTE-01");
            Assert.Equal("woken (MAC address: ARP cache)", phrase);
        }
        finally { Lang.Apply(initial); }
    }
}
