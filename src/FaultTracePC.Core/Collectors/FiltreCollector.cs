using System.Diagnostics;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// POINT 85, LOT 2b — la pile des pilotes-filtres : ce qui s'intercale entre Windows
/// et le système de fichiers. Sur la machine du 24/09/2026, c'est « fltmc filters »,
/// lancé à la main, qui a permis d'écarter l'ancien antivirus : quatorze filtres,
/// tous Microsoft ou du nouvel antivirus. Le rapport le montre désormais lui-même.
///
/// On lit la sortie de fltmc plutôt que d'appeler l'API du gestionnaire de filtres :
/// une commande que le technicien connaît et peut relancer pour comparer. Les
/// en-têtes de colonnes sont traduits selon la langue de Windows ; les LIGNES DE
/// DONNÉES, elles, ne le sont pas — on repère donc le trait de séparation (tirets),
/// jamais un mot d'en-tête. fltmc demande les droits administrateur, que le
/// logiciel a déjà.
/// </summary>
public static class FiltreCollector
{
    public static void Collect(SystemSnapshot s, List<string> errors)
    {
        string sortie;
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "fltmc.exe"), "filters")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) { errors.Add(Lang.T("fltmc n'a pas pu être lancé : pilotes-filtres non lus.", "fltmc could not be started: filter drivers not read.")); return; }
            sortie = p.StandardOutput.ReadToEnd();
            _ = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } }
        }
        catch (Exception ex)
        {
            errors.Add(Lang.T($"fltmc n'a pas pu être lancé ({ex.Message}) : pilotes-filtres non lus.", $"fltmc could not be started ({ex.Message}): filter drivers not read."));
            return;
        }

        var filtres = Lire(sortie);
        if (filtres is null)
        {
            errors.Add(Lang.T("Sortie de fltmc non reconnue : pilotes-filtres non lus.", "fltmc output not recognised: filter drivers not read."));
            return;
        }
        s.Filtres.AddRange(filtres);
        s.FiltresLus = true;
    }

    /// <summary>
    /// Lit la sortie de « fltmc filters ». Null si le trait de séparation est absent
    /// (droits insuffisants, message d'erreur) : on ne prend pas une sortie
    /// inattendue pour une pile vide.
    /// </summary>
    internal static List<FiltreDisque>? Lire(string sortie)
    {
        var lignes = sortie.Replace("\r", "").Split('\n');
        int separateur = Array.FindIndex(lignes, l => l.Trim().Length > 0 && l.Trim().All(c => c == '-' || c == ' '));
        if (separateur < 0) return null;

        var filtres = new List<FiltreDisque>();
        foreach (var ligne in lignes.Skip(separateur + 1))
        {
            var mots = ligne.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (mots.Length < 2) continue;
            filtres.Add(new FiltreDisque
            {
                Nom = mots[0],
                Instances = int.TryParse(mots[1], out var n) ? n : null,
                Altitude = mots.Length > 2 ? mots[2] : "",
            });
        }
        return filtres;
    }
}
