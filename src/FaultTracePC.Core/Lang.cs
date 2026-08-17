using System.Globalization;

namespace FaultTracePC.Core;

/// <summary>Langues dans lesquelles FaultTracePC sait s'exprimer.</summary>
public enum AppLanguage
{
    French,
    English,
}

/// <summary>
/// Choix de la langue de l'interface et des rapports.
///
/// ORDRE DE RÉSOLUTION, du plus fort au plus faible :
///   1. l'argument de ligne de commande « --lang fr|en|auto » (ou « --langue ») ;
///   2. la préférence enregistrée par l'utilisateur (Documents\FaultTracePC\langue.txt) ;
///   3. la langue d'affichage de la session Windows ;
///   4. le français, si rien de tout cela n'a pu être déterminé.
///
/// POURQUOI LA PRÉFÉRENCE EST UN FICHIER DANS « Documents »
/// Sous Windows, la langue d'affichage est un réglage PAR UTILISATEUR
/// (GetUserPreferredUILanguages), pas par machine : deux comptes du même poste
/// peuvent parfaitement travailler dans deux langues. La préférence suit donc le
/// profil, exactement comme « maj.txt » qui garde déjà le réglage « vérifier au
/// démarrage ». Une première tentative écrivait ce marqueur à côté de
/// l'exécutable : sur un poste partagé — le cas normal en établissement — le
/// premier utilisateur imposait sa langue à tous les autres.
///
/// CE QUE CETTE CLASSE NE FAIT PAS
/// Elle ne touche PAS à <c>CultureInfo.DefaultThreadCurrentCulture</c>. Basculer
/// la culture globale ne changerait pas que la langue : le séparateur décimal,
/// les formats de date et l'analyse de chaînes changeraient partout à la fois,
/// y compris dans du code de collecte qui formate ou lit des nombres sans
/// préciser de culture. Traduire ne doit pas modifier silencieusement la façon
/// dont un nombre est lu. La culture retenue est donc simplement EXPOSÉE
/// (<see cref="Culture"/>), à utiliser explicitement là où l'on met en forme une
/// date ou un nombre destiné à être lu.
/// </summary>
public static class Lang
{
    /// <summary>Langue effectivement retenue. Vaut français tant que <see cref="Initialize"/> n'a pas été appelé.</summary>
    public static AppLanguage Current { get; private set; } = AppLanguage.French;

    public static bool IsFrench => Current == AppLanguage.French;

    /// <summary>
    /// Culture à utiliser explicitement pour mettre en forme dates et nombres
    /// destinés à être lus. « en-GB » plutôt que « en-US » : le lectorat
    /// anglophone est mondial, et un « 03/04/2026 » au format américain se lit
    /// à l'envers partout ailleurs — jour d'abord et heures sur 24 lèvent le
    /// doute pour la majorité des lecteurs.
    /// </summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Renvoie le texte français ou anglais selon la langue retenue.</summary>
    public static string T(string fr, string en) => Current == AppLanguage.French ? fr : en;

    /// <summary>
    /// Comme <see cref="T(string,string)"/>, mais dans une langue IMPOSÉE. Sert au
    /// seul cas où la langue en cours est la mauvaise réponse : la question
    /// « redémarrer pour appliquer ? » doit se lire dans la langue que
    /// l'utilisateur vient de choisir, pas dans celle qu'il quitte.
    /// </summary>
    public static string T(AppLanguage language, string fr, string en) =>
        language == AppLanguage.French ? fr : en;

    // ------------------------------------------------------------------
    // Initialisation
    // ------------------------------------------------------------------

    /// <summary>
    /// À appeler une seule fois, au tout début du démarrage, avant d'afficher
    /// ou d'écrire quoi que ce soit. Ne lève jamais : une langue mal déterminée
    /// ne doit pas empêcher un diagnostic de tourner.
    /// </summary>
    public static void Initialize(string[]? args = null) =>
        Apply(Resolve(args, ReadPreferenceRaw(), ReadMachinePreferenceRaw(), SessionCulture()));

    /// <summary>
    /// CurrentUICulture = langue d'AFFICHAGE (les menus de Windows).
    /// CurrentCulture = format des dates et des nombres, qu'un utilisateur français
    /// travaillant sur un Windows anglais laisse souvent en français. C'est bien la
    /// première qui dit dans quelle langue lire. Renvoie null si indéterminable —
    /// la résolution retombe alors sur le français.
    /// </summary>
    private static string? SessionCulture()
    {
        try { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; }
        catch { return null; }
    }

    /// <summary>
    /// Langue qui s'appliquerait au prochain démarrage si la préférence
    /// enregistrée valait <paramref name="preference"/> (null = automatique).
    ///
    /// Le sélecteur de l'interface s'en sert pour savoir s'il doit proposer un
    /// redémarrage : sur un Windows français déjà affiché en français, choisir
    /// « automatique » ne change rien, et proposer de redémarrer serait absurde.
    /// </summary>
    public static AppLanguage Effective(AppLanguage? preference) =>
        Resolve(null, Code(preference), ReadMachinePreferenceRaw(), SessionCulture());

    // ------------------------------------------------------------------
    // Formats de date
    // ------------------------------------------------------------------

    /// <summary>
    /// Le français écrit jj/mm/aaaa. L'anglais prend la forme ISO aaaa-mm-jj, et
    /// non le jj/mm/aaaa britannique : un lecteur américain lirait ce dernier à
    /// l'envers sans s'en apercevoir. Dans un rapport de panne, une date qu'on
    /// peut lire à l'envers vaut moins qu'une date absente.
    /// </summary>
    public static string DateFormat => IsFrench ? "dd/MM/yyyy" : "yyyy-MM-dd";

    /// <summary>Sans l'année, pour les tableaux denses : jj/mm en français, mm-jj en anglais.</summary>
    public static string ShortDateFormat => IsFrench ? "dd/MM" : "MM-dd";

    public static string Date(DateTime t) => t.ToString(DateFormat, Culture);
    public static string DateMinute(DateTime t) => t.ToString(DateFormat + " HH:mm", Culture);
    public static string ShortDateMinute(DateTime t) => t.ToString(ShortDateFormat + " HH:mm", Culture);
    public static string ShortDateSecond(DateTime t) => t.ToString(ShortDateFormat + " HH:mm:ss", Culture);
    public static string ShortDateHour(DateTime t) => t.ToString(ShortDateFormat + " HH", Culture);

    /// <summary>« fr », « en » ou « auto » — la forme exacte écrite dans langue.txt.</summary>
    public static string Code(AppLanguage? preference) => preference switch
    {
        AppLanguage.French => "fr",
        AppLanguage.English => "en",
        _ => "auto",
    };

    /// <summary>Force la langue en cours d'exécution (sélecteur dans l'application). N'enregistre rien.</summary>
    public static void Apply(AppLanguage language)
    {
        Current = language;
        Culture = CultureInfo.GetCultureInfo(language == AppLanguage.French ? "fr-FR" : "en-GB");
    }

    /// <summary>
    /// Règle de décision pure — sans fichier ni environnement, donc testable.
    /// <paramref name="storedRaw"/> est le contenu brut du fichier de préférence
    /// (null s'il n'existe pas), <paramref name="sessionCulture"/> le code à deux
    /// lettres de la session (null si indéterminable).
    /// </summary>
    internal static AppLanguage Resolve(IEnumerable<string>? args, string? storedRaw, string? machineRaw, string? sessionCulture)
    {
        var forced = FromArguments(args);
        if (forced == "fr") return AppLanguage.French;
        if (forced == "en") return AppLanguage.English;

        // « --lang auto » demande explicitement de suivre Windows : on saute
        // alors les deux préférences enregistrées au lieu de les laisser gagner.
        if (forced != "auto")
        {
            // L'UTILISATEUR d'abord. Un réglage d'administrateur est un défaut,
            // pas une contrainte : contredire un choix explicite ferait passer le
            // sélecteur de l'application pour cassé.
            var stored = NormalizeCode(storedRaw);
            if (stored == "fr") return AppLanguage.French;
            if (stored == "en") return AppLanguage.English;

            // Puis la MACHINE, avant la session : c'est précisément quand la
            // session dit « français » et que l'administrateur veut l'anglais que
            // ce réglage existe.
            var machine = NormalizeCode(machineRaw);
            if (machine == "fr") return AppLanguage.French;
            if (machine == "en") return AppLanguage.English;
        }

        // Rien de déterminable : le français est resté la seule langue du
        // logiciel jusqu'à la 1.2.3, c'est le repli qui ne surprend personne.
        if (sessionCulture is null) return AppLanguage.French;

        return sessionCulture.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.French
            : AppLanguage.English;
    }

    /// <summary>
    /// Extrait « --lang &lt;code&gt; », « --lang=&lt;code&gt; » ou leur équivalent
    /// français « --langue ». Renvoie « fr », « en », « auto », ou null si
    /// l'argument est absent ou incompréhensible — un code inconnu est ignoré,
    /// jamais une erreur : une tâche planifiée ne doit pas échouer sur ça.
    /// </summary>
    internal static string? FromArguments(IEnumerable<string>? args)
    {
        if (args is null) return null;

        string? attenduSuivant = null;
        foreach (var brut in args)
        {
            if (brut is null) continue;

            if (attenduSuivant is not null)
            {
                var v = NormalizeCode(brut);
                attenduSuivant = null;
                if (v is not null) return v;
                continue;
            }

            var a = brut.Trim();
            var sep = a.IndexOfAny(['=', ':']);
            var nom = (sep >= 0 ? a[..sep] : a).ToLowerInvariant();

            if (nom is not ("--lang" or "--langue" or "-lang" or "/lang" or "/langue")) continue;

            if (sep >= 0)
            {
                var v = NormalizeCode(a[(sep + 1)..]);
                if (v is not null) return v;
            }
            else
            {
                attenduSuivant = nom; // la valeur est dans l'argument suivant
            }
        }
        return null;
    }

    /// <summary>
    /// « FR », « fr-FR », « french », « français » → « fr ». Sinon « en », « auto »,
    /// ou null si le code est incompréhensible. Publique parce que la CLI en a
    /// besoin pour valider « --set-machine-lang » : deux analyses parallèles du
    /// même code finiraient par diverger.
    /// </summary>
    public static string? NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant();
        if (s == "auto") return "auto";
        if (s.StartsWith("fr", StringComparison.Ordinal) || s.StartsWith("fran", StringComparison.Ordinal)) return "fr";
        if (s.StartsWith("en", StringComparison.Ordinal) || s.StartsWith("angl", StringComparison.Ordinal)) return "en";
        return null;
    }

    // ------------------------------------------------------------------
    // Préférence utilisateur
    // ------------------------------------------------------------------

    /// <summary>
    /// Emplacement du réglage. Même dossier que le reste des données de
    /// l'utilisateur. Sous le service Windows (compte SYSTEM) ce chemin pointe
    /// vers le profil système : aucun fichier n'y sera trouvé, et le service
    /// suivra donc la langue par défaut de la machine — ce qui est le
    /// comportement voulu pour un processus qui n'appartient à personne.
    /// </summary>
    public static string PreferencePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FaultTracePC", "langue.txt");

    /// <summary>
    /// Préférence enregistrée : null signifie « automatique » (suivre Windows),
    /// ce qui est aussi l'état par défaut tant que rien n'a été choisi.
    /// </summary>
    public static AppLanguage? Preference
    {
        get => NormalizeCode(ReadPreferenceRaw()) switch
        {
            "fr" => AppLanguage.French,
            "en" => AppLanguage.English,
            _ => null,
        };
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
                File.WriteAllText(PreferencePath, Code(value));
            }
            catch { /* réglage de confort : un profil en lecture seule ne doit rien casser */ }
        }
    }

    private static string? ReadPreferenceRaw()
    {
        try { return File.Exists(PreferencePath) ? File.ReadAllText(PreferencePath) : null; }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // Préférence de portée MACHINE
    // ------------------------------------------------------------------

    /// <summary>
    /// Réglage posé par l'installeur ou par un administrateur, valable pour tous
    /// les comptes du poste.
    ///
    /// Il ne sert pas qu'au déploiement : le service de surveillance tourne sous
    /// le compte SYSTEM, dont le dossier Documents ne contient jamais le fichier
    /// de l'utilisateur. Sans ce réglage machine, le service suit toujours la
    /// langue par défaut du poste, quoi que l'administrateur ait décidé — et les
    /// alertes qu'il écrit en héritent.
    /// </summary>
    public static string MachinePreferencePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FaultTracePC", "langue.txt");

    /// <summary>Préférence machine ; null signifie « rien d'imposé ».</summary>
    public static AppLanguage? MachinePreference
    {
        get => NormalizeCode(ReadMachinePreferenceRaw()) switch
        {
            "fr" => AppLanguage.French,
            "en" => AppLanguage.English,
            _ => null,
        };
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MachinePreferencePath)!);
                File.WriteAllText(MachinePreferencePath, Code(value));
            }
            catch { /* sans droits d'administrateur : on n'impose rien, on ne casse rien */ }
        }
    }

    private static string? ReadMachinePreferenceRaw()
    {
        try { return File.Exists(MachinePreferencePath) ? File.ReadAllText(MachinePreferencePath) : null; }
        catch { return null; }
    }
}
