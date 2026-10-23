using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace FaultTracePC.Core;

/// <summary>Ce que l'utilisateur choisit d'exporter (point 116).</summary>
public enum JournauxAExporter
{
    /// <summary>Journaux Système et Application : ce qui sert au diagnostic.</summary>
    SystemeEtApplication,

    /// <summary>
    /// Journal Sécurité : les ouvertures de session de CHAQUE personne qui utilise la
    /// machine. Proposé à part, derrière un garde-fou, à la demande de l'auteur.
    /// </summary>
    Securite,
}

/// <summary>
/// POINT 116 (demandé le 23/10/2026). Exporte les journaux de Windows en fichiers
/// .evtx, pour que l'utilisateur les confie à la personne de son choix.
///
/// CE QUE CE CODE NE FAIT PAS
/// Il n'envoie rien, nulle part. Il écrit dans un dossier local et s'arrête là :
/// c'est l'utilisateur qui transmet, à qui il veut. Et il ne se déclenche jamais
/// seul — seulement depuis un bouton de la boîte à outils, après une confirmation
/// qui dit ce que contient le fichier (c'est la fenêtre qui s'en charge).
///
/// LISIBLE SUR UN AUTRE PC
/// Un .evtx ne contient que les données des événements, pas leur texte : celui-ci
/// vient des fichiers du logiciel qui a écrit l'événement. Ouvert sur un PC où ce
/// logiciel n'est pas installé, il affiche « la description de l'événement est
/// introuvable ». « wevtutil archive-log » joint ces textes au fichier, dans un
/// sous-dossier par langue (documentation Microsoft de wevtutil). On l'appelle pour
/// la langue de la session et pour l'anglais.
///
/// PÉRIODE
/// 30 ou 90 jours, comme l'analyse, par une requête de temps passée à
/// « wevtutil export-log /q ». Ou le journal complet : la fenêtre prévient que le
/// fichier peut être gros et ses données anciennes.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ExportJournaux
{
    /// <summary>Noms des journaux, tels que wevtutil les attend (jamais traduits).</summary>
    public static IReadOnlyList<string> Journaux(JournauxAExporter choix) => choix switch
    {
        JournauxAExporter.Securite => new[] { "Security" },
        _ => new[] { "System", "Application" },
    };

    /// <summary>
    /// Requête XPath limitant l'export aux <paramref name="jours"/> derniers jours.
    /// Null (journal complet) si aucune période n'est donnée. Fonction pure : testée.
    /// </summary>
    public static string? RequeteDePeriode(int? jours) =>
        jours is int j && j > 0
            ? $"*[System[TimeCreated[timediff(@SystemTime) <= {(long)j * 86_400_000L}]]]"
            : null;

    /// <summary>Arguments de « wevtutil export-log ». Fonction pure : testée.</summary>
    public static List<string> ArgumentsExport(string journal, string fichier, int? jours)
    {
        var a = new List<string> { "epl", journal, fichier };
        if (RequeteDePeriode(jours) is { } q) a.Add("/q:" + q);
        a.Add("/ow:true");
        return a;
    }

    /// <summary>Arguments de « wevtutil archive-log ». Fonction pure : testée.</summary>
    public static List<string> ArgumentsArchive(string fichier, string langue) =>
        new() { "al", fichier, "/l:" + langue };

    /// <summary>Langues jointes au fichier : celle de la session, puis l'anglais.</summary>
    public static IReadOnlyList<string> Langues()
    {
        var langues = new List<string>();
        var session = CultureInfo.CurrentUICulture.Name;
        if (!string.IsNullOrWhiteSpace(session)) langues.Add(session);
        if (!langues.Contains("en-US", StringComparer.OrdinalIgnoreCase)) langues.Add("en-US");
        return langues;
    }

    /// <summary>Dossier daté sous Documents\FaultTracePC, au nom de la machine.</summary>
    public static string DossierParDefaut(string machine, DateTime quand) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC",
                     NomDuDossier(machine, quand));

    /// <summary>Nom du dossier seul. Fonction pure : testée.</summary>
    public static string NomDuDossier(string machine, DateTime quand)
    {
        var propre = new string((machine ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        if (propre.Length == 0) propre = "PC";
        return $"Journaux_{propre}_{quand:yyyy-MM-dd_HHmm}";
    }

    /// <summary>
    /// Taille ACTUELLE du journal sur le disque, en octets — ce que pèsera, au plus,
    /// un export complet. Null si elle ne se lit pas : on ne l'invente pas.
    /// </summary>
    public static long? TailleDuJournal(string journal)
    {
        try
        {
            var fichier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                                       "winevt", "Logs", journal + ".evtx");
            return File.Exists(fichier) ? new FileInfo(fichier).Length : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Ce que l'export a produit, et ce qui a échoué.</summary>
    public sealed record Resultat(string Dossier, IReadOnlyList<string> Fichiers, IReadOnlyList<string> Erreurs)
    {
        public bool Reussi => Fichiers.Count > 0 && Erreurs.Count == 0;
    }

    /// <summary>
    /// Exporte, puis joint les textes des messages. Ne lève pas : chaque échec est
    /// rendu dans <see cref="Resultat.Erreurs"/>, pour que la fenêtre puisse le DIRE.
    /// Un échec de l'archivage n'annule pas le fichier exporté : il est gardé, et
    /// l'erreur dit qu'il sera moins lisible ailleurs.
    /// </summary>
    public static Resultat Exporter(JournauxAExporter choix, int? jours, string dossier)
    {
        var fichiers = new List<string>();
        var erreurs = new List<string>();
        try
        {
            Directory.CreateDirectory(dossier);
        }
        catch (Exception ex)
        {
            erreurs.Add(ErrorLog.Describe(ex));
            return new Resultat(dossier, fichiers, erreurs);
        }

        foreach (var journal in Journaux(choix))
        {
            var fichier = Path.Combine(dossier, journal + ".evtx");
            if (!Wevtutil(ArgumentsExport(journal, fichier, jours), out var erreur))
            {
                erreurs.Add(Lang.T($"{journal} : export impossible — {erreur}", $"{journal}: export failed — {erreur}"));
                continue;
            }
            fichiers.Add(fichier);

            foreach (var langue in Langues())
                if (!Wevtutil(ArgumentsArchive(fichier, langue), out var erreurArchive))
                    erreurs.Add(Lang.T(
                        $"{journal} : exporté, mais les textes en {langue} n'ont pas pu être joints ; le fichier sera moins lisible sur un autre PC — {erreurArchive}",
                        $"{journal}: exported, but the {langue} texts could not be attached; the file will be harder to read on another PC — {erreurArchive}"));
        }
        return new Resultat(dossier, fichiers, erreurs);
    }

    /// <summary>
    /// Lance wevtutil sans fenêtre. Les arguments passent par ArgumentList : la
    /// requête de période contient des espaces et des crochets, que .NET protège
    /// lui-même. Sorties lues en parallèle, pour qu'un journal bavard ne bloque pas.
    /// </summary>
    private static bool Wevtutil(List<string> arguments, out string erreur)
    {
        erreur = "";
        try
        {
            var psi = new ProcessStartInfo("wevtutil.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in arguments) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi)!;
            var sortie = p.StandardOutput.ReadToEndAsync();
            var err = p.StandardError.ReadToEndAsync();
            // Un journal Sécurité complet, sur un poste de domaine, peut peser des
            // centaines de Mo : dix minutes avant de renoncer.
            if (!p.WaitForExit(10 * 60 * 1000))
            {
                try { p.Kill(); } catch { /* déjà terminé */ }
                erreur = Lang.T("wevtutil n'a pas rendu la main en dix minutes", "wevtutil did not finish within ten minutes");
                return false;
            }
            if (p.ExitCode == 0) return true;

            var message = err.Result.Trim().Length > 0 ? err.Result : sortie.Result;
            erreur = Lang.T($"code {p.ExitCode} : {message.Trim()}", $"code {p.ExitCode}: {message.Trim()}");
            return false;
        }
        catch (Exception ex)
        {
            erreur = ErrorLog.Describe(ex);
            return false;
        }
    }
}
