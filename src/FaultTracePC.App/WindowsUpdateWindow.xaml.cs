using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace FaultTracePC.App;

/// <summary>
/// Mises à jour Windows, pilotées directement par l'API Windows Update Agent (WUA,
/// COM intégré à Windows — aucune dépendance à installer, aucun téléchargement tiers).
///
/// Trois raisons d'avoir cette fenêtre plutôt que d'ouvrir Paramètres :
///  1. la page Paramètres masque les mises à jour OPTIONNELLES et les PILOTES, qui sont
///     très souvent la correction d'un crash imputé à un .sys ;
///  2. la sélection se fait ligne par ligne, alors que Paramètres impose le lot complet ;
///  3. aucun redémarrage automatique n'est déclenché ici — c'est l'utilisateur qui décide.
///
/// Contrainte technique importante : TOUS les appels COM sont exécutés dans des
/// Task.Run (threads du pool = appartement MTA). Les objets WUA sont créés là et
/// restent utilisables d'un thread MTA à l'autre ; ils ne sont jamais touchés depuis
/// le thread UI (STA), ce qui éviterait un marshalling inter-appartements hasardeux.
/// </summary>
public partial class WindowsUpdateWindow : Window
{
    /// <summary>Identifiant du service « Microsoft Update » (pilotes + produits Microsoft).</summary>
    private const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

    private readonly ObservableCollection<UpdateRow> _rows = new();

    /// <summary>Session WUA (objet COM) — créée et utilisée uniquement depuis des threads MTA.</summary>
    private object? _session;

    private bool _busy;

    public WindowsUpdateWindow()
    {
        InitializeComponent();
        LvUpdates.ItemsSource = _rows;
    }

    // ==================================================================
    // Modèle de ligne
    // ==================================================================

    public sealed class UpdateRow : INotifyPropertyChanged
    {
        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { if (_selected != value) { _selected = value; OnPropertyChanged(); } }
        }

        public string Title { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Category { get; set; } = "";
        public string SizeText { get; set; } = "";
        public string RebootText { get; set; } = "";
        public string KbText { get; set; } = "";
        public string Tooltip { get; set; } = "";

        public bool IsDriver { get; set; }
        public bool IsOptional { get; set; }
        public bool EulaAccepted { get; set; }

        /// <summary>Référence COM IUpdate — ne jamais déréférencer depuis le thread UI.</summary>
        public object Com { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ==================================================================
    // Petit pont d'appel tardif COM (IDispatch) — sans « dynamic »,
    // dont le liant COM n'est pas garanti sur toutes les cibles .NET.
    // ==================================================================

    private static object? Get(object o, string name, params object[] args) =>
        o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, args);

    private static void Set(object o, string name, object value) =>
        o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, new[] { value });

    private static object? Call(object o, string name, params object[] args) =>
        o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args);

    private static string GetStr(object o, string name)
    {
        try { return Get(o, name)?.ToString() ?? ""; } catch { return ""; }
    }

    private static bool GetBool(object o, string name, bool fallback = false)
    {
        try { var v = Get(o, name); return v is null ? fallback : Convert.ToBoolean(v); } catch { return fallback; }
    }

    private static int GetInt(object o, string name, int fallback = 0)
    {
        try { var v = Get(o, name); return v is null ? fallback : Convert.ToInt32(v); } catch { return fallback; }
    }

    private static long GetLong(object o, string name, long fallback = 0)
    {
        try { var v = Get(o, name); return v is null ? fallback : Convert.ToInt64(v); } catch { return fallback; }
    }

    // ==================================================================
    // Recherche
    // ==================================================================

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private async Task SearchAsync()
    {
        if (_busy) return;
        bool includeDrivers = ChkDrivers.IsChecked == true;
        bool includeHidden = ChkHidden.IsChecked == true;

        SetBusy(true, "Interrogation de Windows Update… (cela peut prendre 1 à 3 minutes)");
        ClearRows();
        Log("=== Recherche démarrée ===");

        try
        {
            var (found, notes) = await Task.Run(() => SearchCore(includeDrivers, includeHidden));

            foreach (var n in notes) Log(n);
            foreach (var r in found)
            {
                r.PropertyChanged += Row_SelectionChanged;
                _rows.Add(r);
            }

            if (found.Count == 0)
            {
                TxtStatus.Text = "Aucune mise à jour disponible : ce poste est à jour (rien d'important, d'optionnel ni de pilote en attente).";
            }
            else
            {
                int important = found.Count(r => !r.IsOptional);
                int drivers = found.Count(r => r.IsDriver);
                TxtStatus.Text = $"{found.Count} mise(s) à jour disponible(s) : {important} importante(s), "
                               + $"{found.Count - important} optionnelle(s) dont {drivers} pilote(s). "
                               + "Coche ce que tu veux installer.";
            }
            BtnInstall.IsEnabled = found.Count > 0;
        }
        catch (Exception ex)
        {
            var msg = Unwrap(ex);
            TxtStatus.Text = "La recherche a échoué : " + msg;
            Log("ERREUR : " + msg);
            MessageBox.Show(this,
                "Impossible d'interroger Windows Update.\n\n" + msg +
                "\n\nCauses fréquentes :\n" +
                "• le service « Windows Update » (wuauserv) est arrêté ou désactivé ;\n" +
                "• une stratégie de groupe (GPO/WSUS) bloque la recherche en ligne ;\n" +
                "• pas de connexion réseau vers les serveurs de mise à jour.",
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    /// <summary>Exécuté sur un thread MTA : toute la partie COM de la recherche.</summary>
    private (List<UpdateRow> Rows, List<string> Notes) SearchCore(bool includeDrivers, bool includeHidden)
    {
        var notes = new List<string>();
        var rows = new List<UpdateRow>();

        var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session", throwOnError: false)
            ?? throw new InvalidOperationException(
                "L'API Windows Update (Microsoft.Update.Session) est introuvable sur ce système.");

        _session ??= Activator.CreateInstance(sessionType)
            ?? throw new InvalidOperationException("Création de la session Windows Update impossible.");
        var session = _session!;
        try { Set(session, "ClientApplicationID", "FaultTracePC"); } catch { /* facultatif */ }

        string criteria = includeHidden ? "IsInstalled=0" : "IsInstalled=0 and IsHidden=0";
        notes.Add("Critère de recherche : " + criteria);

        object? result = null;

        if (includeDrivers)
        {
            // Catalogue Microsoft Update : seul moyen d'obtenir les PILOTES et les
            // produits Microsoft. S'il n'est pas déclaré sur le poste, WUA renvoie
            // WU_E_DS_UNKNOWNSERVICE (0x80248014) — on retombe alors proprement
            // sur Windows Update seul, en le disant.
            try
            {
                var searcher = Call(session, "CreateUpdateSearcher")!;
                Set(searcher, "Online", true);
                Set(searcher, "ServerSelection", 3); // ssOthers
                Set(searcher, "ServiceID", MicrosoftUpdateServiceId);
                result = Call(searcher, "Search", criteria);
                notes.Add("Catalogue interrogé : Microsoft Update (pilotes et produits Microsoft inclus).");
            }
            catch (Exception ex)
            {
                notes.Add("Microsoft Update indisponible (" + Unwrap(ex) + ") — bascule sur Windows Update seul.");
                notes.Add("Pour activer les pilotes : Paramètres > Windows Update > Options avancées > "
                        + "« Recevoir des mises à jour pour d'autres produits Microsoft ».");
                result = null;
            }
        }

        if (result is null)
        {
            var searcher = Call(session, "CreateUpdateSearcher")!;
            Set(searcher, "Online", true);
            result = Call(searcher, "Search", criteria)
                ?? throw new InvalidOperationException("Réponse vide du service Windows Update.");
            if (!includeDrivers) notes.Add("Catalogue interrogé : Windows Update.");
        }

        var updates = Get(result!, "Updates")!;
        int count = GetInt(updates, "Count");
        notes.Add($"{count} élément(s) renvoyé(s) par le service.");

        for (int i = 0; i < count; i++)
        {
            object? u;
            try { u = Get(updates, "Item", i); } catch { continue; }
            if (u is null) continue;

            bool isDriver = GetInt(u, "Type") == 2;               // 1 = logiciel, 2 = pilote
            bool browseOnly = GetBool(u, "BrowseOnly");           // = « mise à jour optionnelle »
            string severity = GetStr(u, "MsrcSeverity");
            long size = GetLong(u, "MaxDownloadSize");
            int reboot = 2;
            try { var beh = Get(u, "InstallationBehavior"); if (beh is not null) reboot = GetInt(beh, "RebootBehavior"); }
            catch { /* comportement inconnu */ }

            var kbs = new List<string>();
            try
            {
                var coll = Get(u, "KBArticleIDs");
                if (coll is not null)
                {
                    int kc = GetInt(coll, "Count");
                    for (int k = 0; k < kc; k++)
                    {
                        var kb = Get(coll, "Item", k)?.ToString();
                        if (!string.IsNullOrWhiteSpace(kb)) kbs.Add("KB" + kb.TrimStart('K', 'B'));
                    }
                }
            }
            catch { /* pas de KB (typique des pilotes) */ }

            string category = browseOnly ? "Optionnelle"
                            : isDriver ? "Pilote"
                            : !string.IsNullOrEmpty(severity) ? "Sécurité"
                            : "Importante";

            string desc = GetStr(u, "Description");
            if (desc.Length > 600) desc = desc[..600] + "…";

            rows.Add(new UpdateRow
            {
                Com = u,
                Title = GetStr(u, "Title"),
                Kind = isDriver ? "Pilote" : "Logiciel",
                Category = category,
                SizeText = size > 0 ? FormatSize(size) : "—",
                RebootText = reboot switch { 0 => "Non", 1 => "Oui", _ => "Possible" },
                KbText = kbs.Count > 0 ? string.Join(", ", kbs) : "—",
                IsDriver = isDriver,
                IsOptional = browseOnly || isDriver,
                EulaAccepted = GetBool(u, "EulaAccepted", true),
                Tooltip = string.IsNullOrWhiteSpace(desc) ? GetStr(u, "Title") : desc,
                // Pré-cochées : ce qui n'est ni optionnel ni pilote (choix prudent —
                // un pilote WU peut être PLUS ancien que celui du fabricant).
                Selected = !(browseOnly || isDriver),
            });
        }

        return (rows.OrderBy(r => r.IsOptional).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase).ToList(), notes);
    }

    // ==================================================================
    // Sélection
    // ==================================================================

    private void BtnSelectImportant_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _rows) r.Selected = !r.IsOptional;
        UpdateSelectionStatus();
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _rows) r.Selected = true;
        UpdateSelectionStatus();
    }

    private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in _rows) r.Selected = false;
        UpdateSelectionStatus();
    }

    private void Row_SelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_busy && e.PropertyName == nameof(UpdateRow.Selected)) UpdateSelectionStatus();
    }

    private void ClearRows()
    {
        foreach (var r in _rows) r.PropertyChanged -= Row_SelectionChanged;
        _rows.Clear();
    }

    private void UpdateSelectionStatus()
    {
        int n = _rows.Count(r => r.Selected);
        TxtStatus.Text = n == 0
            ? "Aucune mise à jour cochée."
            : $"{n} mise(s) à jour cochée(s) — {FormatSize(_rows.Where(r => r.Selected).Sum(SizeOf))} à télécharger.";
    }

    private static long SizeOf(UpdateRow r)
    {
        // La taille est déjà formatée pour l'affichage ; on ne rappelle pas le COM
        // depuis le thread UI. Estimation à partir du texte, uniquement informative.
        var t = r.SizeText;
        if (t.Length < 3 || !char.IsDigit(t[0])) return 0;
        var parts = t.Split(' ');
        if (parts.Length != 2 || !double.TryParse(parts[0], out var v)) return 0;
        return parts[1] switch
        {
            "Ko" => (long)(v * 1024),
            "Mo" => (long)(v * 1024 * 1024),
            "Go" => (long)(v * 1024 * 1024 * 1024),
            _ => (long)v,
        };
    }

    // ==================================================================
    // Téléchargement + installation
    // ==================================================================

    private void BtnInstall_Click(object sender, RoutedEventArgs e) => _ = InstallAsync();

    private async Task InstallAsync()
    {
        if (_busy) return;

        var selected = _rows.Where(r => r.Selected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Coche d'abord au moins une mise à jour dans la liste.",
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool anyReboot = selected.Any(r => r.RebootText != "Non");
        bool anyEula = selected.Any(r => !r.EulaAccepted);

        var sb = new StringBuilder();
        sb.AppendLine($"Installer {selected.Count} mise(s) à jour ?");
        sb.AppendLine();
        foreach (var r in selected.Take(15)) sb.AppendLine($"  • [{r.Category}] {r.Title}");
        if (selected.Count > 15) sb.AppendLine($"  … et {selected.Count - 15} autre(s).");
        sb.AppendLine();
        if (anyEula)
            sb.AppendLine("⚠ Certaines de ces mises à jour ont un contrat de licence : en continuant, tu l'ACCEPTES en ton nom.");
        sb.AppendLine("• Le téléchargement puis l'installation peuvent durer longtemps ; ne coupe pas l'alimentation.");
        sb.AppendLine(anyReboot
            ? "• Un redémarrage sera nécessaire à la fin : FaultTracePC ne le déclenchera PAS, tu redémarreras quand tu voudras."
            : "• Aucun redémarrage n'est annoncé pour cette sélection.");
        sb.AppendLine();
        sb.AppendLine("Conseil : crée d'abord un point de restauration (bouton 🛟 dans la boîte à outils).");

        if (MessageBox.Show(this, sb.ToString(), "FaultTracePC — confirmation",
                MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK)
            return;

        SetBusy(true, "Téléchargement en cours…");
        Log($"=== Installation de {selected.Count} mise(s) à jour ===");

        try
        {
            var report = await Task.Run(() => InstallCore(selected));

            foreach (var line in report.Log) Log(line);
            TxtStatus.Text = report.Summary;

            MessageBox.Show(this, report.Summary +
                (report.RebootRequired
                    ? "\n\n⚠ Un REDÉMARRAGE est nécessaire pour terminer. FaultTracePC ne redémarre jamais l'ordinateur " +
                      "à ta place : enregistre ton travail et redémarre quand tu es prêt."
                    : "\n\nAucun redémarrage n'est réclamé par Windows."),
                "FaultTracePC", MessageBoxButton.OK,
                report.RebootRequired ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // La liste n'est plus à jour : on force une nouvelle recherche.
            ClearRows();
            BtnInstall.IsEnabled = false;
        }
        catch (Exception ex)
        {
            var msg = Unwrap(ex);
            TxtStatus.Text = "L'installation a échoué : " + msg;
            Log("ERREUR : " + msg);
            MessageBox.Show(this,
                "L'installation a échoué.\n\n" + msg +
                "\n\nSi l'erreur persiste, la réparation classique est : boîte à outils → " +
                "« Réinitialiser les composants Windows Update » (purge des caches), puis relancer.",
                "FaultTracePC", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private sealed class InstallReport
    {
        public string Summary { get; set; } = "";
        public bool RebootRequired { get; set; }
        public List<string> Log { get; } = new();
    }

    /// <summary>Exécuté sur un thread MTA : téléchargement puis installation.</summary>
    private InstallReport InstallCore(List<UpdateRow> selected)
    {
        var rep = new InstallReport();
        var session = _session ?? throw new InvalidOperationException(
            "Session Windows Update perdue — relance la recherche.");

        var collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl", throwOnError: false)
            ?? throw new InvalidOperationException("Microsoft.Update.UpdateColl introuvable.");

        // 1) Acceptation des CLUF (le pas a été explicitement confirmé par l'utilisateur).
        foreach (var r in selected)
        {
            try
            {
                if (!GetBool(r.Com, "EulaAccepted", true))
                {
                    Call(r.Com, "AcceptEula");
                    rep.Log.Add("CLUF accepté : " + r.Title);
                }
            }
            catch (Exception ex) { rep.Log.Add("CLUF non accepté (" + Unwrap(ex) + ") : " + r.Title); }
        }

        // 2) Téléchargement de ce qui ne l'est pas déjà.
        var toDownload = (object)Activator.CreateInstance(collType)!;
        int dl = 0;
        foreach (var r in selected)
        {
            if (!GetBool(r.Com, "IsDownloaded")) { Call(toDownload, "Add", r.Com); dl++; }
        }

        if (dl > 0)
        {
            rep.Log.Add($"Téléchargement de {dl} paquet(s)…");
            var downloader = Call(session, "CreateUpdateDownloader")!;
            Set(downloader, "Updates", toDownload);
            var dres = Call(downloader, "Download")!;
            int dcode = GetInt(dres, "ResultCode");
            rep.Log.Add("Téléchargement : " + ResultText(dcode) + $" (HRESULT 0x{GetInt(dres, "HResult"):X8})");
            if (dcode is 4 or 5)
                throw new InvalidOperationException("Le téléchargement a échoué (" + ResultText(dcode) + ").");
        }
        else rep.Log.Add("Tous les paquets étaient déjà téléchargés.");

        // 3) Installation. On garde l'ordre exact d'ajout à la collection COM :
        // c'est lui qui indexe GetUpdateResult(i), pas l'ordre de la sélection.
        var toInstall = (object)Activator.CreateInstance(collType)!;
        var installed = new List<UpdateRow>();
        foreach (var r in selected)
        {
            if (GetBool(r.Com, "IsDownloaded")) { Call(toInstall, "Add", r.Com); installed.Add(r); }
            else rep.Log.Add("Ignorée (non téléchargée) : " + r.Title);
        }
        int ok = installed.Count;
        if (ok == 0) throw new InvalidOperationException("Aucune mise à jour téléchargée : rien à installer.");

        rep.Log.Add($"Installation de {ok} mise(s) à jour…");
        var installer = Call(session, "CreateUpdateInstaller")!;
        Set(installer, "Updates", toInstall);
        var ires = Call(installer, "Install")!;

        int code = GetInt(ires, "ResultCode");
        rep.RebootRequired = GetBool(ires, "RebootRequired");

        int succeeded = 0, failed = 0;
        for (int i = 0; i < ok; i++)
        {
            try
            {
                var ur = Call(ires, "GetUpdateResult", i)!;
                int uc = GetInt(ur, "ResultCode");
                int hr = GetInt(ur, "HResult");
                var title = installed[i].Title;
                if (uc == 2) { succeeded++; rep.Log.Add("✔ " + title); }
                else { failed++; rep.Log.Add($"✘ {title} — {ResultText(uc)} (HRESULT 0x{hr:X8})"); }
            }
            catch { /* résultat détaillé indisponible */ }
        }

        rep.Summary = code switch
        {
            2 => $"{succeeded} mise(s) à jour installée(s) avec succès.",
            3 => $"Installation terminée avec des erreurs : {succeeded} réussie(s), {failed} en échec. "
               + "Ouvre « Détail technique » pour voir lesquelles.",
            5 => "Installation interrompue.",
            _ => $"Installation en échec ({ResultText(code)}) : {succeeded} réussie(s), {failed} en échec.",
        };
        return rep;
    }

    private static string ResultText(int code) => code switch
    {
        0 => "non démarrée",
        1 => "en cours",
        2 => "réussie",
        3 => "réussie avec erreurs",
        4 => "échec",
        5 => "annulée",
        _ => "code " + code,
    };

    // ==================================================================
    // Utilitaires UI
    // ==================================================================

    private void SetBusy(bool busy, string? status)
    {
        _busy = busy;
        PbBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BtnSearch.IsEnabled = !busy;
        BtnInstall.IsEnabled = !busy && _rows.Count > 0;
        BtnSelectAll.IsEnabled = BtnSelectNone.IsEnabled = BtnSelectImportant.IsEnabled = !busy;
        ChkDrivers.IsEnabled = ChkHidden.IsEnabled = !busy;
        LvUpdates.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        if (status is not null) TxtStatus.Text = status;
    }

    private void Log(string line)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:0.#} Go",
        >= 1024L * 1024 => $"{bytes / 1024.0 / 1024:0.#} Mo",
        >= 1024 => $"{bytes / 1024.0:0} Ko",
        > 0 => $"{bytes} o",
        _ => "—",
    };

    /// <summary>Les appels COM tardifs enveloppent l'erreur réelle dans une TargetInvocationException.</summary>
    private static string Unwrap(Exception ex)
    {
        var e = ex;
        while (e is TargetInvocationException { InnerException: not null } tie) e = tie.InnerException!;
        return e is System.Runtime.InteropServices.COMException com
            ? $"{com.Message.Trim()} (HRESULT 0x{com.HResult:X8})"
            : e.Message;
    }
}
