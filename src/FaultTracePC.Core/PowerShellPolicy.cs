using System.Diagnostics;
using System.Text;

namespace FaultTracePC.Core;

/// <summary>État de la stratégie d'exécution PowerShell, du point de vue de la
/// seule question qui nous intéresse : ce poste acceptera-t-il de lancer notre
/// script de réparation ?</summary>
/// <param name="Scope">Portée qui impose la décision, ou null si rien ne bloque.</param>
/// <param name="Policy">Valeur imposée par cette portée.</param>
public sealed record ExecutionPolicyState(string? Scope, string? Policy)
{
    /// <summary>Vrai quand une stratégie de groupe interdit le script.</summary>
    public bool Blocked => Scope is not null;
}

/// <summary>
/// POURQUOI CETTE CLASSE EXISTE
/// Le bouton « Lancer la réparation » démarre PowerShell avec
/// <c>-ExecutionPolicy Bypass -File monscript.ps1</c>. Cette option agit sur la
/// portée « Process », qui est la plus faible : une stratégie de groupe
/// (portées <c>MachinePolicy</c> et <c>UserPolicy</c>) prime sur elle. Sur un
/// poste d'entreprise ou d'établissement où l'administrateur a fixé
/// <c>Restricted</c> ou <c>AllSigned</c>, PowerShell refuse donc le fichier,
/// écrit son refus, et la console se referme aussitôt — trop vite pour être lue.
///
/// C'est exactement le symptôme signalé en août 2026 : « la fenêtre s'ouvre et se
/// ferme de suite sans faire le travail ». Le script généré se termine pourtant
/// par un « Appuyer sur Entrée pour fermer » : s'il ne s'affiche pas, c'est que
/// la première ligne n'a jamais été lue.
///
/// CE QU'ON NE FAIT PAS
/// Contourner la stratégie. C'est techniquement possible — on sait faire passer
/// un script par l'entrée standard — et ce serait exactement ce qu'un
/// administrateur a interdit. Le logiciel constate, nomme la cause, et laisse la
/// décision à qui de droit. Un outil de diagnostic qui désobéit à la stratégie
/// du parc perd le droit d'être installé dessus.
/// </summary>
public static class PowerShellPolicy
{
    /// <summary>Portées imposées par une stratégie de groupe, dans l'ordre de priorité.</summary>
    private static readonly string[] PortéesDeGroupe = ["MachinePolicy", "UserPolicy"];

    /// <summary>Valeurs qui refusent un script local non signé.</summary>
    private static readonly string[] Bloquantes = ["Restricted", "AllSigned"];

    /// <summary>
    /// Interroge PowerShell. Renvoie null si la question n'a pas pu être posée —
    /// on ne conclut alors rien, plutôt que d'annoncer à tort que tout va bien.
    /// </summary>
    public static ExecutionPolicyState? Read(TimeSpan timeout)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -Command \"Get-ExecutionPolicy -List | ForEach-Object { \\\"$($_.Scope)=$($_.ExecutionPolicy)\\\" }\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var p = Process.Start(psi);
            if (p is null) return null;

            var sortie = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit((int)timeout.TotalMilliseconds)) { try { p.Kill(true); } catch { } return null; }
            if (string.IsNullOrWhiteSpace(sortie)) return null;

            return Interpret(sortie);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lit la sortie de <c>Get-ExecutionPolicy -List</c> mise sous la forme
    /// « Portée=Valeur ». Séparée de l'appel à PowerShell pour être vérifiable.
    /// </summary>
    internal static ExecutionPolicyState Interpret(string listOutput)
    {
        var valeurs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ligne in listOutput.Split('\n'))
        {
            var i = ligne.IndexOf('=');
            if (i <= 0) continue;
            var portée = ligne[..i].Trim();
            var valeur = ligne[(i + 1)..].Trim();
            if (portée.Length > 0 && valeur.Length > 0) valeurs[portée] = valeur;
        }

        foreach (var portée in PortéesDeGroupe)
            if (valeurs.TryGetValue(portée, out var v)
                && Bloquantes.Contains(v, StringComparer.OrdinalIgnoreCase))
                return new ExecutionPolicyState(portée, v);

        // Toute autre portée est plus faible que « Process », que nous fixons
        // nous-mêmes à Bypass : elle ne peut pas nous bloquer.
        return new ExecutionPolicyState(null, null);
    }
}
