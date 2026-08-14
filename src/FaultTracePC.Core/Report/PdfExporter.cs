using System.Diagnostics;
using Microsoft.Win32;

namespace FaultTracePC.Core.Report;

/// <summary>
/// Conversion du rapport HTML en PDF, à la demande explicite de l'utilisateur.
///
/// Pourquoi passer par le navigateur plutôt que par une bibliothèque PDF :
/// Edge est présent sur toute machine Windows 10 et 11, il sait déjà rendre
/// exactement ce rapport — puisque c'est lui qui l'affiche — et il n'ajoute
/// aucune dépendance au projet. Une bibliothèque tierce pèserait plusieurs
/// mégaoctets, aurait son propre moteur de rendu, et le PDF finirait par ne
/// plus ressembler à ce que l'utilisateur voit à l'écran.
///
/// Deux précautions qui font toute la différence :
///  · un profil temporaire dédié (--user-data-dir), sans lequel le processus
///    sans interface se rattache à l'Edge déjà ouvert de l'utilisateur, rend
///    la main immédiatement et ne produit aucun fichier ;
///  · le PDF est généré depuis une copie du rapport en mode COMPLET : un PDF
///    destiné à un ticket ou à un SAV doit contenir les détails techniques,
///    que le mode simple masque à l'écran.
/// </summary>
public static class PdfExporter
{
    public sealed record Result(bool Ok, string? PdfPath, string? Error);

    /// <summary>Chemin d'Edge, puis de Chrome en repli. Null si aucun n'est installé.</summary>
    public static string? FindBrowser()
    {
        foreach (var exe in new[] { "msedge.exe", "chrome.exe" })
        {
            // La clé « App Paths » est la source officielle : elle survit aux
            // installations dans un dossier non standard.
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                    if (key?.GetValue(null) is string path && File.Exists(path)) return path;
                }
                catch { /* clé inaccessible : on tente le chemin suivant */ }
            }
        }

        foreach (var candidate in new[]
                 {
                     @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                     @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                     @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                     @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                 })
            if (File.Exists(candidate)) return candidate;

        return null;
    }

    /// <summary>
    /// Convertit <paramref name="htmlPath"/> en PDF. Ne lève jamais : les échecs
    /// reviennent dans <see cref="Result.Error"/>, formulés pour être lus par
    /// quelqu'un qui n'y connaît rien.
    /// </summary>
    public static Result Export(string htmlPath, string? pdfPath = null, TimeSpan? timeout = null)
    {
        if (!File.Exists(htmlPath))
            return new Result(false, null, "Le rapport HTML est introuvable : " + htmlPath);

        var browser = FindBrowser();
        if (browser is null)
            return new Result(false, null,
                "Aucun navigateur compatible n'a été trouvé. FaultTracePC s'appuie sur Microsoft Edge, "
                + "présent d'origine sur Windows 10 et 11, ou sur Google Chrome. "
                + "Solution de repli : ouvre le rapport dans ton navigateur et utilise « Imprimer » → « Enregistrer au format PDF ».");

        pdfPath ??= Path.ChangeExtension(htmlPath, ".pdf");
        string? tempHtml = null;
        string? profileDir = null;

        try
        {
            tempHtml = CreateFullModeCopy(htmlPath);

            // Profil jetable : sans lui, Edge se rattache à l'instance déjà ouverte
            // et le mode sans interface ne produit rien du tout.
            profileDir = Path.Combine(Path.GetTempPath(), "FaultTracePC_pdf_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(profileDir);

            if (File.Exists(pdfPath)) { try { File.Delete(pdfPath); } catch { } }

            var deadline = timeout ?? TimeSpan.FromMinutes(3);

            // Les versions récentes exigent --headless=new ; les anciennes ne
            // connaissent que --headless. On tente la première, puis la seconde.
            foreach (var headless in new[] { "--headless=new", "--headless" })
            {
                var args = $"{headless} --disable-gpu --no-first-run --no-default-browser-check "
                         + $"--user-data-dir=\"{profileDir}\" --no-pdf-header-footer "
                         + $"--print-to-pdf=\"{pdfPath}\" \"{new Uri(tempHtml).AbsoluteUri}\"";

                using var p = Process.Start(new ProcessStartInfo(browser, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                });
                if (p is null) continue;
                if (!p.WaitForExit((int)deadline.TotalMilliseconds))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return new Result(false, null, "La conversion a dépassé le délai imparti et a été interrompue.");
                }

                // Le fichier peut apparaître avec un court décalage après la sortie.
                for (int i = 0; i < 20 && !File.Exists(pdfPath); i++) Thread.Sleep(100);
                if (File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 1024)
                    return new Result(true, pdfPath, null);
            }

            return new Result(false, null,
                "Le navigateur n'a pas produit de fichier PDF. "
                + "Solution de repli : ouvre le rapport dans ton navigateur et utilise « Imprimer » → « Enregistrer au format PDF ».");
        }
        catch (Exception ex)
        {
            return new Result(false, null, ex.Message);
        }
        finally
        {
            if (tempHtml is not null) { try { File.Delete(tempHtml); } catch { } }
            if (profileDir is not null) { try { Directory.Delete(profileDir, recursive: true); } catch { } }
        }
    }

    /// <summary>
    /// Copie du rapport avec le mode simple retiré : à l'écran on masque les
    /// sections techniques pour ne pas noyer le lecteur, mais un PDF que l'on
    /// transmet doit être complet — sinon le destinataire reçoit un document
    /// amputé sans savoir qu'il l'est.
    /// </summary>
    private static string CreateFullModeCopy(string htmlPath)
    {
        var html = File.ReadAllText(htmlPath);
        html = html.Replace("<body class=\"simple\">", "<body>");
        var temp = Path.Combine(Path.GetTempPath(),
            $"FaultTracePC_print_{Guid.NewGuid().ToString("N")[..8]}.html");
        File.WriteAllText(temp, html, System.Text.Encoding.UTF8);
        return temp;
    }
}
