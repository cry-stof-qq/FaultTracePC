using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 64, lot B. Ce qui se teste ici, c'est la LECTURE du canal — la partie qui
/// ne demande ni PowerShell, ni réseau, ni poste distant. C'est aussi la partie où
/// une erreur serait invisible : un journal mal lu ne lève pas, il rend simplement
/// un décompte faux.
/// </summary>
[Collection("Langue")]
public class JournalDeDeploiementTests
{
    private const string LigneComplete =
        """{"horodatage":"2026-09-19T08:12:03+02:00","poste":"POSTE-01","etape":"winrm","etat":"ok","detail":"5985 repond","adresse":"10.0.0.12","code":0}""";

    [Fact]
    public void Une_ligne_bien_formee_se_lit_en_entier()
    {
        var lecture = ParkDeployment.LireLignes([LigneComplete]);

        var l = Assert.Single(lecture.Lignes);
        Assert.Equal("POSTE-01", l.Poste);
        Assert.Equal(ParkDeployment.EtapeGestionADistance, l.Etape);
        Assert.Equal(ParkDeployment.EtatOk, l.Etat);
        Assert.Equal("5985 repond", l.Detail);
        Assert.Equal("10.0.0.12", l.Adresse);
        Assert.Equal(0, l.Code);
        Assert.Equal(2026, l.Horodatage!.Value.Year);
        Assert.False(l.EstEchec);
        Assert.Equal(0, lecture.LignesIllisibles);
    }

    [Fact]
    public void La_casse_des_noms_de_champs_ne_compte_pas()
    {
        // PowerShell écrit « Poste » aussi volontiers que « poste » selon la façon
        // dont l'objet a été construit. S'y fier serait un piège silencieux.
        var lecture = ParkDeployment.LireLignes(
            ["""{"Poste":"POSTE-02","Etape":"compte","Etat":"ok"}"""]);

        Assert.Equal("POSTE-02", Assert.Single(lecture.Lignes).Poste);
    }

    [Fact]
    public void Le_nom_du_poste_est_normalise_comme_partout_ailleurs()
    {
        // Même clé que ParkInventory : sans ça, le journal et l'inventaire
        // parleraient du même poste sans se reconnaître.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"poste-03.exemple.fr","etape":"compte","etat":"ok"}"""]);

        Assert.Equal("POSTE-03", Assert.Single(lecture.Lignes).Poste);
    }

    [Fact]
    public void Une_ligne_vide_n_est_pas_une_ligne()
    {
        var lecture = ParkDeployment.LireLignes(["", "   ", LigneComplete, ""]);

        Assert.Single(lecture.Lignes);
        Assert.Equal(0, lecture.LignesIllisibles);
        Assert.False(lecture.DerniereLigneIncomplete);
    }

    [Fact]
    public void Une_ligne_illisible_est_comptee_jamais_effacee()
    {
        // LE POINT CENTRAL DE CE FICHIER. Un journal dont une ligne sur trois n'a
        // pas pu être lue n'est pas un journal de deux lignes.
        var lecture = ParkDeployment.LireLignes(["ceci n'est pas du JSON", LigneComplete, LigneComplete]);

        Assert.Equal(2, lecture.Lignes.Count);
        Assert.Equal(1, lecture.LignesIllisibles);
        Assert.NotEmpty(lecture.Notes);
    }

    [Fact]
    public void Une_derniere_ligne_tronquee_n_est_pas_un_defaut()
    {
        // Le script écrit pendant qu'on relit : la dernière ligne peut être à
        // moitié écrite. La compter comme illisible ferait voir un défaut là où il
        // n'y a qu'une lecture prise en cours de route.
        var lecture = ParkDeployment.LireLignes([LigneComplete, """{"poste":"POSTE-04","eta"""]);

        Assert.Single(lecture.Lignes);
        Assert.Equal(0, lecture.LignesIllisibles);
        Assert.True(lecture.DerniereLigneIncomplete);
        Assert.Empty(lecture.Notes);
    }

    [Fact]
    public void Une_ligne_sans_poste_ne_se_rattache_a_personne()
    {
        // ET ELLE RESTE UN DÉFAUT MÊME EN DERNIÈRE POSITION. C'est ici que la
        // première version s'est trompée : elle traitait toute dernière ligne
        // inexploitable comme une écriture en cours, si bien qu'un journal d'une
        // seule ligne fautive se lisait « lecture prise en cours de route ».
        // Un JSON INCOMPLET peut être une écriture en cours ; un JSON COMPLET mais
        // incohérent est un défaut, où qu'il se trouve.
        var lecture = ParkDeployment.LireLignes(["""{"etape":"copie","etat":"ok"}"""]);

        Assert.Empty(lecture.Lignes);
        Assert.Equal(1, lecture.LignesIllisibles);
        Assert.False(lecture.DerniereLigneIncomplete);
    }

    [Fact]
    public void Les_accents_echappes_par_le_script_se_relisent_intacts()
    {
        // Le script écrit son journal en ASCII PUR : tout caractère accentué part en
        // \uXXXX. C'est ce qui rend le fichier lisible quelle que soit l'idée que le
        // lecteur se fait de l'encodage — y compris « Get-Content » sans -Encoding,
        // qui lit en page de code ANSI et affichait « vÃ©rifier » le 19/09/2026.
        //
        // Littéral brut : le compilateur C# n'y touche pas, c'est bien la séquence de
        // six caractères que le script produit qui est donnée à lire.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-09","etape":"reveil","etat":"ignore","detail":"mode v\u00e9rifier seulement"}"""]);

        Assert.Equal("mode vérifier seulement", Assert.Single(lecture.Lignes).Detail);
    }

    [Fact]
    public void Un_code_ecrit_en_texte_reste_un_nombre()
    {
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-05","etape":"installation","etat":"echec","code":"1603"}"""]);

        Assert.Equal(1603, Assert.Single(lecture.Lignes).Code);
    }

    [Fact]
    public void Un_champ_inconnu_n_empeche_pas_la_lecture()
    {
        // Le script évoluera. Une version qui ajoute un champ ne doit pas rendre
        // ses journaux illisibles par une console plus ancienne.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-06","etape":"compte","etat":"ok","duree_ms":412}"""]);

        Assert.Equal("POSTE-06", Assert.Single(lecture.Lignes).Poste);
    }

    [Fact]
    public void L_echec_se_lit_sur_l_etat_et_nulle_part_ailleurs()
    {
        // 3010 veut dire « installé, redémarrage demandé ». Déduire l'échec du code
        // ferait compter en échec un poste correctement installé.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-07","etape":"installation","etat":"ok","code":3010}""",
             """{"poste":"POSTE-08","etape":"installation","etat":"echec","code":0}"""]);

        Assert.False(lecture.Lignes[0].EstEchec);
        Assert.True(lecture.Lignes[1].EstEchec);
        Assert.Equal(["POSTE-08"], lecture.PostesEnEchec);
        Assert.Equal(["POSTE-07", "POSTE-08"], lecture.Postes);
    }

    [Fact]
    public void Un_poste_eteint_puis_reveille_n_est_pas_un_poste_en_echec()
    {
        // LE CAS NORMAL D'UN DÉPLOIEMENT SUR MACHINE ÉTEINTE, et le défaut constaté
        // le 21/09/2026 : trois postes réveillés, installés et joignables étaient
        // déclarés « échec : pas de réponse réseau » à cause de la seule ligne que
        // le script écrit AVANT de les réveiller.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-09","etape":"compte","etat":"ok"}""",
             """{"poste":"POSTE-09","etape":"reponse","etat":"echec","detail":"ni ping, ni port 445"}""",
             """{"poste":"POSTE-09","etape":"reveil","etat":"ok","detail":"reveille puis joignable"}""",
             """{"poste":"POSTE-09","etape":"partage","etat":"ok"}""",
             """{"poste":"POSTE-09","etape":"installation","etat":"ok","code":0}""",
             """{"poste":"POSTE-09","etape":"parc","etat":"ok"}"""]);

        Assert.Empty(lecture.EchecsDecisifs("POSTE-09"));
        Assert.Empty(lecture.PostesEnEchec);
    }

    [Fact]
    public void Une_etape_reecrite_ne_vaut_que_par_sa_derniere_ligne()
    {
        // La gestion à distance muette : le script écrit l'échec, démarre le service
        // à distance, puis réécrit la MÊME étape en succès.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-10","etape":"winrm","etat":"echec","detail":"aucune reponse sur 5985"}""",
             """{"poste":"POSTE-10","etape":"winrm","etat":"ok","detail":"service demarre a distance"}"""]);

        Assert.Empty(lecture.EchecsDecisifs("POSTE-10"));
        Assert.Empty(lecture.PostesEnEchec);
    }

    [Fact]
    public void Un_reveil_qui_echoue_reste_un_echec()
    {
        // La limite de la règle : on annule l'échec de « reponse » PARCE QUE le
        // réveil a réussi. S'il échoue, les deux lignes disent la même chose et le
        // poste est bien en échec.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-11","etape":"reponse","etat":"echec","detail":"ni ping, ni port 445"}""",
             """{"poste":"POSTE-11","etape":"reveil","etat":"echec","detail":"adresse MAC inconnue"}"""]);

        Assert.Equal(2, lecture.EchecsDecisifs("POSTE-11").Count);
        Assert.Equal(["POSTE-11"], lecture.PostesEnEchec);
    }

    [Fact]
    public void Une_installation_refusee_reste_un_echec_meme_apres_un_reveil_reussi()
    {
        // Le réveil n'absout que « reponse ». Il ne rend pas bon ce qui a échoué
        // après lui — sinon un poste allumé pour rien passerait pour installé.
        var lecture = ParkDeployment.LireLignes(
            ["""{"poste":"POSTE-12","etape":"reponse","etat":"echec"}""",
             """{"poste":"POSTE-12","etape":"reveil","etat":"ok"}""",
             """{"poste":"POSTE-12","etape":"installation","etat":"echec","code":1603}"""]);

        var echecs = lecture.EchecsDecisifs("POSTE-12");
        Assert.Equal("installation", Assert.Single(echecs).Etape);
        Assert.Equal(["POSTE-12"], lecture.PostesEnEchec);
    }

    [Fact]
    public void Un_fichier_absent_n_est_pas_un_journal_vide()
    {
        // L'un veut dire « pas encore commencé », l'autre « commencé, rien écrit ».
        var absent = ParkDeployment.Lire(Path.Combine(Path.GetTempPath(), "ftpc_absent_" + Guid.NewGuid().ToString("N") + ".jsonl"));

        Assert.True(absent.FichierAbsent);
        Assert.Empty(absent.Lignes);
        Assert.False(ParkDeployment.LireLignes([]).FichierAbsent);
    }

    [Fact]
    public void Le_mode_verifier_seulement_ne_connait_que_des_etapes_inoffensives()
    {
        // GARDE-FOU. Si quelqu'un ajoute un jour « copie » ou « installation » à
        // cette liste, ce test tombe — et c'est tout ce qu'on lui demande.
        Assert.True(ParkDeployment.EstInoffensive(ParkDeployment.EtapeCompte));
        Assert.True(ParkDeployment.EstInoffensive(ParkDeployment.EtapeReponse));
        Assert.True(ParkDeployment.EstInoffensive(ParkDeployment.EtapePartageAdmin));
        Assert.True(ParkDeployment.EstInoffensive(ParkDeployment.EtapeGestionADistance));

        // LE RÉVEIL RÉSEAU N'EN EST PAS. Il n'écrit rien SUR la machine, mais il
        // l'allume : trente postes qui démarrent parce qu'on a cliqué sur
        // « vérifier » est un effet que personne n'a demandé.
        Assert.False(ParkDeployment.EstInoffensive(ParkDeployment.EtapeReveil));
        Assert.False(ParkDeployment.EstInoffensive(ParkDeployment.EtapeCopie));
        Assert.False(ParkDeployment.EstInoffensive(ParkDeployment.EtapeInstallation));
        Assert.False(ParkDeployment.EstInoffensive(ParkDeployment.EtapeParc));
        Assert.False(ParkDeployment.EstInoffensive(null));
    }
}
