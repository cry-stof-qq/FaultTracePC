using System.Windows;
using FaultTracePC.Core;

namespace FaultTracePC.App;

/// <summary>
/// POINT 43, deuxième partie — l'ARCHIVAGE BRANCHÉ SUR L'ACTUALISATION, et la
/// colonne qui rend l'archive utile.
///
/// CE QUE FAIT CE FICHIER, EN UNE PHRASE
/// À chaque actualisation, la console écrit dans son archive les alertes qu'elle
/// vient de lire sur les postes, puis affiche pour chaque poste combien il en a
/// accumulé sur 90 jours.
///
/// POURQUOI ICI ET PAS AILLEURS
/// L'actualisation est le SEUL moment où la console a les alertes sous la main.
/// Les postes ne les gardent que sept jours et un poste réimagé repart à zéro :
/// ce qui n'est pas archivé à cet instant-là est perdu pour de bon. Archiver
/// ailleurs — à la fermeture, à la demande, une fois par jour — reviendrait à
/// parier que la console est ouverte au bon moment.
///
/// POURQUOI 90 JOURS
/// C'est la durée qui sépare deux vacances scolaires. Elle répond à la seule
/// question qu'on se pose vraiment devant un parc : « est-ce que ce poste-là
/// alerte plus que les autres, ou est-ce que j'ai juste regardé le mauvais jour ».
/// Sept jours ne le dit pas — un poste qui chauffe une fois par mois y passe
/// inaperçu. Un an le dirait aussi, mais mélangerait la machine d'aujourd'hui
/// avec celle d'avant sa réparation.
///
/// L'ÉCRITURE NE BLOQUE PAS LA FENÊTRE
/// Archiver, c'est lire puis réécrire un fichier par poste. Fait sur le fil
/// d'affichage, cela figerait la fenêtre le temps des accès disque. Tout passe
/// donc par Task.Run, et seul le résultat — un décompte — revient à l'affichage.
///
/// UN ÉCHEC D'ARCHIVAGE N'INTERROMPT RIEN
/// Si le disque est plein ou le dossier verrouillé, la supervision continue
/// d'afficher ce qu'elle vient de lire. Le problème est DIT dans la barre d'état,
/// parce qu'une archive qui ne s'écrit plus en silence est pire que pas d'archive
/// du tout : on croirait avoir la preuve.
/// </summary>
public partial class ParkWindow
{
    /// <summary>La période de la colonne, en jours. Voir le commentaire de classe.</summary>
    private const int JoursDeLaColonne = 90;

    /// <summary>
    /// Décompte d'alertes archivées par poste, recalculé à chaque actualisation.
    ///
    /// C'est un CACHE, et il en faut un : l'affichage du tableau est reconstruit à
    /// chaque ajout, chaque retrait et chaque actualisation. Recompter en relisant
    /// les fichiers à chacun de ces moments ferait des accès disque sur le fil
    /// d'affichage pour un résultat qui n'a pas changé.
    /// </summary>
    private Dictionary<string, (int Total, int Critiques)> _alertes90 = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Archive les alertes rapportées par l'actualisation, puis recompte.
    /// Rend ce qu'il faut dire à l'utilisateur, ou une chaîne vide s'il n'y a rien
    /// à signaler.
    /// </summary>
    private async Task<string> ArchiverLesAlertes(IEnumerable<QueryResult> resultats)
    {
        // On fige la liste AVANT de partir sur un autre fil : les objets de
        // l'interface ne se lisent pas depuis un fil de fond.
        var aArchiver = resultats
            .Where(r => r.Ok)
            .Select(r => (r.Machine.Name, r.Alerts))
            .ToList();

        var noms = _machines.Select(m => m.Name).ToList();

        var (nouvelles, notes, comptes) = await Task.Run(() =>
        {
            var journal = new List<string>();
            var total = 0;

            foreach (var (nom, alertes) in aArchiver)
                total += ArchiveDesAlertes.Archiver(nom, alertes, journal);

            var table = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
            foreach (var nom in noms)
            {
                var liste = ArchiveDesAlertes.Lire(nom, JoursDeLaColonne);
                table[nom] = (liste.Count, liste.Count(a => a.Level == "crit"));
            }

            return (total, journal, table);
        });

        _alertes90 = comptes;

        if (notes.Count > 0)
        {
            // On ne recopie pas les vingt mêmes messages : le premier suffit à
            // nommer le problème, le nombre dit son ampleur.
            var premier = notes[0];
            return notes.Count == 1
                ? Lang.T($" ⚠ Archivage : {premier}", $" ⚠ Archiving: {premier}")
                : Lang.T($" ⚠ Archivage : {premier} (et {notes.Count - 1} autre(s) poste(s) dans le même cas)",
                         $" ⚠ Archiving: {premier} (and {notes.Count - 1} other machine(s) in the same situation)");
        }

        return nouvelles > 0
            ? Lang.T($" 🗄 {nouvelles} nouvelle(s) alerte(s) archivée(s).", $" 🗄 {nouvelles} new alert(s) archived.")
            : "";
    }

    /// <summary>
    /// Ce qui s'affiche dans la colonne pour un poste.
    ///
    /// ZÉRO S'ÉCRIT « — » ET PAS « 0 » : une colonne pleine de zéros attire l'œil
    /// sur ce qui ne s'est pas produit. Ce qu'on cherche, ce sont les nombres.
    /// Les critiques sont rappelées entre parenthèses parce que dix alertes dont
    /// aucune critique et dix alertes dont six critiques ne demandent pas la même
    /// chose.
    /// </summary>
    private string LibelleAlertes90(string nom)
    {
        if (!_alertes90.TryGetValue(nom, out var c) || c.Total == 0) return "—";
        return c.Critiques > 0 ? $"{c.Total} ({c.Critiques} ⛔)" : c.Total.ToString();
    }

    /// <summary>
    /// Ouvre l'historique archivé du poste sélectionné.
    ///
    /// CETTE FENÊTRE NE CONTACTE AUCUNE MACHINE : elle relit ce que la console a
    /// écrit. Elle répond donc même quand le poste est éteint — et c'est
    /// précisément le cas où on la consulte.
    /// </summary>
    private void BtnHistoriqueAlertes_Click(object sender, RoutedEventArgs e)
    {
        if (LvMachines.SelectedItem is not Row row)
        {
            MessageBox.Show(this,
                Lang.T("Sélectionne d'abord une machine dans la liste.", "Select a machine in the list first."),
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new HistoriqueAlertesWindow(this, row.Name).ShowDialog();

        // L'archive n'a pas bougé, mais la fenêtre a pu être ouverte longtemps :
        // on ne recompte pas pour autant. Le décompte se rafraîchit à la prochaine
        // actualisation, comme le reste du tableau.
    }
}
