using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Garde-fou de la version bilingue : aucun texte français ne doit pouvoir
/// atteindre l'utilisateur anglophone.
///
/// LA RÈGLE
/// Tout littéral de chaîne qui « a l'air français » doit se trouver à l'intérieur
/// d'un appel <c>Lang.T(...)</c> ou <c>Pick(...)</c>. C'est la forme que prend
/// chaque texte traduit dans ce logiciel ; un fragment laissé dehors est
/// précisément le bug que ce test existe pour attraper — une phrase coupée en
/// deux dont seule la première moitié a été enveloppée, et dont la seconde
/// s'imprime en français au milieu d'une page anglaise.
///
/// TROIS SIGNAUX, ET POURQUOI IL EN FAUT TROIS
///   · un accent ou un chevron « » ;
///   · à défaut, trois mots outils français distincts ;
///   · à défaut, l'espace avant <c>: ; ! ?</c> — une marque typographique
///     FRANÇAISE, que l'anglais n'a pas.
/// Les deux premiers ont laissé passer « Surveillance : ACTIVE » et
/// « Connu de FaultTracePC ? » : ni accent, ni trois mots. Le troisième les
/// attrape tous les deux.
///
/// LES DEUX SOUPAPES
///   · <c>FichiersExemptes</c> — un fichier entier, avec sa raison. Réservé aux
///     tables où les deux langues sont stockées côte à côte dans un enregistrement
///     ou un tuple : la complétude y est vérifiée par un test dédié.
///   · le commentaire <c>// pas-de-traduction : …</c> sur la ligne du littéral ou
///     la ligne juste avant. Pour les cas isolés : clé interne, fragment de la
///     sortie de Windows qu'on reconnaît, CSS.
///
/// Ce test lit les SOURCES, pas les assemblages : c'est le seul moyen de voir un
/// littéral qui n'est jamais exécuté par les autres tests.
/// </summary>
public class TraductionTests
{
    // ==================================================================
    // Exemptions de fichier entier — chacune avec sa raison
    // ==================================================================

    private static readonly Dictionary<string, string> FichiersExemptes = new()
    {
        ["FaultTracePC.Core/Repair/RepairOutput.cs"] =
            "phrases de sfc et DISM relevées dans les .mui de Windows : c'est le texte de "
          + "Windows qu'on reconnaît, pas le nôtre.",
        ["FaultTracePC.Core/Analysis/BugCheckCatalog.cs"] =
            "table (DescriptionFr, DescriptionEn, AdviceFr, AdviceEn) ; la complétude est "
          + "vérifiée par BugCheckCatalogTests.",
        ["FaultTracePC.Core/Analysis/DriverKnowledgeBase.cs"] =
            "table (…Fr, …En) ; la complétude est vérifiée par DriverKnowledgeBaseTests.",
        ["FaultTracePC.App/RunningTools.cs"] =
            "table de tuples (Fr, En) résolus à la lecture par LabelOf : une table "
          + "« static readonly » ne peut pas appeler Lang.T sans figer la langue.",
    };

    /// <summary>Le seul fichier qui a le droit d'écrire un format de date : c'est lui qui les définit.</summary>
    private const string FichierDesFormats = "FaultTracePC.Core/Lang.cs";

    private const string Marqueur = "pas-de-traduction";

    // ==================================================================
    // Les tests
    // ==================================================================

    [Fact]
    public void Aucun_texte_francais_hors_de_Lang_T()
    {
        var src = Path.Combine(RacineDesSources(), "src");
        var fautes = new List<string>();
        int litterauxVus = 0, fichiersVus = 0;

        foreach (var chemin in FichiersSource(src))
        {
            var relatif = Path.GetRelativePath(src, chemin).Replace('\\', '/');
            if (FichiersExemptes.ContainsKey(relatif)) continue;

            fichiersVus++;
            var texte = File.ReadAllText(chemin);
            var litteraux = Litteraux(texte);
            litterauxVus += litteraux.Count;
            var couverts = Couvertures(texte, litteraux);

            foreach (var (debut, fin) in litteraux)
            {
                // Les TROUS d'interpolation sont du code, pas du texte : sans les
                // retirer, chaque ternaire « cond ? "a" : "b" » ressemblerait à une
                // ponctuation française.
                var contenu = TexteSansTrous(texte[debut..fin]);
                if (!SembleFrancais(contenu)) continue;
                if (couverts.Any(c => c.Debut <= debut && fin <= c.Fin)) continue;
                if (MarqueSurPlace(texte, debut)) continue;

                fautes.Add($"{relatif}:{Ligne(texte, debut)} → {Extrait(contenu)}");
            }
        }

        // Garde-fou du garde-fou : un scanner qui ne trouve plus rien passerait
        // le test sans rien vérifier. On exige donc qu'il ait vu la matière.
        Assert.True(fichiersVus >= 30, $"seulement {fichiersVus} fichier(s) source analysé(s)");
        Assert.True(litterauxVus >= 3000, $"seulement {litterauxVus} littéral(aux) analysé(s)");

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} littéral(aux) français hors de Lang.T :\n  " + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Aucun_texte_francais_en_dur_dans_le_XAML()
    {
        // Deux endroits, pas un : un libellé peut être écrit en ATTRIBUT
        // (Text="…") ou en CONTENU d'élément (<TextBlock>…</TextBlock>). La
        // fenêtre Réseau portait la seconde forme, invisible à un test qui ne
        // lisait que les attributs.
        var src = Path.Combine(RacineDesSources(), "src");
        var attributs = new Regex(
            "\\b(Text|Content|Header|Title|ToolTip|Watermark)\\s*=\\s*\"([^\"]*)\"",
            RegexOptions.Compiled);
        var commentaires = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
        var contenu = new Regex(">([^<>]+)<", RegexOptions.Compiled);

        var fautes = new List<string>();
        int fichiersVus = 0;

        foreach (var chemin in Directory.EnumerateFiles(src, "*.xaml", SearchOption.AllDirectories))
        {
            if (EstEngendre(chemin)) continue;
            fichiersVus++;
            var brut = File.ReadAllText(chemin);
            var texte = commentaires.Replace(brut, "");
            var relatif = Path.GetRelativePath(src, chemin).Replace('\\', '/');

            foreach (Match m in attributs.Matches(texte))
            {
                var valeur = m.Groups[2].Value;
                if (valeur.StartsWith("{", StringComparison.Ordinal)) continue;  // liaison ou x:Static
                if (!SembleFrancais(valeur)) continue;
                fautes.Add($"{relatif} (attribut) → {Extrait(valeur)}");
            }

            foreach (Match m in contenu.Matches(texte))
            {
                var valeur = m.Groups[1].Value.Trim();
                if (valeur.Length < 8) continue;
                if (!SembleFrancais(valeur)) continue;
                fautes.Add($"{relatif} (contenu) → {Extrait(valeur)}");
            }
        }

        Assert.True(fichiersVus >= 5, $"seulement {fichiersVus} fichier(s) XAML analysé(s)");
        Assert.True(fautes.Count == 0,
            $"{fautes.Count} libellé(s) français en dur dans le XAML :\n  " + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Aucun_format_de_date_ecrit_en_dur()
    {
        // Une date au format jj/mm dans un rapport anglais n'est pas seulement
        // dépaysante : un lecteur américain lit 03/08 comme le 8 mars. Le format
        // se décide dans Lang, et nulle part ailleurs.
        var src = Path.Combine(RacineDesSources(), "src");
        var motif = new Regex("dd/MM|dd-MM|MM/dd", RegexOptions.Compiled);
        var fautes = new List<string>();

        foreach (var chemin in FichiersSource(src))
        {
            var relatif = Path.GetRelativePath(src, chemin).Replace('\\', '/');
            if (relatif == FichierDesFormats) continue;

            var texte = File.ReadAllText(chemin);
            var litteraux = Litteraux(texte);
            var couverts = Couvertures(texte, litteraux);

            foreach (var (debut, fin) in litteraux)
            {
                if (!motif.IsMatch(texte[debut..fin])) continue;
                if (couverts.Any(c => c.Debut <= debut && fin <= c.Fin)) continue;
                if (MarqueSurPlace(texte, debut)) continue;

                fautes.Add($"{relatif}:{Ligne(texte, debut)} → {Extrait(texte[debut..fin])}");
            }
        }

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} format(s) de date écrit(s) en dur — utilise Lang.Date / Lang.ShortDateMinute :\n  "
            + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Aucun_balisage_de_structure_non_traduit()
    {
        // Règle STRUCTURELLE, sans détection de langue : un littéral qui porte un
        // titre, un en-tête de tableau ou un paragraphe d'explication et qui
        // contient des mots doit être traduit, point.
        //
        // Elle existe parce que la détection de langue ne peut rien pour un
        // libellé court : « Pilotes tiers actifs » n'a ni accent, ni espace avant
        // une ponctuation, et un seul mot outil là où il en faut trois. Il est
        // resté en français dans le rapport anglais jusqu'à ce qu'un lecteur le
        // voie. Ici, aucun faux positif n'est possible : on ne devine rien.
        var src = Path.Combine(RacineDesSources(), "src");
        var balise = new Regex(
            "<(h1|h2|h3|h4|th|caption|figcaption)[ >]|class=\\\\?\"(explain|empty|legend)\\\\?\"",
            RegexOptions.Compiled);
        var horsBalises = new Regex("<[^>]*>", RegexOptions.Compiled);
        var mot = new Regex("[A-Za-zÀ-ÿ]{3,}", RegexOptions.Compiled);

        var fautes = new List<string>();

        foreach (var chemin in FichiersSource(src))
        {
            var texte = File.ReadAllText(chemin);
            var relatif = Path.GetRelativePath(src, chemin).Replace('\\', '/');
            var litteraux = Litteraux(texte);
            var couverts = Couvertures(texte, litteraux);

            foreach (var (debut, fin) in litteraux)
            {
                var brut = texte[debut..fin];
                if (!balise.IsMatch(brut)) continue;
                if (couverts.Any(c => c.Debut <= debut && fin <= c.Fin)) continue;
                if (MarqueSurPlace(texte, debut)) continue;

                // Ce qui reste une fois les balises retirées : le texte lu par un
                // humain. Un littéral purement structurel n'en a aucun.
                var visible = horsBalises.Replace(TexteSansTrous(brut), " ");
                if (!mot.IsMatch(visible)) continue;

                fautes.Add($"{relatif}:{Ligne(texte, debut)} → {Extrait(visible)}");
            }
        }

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} balisage(s) de structure portant du texte non traduit :\n  "
            + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Aucune_moitie_anglaise_ecrasee()
    {
        // Une moitié anglaise sans rapport de longueur avec la française est le
        // signe qu'un outil de réécriture a écrasé la traduction. C'est arrivé.
        var src = Path.Combine(RacineDesSources(), "src");
        var fautes = new List<string>();

        foreach (var chemin in FichiersSource(src))
        {
            var texte = File.ReadAllText(chemin);
            var litteraux = Litteraux(texte);
            var relatif = Path.GetRelativePath(src, chemin).Replace('\\', '/');

            var dedans = new bool[texte.Length];
            foreach (var l in litteraux)
                for (int i = l.Debut; i < l.Fin; i++) dedans[i] = true;

            foreach (var appel in Couvertures(texte, litteraux))
            {
                if (string.CompareOrdinal(texte, appel.Debut, "Lang.T(", 0, 7) != 0) continue;
                int sep = PremiereVirguleDeNiveauZero(texte, dedans, appel);
                if (sep < 0) continue;

                int fr = litteraux.Where(l => l.Debut > appel.Debut && l.Fin <= sep).Sum(l => l.Fin - l.Debut);
                int en = litteraux.Where(l => l.Debut > sep && l.Fin <= appel.Fin).Sum(l => l.Fin - l.Debut);

                if (fr > 25 && en * 100 < fr * 40)
                    fautes.Add($"{relatif}:{Ligne(texte, appel.Debut)} → fr={fr} caractères, en={en}");
            }
        }

        Assert.True(fautes.Count == 0,
            $"{fautes.Count} appel(s) Lang.T dont la moitié anglaise semble écrasée :\n  "
            + string.Join("\n  ", fautes));
    }

    [Fact]
    public void Les_exemptions_de_fichier_designent_toutes_un_fichier_existant()
    {
        // Une exemption devenue obsolète est un trou silencieux dans le garde-fou :
        // le fichier a été renommé, l'exemption ne protège plus rien, et personne
        // ne s'en aperçoit.
        var src = Path.Combine(RacineDesSources(), "src");
        foreach (var (relatif, raison) in FichiersExemptes)
        {
            Assert.True(File.Exists(Path.Combine(src, relatif.Replace('/', Path.DirectorySeparatorChar))),
                $"exemption obsolète : {relatif} n'existe plus");
            Assert.False(string.IsNullOrWhiteSpace(raison), $"exemption sans raison : {relatif}");
        }
        Assert.True(File.Exists(Path.Combine(src, FichierDesFormats.Replace('/', Path.DirectorySeparatorChar))),
            $"exemption obsolète : {FichierDesFormats} n'existe plus");
    }

    [Fact]
    public void Le_detecteur_de_francais_fait_la_difference()
    {
        // Sans ces contrôles, une expression régulière cassée rendrait le test
        // vert en permanence.
        Assert.True(SembleFrancais("Le disque système est défaillant"));
        Assert.True(SembleFrancais("« Je ne sais pas ce que j'ai »"));
        Assert.True(SembleFrancais("Le pilote de la carte est en jeu"));        // sans accent
        Assert.True(SembleFrancais("📡  Surveillance : ACTIVE"));               // typographie seule
        Assert.True(SembleFrancais("<th>Connu de FaultTracePC ?</th>"));        // typographie seule

        Assert.False(SembleFrancais("The system disk is failing"));
        Assert.False(SembleFrancais("nvlddmkm.sys"));
        Assert.False(SembleFrancais("SELECT * FROM Win32_PageFileUsage"));
        Assert.False(SembleFrancais("<td class=\"small\">"));
        Assert.False(SembleFrancais("Monitoring: ACTIVE"));                     // deux-points collé
        Assert.False(SembleFrancais("Analyse this machine"));                   // mots communs aux deux langues
    }

    [Fact]
    public void Les_trous_d_interpolation_sont_retires_du_texte()
    {
        // Le ternaire d'un trou porte un « ? » et un « : » entourés d'espaces :
        // sans ce retrait, la règle typographique déclencherait sur tout le code.
        Assert.Equal("Etat  fini", TexteSansTrous("$\"Etat {(ok ? \"oui\" : \"non\")} fini\""));
        Assert.Equal("brut : reste", TexteSansTrous("\"brut : reste\""));
        Assert.Equal("accolade { littérale", TexteSansTrous("$\"accolade {{ littérale\""));

        Assert.False(SembleFrancais(TexteSansTrous("$\"Rights: {(admin ? \"yes\" : \"NO\")}\"")));
        Assert.True(SembleFrancais(TexteSansTrous("$\"Total : {n} fichiers\"")));
    }

    [Fact]
    public void Le_decoupage_des_litteraux_couvre_les_formes_du_langage()
    {
        // Les quatre formes qui existent réellement dans ce dépôt, plus le piège
        // du trou d'interpolation contenant lui-même des guillemets.
        const string code = """
            var a = "simple";
            var b = $"trou {string.Join(", ", x)} fin";
            var c = @"verbatim ""échappé"" ici";
            var d = "avec \" une échappée";
            // "commentaire ignoré"
            var e = 'x';
            """;

        var trouves = Litteraux(code).Select(p => code[p.Debut..p.Fin]).ToList();

        Assert.Equal(4, trouves.Count);
        Assert.Equal("\"simple\"", trouves[0]);
        Assert.Equal("$\"trou {string.Join(\", \", x)} fin\"", trouves[1]);
        Assert.Equal("@\"verbatim \"\"échappé\"\" ici\"", trouves[2]);
        Assert.Equal("\"avec \\\" une échappée\"", trouves[3]);
    }

    [Fact]
    public void La_couverture_reconnait_un_appel_Lang_T_multiligne()
    {
        const string code = """
            var x = Lang.T("Analyse terminée : " + n + " résultats",
                           "Analysis finished: " + n + " results");
            var y = "Analyse abandonnée";
            """;

        var litteraux = Litteraux(code);
        var couverts = Couvertures(code, litteraux);

        var dehors = litteraux
            .Where(l => SembleFrancais(code[l.Debut..l.Fin]))
            .Where(l => !couverts.Any(c => c.Debut <= l.Debut && l.Fin <= c.Fin))
            .Select(l => code[l.Debut..l.Fin])
            .ToList();

        Assert.Single(dehors);
        Assert.Equal("\"Analyse abandonnée\"", dehors[0]);
    }

    // ==================================================================
    // Localisation des sources
    // ==================================================================

    private static string RacineDesSources()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FaultTracePC.slnx")))
                return dir.FullName;

        Assert.Fail("sources introuvables depuis " + AppContext.BaseDirectory
                  + " : ce test lit le code source, il doit tourner dans le dépôt.");
        return "";
    }

    private static IEnumerable<string> FichiersSource(string src) =>
        Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                 .Where(f => !EstEngendre(f))
                 .OrderBy(f => f, StringComparer.Ordinal);

    private static bool EstEngendre(string chemin)
    {
        var p = chemin.Replace('\\', '/');
        return p.Contains("/bin/", StringComparison.Ordinal)
            || p.Contains("/obj/", StringComparison.Ordinal);
    }

    // ==================================================================
    // Détection du français
    // ==================================================================

    private static readonly Regex Accent =
        new("[àâäéèêëîïôöùûüÿçÀÂÄÉÈÊËÎÏÔÖÙÛÜŸÇ]|«|»", RegexOptions.Compiled);

    /// <summary>
    /// Espace — ordinaire, insécable ou insécable fine — avant <c>: ; ! ?</c>.
    /// C'est la règle typographique française ; l'anglais colle la ponctuation au
    /// mot. Signal très sûr, à condition d'avoir retiré les trous d'interpolation.
    /// </summary>
    private static readonly Regex TypographieFrancaise =
        new("[ \u00A0\u202F][:;!?](?![\\w])", RegexOptions.Compiled);

    /// <summary>
    /// Mots outils français. Aucun mot qui existe AUSSI en anglais n'y figure —
    /// « analyse », « machine », « cause », « plus », « son », « non » en ont été
    /// retirés : un mot commun aux deux langues n'apporte aucun signal, et fait
    /// prendre une phrase anglaise pour du français.
    /// </summary>
    private static readonly Regex MotsFrancais = new(
        "(?i)(?<![\\w-])(le|la|les|un|une|des|du|de|et|ou|est|sont|pas|pour|avec|sur|dans"
      + "|par|aux|cette|ces|vous|votre|nous|qui|que|aucun|aucune|tous|toutes|sans|mais"
      + "|ses|fait|faire|voir|peut|doit|depuis|encore|toujours|jamais|erreur|fichier"
      + "|fichiers|disque|pilote|pilotes|rapport|ordinateur)(?![\\w-])",
        RegexOptions.Compiled);

    /// <summary>
    /// Un accent, un chevron, ou l'espace avant la ponctuation suffisent. À
    /// défaut, trois mots outils français DISTINCTS : deux se rencontrent par
    /// hasard dans du HTML ou de l'anglais (« la », « de »), trois beaucoup moins.
    /// </summary>
    internal static bool SembleFrancais(string texte)
    {
        if (Accent.IsMatch(texte)) return true;
        if (TypographieFrancaise.IsMatch(texte)) return true;

        var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in MotsFrancais.Matches(texte))
        {
            vus.Add(m.Value);
            if (vus.Count >= 3) return true;
        }
        return false;
    }

    // ==================================================================
    // Découpage des littéraux — portage fidèle de l'outil de traduction
    // ==================================================================

    internal readonly record struct Intervalle(int Debut, int Fin);

    /// <summary>
    /// Positions de chaque littéral de chaîne, délimiteurs et préfixe compris.
    /// Gère "", $"", @"", $@"", les chaînes brutes """ """, les échappements, et
    /// les guillemets imbriqués dans un trou d'interpolation (légaux depuis C# 11).
    /// Les commentaires et les littéraux de caractère sont sautés.
    /// </summary>
    internal static List<Intervalle> Litteraux(string src)
    {
        var res = new List<Intervalle>();
        int i = 0, n = src.Length;

        while (i < n)
        {
            char c = src[i];

            if (c == '/' && i + 1 < n && src[i + 1] == '/')
            {
                int f = src.IndexOf('\n', i);
                i = f < 0 ? n : f;
                continue;
            }
            if (c == '/' && i + 1 < n && src[i + 1] == '*')
            {
                int f = src.IndexOf("*/", i, StringComparison.Ordinal);
                i = f < 0 ? n : f + 2;
                continue;
            }
            if (c == '\'')
            {
                int j0 = i + 1;
                while (j0 < n)
                {
                    if (src[j0] == '\\') { j0 += 2; continue; }
                    if (src[j0] == '\'') break;
                    j0++;
                }
                i = j0 + 1;
                continue;
            }

            int k = i;
            while (k < n && (src[k] == '$' || src[k] == '@')) k++;
            if (k >= n || src[k] != '"') { i++; continue; }

            var prefixe = src[i..k];
            bool interpole = prefixe.Contains('$');
            bool verbatim = prefixe.Contains('@');

            int q = 0;
            while (k + q < n && src[k + q] == '"') q++;

            if (q >= 3)                                   // chaîne brute
            {
                var cloture = new string('"', q);
                int f = src.IndexOf(cloture, k + q, StringComparison.Ordinal);
                int fin0 = f < 0 ? n : f + q;
                res.Add(new Intervalle(i, fin0));
                i = fin0;
                continue;
            }

            int j = k + 1, prof = 0;
            while (j < n)
            {
                char ch = src[j];
                if (prof == 0)
                {
                    if (verbatim && ch == '"')
                    {
                        if (j + 1 < n && src[j + 1] == '"') { j += 2; continue; }
                        j++; break;
                    }
                    if (!verbatim && ch == '\\') { j += 2; continue; }
                    if (!verbatim && ch == '"') { j++; break; }
                    if (interpole && ch == '{')
                    {
                        if (j + 1 < n && src[j + 1] == '{') { j += 2; continue; }
                        prof = 1; j++; continue;
                    }
                }
                else
                {
                    if (ch == '{') prof++;
                    else if (ch == '}') prof--;
                    else if (ch == '"')
                    {
                        j++;
                        while (j < n && src[j] != '"') j += src[j] == '\\' ? 2 : 1;
                    }
                    j++;
                    continue;
                }
                j++;
            }

            res.Add(new Intervalle(i, j));
            i = j;
        }

        return res;
    }

    /// <summary>
    /// Contenu d'un littéral, délimiteurs et TROUS D'INTERPOLATION retirés. Ce
    /// qui reste est du texte destiné à un humain ; ce qui part est du code.
    /// </summary>
    internal static string TexteSansTrous(string litteral)
    {
        var noyau = litteral.TrimStart('$', '@');
        bool interpole = litteral.Length > noyau.Length
                      && litteral[..^noyau.Length].Contains('$');

        if (noyau.StartsWith("\"\"\"", StringComparison.Ordinal)) noyau = noyau.Trim('"');
        else if (noyau.Length >= 2) noyau = noyau[1..^1];

        if (!interpole) return noyau;

        var sortie = new StringBuilder();
        int prof = 0;
        for (int i = 0; i < noyau.Length; i++)
        {
            char c = noyau[i];
            if (prof == 0)
            {
                if (c == '{')
                {
                    if (i + 1 < noyau.Length && noyau[i + 1] == '{') { sortie.Append('{'); i++; continue; }
                    prof = 1;
                    continue;
                }
                if (c == '}' && i + 1 < noyau.Length && noyau[i + 1] == '}') { sortie.Append('}'); i++; continue; }
                sortie.Append(c);
            }
            else
            {
                if (c == '{') prof++;
                else if (c == '}') prof--;
            }
        }
        return sortie.ToString();
    }

    /// <summary>
    /// Intervalles des appels <c>Lang.T(...)</c> et <c>Pick(...)</c>, parenthèses
    /// appariées. L'intérieur des littéraux est ignoré pendant l'appariement,
    /// sinon une parenthèse dans une phrase déséquilibrerait le compte.
    /// </summary>
    internal static List<Intervalle> Couvertures(string src, List<Intervalle> litteraux)
    {
        var dedans = new bool[src.Length];
        foreach (var l in litteraux)
            for (int i = l.Debut; i < l.Fin; i++) dedans[i] = true;

        var res = new List<Intervalle>();
        string[] appels = ["Lang.T(", "Pick("];
        int p = 0, n = src.Length;

        while (p < n)
        {
            if (dedans[p]) { p++; continue; }
            if (src[p] == '/' && p + 1 < n && src[p + 1] == '/')
            {
                int f = src.IndexOf('\n', p); p = f < 0 ? n : f; continue;
            }
            if (src[p] == '/' && p + 1 < n && src[p + 1] == '*')
            {
                int f = src.IndexOf("*/", p, StringComparison.Ordinal); p = f < 0 ? n : f + 2; continue;
            }

            var trouve = appels.FirstOrDefault(a => string.CompareOrdinal(src, p, a, 0, a.Length) == 0);
            if (trouve is null) { p++; continue; }

            // « Pick( » ne doit pas se déclencher sur la fin d'un autre identifiant.
            if (trouve == "Pick(" && p > 0 && (char.IsLetterOrDigit(src[p - 1]) || src[p - 1] is '_' or '.'))
            {
                p++; continue;
            }

            int j = p + trouve.Length, prof = 1;
            while (j < n && prof > 0)
            {
                if (dedans[j]) { j++; continue; }
                if (src[j] == '(') prof++;
                else if (src[j] == ')') prof--;
                j++;
            }
            res.Add(new Intervalle(p, j));
            p = j;
        }

        return res;
    }

    /// <summary>Position de la virgule qui sépare les deux langues, ou -1.</summary>
    private static int PremiereVirguleDeNiveauZero(string src, bool[] dedans, Intervalle appel)
    {
        int prof = 0;
        for (int i = appel.Debut + "Lang.T(".Length; i < appel.Fin; i++)
        {
            if (dedans[i]) continue;
            char c = src[i];
            if (c is '(' or '[' or '{') prof++;
            else if (c is ')' or ']' or '}')
            {
                if (prof == 0) return -1;
                prof--;
            }
            else if (c == ',' && prof == 0) return i;
        }
        return -1;
    }

    // ==================================================================
    // Petits utilitaires
    // ==================================================================

    /// <summary>Le marqueur est accepté sur la ligne du littéral ou celle d'avant.</summary>
    private static bool MarqueSurPlace(string src, int position)
    {
        int debutLigne = src.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
        int debutPrecedente = debutLigne == 0
            ? 0
            : src.LastIndexOf('\n', Math.Max(0, debutLigne - 2)) + 1;

        int finLigne = src.IndexOf('\n', position);
        if (finLigne < 0) finLigne = src.Length;

        return src[debutPrecedente..finLigne].Contains(Marqueur, StringComparison.Ordinal);
    }

    private static int Ligne(string src, int position)
    {
        int n = 1;
        for (int i = 0; i < position && i < src.Length; i++)
            if (src[i] == '\n') n++;
        return n;
    }

    private static string Extrait(string brut)
    {
        var t = new StringBuilder();
        foreach (var ch in brut)
            t.Append(ch is '\n' or '\r' ? ' ' : ch);
        var s = t.ToString().Trim();
        return s.Length <= 90 ? s : s[..90] + "…";
    }
}
