using FaultTracePC.Core;
using Xunit;

namespace FaultTracePC.Tests;

/// <summary>
/// Filtrage des adresses et authentification HMAC — le cœur de la sécurité du
/// mode Client. Une régression ici exposerait la télémétrie : ces tests sont
/// les plus importants du projet.
/// </summary>
public class SecurityTests
{
    // ---------- Filtrage des adresses (RFC 1918 + boucle locale) ----------

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.55.1.2")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.10")]
    public void Adresses_privees_ou_locales_sont_acceptees(string ip) =>
        Assert.True(RemoteConfig.IsPrivateOrLoopback(System.Net.IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]           // Internet
    [InlineData("172.15.0.1")]        // juste sous la plage 172.16/12
    [InlineData("172.32.0.1")]        // juste au-dessus
    [InlineData("192.167.1.1")]       // proche mais hors 192.168/16
    [InlineData("11.0.0.1")]          // proche mais hors 10/8
    [InlineData("169.254.1.1")]       // lien-local : non autorisé
    [InlineData("100.64.0.1")]        // CGNAT : non autorisé
    public void Adresses_publiques_sont_refusees(string ip) =>
        Assert.False(RemoteConfig.IsPrivateOrLoopback(System.Net.IPAddress.Parse(ip)));

    [Fact]
    public void Adresse_nulle_est_refusee() =>
        Assert.False(RemoteConfig.IsPrivateOrLoopback(null));

    // ---------- Signature HMAC ----------

    private static bool AlwaysFresh(string _) => true;

    private static bool Verify(string token, Dictionary<string, string> headers,
                               string method = "GET", string path = "/api/status", string query = "",
                               Func<string, bool>? fresh = null) =>
        RemoteConfig.VerifySignature(token, method, path, query,
            headers[RemoteConfig.HeaderTimestamp],
            headers[RemoteConfig.HeaderNonce],
            headers[RemoteConfig.HeaderSignature],
            fresh ?? AlwaysFresh);

    [Fact]
    public void Requete_signee_est_acceptee()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        Assert.True(Verify(token, headers));
    }

    [Fact]
    public void Le_token_ne_figure_pas_dans_les_entetes()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        Assert.DoesNotContain(headers.Values, v => v.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mauvais_token_est_refuse()
    {
        var headers = RemoteConfig.BuildAuthHeaders(RemoteConfig.GenerateToken(), "GET", "/api/status", "");
        Assert.False(Verify(RemoteConfig.GenerateToken(), headers));
    }

    [Fact]
    public void Chemin_different_invalide_la_signature()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        // Une signature capturée sur /api/status ne doit pas ouvrir /api/scan.
        Assert.False(Verify(token, headers, path: "/api/scan"));
    }

    [Fact]
    public void Parametres_differents_invalident_la_signature()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/flight", "minutes=60");
        Assert.False(Verify(token, headers, path: "/api/flight", query: "minutes=1440"));
    }

    [Fact]
    public void Methode_differente_invalide_la_signature()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/scan", "");
        Assert.False(Verify(token, headers, method: "POST", path: "/api/scan"));
    }

    [Fact]
    public void Rejeu_est_refuse_quand_le_nonce_est_deja_vu()
    {
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        Assert.True(Verify(token, headers));                       // 1re fois : acceptée
        Assert.False(Verify(token, headers, fresh: _ => false));   // rejouée : refusée
    }

    [Fact]
    public void Horodatage_trop_ancien_est_refuse()
    {
        var token = RemoteConfig.GenerateToken();
        var old = DateTimeOffset.UtcNow.AddSeconds(-(RemoteConfig.ClockToleranceSeconds + 60)).ToUnixTimeSeconds();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        headers[RemoteConfig.HeaderTimestamp] = old.ToString();
        Assert.False(Verify(token, headers));
    }

    [Fact]
    public void Signature_invalide_ne_consomme_pas_de_nonce()
    {
        // Régression : le nonce ne doit être consommé qu'APRÈS validation de la
        // signature, sinon un inconnu peut saturer la mémoire du service.
        var token = RemoteConfig.GenerateToken();
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        bool nonceConsumed = false;
        RemoteConfig.VerifySignature("00" + RemoteConfig.GenerateToken()[2..], "GET", "/api/status", "",
            headers[RemoteConfig.HeaderTimestamp], headers[RemoteConfig.HeaderNonce],
            headers[RemoteConfig.HeaderSignature],
            _ => { nonceConsumed = true; return true; });
        Assert.False(nonceConsumed);
    }

    [Fact]
    public void Entetes_manquants_sont_refuses()
    {
        var token = RemoteConfig.GenerateToken();
        Assert.False(RemoteConfig.VerifySignature(token, "GET", "/api/status", "", null, null, null, AlwaysFresh));
    }

    [Fact]
    public void Token_saisi_a_la_main_fonctionne_des_deux_cotes()
    {
        // Un token non hexadécimal ne doit pas faire échouer la signature :
        // les deux extrémités appliquent la même dérivation de clé.
        const string token = "mon secret d'établissement !";
        var headers = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        Assert.True(Verify(token, headers));
    }

    [Fact]
    public void Deux_requetes_ont_des_nonces_differents()
    {
        var token = RemoteConfig.GenerateToken();
        var a = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        var b = RemoteConfig.BuildAuthHeaders(token, "GET", "/api/status", "");
        Assert.NotEqual(a[RemoteConfig.HeaderNonce], b[RemoteConfig.HeaderNonce]);
    }
}
