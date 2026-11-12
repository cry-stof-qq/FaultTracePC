using System.Globalization;
using System.Text.RegularExpressions;

namespace FaultTracePC.Core;

/// <summary>
/// POINT 121 (validé par l'auteur le 12/11/2026) — l'ordre de deux cases d'une colonne
/// de la console de parc, quand on clique sur son en-tête.
///
/// Les colonnes affichent du TEXTE (« 51,6 °C », « 48 (14 ⛔) », « S2-00-10 »). Trié
/// comme du texte, « 10 » passerait avant « 9 ». D'où trois règles :
///  · une case vide (« », « — ») va TOUJOURS en bas, quel que soit le sens du tri —
///    sinon un tri décroissant remonterait en tête les postes qui n'ont rien dit ;
///  · deux valeurs déjà typées (date, nombre) se comparent comme telles ;
///  · deux textes qui COMMENCENT par un nombre se comparent sur ce nombre (virgule ou
///    point décimal) ; les autres textes, dans l'ordre naturel : les suites de chiffres
///    comptent comme des nombres (« POSTE-2 » avant « POSTE-10 »), sans tenir compte
///    des majuscules.
/// Placé dans Core pour être testé ; la fenêtre, elle, n'est pas chargée par les tests.
/// </summary>
public static class TriColonnes
{
    private static readonly Regex NombreEnTete = new(@"^\s*(-?\d+(?:[.,]\d+)?)", RegexOptions.CultureInvariant);

    /// <summary>Compare deux cases ; <paramref name="decroissant"/> inverse l'ordre, sauf pour les cases vides.</summary>
    public static int Comparer(object? a, object? b, bool decroissant)
    {
        bool videA = EstVide(a), videB = EstVide(b);
        if (videA || videB) return videA == videB ? 0 : videA ? 1 : -1;

        var resultat = ComparerRemplies(a!, b!);
        return decroissant ? -resultat : resultat;
    }

    private static bool EstVide(object? v) =>
        v is null || v is string s && (string.IsNullOrWhiteSpace(s) || s.Trim() is "—" or "-");

    private static int ComparerRemplies(object a, object b)
    {
        if (a is not string && b is not string && a.GetType() == b.GetType() && a is IComparable ca)
            return ca.CompareTo(b);

        var ta = Convert.ToString(a, CultureInfo.InvariantCulture) ?? "";
        var tb = Convert.ToString(b, CultureInfo.InvariantCulture) ?? "";
        if (Nombre(ta) is { } na && Nombre(tb) is { } nb && na != nb)
            return na.CompareTo(nb);
        return OrdreNaturel(ta, tb);
    }

    private static double? Nombre(string texte)
    {
        var m = NombreEnTete.Match(texte);
        return m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : null;
    }

    /// <summary>Ordre naturel : « POSTE-2 » avant « POSTE-10 », majuscules ignorées.</summary>
    public static int OrdreNaturel(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int di = i, dj = j;
                while (di < a.Length && char.IsDigit(a[di])) di++;
                while (dj < b.Length && char.IsDigit(b[dj])) dj++;
                var sa = a[i..di].TrimStart('0');
                var sb = b[j..dj].TrimStart('0');
                if (sa.Length != sb.Length) return sa.Length.CompareTo(sb.Length);
                var c = string.CompareOrdinal(sa, sb);
                if (c != 0) return c;
                i = di; j = dj;
                continue;
            }
            var cc = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
            if (cc != 0) return cc;
            i++; j++;
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
