using System.Security.AccessControl;
using System.Security.Principal;
using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 14 : remote.json porte le jeton de la machine, et les permissions par
/// défaut de ProgramData laissent le groupe Utilisateurs le lire.
///
/// Ces tests ne vérifient pas qu'on a « appelé la bonne méthode » : ils relisent
/// la liste de contrôle d'accès réellement posée sur un fichier réel.
/// </summary>
public class ProtectionFichierTests
{
    private static readonly SecurityIdentifier Systeme = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Admins = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    private static string FichierTemporaire()
    {
        var chemin = Path.Combine(Path.GetTempPath(), "ftpc-acl-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(chemin, "{}");
        return chemin;
    }

    /// <summary>
    /// Se redonner l'accès avant d'effacer : le test tourne dans une session
    /// ordinaire, et il vient justement de se retirer le fichier.
    /// </summary>
    private static void Effacer(string chemin)
    {
        try
        {
            var fichier = new FileInfo(chemin);
            if (!fichier.Exists) return;
            var acces = fichier.GetAccessControl();
            acces.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            fichier.SetAccessControl(acces);
            File.Delete(chemin);
        }
        catch { /* un fichier oublié dans %TEMP% ne justifie pas un test rouge */ }
    }

    private static List<FileSystemAccessRule> Regles(string chemin) =>
        new FileInfo(chemin).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToList();

    [Fact]
    public void Un_fichier_protege_n_herite_plus_de_rien()
    {
        var chemin = FichierTemporaire();
        try
        {
            Assert.True(FileProtection.RestrictToSystemAndAdministrators(chemin, out var erreur), erreur);

            // L'héritage est le point qui compte : une seule règle héritée du dossier
            // parent rouvrirait le fichier à tout le groupe Utilisateurs.
            Assert.DoesNotContain(Regles(chemin), r => r.IsInherited);
        }
        finally { Effacer(chemin); }
    }

    [Fact]
    public void Seuls_SYSTEM_et_les_administrateurs_gardent_l_acces()
    {
        var chemin = FichierTemporaire();
        try
        {
            FileProtection.RestrictToSystemAndAdministrators(chemin, out _);

            var identites = Regles(chemin).Select(r => r.IdentityReference).ToList();

            Assert.Equal(2, identites.Count);
            Assert.Contains(Systeme, identites);
            Assert.Contains(Admins, identites);
            Assert.All(Regles(chemin), r =>
            {
                Assert.Equal(AccessControlType.Allow, r.AccessControlType);
                Assert.Equal(FileSystemRights.FullControl, r.FileSystemRights & FileSystemRights.FullControl);
            });
            Assert.True(FileProtection.IsRestricted(chemin));
        }
        finally { Effacer(chemin); }
    }

    [Fact]
    public void Un_fichier_ordinaire_n_est_pas_declare_protege()
    {
        // Le contrôle doit dire NON par défaut : un test qui ne sait dire que « oui »
        // ne prouve rien.
        var chemin = FichierTemporaire();
        try { Assert.False(FileProtection.IsRestricted(chemin)); }
        finally { Effacer(chemin); }
    }

    [Fact]
    public void Un_fichier_absent_repond_faux_et_explique()
    {
        var chemin = Path.Combine(Path.GetTempPath(), "ftpc-absent-" + Guid.NewGuid().ToString("N"));

        Assert.False(FileProtection.RestrictToSystemAndAdministrators(chemin, out var erreur));
        Assert.False(string.IsNullOrWhiteSpace(erreur));
        Assert.False(FileProtection.IsRestricted(chemin));
    }
}
