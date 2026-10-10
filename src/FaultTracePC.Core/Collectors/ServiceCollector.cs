using System.Management;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// POINT 85, LOT 2 — les services inscrits dont le programme n'existe plus.
///
/// Une mesure, pas une supposition : Windows connaît le chemin du programme de
/// chaque service ; on vérifie seulement que le fichier est encore là. Ce qui est
/// volontairement laissé de côté (décidé le 10/10/2026) : les dossiers restés sur
/// le disque après une désinstallation. Les rattacher à un logiciel disparu
/// demanderait de deviner d'après leur nom — trop de fausses alertes possibles.
/// </summary>
public static class ServiceCollector
{
    public static List<ServiceOrphelin> Orphelins()
    {
        var liste = new List<ServiceOrphelin>();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName, StartMode, State FROM Win32_Service");
        foreach (ManagementObject mo in searcher.Get())
        {
            var brut = mo["PathName"]?.ToString() ?? "";
            var chemin = CheminDuProgramme(brut);
            if (chemin.Length == 0) continue;

            bool present;
            try { present = File.Exists(chemin); }
            catch { continue; } // chemin illisible : on ne conclut rien

            if (!present)
                liste.Add(new ServiceOrphelin
                {
                    Nom = mo["Name"]?.ToString() ?? "",
                    NomAffiche = mo["DisplayName"]?.ToString() ?? "",
                    Chemin = chemin,
                    Demarrage = mo["StartMode"]?.ToString() ?? "",
                    Etat = mo["State"]?.ToString() ?? "",
                });
        }
        return liste;
    }

    /// <summary>
    /// Isole le chemin du programme dans la ligne de commande d'un service :
    /// partie entre guillemets si elle en a ; sinon, jusqu'à « .exe » ou « .sys »
    /// (un chemin non entouré de guillemets peut contenir des espaces —
    /// « C:\Program Files\… » — on ne coupe donc pas au premier espace).
    /// Chaîne vide si rien d'exploitable : on ne vérifie que ce qu'on sait lire.
    /// </summary>
    internal static string CheminDuProgramme(string ligne)
    {
        var l = ligne.Trim();
        if (l.Length == 0) return "";

        string chemin;
        if (l.StartsWith('"'))
        {
            var fin = l.IndexOf('"', 1);
            if (fin <= 1) return "";
            chemin = l[1..fin];
        }
        else
        {
            var exe = l.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            var sys = l.IndexOf(".sys", StringComparison.OrdinalIgnoreCase);
            var fin = exe >= 0 ? exe : sys;
            if (fin < 0) return "";
            chemin = l[..(fin + 4)];
        }
        return DriverCollector.NormalizePath(Environment.ExpandEnvironmentVariables(chemin));
    }
}
