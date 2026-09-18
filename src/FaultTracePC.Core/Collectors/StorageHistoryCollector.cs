using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FaultTracePC.Core.Collectors;

/// <summary>
/// Met un nom sur un support qui n'est plus branché.
///
/// LE PROBLÈME QU'IL RÉSOUT
/// Constaté le 18/09/2026 sur TECH-INFO-2025 : 479 erreurs de bloc défectueux sur
/// « \Device\Harddisk1 », un numéro de disque qui n'existait plus au moment de
/// l'analyse. Le rapport ne pouvait écrire que « un disque qui portait le numéro 1 ».
/// Les numéros de disque sont attribués au branchement : celui-là ne désigne plus rien.
///
/// Windows, lui, retient dans SYSTEM\MountedDevices quel matériel a monté chaque
/// lettre de lecteur, et dans Enum\USBSTOR le nom lisible des supports USB déjà vus.
/// Recouper les deux permet d'écrire « D: — SanDisk Cruzer USB Device » au lieu d'un
/// numéro mort.
///
/// CE QUE CETTE SOURCE NE PERMET PAS
/// Rien, dans le registre, ne relie une lettre au NUMÉRO de disque qu'elle portait ce
/// jour-là. Un rapprochement entre « Harddisk1 » et « D: » reste une piste. Le moteur
/// de règles doit l'écrire comme telle, et jamais comme une identification.
///
/// LECTURE SEULE, ET RIEN QUI IDENTIFIE UNE PERSONNE
/// Le numéro de série du support n'est PAS retenu : il n'apporte rien au diagnostic
/// et c'est un identifiant matériel unique, au même titre qu'une adresse MAC — que ce
/// logiciel masque déjà. Seuls le fabricant, le modèle et la révision sont conservés.
/// </summary>
public static class StorageHistoryCollector
{
    private const string CleMontages = @"SYSTEM\MountedDevices";
    private const string CleUsbStor = @"SYSTEM\CurrentControlSet\Enum\USBSTOR";

    /// <summary>« \DosDevices\D: » → « D: »</summary>
    private static readonly Regex NomDeLettre = new(@"^\\DosDevices\\([A-Za-z]:)$", RegexOptions.IgnoreCase);

    /// <summary>« Disk&amp;Ven_SanDisk&amp;Prod_Cruzer_Blade&amp;Rev_1.00 » dans le chemin du périphérique.</summary>
    private static readonly Regex IdentifiantUsbStor =
        new(@"USBSTOR#(Disk&Ven_[^#]*?&Prod_[^#]*?&Rev_[^#]*?)#", RegexOptions.IgnoreCase);

    /// <summary>
    /// « \DosDevices\D: » → « D: ». Chaîne vide pour tout le reste (les entrées
    /// « \??\Volume{…} » décrivent le même volume sans lettre : elles n'apportent
    /// rien de lisible à un utilisateur).
    /// </summary>
    internal static string ExtraireLettre(string nomDeValeur)
    {
        var m = NomDeLettre.Match(nomDeValeur ?? "");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
    }

    /// <summary>
    /// Identifiant matériel contenu dans le chemin de périphérique stocké par Windows,
    /// numéro de série exclu. Chaîne vide si ce n'est pas un support USB de stockage —
    /// un disque fixe y laisse une signature binaire, pas un chemin.
    /// </summary>
    internal static string ExtraireIdentifiant(string cheminDePeripherique)
    {
        var m = IdentifiantUsbStor.Match(cheminDePeripherique ?? "");
        return m.Success ? m.Groups[1].Value : "";
    }

    public static StorageHistoryInfo Collect(List<string> errors)
    {
        var info = new StorageHistoryInfo();

        var montes = LettresMontees();
        var nomsUsb = NomsUsbConnus(errors);

        try
        {
            using var cle = Registry.LocalMachine.OpenSubKey(CleMontages);
            if (cle is null)
            {
                info.Note = Lang.T("La clé SYSTEM\\MountedDevices est absente : aucun historique de montage n'a pu être lu.",
                                   "The SYSTEM\\MountedDevices key is missing: no mount history could be read.");
                return info;
            }

            info.Readable = true;

            foreach (var nom in cle.GetValueNames())
            {
                var lettre = ExtraireLettre(nom);
                if (lettre.Length == 0) continue;
                if (cle.GetValue(nom) is not byte[] donnees || donnees.Length == 0) continue;

                // La donnée est soit une signature binaire (disque fixe MBR ou GPT),
                // soit le chemin du périphérique en UTF-16. Seul le second cas nous
                // intéresse, et il se reconnaît à « USBSTOR ».
                var id = ExtraireIdentifiant(Encoding.Unicode.GetString(donnees));

                var volume = new RememberedVolume
                {
                    Letter = lettre,
                    IsRemovable = id.Length > 0,
                    DeviceId = id,
                };
                volume.CurrentlyMounted = montes.Contains(volume.Letter);
                if (volume.DeviceId.Length > 0 && nomsUsb.TryGetValue(volume.DeviceId, out var joli))
                    volume.FriendlyName = joli;

                info.Volumes.Add(volume);
            }
        }
        catch (System.Security.SecurityException)
        {
            // Lire SYSTEM\MountedDevices demande des droits d'administrateur. Le dire,
            // plutôt que de rendre une liste vide qui se lirait « aucun support connu ».
            info.Readable = false;
            info.Note = Lang.T("La lecture de l'historique des montages demande des droits d'administrateur : elle n'a pas pu être faite.",
                               "Reading the mount history requires administrator rights: it could not be done.");
        }
        catch (UnauthorizedAccessException)
        {
            info.Readable = false;
            info.Note = Lang.T("La lecture de l'historique des montages demande des droits d'administrateur : elle n'a pas pu être faite.",
                               "Reading the mount history requires administrator rights: it could not be done.");
        }
        catch (Exception ex)
        {
            info.Readable = false;
            errors.Add(Lang.T($"Historique des montages : {ex.Message}", $"Mount history: {ex.Message}"));
        }

        info.Volumes = info.Volumes.OrderBy(v => v.Letter, StringComparer.OrdinalIgnoreCase).ToList();
        return info;
    }

    /// <summary>Lettres montées à l'instant de l'analyse.</summary>
    private static HashSet<string> LettresMontees()
    {
        var montes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                var nom = d.Name.TrimEnd('\\', '/');
                if (nom.Length == 2) montes.Add(nom.ToUpperInvariant());
            }
        }
        catch { /* une lettre illisible ne doit pas faire échouer la collecte */ }
        return montes;
    }

    /// <summary>
    /// Noms lisibles des supports USB de stockage déjà vus par ce poste, indexés par
    /// identifiant matériel. Le niveau sous chaque identifiant est le numéro de série :
    /// on le traverse pour atteindre FriendlyName, on ne le retient pas.
    /// </summary>
    private static Dictionary<string, string> NomsUsbConnus(List<string> errors)
    {
        var noms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var racine = Registry.LocalMachine.OpenSubKey(CleUsbStor);
            if (racine is null) return noms;

            foreach (var idMateriel in racine.GetSubKeyNames())
            {
                using var cleMateriel = racine.OpenSubKey(idMateriel);
                if (cleMateriel is null) continue;

                foreach (var serie in cleMateriel.GetSubKeyNames())
                {
                    using var cleSerie = cleMateriel.OpenSubKey(serie);
                    var joli = cleSerie?.GetValue("FriendlyName") as string;
                    if (string.IsNullOrWhiteSpace(joli)) continue;
                    noms[idMateriel] = joli.Trim();
                    break;   // un nom par matériel suffit ; les séries suivantes sont le même modèle
                }
            }
        }
        catch (System.Security.SecurityException) { /* sans droits : on se passera du nom lisible */ }
        catch (UnauthorizedAccessException) { /* idem */ }
        catch (Exception ex)
        {
            errors.Add(Lang.T($"Noms des supports USB connus : {ex.Message}", $"Names of known USB media: {ex.Message}"));
        }
        return noms;
    }
}
