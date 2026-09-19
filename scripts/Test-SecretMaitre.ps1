<#
    QUEL SECRET MAÎTRE CE POSTE ATTEND-IL ?

    Le poste ne connaît pas le secret maître : il ne garde que SON jeton, dans
    C:\ProgramData\FaultTracePC\remote.json. Ce jeton vaut
    HMAC-SHA256(secret maître, NOM-DU-POSTE-EN-MAJUSCULES).

    Ce script lit ce jeton sur le poste, puis, pour chaque secret que tu proposes,
    refait le même calcul et dit si ça tombe juste. Tu peux en essayer plusieurs de
    suite : c'est la façon de retrouver LEQUEL de tes secrets est celui du parc,
    sans avoir à te souvenir duquel tu t'es servi.

    NI LE JETON NI LE SECRET NE SONT AFFICHÉS, et rien n'est écrit sur le disque.
    Seul le verdict — « c'est celui-là » ou « ce n'est pas celui-là » — sort à
    l'écran.
#>

param(
    [Parameter(Mandatory)][string]$Poste
)

Write-Host ''
Write-Host "=== Lecture du jeton enregistre sur $Poste ===" -ForegroundColor Cyan

try {
    $brut = Invoke-Command -ComputerName $Poste -ErrorAction Stop -ScriptBlock {
        $f = Join-Path $env:ProgramData 'FaultTracePC\remote.json'
        if (-not (Test-Path $f)) { return $null }
        Get-Content $f -Raw
    }
}
catch {
    Write-Host "  Lecture impossible : $($_.Exception.Message)" -ForegroundColor Red
    Write-Host '  (Il faut que la gestion a distance reponde, et des droits administrateur sur le poste.)' -ForegroundColor Red
    return
}

if (-not $brut) {
    Write-Host '  Le poste n a pas de fichier remote.json : il n est pas en mode parc.' -ForegroundColor Red
    return
}

$cfg = $brut | ConvertFrom-Json
$jetonDuPoste = $cfg.Token

if (-not $jetonDuPoste) {
    Write-Host '  Le fichier existe mais ne contient aucun jeton.' -ForegroundColor Red
    return
}

Write-Host "  Jeton lu. Mode : $($cfg.Mode), port : $($cfg.Port), longueur du jeton : $($jetonDuPoste.Length) caracteres." -ForegroundColor Green
Write-Host '  (Le jeton lui-meme n est pas affiche.)' -ForegroundColor DarkGray

# Le NOM qui entre dans le calcul est celui que Windows donne au poste,
# pas le libelle saisi dans la console. On le demande au poste lui-meme.
$nomWindows = Invoke-Command -ComputerName $Poste -ScriptBlock { $env:COMPUTERNAME }
Write-Host "  Nom Windows du poste : $nomWindows" -ForegroundColor Green

Write-Host ''
Write-Host '=== Essaie tes secrets, un par un (Entree vide pour arreter) ===' -ForegroundColor Cyan

$essai = 0
while ($true) {
    Write-Host ''
    $sec = Read-Host "Secret maitre a tester (rien ne s affiche)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    $clair = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

    if (-not $clair) { break }
    $essai++

    $clair = $clair.Trim()
    Write-Host "  (secret de $($clair.Length) caracteres)" -ForegroundColor DarkGray

    if ($clair.Length -lt 32) {
        Write-Host '  TROP COURT : le logiciel exige 32 caracteres au minimum.' -ForegroundColor Red
        Write-Host '  Un secret genere par le logiciel en fait 64. Ce n est pas celui du parc.' -ForegroundColor Red
        $clair = $null
        continue
    }

    $h = New-Object System.Security.Cryptography.HMACSHA256
    $h.Key = [Text.Encoding]::UTF8.GetBytes($clair)
    $calcule = ([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($nomWindows.Trim().ToUpperInvariant())))).Replace('-','')
    $clair = $null

    if ($calcule -eq $jetonDuPoste) {
        Write-Host ''
        Write-Host "  >>> C EST CELUI-LA. Ce secret est bien celui avec lequel $Poste a ete configure." -ForegroundColor Green
        Write-Host '  Saisis-le dans la console (Secret maitre > Enregistrer), puis Actualiser tout.' -ForegroundColor Green
        break
    }

    Write-Host '  Ce n est pas celui-la.' -ForegroundColor Yellow
}

Write-Host ''
if ($essai -eq 0) { Write-Host 'Aucun secret essaye.' -ForegroundColor DarkGray }
Write-Host ''
