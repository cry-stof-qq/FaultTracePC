using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace FaultTracePC.Core;

/// <summary>
/// Coffre du secret maître de parc, sur la machine qui sert de console.
///
/// CE QU'IL REMPLACE
/// Jusqu'ici la console gardait, dans <c>Documents\FaultTracePC\parc.json</c>, la
/// liste des jetons de toutes les machines supervisées — en clair, dans un dossier
/// qu'une stratégie d'établissement redirige couramment vers un partage réseau.
/// Avec le secret maître, il n'y a plus de liste à protéger : la console recalcule
/// le jeton de chaque poste (voir <see cref="RemoteConfig.DeriveToken"/>).
///
/// POURQUOI %LOCALAPPDATA% ET PAS %APPDATA%
/// AppData\Roaming suit le profil itinérant, donc part sur un partage réseau — le
/// défaut qu'on est en train de corriger. Local ne bouge pas de la machine.
///
/// CE QUE DPAPI APPORTE, ET CE QU'IL N'APPORTE PAS
/// Windows chiffre avec une clé dérivée du compte utilisateur : le fichier est
/// illisible pour une autre session et sur une autre machine, même copié. Il ne
/// protège pas contre un programme lancé par TOI, dans TA session — aucun coffre
/// logiciel ne le fait. Le secret reste par ailleurs dans ton gestionnaire de mots
/// de passe : ce fichier est un confort, pas la copie de référence, et le perdre
/// (profil reconstruit) ne coûte qu'une ressaisie.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ParkSecret
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaultTracePC");

    public static string FilePath => Path.Combine(Directory, "parc.secret");

    /// <summary>
    /// Entropie supplémentaire. Elle n'est pas secrète et n'a pas à l'être : elle
    /// cloisonne, de sorte qu'un blob chiffré par une autre application du même
    /// utilisateur ne se déchiffre pas ici, ni l'inverse.
    /// </summary>
    private static readonly byte[] Entropie = Encoding.UTF8.GetBytes("FaultTracePC.parc.v1");

    public static bool Exists => File.Exists(FilePath);

    /// <summary>Enregistre le secret, chiffré pour l'utilisateur courant.</summary>
    public static bool Save(string secret, out string erreur)
    {
        erreur = "";
        var valeur = (secret ?? "").Trim();
        if (valeur.Length < RemoteConfig.MasterSecretMinLength)
        {
            erreur = Lang.T($"Secret maître trop court — {RemoteConfig.MasterSecretMinLength} caractères au minimum.",
                            $"Master secret too short — {RemoteConfig.MasterSecretMinLength} characters minimum.");
            return false;
        }

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllBytes(FilePath, Transformer(Encoding.UTF8.GetBytes(valeur), proteger: true));
            return true;
        }
        catch (Exception ex)
        {
            erreur = ErrorLog.Describe(ex);
            return false;
        }
    }

    /// <summary>
    /// Relit le secret, ou null s'il n'y en a pas — fichier absent, tronqué, ou
    /// chiffré pour un autre compte. Aucun de ces cas n'est une panne : ils veulent
    /// tous dire « demande le secret à l'utilisateur ».
    /// </summary>
    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var clair = Encoding.UTF8.GetString(Transformer(File.ReadAllBytes(FilePath), proteger: false));
            return clair.Length == 0 ? null : clair;
        }
        catch { return null; }
    }

    /// <summary>Oublie le secret enregistré. Vrai aussi s'il n'y en avait pas.</summary>
    public static bool Forget(out string erreur)
    {
        erreur = "";
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return true;
        }
        catch (Exception ex)
        {
            erreur = ErrorLog.Describe(ex);
            return false;
        }
    }

    // ------------------------------------------------------------------
    // DPAPI, appelé directement.
    //
    // Le paquet System.Security.Cryptography.ProtectedData ferait la même chose ;
    // ces trente lignes évitent une dépendance de plus à suivre de version en
    // version pour deux fonctions dont la signature n'a pas bougé depuis
    // Windows 2000.
    // ------------------------------------------------------------------

    /// <summary>Chiffre pour l'utilisateur courant. Exposé au projet de tests :
    /// le va-et-vient se vérifie sans écrire dans le profil de l'utilisateur.</summary>
    internal static byte[] Proteger(byte[] clair) => Transformer(clair, proteger: true);

    /// <summary>Déchiffre. Lève si le contenu a été altéré ou vient d'un autre compte.</summary>
    internal static byte[] Deproteger(byte[] chiffre) => Transformer(chiffre, proteger: false);

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static byte[] Transformer(byte[] donnees, bool proteger)
    {
        if (donnees.Length == 0) return [];

        var pinDonnees = GCHandle.Alloc(donnees, GCHandleType.Pinned);
        var pinEntropie = GCHandle.Alloc(Entropie, GCHandleType.Pinned);
        DATA_BLOB sortie = default;
        try
        {
            var entree = new DATA_BLOB { cbData = donnees.Length, pbData = pinDonnees.AddrOfPinnedObject() };
            var entropie = new DATA_BLOB { cbData = Entropie.Length, pbData = pinEntropie.AddrOfPinnedObject() };

            var ok = proteger
                ? CryptProtectData(ref entree, null, ref entropie, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out sortie)
                : CryptUnprotectData(ref entree, IntPtr.Zero, ref entropie, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out sortie);

            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());

            var resultat = new byte[sortie.cbData];
            Marshal.Copy(sortie.pbData, resultat, 0, sortie.cbData);
            return resultat;
        }
        finally
        {
            // Windows alloue le tampon de sortie : c'est à nous de le rendre,
            // y compris quand l'appel a échoué après l'avoir rempli.
            if (sortie.pbData != IntPtr.Zero) LocalFree(sortie.pbData);
            pinDonnees.Free();
            pinEntropie.Free();
        }
    }
}
