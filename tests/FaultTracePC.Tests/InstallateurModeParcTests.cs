using System.Xml.Linq;
using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Point 79, lot 2 : l'installateur et le logiciel doivent parler des mêmes
/// valeurs de registre. Une faute de frappe d'un côté ne se verrait qu'à
/// l'installation — console cachée à tort, ou jamais cachée. Ces tests lisent
/// installer/FaultTracePC.wxs et le confrontent à <see cref="ModeParc"/>.
/// </summary>
public class InstallateurModeParcTests
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";

    private static XDocument Paquet()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FaultTracePC.slnx")))
                return XDocument.Load(Path.Combine(dir.FullName, "installer", "FaultTracePC.wxs"));
        Assert.Fail("installer/FaultTracePC.wxs introuvable depuis " + AppContext.BaseDirectory);
        return new XDocument();
    }

    private static XElement Fonction(XDocument d, string id) =>
        d.Descendants(Wix + "Feature").Single(f => (string?)f.Attribute("Id") == id);

    private static XElement ValeurDuComposant(XDocument d, string composant) =>
        d.Descendants(Wix + "Component").Single(c => (string?)c.Attribute("Id") == composant)
         .Element(Wix + "RegistryValue")!;

    [Theory]
    [InlineData("ParkFeatureManagedRegistry", ModeParc.ValeurGeree)]
    [InlineData("ParkFeatureRegistry", ModeParc.ValeurCase)]
    public void Les_valeurs_posees_sont_celles_que_lit_le_logiciel(string composant, string nom)
    {
        var v = ValeurDuComposant(Paquet(), composant);
        Assert.Equal("HKLM", (string?)v.Attribute("Root"));
        Assert.Equal(ModeParc.CleRegistre, (string?)v.Attribute("Key"));
        Assert.Equal(nom, (string?)v.Attribute("Name"));
        Assert.Equal("integer", (string?)v.Attribute("Type"));
        Assert.Equal("1", (string?)v.Attribute("Value"));
    }

    [Fact]
    public void La_valeur_geree_est_posee_par_la_fonctionnalite_principale()
    {
        var main = Fonction(Paquet(), "Main");
        Assert.Contains(main.Elements(Wix + "ComponentRef"), c => (string?)c.Attribute("Id") == "ParkFeatureManagedRegistry");
    }

    [Fact]
    public void Le_mode_parc_est_absent_par_defaut()
    {
        var parc = Fonction(Paquet(), "ParkFeature");
        Assert.True(int.Parse((string)parc.Attribute("Level")!) > 1);
        Assert.Contains(parc.Elements(Wix + "ComponentRef"), c => (string?)c.Attribute("Id") == "ParkFeatureRegistry");
    }

    [Fact]
    public void La_mise_a_jour_d_une_version_anterieure_coche_la_case()
    {
        // Décision D2 : seulement quand l'installateur précédent ne connaissait pas la case.
        var d = Paquet();
        var niveau = Fonction(d, "ParkFeature").Element(Wix + "Level")!;
        Assert.Equal("1", (string?)niveau.Attribute("Value"));
        var condition = (string?)niveau.Attribute("Condition") ?? "";
        Assert.Contains("WIX_UPGRADE_DETECTED", condition);
        Assert.Contains("NOT PARKFEATUREMANAGED", condition);

        // La propriété testée lit bien la valeur posée par Main.
        var recherche = d.Descendants(Wix + "Property")
            .Single(p => (string?)p.Attribute("Id") == "PARKFEATUREMANAGED")
            .Element(Wix + "RegistrySearch")!;
        Assert.Equal(ModeParc.CleRegistre, (string?)recherche.Attribute("Key"));
        Assert.Equal(ModeParc.ValeurGeree, (string?)recherche.Attribute("Name"));
        Assert.Equal("always64", (string?)recherche.Attribute("Bitness"));
    }
}
