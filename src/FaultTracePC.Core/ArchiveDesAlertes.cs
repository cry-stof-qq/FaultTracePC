using System.Text.Json;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 43 — les alertes préventives, conservées PAR LA CONSOLE.
///
/// LE DÉFAUT QU'ELLE CORRIGE
/// Les alertes vivent sur la machine, dans son journal de vol. Un poste réimagé —
/// opération routinière en établissement, souvent pendant les vacances — repart
/// avec un journal vide. La console les collecte à chaque actualisation mais ne les
/// conservait pas : elle les affichait, puis les oubliait.
///
/// Conséquence concrète : on perd la preuve qu'une machine chauffait depuis six
/// mois, précisément au moment où elle servirait à justifier son remplacement.
///
/// RIEN N'EST JAMAIS EFFACÉ, ET C'EST UNE DÉCISION
/// Une alerte pèse quelques centaines d'octets ; même un poste bavard produira
/// moins d'un mégaoctet par an. Effacer automatiquement au bout de N jours, ce
/// serait risquer de supprimer exactement la preuve qu'on cherchait — et la
/// supprimer sans que personne l'ait demandé. Un fichier se supprime à la main,
/// quand on a décidé de le supprimer.
///
/// UN FICHIER PAR POSTE
/// Retirer un poste du parc revient à supprimer son fichier. Et afficher
/// l'historique d'un seul poste ne demande pas de relire des années d'alertes de
/// tout l'établissement.
///
/// LE FICHIER EST EN ASCII PUR, sans qu'on ait rien à faire : le sérialiseur de
/// .NET échappe par défaut tout caractère non ASCII en <c>\\uXXXX</c>. La question
/// de l'encodage ne se pose donc pas, comme pour le journal de déploiement.
/// </summary>
public static class ArchiveDesAlertes
{
    public static string Dossier => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "Alertes");

    /// <summary>
    /// Le fichier d'un poste, ou une chaîne vide si son nom n'en est pas un.
    ///
    /// LE NOM EST CONTRÔLÉ AVANT DE DEVENIR UN CHEMIN. Un nom venu d'une source
    /// extérieure qui contiendrait « .. » ou une barre oblique désignerait un
    /// fichier ailleurs sur le disque. Le contrôle est celui de
    /// <see cref="ListeDePostes.EstNomValide"/> — lettres, chiffres, traits d'union
    /// — et il ferme la question au lieu de la traiter au cas par cas.
    /// </summary>
    public static string FichierDe(string? poste)
    {
        var nom = ParkInventory.NomWindows(poste);
        return ListeDePostes.EstNomValide(nom) ? Path.Combine(Dossier, nom + ".jsonl") : "";
    }

    /// <summary>
    /// Ajoute au fichier du poste les alertes qu'il ne contient pas encore, et rend
    /// le nombre de NOUVELLES.
    ///
    /// LE DOUBLON EST LA RÈGLE, PAS L'EXCEPTION. La console relit sept jours
    /// d'alertes à chaque actualisation : sans dédoublonnage, une alerte du lundi
    /// serait archivée une fois par actualisation jusqu'au lundi suivant. Deux
    /// alertes sont la même quand elles portent le même instant et la même règle.
    /// </summary>
    public static int Archiver(string? poste, IEnumerable<PreventiveAlert>? alertes, List<string>? notes = null)
    {
        var fichier = FichierDe(poste);
        if (fichier.Length == 0)
        {
            notes?.Add(Lang.T($"Nom de poste inattendu, rien n'est archivé : « {poste} ».",
                              $"Unexpected computer name, nothing archived: “{poste}”."));
            return 0;
        }

        var aEcrire = new List<string>();
        try
        {
            var connues = new HashSet<string>(LireLesLignes(fichier).Select(Cle), StringComparer.Ordinal);

            foreach (var a in alertes ?? [])
            {
                if (a is null) continue;
                if (!connues.Add(Cle(a))) continue;
                aEcrire.Add(JsonSerializer.Serialize(a));
            }

            if (aEcrire.Count == 0) return 0;

            Directory.CreateDirectory(Dossier);
            File.AppendAllLines(fichier, aEcrire, new System.Text.UTF8Encoding(false));
            return aEcrire.Count;
        }
        catch (Exception ex)
        {
            // Ne pas pouvoir archiver n'interrompt pas la surveillance : la console
            // continue d'afficher ce qu'elle vient de lire. Mais on le dit.
            notes?.Add(Lang.T($"Archivage des alertes impossible pour {poste} : {ex.Message}",
                              $"Could not archive alerts for {poste}: {ex.Message}"));
            return 0;
        }
    }

    /// <summary>
    /// Les alertes archivées d'un poste sur les <paramref name="jours"/> derniers
    /// jours, la plus récente d'abord. <paramref name="jours"/> à zéro ou négatif
    /// rend tout l'historique.
    /// </summary>
    public static List<PreventiveAlert> Lire(string? poste, int jours = 0)
    {
        var fichier = FichierDe(poste);
        if (fichier.Length == 0) return [];

        var depuis = jours > 0 ? DateTime.Now.AddDays(-jours) : DateTime.MinValue;

        return LireLesLignes(fichier)
               .Where(a => a.Time >= depuis)
               .OrderByDescending(a => a.Time)
               .ToList();
    }

    /// <summary>Combien d'alertes archivées sur la période, sans construire la liste.</summary>
    public static int Compter(string? poste, int jours = 0) => Lire(poste, jours).Count;

    /// <summary>
    /// Les postes qui ont un fichier d'archive, qu'ils soient encore au parc ou non.
    /// Un poste retiré garde son historique jusqu'à ce qu'on le supprime à la main.
    /// </summary>
    public static List<string> PostesArchives()
    {
        try
        {
            if (!Directory.Exists(Dossier)) return [];

            return Directory.GetFiles(Dossier, "*.jsonl")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .Select(n => n!)
                            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                            .ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Deux alertes sont la même quand elles portent le même INSTANT et la même
    /// RÈGLE. Les ticks plutôt que le texte de la date : une date reformatée par un
    /// aller-retour de sérialisation ne doit pas créer un faux nouveau.
    /// </summary>
    private static string Cle(PreventiveAlert a) => a.Time.Ticks + "|" + a.RuleId;

    private static List<PreventiveAlert> LireLesLignes(string fichier)
    {
        var liste = new List<PreventiveAlert>();
        if (!File.Exists(fichier)) return liste;

        foreach (var ligne in File.ReadAllLines(fichier))
        {
            if (string.IsNullOrWhiteSpace(ligne)) continue;
            try
            {
                if (JsonSerializer.Deserialize<PreventiveAlert>(ligne) is { } a) liste.Add(a);
            }
            catch (JsonException)
            {
                // Une ligne abîmée n'empêche pas de lire les autres. Elle n'est pas
                // comptée non plus : c'est un fichier d'archive, pas un décompte.
            }
        }
        return liste;
    }
}
