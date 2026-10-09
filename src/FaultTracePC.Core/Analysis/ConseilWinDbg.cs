namespace FaultTracePC.Core.Analysis;

/// <summary>
/// POINT 80, LOT B — la question posée à l'utilisateur juste après une analyse,
/// quand des fichiers d'incident n'ont pas pu être analysés en profondeur.
///
/// Jusqu'au 09/10/2026, la fenêtre principale déduisait « les outils ne sont pas
/// installés » du seul fait qu'aucun dump n'avait été analysé. Sur la machine du
/// 24/09/2026 c'était faux : WinDbg était installé, mais Windows refusait de le
/// lancer — et le bouton proposé aurait réinstallé le même paquet. La décision
/// lit désormais l'état précis calculé pendant l'analyse
/// (<see cref="DiagnosticReport.AnalyseProfonde"/>), le même que le rapport.
///
/// Placée dans Core et non dans la fenêtre pour pouvoir être testée : le projet de
/// tests ne charge pas l'interface WPF.
/// </summary>
public sealed record ConseilWinDbg(string Titre, string Message, bool ProposerBoiteAOutils)
{
    /// <summary>
    /// Ce qu'il faut dire après l'analyse, ou <c>null</c> s'il n'y a rien à dire :
    /// analyse faite, non demandée, ou aucun dump noyau à analyser.
    /// </summary>
    public static ConseilWinDbg? ApresAnalyse(DiagnosticReport report)
    {
        var nonAnalyses = report.Dumps.Count(d =>
            (d.Kind is DumpKind.KernelMinidump or DumpKind.FullMemoryDump) && !d.DeepAnalyzed);

        return report.AnalyseProfonde switch
        {
            // Texte inchangé depuis la version précédente : il était juste dans ce cas.
            EtatAnalyseProfonde.Absente when nonAnalyses > 0 => new ConseilWinDbg(
                Lang.T("Analyse incomplète", "Incomplete analysis"),
                Lang.T($"{nonAnalyses} fichier(s) d'incident ont été trouvés, mais le pilote fautif n'a pas pu être nommé : ", $"{nonAnalyses} crash file(s) were found, but the faulting driver could not be named: ")
                + Lang.T("les outils de débogage de Microsoft ne sont pas installés sur cette machine.\n\n", "the Microsoft debugging tools are not installed on this machine.\n\n")
                + Lang.T("Sans eux, le code d'arrêt est lu, mais le coupable reste souvent anonyme — c'est la différence ", "Without them the stop code is read, but the culprit often stays anonymous — that is the difference ")
                + Lang.T("entre « la machine a planté » et « c'est ce pilote-là ».\n\n", "between “the machine crashed” and “it is that driver”.\n\n")
                + Lang.T("Ouvrir la boîte à outils pour les installer ?", "Open the toolbox to install them?"),
                ProposerBoiteAOutils: true),

            // Installé mais refusé : NE PAS proposer la boîte à outils, qui installerait
            // la même version. On informe, et on renvoie vers la raison écrite dans le rapport.
            EtatAnalyseProfonde.Inaccessible when nonAnalyses > 0 => new ConseilWinDbg(
                Lang.T("Analyse incomplète", "Incomplete analysis"),
                Lang.T($"{nonAnalyses} fichier(s) d'incident ont été trouvés, mais le pilote fautif n'a pas pu être nommé : ", $"{nonAnalyses} crash file(s) were found, but the faulting driver could not be named: ")
                + Lang.T("les outils de débogage de Microsoft sont bien installés sur cette machine, mais Windows a refusé de les lancer.\n\n", "the Microsoft debugging tools are installed on this machine, but Windows refused to start them.\n\n")
                + Lang.T("Ce n'est pas un défaut de la machine analysée. La raison donnée par Windows est écrite dans les limitations, en fin de rapport.\n\n", "This is not a fault of the analysed machine. The reason given by Windows is written in the limitations, at the end of the report.\n\n")
                + Lang.T("Le bouton de la boîte à outils réinstallerait la même version : inutile de l'utiliser pour cela.", "The toolbox button would reinstall the same version: there is no point using it for this."),
                ProposerBoiteAOutils: false),

            _ => null,
        };
    }
}
