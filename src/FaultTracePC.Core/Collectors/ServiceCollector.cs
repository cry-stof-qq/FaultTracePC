using System.Management;
using Microsoft.Win32;

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
    /// POINT 103 (correction validée le 19/10/2026) — les PILOTES inscrits dont le
    /// fichier a disparu, lus dans le registre.
    ///
    /// Reproduit le 19/10/2026 en machine virtuelle : un pilote noyau en démarrage
    /// automatique pointant vers un fichier absent fait échouer Windows à chaque
    /// démarrage (« Le fichier spécifié est introuvable »), mais
    /// <c>Win32_SystemDriver</c> ne le liste PAS. <see cref="DriverCollector"/>, qui
    /// lit WMI, ne pouvait donc pas le voir — c'était le cas d'un pilote de
    /// l'ancien antivirus sur un poste du parc.
    ///
    /// Règle, mesurée le même jour sans fausse alerte sur la machine virtuelle, le
    /// poste de l'auteur et un poste du parc : pilote noyau ou de système de
    /// fichiers (Type 1 ou 2), démarrant avec Windows (Start 0, 1 ou 2), dont le
    /// fichier n'existe pas. Sans ImagePath, Windows prend
    /// \SystemRoot\System32\drivers
om.sys : on vérifie ce même chemin.
    /// </summary>
    public static List<ServiceOrphelin> PilotesOrphelinsDuRegistre(ICollection<string> dejaCites)
    {
        var liste = new List<ServiceOrphelin>();
        using var racine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                      .OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (racine is null) return liste;

        foreach (var nom in racine.GetSubKeyNames())
        {
            if (dejaCites.Contains(nom)) continue;
            using var cle = racine.OpenSubKey(nom);
            if (cle is null) continue;

            int? type = cle.GetValue("Type") as int?;
            int? start = cle.GetValue("Start") as int?;
            if (!EstPiloteAuDemarrage(type, start)) continue;

            var chemin = CheminDuPilote(cle.GetValue("ImagePath") as string, nom);
            if (chemin.Length == 0) continue;

            bool present;
            try { present = File.Exists(chemin); }
            catch { continue; } // chemin illisible : on ne conclut rien

            if (!present)
            {
                var affiche = cle.GetValue("DisplayName") as string ?? "";
                liste.Add(new ServiceOrphelin
                {
                    Nom = nom,
                    // Un nom d'affichage « @fichier.dll,-100 » est une référence de
                    // ressource, illisible telle quelle : on garde le nom court.
                    NomAffiche = affiche.Length == 0 || affiche.StartsWith('@') ? nom : affiche,
                    Chemin = chemin,
                    Demarrage = ModeDeDemarrage(start!.Value),
                    Etat = "",
                    EstPilote = true,
                });
            }
        }
        return liste;
    }

    /// <summary>Pilote noyau (1) ou de système de fichiers (2), démarrant avec Windows (0 boot, 1 système, 2 automatique).</summary>
    internal static bool EstPiloteAuDemarrage(int? type, int? start) =>
        type is 1 or 2 && start is >= 0 and <= 2;

    /// <summary>Les noms que WMI emploie pour ces modes, pour rester cohérent avec les autres lignes de la carte.</summary>
    internal static string ModeDeDemarrage(int start) => start switch
    {
        0 => "Boot",
        1 => "System",
        _ => "Auto",
    };

    /// <summary>Chemin du fichier d'un pilote ; sans ImagePath, le chemin que Windows prend par défaut.</summary>
    internal static string CheminDuPilote(string? imagePath, string nom)
    {
        var brut = string.IsNullOrWhiteSpace(imagePath) ? $@"\SystemRoot\System32\drivers\{nom}.sys" : imagePath.Trim().Trim('"');
        return DriverCollector.NormalizePath(Environment.ExpandEnvironmentVariables(brut));
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
