using System.Reflection;
using System.Text;

namespace FaultTracePC.Core;

/// <summary>
/// Journal des pannes du logiciel lui-même — pas des pannes de la machine.
///
/// LE PROBLÈME QU'IL RÉSOUT
/// Un utilisateur a signalé que l'assistant guidé « s'ouvre et se referme
/// aussitôt ». Il n'a rien pu transmettre, et nous n'avons rien pu conclure :
/// aucune exception n'était rattrapée nulle part, et rien n'était écrit.
/// Un logiciel dont toute la démarche consiste à dire « je n'ai pas su lire »
/// ne peut pas disparaître sans un mot.
///
/// POURQUOI DANS ProgramData ET PAS DANS Documents
/// Le service de surveillance tourne sous le compte SYSTEM, qui n'a aucun
/// dossier Documents d'utilisateur : un journal rangé là ne recueillerait
/// jamais SES pannes à lui. Même raisonnement que pour langue.txt en 1.3.0.
/// Les trois exécutables écrivent donc au même endroit, et le chemin est
/// montré à l'utilisateur pour qu'il sache quoi envoyer.
///
/// CE QU'IL NE FAIT JAMAIS
/// Lever une exception. Un journal d'erreurs qui plante en écrivant une erreur
/// remplacerait la panne d'origine par la sienne — c'est-à-dire exactement le
/// défaut qu'il existe pour corriger. Toute écriture qui échoue est abandonnée
/// en silence, et l'appelant l'apprend par un retour nul.
///
/// LE CONTENU N'EST PAS TRADUIT
/// C'est une pièce technique destinée à être relue par le développeur, pas par
/// l'utilisateur. Un journal reçu d'une machine anglaise et un autre d'une
/// machine française doivent se comparer ligne à ligne.
/// </summary>
public static class ErrorLog
{
    /// <summary>Au-delà, le fichier est reculé d'un cran. Un journal sans limite
    /// finirait par occuper l'espace disque dont il diagnostique le manque.</summary>
    private const long MaxBytes = 512 * 1024;

    private static readonly object Verrou = new();

    /// <summary>Dossier commun aux trois exécutables.</summary>
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FaultTracePC");

    /// <summary>Chemin du journal. Toujours calculable, même si l'écriture est impossible.</summary>
    public static string FilePath => Path.Combine(Directory, "erreurs.log");

    /// <summary>
    /// Enregistre une exception. Renvoie le chemin du fichier écrit, ou
    /// <c>null</c> si l'écriture n'a pas abouti — auquel cas l'appelant doit le
    /// dire à l'utilisateur plutôt que de lui indiquer un fichier absent.
    /// </summary>
    public static string? Write(string origin, Exception ex)
    {
        try { return Write(origin, Describe(ex)); }
        catch { return null; }
    }

    /// <summary>Enregistre un texte libre déjà mis en forme.</summary>
    public static string? Write(string origin, string body)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            Rotate();

            var b = new StringBuilder();
            b.Append("---- ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" ----\n");
            b.Append("origin    ").Append(origin).Append('\n');
            b.Append("process   ").Append(ProcessName()).Append('\n');
            b.Append("version   ").Append(UpdateChecker.CurrentVersion).Append('\n');
            b.Append("os        ").Append(Environment.OSVersion.VersionString)
             .Append(Environment.Is64BitOperatingSystem ? " x64" : " x86").Append('\n');
            b.Append("culture   ").Append(System.Globalization.CultureInfo.CurrentUICulture.Name)
             // Lang.Current, pas Lang.Preference : la préférence relit un fichier, et
             // un chemin de panne ne doit rien faire qui puisse échouer à son tour.
             .Append(" / lang=").Append(Lang.Current).Append('\n');
            b.Append("admin     ").Append(IsElevated()).Append('\n');
            b.Append(body.TrimEnd()).Append("\n\n");

            File.AppendAllText(FilePath, b.ToString(), new UTF8Encoding(false));
            return FilePath;
        }
        catch
        {
            // Dossier protégé, disque plein, droits refusés : on renonce. La panne
            // d'origine reste la seule chose que l'utilisateur doit voir.
            return null;
        }
    }

    /// <summary>Type, message et pile, en remontant toutes les exceptions internes.</summary>
    private static string Describe(Exception ex)
    {
        var b = new StringBuilder();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            b.Append(e.GetType().FullName).Append('\n');
            b.Append("  ").Append(e.Message.Replace("\n", "\n  ")).Append('\n');
            if (!string.IsNullOrWhiteSpace(e.StackTrace)) b.Append(e.StackTrace).Append('\n');
            if (e.InnerException is not null) b.Append("caused by\n");
        }
        return b.ToString();
    }

    private static void Rotate()
    {
        lock (Verrou)
        {
            var f = new FileInfo(FilePath);
            if (!f.Exists || f.Length < MaxBytes) return;
            var vieux = FilePath + ".1";
            if (File.Exists(vieux)) File.Delete(vieux);
            File.Move(FilePath, vieux);
        }
    }

    private static string ProcessName()
    {
        try { return Path.GetFileName(Assembly.GetEntryAssembly()?.Location ?? "?"); }
        catch { return "?"; }
    }

    private static string IsElevated()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "yes" : "no";
        }
        catch { return "?"; }
    }
}
