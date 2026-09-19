<#
    POURQUOI CE SCRIPT EXISTE

    La console reçoit un « 403 » et ne peut pas savoir lequel des deux verrous du
    service l'a produit : l'adresse source refusée, ou la signature refusée. Ce
    script refait la MÊME requête que la console, à la main, deux fois :
    une fois en passant par le proxy du système, une fois sans.

    Si la version « sans proxy » répond 200 et l'autre 403, la cause est trouvée :
    la requête de la console part dans un proxy web, qui n'a rien à faire là et
    qui abîme ou perd les trois en-têtes de signature.

    RIEN N'EST ÉCRIT NULLE PART. Le secret est demandé à l'écran, gardé en
    mémoire le temps du test, et rien n'est journalisé.
#>

param(
    [Parameter(Mandatory)][string]$Poste,
    [string]$Hote  = '',
    [int]$Port     = 58620
)

if (-not $Hote) { $Hote = $Poste }

Write-Host ''
Write-Host '=== 1. Est-ce qu''un proxy s''interpose ? ===' -ForegroundColor Cyan

$url = 'http://{0}:{1}/api/status' -f $Hote, $Port
$choisi = [System.Net.WebRequest]::GetSystemWebProxy().GetProxy($url)

if ($choisi.AbsoluteUri -eq ([Uri]$url).AbsoluteUri) {
    Write-Host '  Aucun proxy pour cette adresse : la requete part en direct.' -ForegroundColor Green
} else {
    Write-Host "  UN PROXY S'INTERPOSE : $($choisi.AbsoluteUri)" -ForegroundColor Yellow
    Write-Host '  C''est tres probablement la cause.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '  Ton adresse source pour joindre ce poste :' -ForegroundColor Cyan
$source = (Test-NetConnection -ComputerName $Hote -Port $Port -WarningAction SilentlyContinue).SourceAddress.IPAddress
Write-Host "    $source" -ForegroundColor Cyan

$privee = ($source -match '^10\.') -or ($source -match '^192\.168\.') -or
          (($source -match '^172\.(\d+)\.') -and [int]$Matches[1] -ge 16 -and [int]$Matches[1] -le 31)

if ($privee) {
    Write-Host '    Dans les plages privees : le premier verrou du service la laisse passer.' -ForegroundColor Green
} else {
    Write-Host '    PAS dans les plages privees (10/8, 172.16-31/12, 192.168/16).' -ForegroundColor Red
    Write-Host '    Le premier verrou du service la refuse : c est la cause, et c est voulu.' -ForegroundColor Red
}

Write-Host ''
Write-Host '=== 2. La meme requete que la console, avec et sans proxy ===' -ForegroundColor Cyan

$secret = Read-Host 'Secret maitre du parc (il ne sera ni affiche ni ecrit)' -AsSecureString
$bstr   = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
$clair  = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

if (-not $clair -or $clair.Trim().Length -lt 32) {
    Write-Host '  Secret trop court (32 caracteres au minimum). On s''arrete.' -ForegroundColor Red
    return
}

# jeton = HMAC-SHA256(secret, NOM-DE-LA-MACHINE-EN-MAJUSCULES), en hexadecimal.
# C'est exactement RemoteConfig.DeriveToken.
$hmac     = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [Text.Encoding]::UTF8.GetBytes($clair.Trim())
$jeton    = ([BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($Poste.Trim().ToUpperInvariant())))).Replace('-','')

# Le jeton etant de l'hexadecimal, la cle de signature est sa valeur en octets.
$cle = New-Object byte[] ($jeton.Length / 2)
for ($i = 0; $i -lt $jeton.Length; $i += 2) { $cle[$i / 2] = [Convert]::ToByte($jeton.Substring($i, 2), 16) }

function Nouvelles-Entetes {
    $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()

    $octets = New-Object byte[] 12
    ([System.Security.Cryptography.RandomNumberGenerator]::Create()).GetBytes($octets)
    $nonce = ([BitConverter]::ToString($octets)).Replace('-','')

    # payload = METHODE \n chemin \n requete \n horodatage \n nonce
    $payload = "GET`n/api/status`n`n$ts`n$nonce"

    $h = New-Object System.Security.Cryptography.HMACSHA256
    $h.Key = $script:cle
    $sig = ([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload)))).Replace('-','')

    return @{
        'X-FaultTrace-Ts'    = $ts
        'X-FaultTrace-Nonce' = $nonce
        'X-FaultTrace-Sig'   = $sig
    }
}

function Appeler([string]$adresse, [bool]$avecProxy) {
    $req = [System.Net.HttpWebRequest]::Create($adresse)
    $req.Method  = 'GET'
    $req.Timeout = 10000
    if (-not $avecProxy) { $req.Proxy = $null }

    $entetes = Nouvelles-Entetes
    foreach ($k in $entetes.Keys) { $req.Headers.Add($k, $entetes[$k]) }

    try {
        $rep = $req.GetResponse()
        $code = [int]$rep.StatusCode
        $rep.Close()
        return "$code OK"
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            return "$([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusDescription)"
        }
        return "pas de reponse : $($_.Exception.Message)"
    }
    catch { return "erreur : $($_.Exception.Message)" }
}

$script:cle = $cle

Write-Host ''
Write-Host "  Poste  : $Poste"
Write-Host "  Adresse: $url"
Write-Host ''

$avec = Appeler $url $true
Write-Host "  AVEC le proxy du systeme : $avec" -ForegroundColor $(if ($avec -like '200*') { 'Green' } else { 'Yellow' })

$sans = Appeler $url $false
Write-Host "  SANS proxy (direct)      : $sans" -ForegroundColor $(if ($sans -like '200*') { 'Green' } else { 'Yellow' })

Write-Host ''
Write-Host '=== Ce que ca veut dire ===' -ForegroundColor Cyan

if ($sans -like '200*' -and $avec -notlike '200*') {
    Write-Host '  TROUVE. La requete directe passe, celle qui traverse le proxy est refusee.' -ForegroundColor Green
    Write-Host '  La console doit cesser d''utiliser le proxy du systeme pour ces appels.' -ForegroundColor Green
}
elseif ($sans -like '200*' -and $avec -like '200*') {
    Write-Host '  Les deux passent. Le proxy n''est pas en cause, et le jeton calcule ici' -ForegroundColor Yellow
    Write-Host '  est le bon : le secret saisi a l''instant est donc le bon. Si la console' -ForegroundColor Yellow
    Write-Host '  refuse toujours, c''est qu''ELLE n''a pas ce secret-la enregistre.' -ForegroundColor Yellow
}
elseif ($sans -notlike '200*' -and $sans -notlike '403*') {
    Write-Host '  Le poste ne repond pas du tout : ce n''est plus une question de jeton.' -ForegroundColor Yellow
}
else {
    Write-Host '  403 dans les deux cas. Le proxy n''est pas en cause.' -ForegroundColor Yellow
    Write-Host '  Restent : un secret maitre different de celui du poste, ou l''adresse' -ForegroundColor Yellow
    Write-Host '  source refusee par le service (hors 10/8, 172.16/12, 192.168/16).' -ForegroundColor Yellow
    Write-Host '  Ton adresse source vue d''ici :' -ForegroundColor Yellow
    (Test-NetConnection -ComputerName $Hote -Port $Port -WarningAction SilentlyContinue).SourceAddress.IPAddress |
        ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
}

$clair = $null
Write-Host ''
