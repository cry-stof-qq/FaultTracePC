using FaultTracePC.Core.Collectors;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 15, lot 1b de la 1.10.0 : interroger winget pour les deux étendues et réunir.
/// Seules les fonctions pures sont testées : aucun test ne lance winget. Les sorties
/// réelles sont celles capturées le 26/10/2026 (voir <see cref="LecteurWingetTests"/>).
/// </summary>
public class WingetCollectorTests
{
    private static InterrogationWinget Machine => WingetCollector.Interpreter(EtendueWinget.Machine, 0, LecteurWingetTests.SortieMachine);
    private static InterrogationWinget Utilisateur => WingetCollector.Interpreter(EtendueWinget.Utilisateur, 0, LecteurWingetTests.SortieUtilisateur);

    [Theory]
    [InlineData(EtendueWinget.Machine, "machine")]
    [InlineData(EtendueWinget.Utilisateur, "user")]
    public void Les_arguments_listent_une_etendue_sans_jamais_rien_installer(EtendueWinget etendue, string scope)
    {
        var a = WingetCollector.Arguments(etendue);

        Assert.Equal(new[] { "upgrade", "--scope", scope, "--disable-interactivity", "--accept-source-agreements" }, a);
        foreach (var interdit in new[] { "--all", "-r", "--recurse", "--id", "--allow-reboot", "--force",
                                         "--uninstall-previous", "--ignore-security-hash", "--include-unknown", "--include-pinned" })
            Assert.DoesNotContain(interdit, a);
    }

    [Fact]
    public void Les_captures_reelles_sont_lues_avec_le_code_0()
    {
        Assert.Equal(EtatWinget.Lu, Machine.Etat);
        Assert.Equal(EtatWinget.Lu, Utilisateur.Etat);
        Assert.Null(Machine.Message);
        Assert.Equal("0x00000000", Machine.CodeHexadecimal);
    }

    [Fact]
    public void Les_deux_etendues_reelles_se_reunissent_par_identifiant_sans_doublon()
    {
        var bilan = WingetCollector.Bilan(new[] { Machine, Utilisateur });

        // 2 côté machine + 7 côté utilisateur, dont Microsoft.WSL des deux côtés.
        Assert.Equal(8, bilan.Logiciels.Count);
        Assert.True(bilan.Complet);
        Assert.Equal(0, bilan.LignesIllisibles);

        var wsl = Assert.Single(bilan.Logiciels, l => l.Identifiant == "Microsoft.WSL");
        Assert.Equal(EtenduesWinget.Machine | EtenduesWinget.Utilisateur, wsl.Etendues);
        // Le nom écrit pour être lu l'emporte sur « MicrosoftCorporationII.WindowsSubsystemForLinux ».
        Assert.Equal("Sous-système Windows pour Linux", wsl.Nom);

        Assert.Equal(EtenduesWinget.Machine, Assert.Single(bilan.Logiciels, l => l.Identifiant == "Mobatek.MobaXterm").Etendues);
        var notepad = Assert.Single(bilan.Logiciels, l => l.Identifiant == "Notepad++.Notepad++");
        Assert.Equal(EtenduesWinget.Utilisateur, notepad.Etendues);
        Assert.Equal("8.9.8.1", notepad.VersionDisponible);
    }

    [Fact]
    public void Un_code_de_sortie_non_nul_est_un_echec_avec_sa_raison_et_son_code_lisible()
    {
        var i = WingetCollector.Interpreter(EtendueWinget.Machine, unchecked((int)0x8A15000F),
            "\r\nÉchec lors de la recherche dans la source : winget\r\n");

        Assert.Equal(EtatWinget.Echec, i.Etat);
        Assert.Equal("0x8A15000F", i.CodeHexadecimal);
        Assert.Equal("Échec lors de la recherche dans la source : winget", i.Message);
    }

    [Fact]
    public void Un_echec_ne_donne_aucune_ligne_meme_s_il_contient_un_tableau()
    {
        var echec = WingetCollector.Interpreter(EtendueWinget.Machine, 1, LecteurWingetTests.SortieMachine);
        var bilan = WingetCollector.Bilan(new[] { echec, Utilisateur });

        Assert.Equal(7, bilan.Logiciels.Count);
        Assert.False(bilan.Complet); // la liste est partielle : le rapport devra le dire
    }

    [Fact]
    public void Code_0_sans_tableau_rien_de_signale_et_la_phrase_de_winget_est_gardee()
    {
        // Phrase d'exemple : le texte exact de winget sans mise à jour n'est pas encore capturé.
        var i = WingetCollector.Interpreter(EtendueWinget.Utilisateur, 0, "Aucune mise à jour disponible.\r\n");

        Assert.Equal(EtatWinget.RienDeSignale, i.Etat);
        Assert.Equal("Aucune mise à jour disponible.", i.Message);
    }

    [Fact]
    public void Une_sortie_vide_garde_le_message_d_erreur()
    {
        var i = WingetCollector.Interpreter(EtendueWinget.Utilisateur, 0, "", "message sur la sortie d'erreur\r\n");

        Assert.Equal(EtatWinget.RienDeSignale, i.Etat);
        Assert.Equal("message sur la sortie d'erreur", i.Message);
    }

    [Fact]
    public void Un_en_tete_inattendu_donne_tableau_illisible()
    {
        var sortie = "Nom   ID   Version   Disponible\r\n" + new string('-', 40) + "\r\nA   B.C   1   2\r\n";

        Assert.Equal(EtatWinget.TableauIllisible, WingetCollector.Interpreter(EtendueWinget.Machine, 0, sortie).Etat);
    }

    [Fact]
    public void Le_compte_systeme_ou_un_delai_depasse_ne_rendent_jamais_une_liste_complete()
    {
        var systeme = WingetCollector.Bilan(new[]
        {
            new InterrogationWinget(EtendueWinget.Machine, EtatWinget.NonVerifieSousSysteme, null, null, null),
            new InterrogationWinget(EtendueWinget.Utilisateur, EtatWinget.NonVerifieSousSysteme, null, null, null),
        });
        Assert.False(systeme.Complet);
        Assert.Empty(systeme.Logiciels);

        var delai = WingetCollector.Bilan(new[]
        {
            Machine,
            new InterrogationWinget(EtendueWinget.Utilisateur, EtatWinget.DelaiDepasse, null, null, null),
        });
        Assert.False(delai.Complet);
        Assert.Equal(2, delai.Logiciels.Count);
    }

    [Fact]
    public void Un_bilan_sans_interrogation_n_est_pas_complet()
    {
        Assert.False(WingetCollector.Bilan(Array.Empty<InterrogationWinget>()).Complet);
    }

    [Fact]
    public void Meme_identifiant_mais_versions_differentes_donne_deux_lignes()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Lue(EtendueWinget.Machine, new MiseAJourWinget("Outil", "Editeur.Outil", "1.0", "2.0", "winget", 0, false, false)),
            Lue(EtendueWinget.Utilisateur, new MiseAJourWinget("Outil", "Editeur.Outil", "1.5", "2.0", "winget", 0, false, false)),
        });

        Assert.Equal(2, bilan.Logiciels.Count);
    }

    [Fact]
    public void Un_identifiant_illisible_n_est_jamais_rapproche()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Lue(EtendueWinget.Machine, new MiseAJourWinget("Outil", null, "1.0", "2.0", "winget", 0, false, false)),
            Lue(EtendueWinget.Utilisateur, new MiseAJourWinget("Outil", null, "1.0", "2.0", "winget", 0, false, false)),
        });

        Assert.Equal(2, bilan.Logiciels.Count);
    }

    [Fact]
    public void L_identifiant_se_compare_sans_tenir_compte_des_majuscules()
    {
        var bilan = WingetCollector.Bilan(new[]
        {
            Lue(EtendueWinget.Machine, new MiseAJourWinget("Outil", "Editeur.Outil", "1.0", "2.0", "winget", 0, false, false)),
            Lue(EtendueWinget.Utilisateur, new MiseAJourWinget("Outil", "editeur.outil", "1.0", "2.0", "winget", 0, false, false)),
        });

        Assert.Equal(EtenduesWinget.Machine | EtenduesWinget.Utilisateur, Assert.Single(bilan.Logiciels).Etendues);
    }

    [Fact]
    public void Un_message_trop_long_est_raccourci()
    {
        var long_ = new string('x', WingetCollector.LongueurMessageMax + 50);
        var m = WingetCollector.PremiereLigne(long_)!;

        Assert.Equal(WingetCollector.LongueurMessageMax + 1, m.Length);
        Assert.EndsWith("…", m);
    }

    private static InterrogationWinget Lue(EtendueWinget etendue, params MiseAJourWinget[] lignes) =>
        new(etendue, EtatWinget.Lu, 0, null,
            new LectureWinget(EtatLectureWinget.Lu, lignes, 0, lignes.Length, 0));
}
