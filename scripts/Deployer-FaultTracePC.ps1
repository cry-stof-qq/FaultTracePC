<#
    Déploiement de FaultTracePC sur un ou plusieurs postes distants.
    Version 1.1 — 5 septembre 2026. Licence MIT, sans garantie.
    https://palisser.fr — https://github.com/cry-stof-qq/FaultTracePC

    Le script se trouve tout seul ($PSScriptRoot) : il fonctionne depuis
    n'importe quel dossier, y compris une clé USB.

    Ce qu'il fait, pour CHAQUE poste nommé :
      1. vérifie que le nom est un compte d'ordinateur du domaine ;
      2. vérifie qu'il répond — ping OU port 445, voir plus bas ;
      2 bis. vérifie que la Gestion à distance (WinRM, 5985) répond, AVANT
         de copier quoi que ce soit : c'est elle qui installe, pas 445 ;
      3. s'il ne répond pas, tente un réveil réseau (WoL) puis réessaie ;
      4. copie le MSI, l'installe en contrôlant le CODE DE SORTIE, puis efface le paquet ;
      5. configure le mode parc si on le lui demande.

    ON N'INSTALLE QUE SUR CE QU'ON NOMME
    postes.csv n'est PAS une liste de cibles : c'est un annuaire d'adresses MAC,
    lu uniquement pour réveiller une machine qui ne répond pas. Il peut donc
    contenir sans risque les téléphones et tablettes rendus par le DHCP — rien
    ne les lit. Le contrôle du compte d'ordinateur (étape 1) les écarterait de
    toute façon : un téléphone n'a pas de compte dans l'annuaire.

    POURQUOI IL COPIE LE MSI AU LIEU DE L'INSTALLER DEPUIS LE PARTAGE
    C'est le « double saut » : une session distante ne peut pas présenter ton
    identité à un TROISIÈME ordinateur. Le poste se présenterait au serveur de
    fichiers sans identifiants et le partage le refuserait. Être administrateur
    du domaine n'y change rien : c'est une limite de délégation Kerberos, pas
    une question de droits.

    POURQUOI IL NE SE FIE PAS AU PING SEUL
    Le pare-feu Windows bloque ICMP par défaut dans beaucoup de stratégies : un
    poste allumé peut très bien ne pas répondre au ping. Abandonner sur une
    machine vivante serait pire que de réveiller une machine déjà allumée.
#>

[CmdletBinding()]
param(
    [string[]]$Poste,
    [string]$Msi,
    [ValidateSet('fr','en','auto')]
    [string]$Langue  = 'fr',
    [int]$Port       = 58620,

    # Mode parc. Le secret est demandé à l'écran s'il n'est pas donné par fichier.
    [switch]$ConfigurerParc,
    [string]$SecretFichier,

    # Ne configure QUE le mode parc : ni copie, ni installation. Pour un poste
    # déjà installé — le cas de loin le plus fréquent après un premier passage.
    [switch]$SeulementParc,

    # Ne fait QUE réveiller les postes : ni installation, ni configuration.
    [switch]$ReveilSeulement,

    # Consulté pour retrouver l'adresse MAC d'un poste au moment où on en a
    # besoin. Le fichier postes.csv n'est qu'un cache : il vieillit, le DHCP non.
    [string]$ServeurDhcp,

    # Interface à employer pour le réveil réseau : nom de carte ou adresse IP
    # locale. Laissé vide, le script choisit celle qui mène au poste.
    [string]$Interface,

    [int]$AttenteReveilSecondes = 90,

    # POINT 64, LOT B — le canal vers la console.
    # Chemin du journal JSON : une ligne par poste et par étape. Sans ce
    # paramètre, comportement strictement inchangé pour qui lance le script à la
    # main.
    [string]$SortieJson,

    # Ne fait QUE les contrôles en lecture : compte d'ordinateur, réponse réseau,
    # partage administratif, gestion à distance. Ni réveil, ni copie, ni
    # installation, ni mise en parc. Et aucune question posée.
    [switch]$VerifierSeulement,

    # Liste de postes lue dans un fichier, un nom par ligne. Alternative à -Poste
    # quand il y en a beaucoup : une ligne de commande Windows a une longueur
    # maximale, et quelques centaines de noms la dépassent. L'échec n'arriverait
    # pas au moment où on le comprendrait, mais le jour où le parc a grossi.
    [string]$FichierPostes
)

$ErrorActionPreference = 'Stop'
$racine = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

# ---------------------------------------------------------------- journal
$journalDir = Join-Path $racine 'Journal'
New-Item -ItemType Directory -Force -Path $journalDir | Out-Null
$journal = Join-Path $journalDir ("Deploiement_{0:yyyy-MM-dd_HHmm}.txt" -f (Get-Date))
Start-Transcript -Path $journal | Out-Null

function Titre([string]$t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }
function Bien([string]$t)  { Write-Host "  $t" -ForegroundColor Green }
function Alerte([string]$t){ Write-Host "  $t" -ForegroundColor Yellow }
function Grave([string]$t) { Write-Host "  $t" -ForegroundColor Red }

# ---------------------------------------------------------------- journal JSON
<#
    LE CANAL VERS LA CONSOLE (point 64, lot B).

    Une ligne JSON par poste et par étape, écrite DANS UN FICHIER — jamais sur la
    sortie standard, que PowerShell 5.1 décode avec la page de code de la console :
    un « é » en ressortirait en charabia.

    « etape » et « etat » sont des CODES invariants. Ce script parle français, la
    console parle deux langues : c'est elle qui fabrique la phrase. Le champ
    « detail » reste le texte brut, affiché tel quel et jamais traduit.

    UNE ÉCRITURE IMPOSSIBLE N'INTERROMPT PAS LE DÉPLOIEMENT. On prévient une fois,
    puis on continue sans réessayer : un journal raconte le travail, il n'est pas
    le travail.
#>
$script:cheminJson = if ($SortieJson) {
    if ([IO.Path]::IsPathRooted($SortieJson)) { $SortieJson } else { Join-Path $racine $SortieJson }
} else { '' }
$script:jsonAvorte = $false

function Write-Json {
    param(
        [string]$Poste,
        [string]$Etape,
        [string]$Etat,
        [string]$Detail  = '',
        [string]$Adresse = '',
        $Code = $null
    )
    if (-not $script:cheminJson -or $script:jsonAvorte) { return }

    try {
        $ligne = [ordered]@{
            horodatage = (Get-Date).ToString('o')
            poste      = $Poste
            etape      = $Etape
            etat       = $Etat
        }
        if ($Detail)         { $ligne.detail  = $Detail }
        if ($Adresse)        { $ligne.adresse = $Adresse }
        if ($null -ne $Code) { $ligne.code    = [int]$Code }

        $dossier = Split-Path $script:cheminJson -Parent
        if ($dossier -and -not (Test-Path $dossier)) {
            New-Item -ItemType Directory -Force -Path $dossier | Out-Null
        }

        # ASCII PUR : TOUT CARACTÈRE ACCENTUÉ PART EN \uXXXX.
        #
        # Constaté le 19/09/2026 au premier essai réel : « Get-Content » sans
        # -Encoding lit le fichier avec la page de code ANSI, et « vérifier »
        # s'affichait « vÃ©rifier ». Le fichier était correct, sa lecture ne l'était
        # pas — mais un journal qu'on ne peut lire qu'en connaissant son encodage est
        # un journal à moitié utile, et personne ne devrait avoir à le savoir.
        #
        # L'échappement \uXXXX est du JSON standard : il se relit à l'identique par
        # System.Text.Json côté console, par ConvertFrom-Json côté PowerShell, et par
        # n'importe quel lecteur, quelle que soit l'idée qu'il se fait de l'encodage.
        $json = $ligne | ConvertTo-Json -Compress -Depth 3
        $ascii = New-Object Text.StringBuilder
        foreach ($c in $json.ToCharArray()) {
            $n = [int][char]$c
            if ($n -lt 32 -or $n -gt 126) { [void]$ascii.AppendFormat('\u{0:x4}', $n) }
            else { [void]$ascii.Append($c) }
        }

        # UTF-8 SANS MARQUE D'ORDRE DES OCTETS. « Out-File -Encoding utf8 » en
        # ajoute une en PowerShell 5.1 — et comme on AJOUTE à chaque étape, elle se
        # retrouverait au milieu du fichier, au début d'une ligne sur deux.
        [IO.File]::AppendAllText(
            $script:cheminJson,
            $ascii.ToString() + "`r`n",
            (New-Object Text.UTF8Encoding $false))
    }
    catch {
        $script:jsonAvorte = $true
        Alerte "Journal JSON non écrit : $($_.Exception.Message)"
    }
}

# ---------------------------------------------------------------- paramètres du site
$script:cheminParams = Join-Path $racine 'parametres.json'

<#
    Les valeurs propres à VOTRE réseau — chemin du paquet, serveur DHCP — ne sont
    écrites nulle part dans ce script : il doit pouvoir être diffusé tel quel.
    Elles sont demandées au premier lancement et, si vous l'acceptez, rangées
    dans parametres.json À CÔTÉ du script.

    Ce fichier ne contient AUCUN secret : le secret maître du parc n'y est jamais
    écrit, il est demandé à chaque fois. Il nomme en revanche vos serveurs — ne
    le diffusez pas avec le script.
#>
function Initialize-Parametres {
    $enregistres = if (Test-Path $script:cheminParams) {
        try { Get-Content $script:cheminParams -Raw | ConvertFrom-Json } catch { $null }
    } else { $null }

    $demande = $false

    # EN VÉRIFICATION SEULE, AUCUNE QUESTION N'EST POSÉE. Ce mode est fait pour
    # être piloté par la console : une invite sans personne devant bloquerait
    # indéfiniment. Le paquet MSI n'est de toute façon pas nécessaire, puisque rien
    # n'est installé.
    $muet = [bool]$VerifierSeulement

    if (-not $Msi) {
        if ($enregistres -and $enregistres.Msi) { $script:Msi = $enregistres.Msi }
        elseif (-not $muet) {
            Titre 'Premier lancement — paramètres de votre site'
            Write-Host '  Chemin du paquet MSI de FaultTracePC.'
            Write-Host '  Exemple : \\serveur\partage\FaultTracePC-1.6.2.msi' -ForegroundColor DarkGray
            $script:Msi = (Read-Host '  Chemin du MSI').Trim('"', ' ')
            $demande = $true
        }
    }

    if (-not $ServeurDhcp) {
        if ($enregistres -and $enregistres.ServeurDhcp) { $script:ServeurDhcp = $enregistres.ServeurDhcp }
        elseif (-not $muet) {
            Write-Host ''
            Write-Host '  Serveur DHCP, consulté pour retrouver l''adresse MAC d''un poste éteint.'
            Write-Host '  Exemple : SRV-DHCP   (laisser vide si vous n''en avez pas)' -ForegroundColor DarkGray
            $script:ServeurDhcp = (Read-Host '  Serveur DHCP').Trim()
            $demande = $true
        }
    }

    if ($demande) {
        Write-Host ''
        Write-Host '  1 = enregistrer ces valeurs pour les prochaines fois' -ForegroundColor Cyan
        Write-Host '  2 = ne rien enregistrer (elles seront redemandées)'
        if ((Read-Host '  Ton choix [1/2]').Trim() -eq '1') {
            try {
                [pscustomobject]@{
                    _lisezmoi   = 'Parametres locaux de Deployer-FaultTracePC. Aucun secret ici. Ne pas diffuser : ce fichier nomme vos serveurs.'
                    Msi         = $script:Msi
                    ServeurDhcp = $script:ServeurDhcp
                } | ConvertTo-Json | Set-Content -Path $script:cheminParams -Encoding UTF8
                Bien "Enregistré dans $script:cheminParams"
            }
            catch { Alerte "Enregistrement impossible : $($_.Exception.Message)" }
        }
        else { Alerte 'Rien n''a été enregistré.' }
    }
}

<#
    LA LISTE DE POSTES, LUE DANS UN FICHIER.

    Format volontairement pauvre : un nom par ligne, « # » commence un commentaire,
    les lignes vides sont ignorées. Il se relit à l'œil et se corrige dans le
    Bloc-notes. La console l'écrit en ASCII pur — les noms de poste n'ont ni accent
    ni espace — si bien que la question de l'encodage ne se pose pas.

    UN FICHIER INTROUVABLE OU ILLISIBLE NE REND PAS UNE LISTE VIDE EN SILENCE : il
    le dit. Une liste vide et une liste qu'on n'a pas pu lire ne veulent pas dire la
    même chose, et seule la première autorise à conclure qu'il n'y a rien à faire.
#>
function Get-PostesDuFichier([string]$chemin) {
    if (-not (Test-Path $chemin)) {
        Grave "Liste de postes introuvable : $chemin"
        return @()
    }
    try {
        return @(Get-Content $chemin -Encoding UTF8 -ErrorAction Stop |
                 ForEach-Object { ($_ -split '#')[0].Trim() } |
                 Where-Object { $_ })
    }
    catch {
        Grave "Liste de postes illisible : $($_.Exception.Message)"
        return @()
    }
}

# ---------------------------------------------------------------- annuaire
<#
    Vrai si le nom correspond à un compte d'ordinateur du domaine.

    [ADSISearcher] interroge l'annuaire SANS RSAT ni module : c'est du .NET
    intégré à Windows. Un téléphone, une tablette ou une faute de frappe n'ont
    pas de compte, et sont donc écartés avant qu'on touche à quoi que ce soit.

    On distingue deux échecs, et la distinction est essentielle :
      · l'annuaire répond et ne connaît pas ce nom  -> on refuse ;
      · l'annuaire est injoignable                  -> on prévient et on continue,
        parce que refuser tout un déploiement parce que LDAP tousse serait pire.
#>
function Test-PosteDuDomaine([string]$nom) {
    try {
        $chercheur = [ADSISearcher] "(&(objectCategory=computer)(cn=$nom))"
        $chercheur.PropertiesToLoad.Add('cn') | Out-Null
        return @{ Connu = ($null -ne $chercheur.FindOne()); AnnuaireOk = $true }
    }
    catch {
        return @{ Connu = $true; AnnuaireOk = $false }
    }
}

# ---------------------------------------------------------------- réveil réseau
$script:cheminCsv = Join-Path $racine 'postes.csv'

<#
    Écrit ou remplace la ligne d'UN poste dans postes.csv, sans toucher aux
    autres. Le fichier reste donc utilisable pour tout le parc, y compris les
    téléphones que le DHCP y a mis : on n'y touche jamais globalement.
#>
function Update-LigneCsv([string]$nom, [string]$mac) {
    try {
        $lignes = if (Test-Path $script:cheminCsv) {
            @(Import-Csv $script:cheminCsv -Delimiter ';' | Where-Object { $_.Nom -ne $nom })
        } else { @() }
        $lignes += [pscustomobject]@{ Nom = $nom; MAC = $mac }
        $lignes | Sort-Object Nom |
            Export-Csv $script:cheminCsv -Delimiter ';' -NoTypeInformation -Encoding UTF8
    }
    catch { Alerte "postes.csv non mis à jour : $($_.Exception.Message)" }
}

<#
    Adresse MAC d'un poste, par ordre de FIABILITÉ et non de commodité :

      1. le serveur DHCP, qui fait foi — et dont la réponse rafraîchit le cache ;
      2. postes.csv, photographie prise un jour et qui vieillit ;
      3. le cache ARP local, qui ne vaut que si le poste a parlé récemment.

    Le DHCP n'est interrogé que pour LE poste demandé, et seulement quand on a
    besoin de sa MAC — c'est-à-dire quand il ne répond pas. Aucun balayage.
#>
function Get-MacDuPoste([string]$nom) {
    if ($ServeurDhcp) {
        try {
            $mac = Invoke-Command -ComputerName $ServeurDhcp -ArgumentList $nom -ErrorAction Stop -ScriptBlock {
                param($n)
                Get-DhcpServerv4Scope | ForEach-Object {
                    Get-DhcpServerv4Lease -ScopeId $_.ScopeId -AllLeases
                    Get-DhcpServerv4Reservation -ScopeId $_.ScopeId
                } | Where-Object { $_.HostName -and ($_.HostName -split '\.')[0] -eq $n } |
                    Sort-Object { if ($_.LeaseExpiryTime) { $_.LeaseExpiryTime } else { Get-Date '2100-01-01' } } -Descending |
                    Select-Object -First 1 ClientId, IPAddress
            }
            if ($mac -and $mac.ClientId) {
                Bien "Adresse MAC obtenue du DHCP ($ServeurDhcp) : $($mac.ClientId)"
                # Le bail donne aussi l'ADRESSE : c'est elle qui dira, plus bas,
                # par quelle carte réseau envoyer le paquet de réveil.
                if ($mac.IPAddress) {
                    $script:ipAttendue = $mac.IPAddress.IPAddressToString
                    Bien "Adresse attendue du poste : $script:ipAttendue"
                }
                Update-LigneCsv $nom $mac.ClientId
                return $mac.ClientId
            }
            Alerte "$ServeurDhcp ne connaît pas de bail pour $nom."
        }
        catch { Alerte "DHCP $ServeurDhcp injoignable : $($_.Exception.Message)" }
    }

    if (Test-Path $script:cheminCsv) {
        $ligne = Import-Csv $script:cheminCsv -Delimiter ';' | Where-Object { $_.Nom -eq $nom }
        if ($ligne -and @($ligne)[0].MAC) {
            Alerte 'Adresse MAC lue dans postes.csv (elle peut être périmée).'
            return @($ligne)[0].MAC
        }
    }

    try {
        $ip = (Resolve-DnsName $nom -Type A -ErrorAction Stop | Select-Object -First 1).IPAddress
        $arp = arp -a | Select-String ([regex]::Escape($ip))
        if ($arp -match '([0-9a-fA-F]{2}[-:]){5}[0-9a-fA-F]{2}') {
            Alerte 'Adresse MAC lue dans le cache ARP.'
            return $Matches[0]
        }
    } catch { }
    return $null
}

<#
    Adresse de diffusion du sous-réseau d'une interface : 10.12.4.87/24 -> 10.12.4.255.
    C'est CETTE adresse que les commutateurs relaient correctement, là où la
    diffusion générale 255.255.255.255 est souvent filtrée ou mal routée.
#>
function Get-BroadcastLocal([string]$ip, [int]$prefixe) {
    $o = ([Net.IPAddress]::Parse($ip)).GetAddressBytes()
    for ($i = 0; $i -lt 4; $i++) {
        $restants = $prefixe - ($i * 8)
        $masque = if ($restants -ge 8) { 255 } elseif ($restants -le 0) { 0 } else { (255 -shl (8 - $restants)) -band 255 }
        $o[$i] = [byte]((($o[$i] -band $masque) -bor (255 -band (-bnot $masque))))
    }
    return ([Net.IPAddress]::new($o)).ToString()
}

<#
    Vrai si deux adresses partagent le même sous-réseau, au préfixe donné.
    C'est ce qui permet de n'émettre QUE par la carte qui mène au poste.
#>
function Test-MemeSousReseau([string]$ipA, [string]$ipB, [int]$prefixe) {
    try {
        $a = ([Net.IPAddress]::Parse($ipA)).GetAddressBytes()
        $b = ([Net.IPAddress]::Parse($ipB)).GetAddressBytes()
        for ($i = 0; $i -lt 4; $i++) {
            $restants = $prefixe - ($i * 8)
            $m = if ($restants -ge 8) { 255 } elseif ($restants -le 0) { 0 } else { (255 -shl (8 - $restants)) -band 255 }
            if (($a[$i] -band $m) -ne ($b[$i] -band $m)) { return $false }
        }
        return $true
    }
    catch { return $false }
}

<#
    Envoi du paquet magique — version qui marche vraiment sur une machine à
    plusieurs cartes réseau.

    DÉFAUT CONSTATÉ LE 31/08/2026 : le paquet était envoyé à 255.255.255.255 sans
    préciser l'interface. Sur un poste équipé de cartes virtuelles — VirtualBox,
    Hyper-V, VPN — Windows choisit la sortie par sa table de routage, et le
    paquet part sur une carte qui ne mène nulle part. Le poste ne se réveille
    pas, et rien ne le signale : un paquet UDP perdu ne remonte aucune erreur.

    Ce que fait cette version, comme les outils dédiés :
      · elle envoie depuis CHAQUE interface locale, en liant la socket à son
        adresse — c'est ce qui force la sortie physique ;
      · vers le broadcast DU SOUS-RÉSEAU de cette interface, puis vers la
        diffusion générale en secours ;
      · sur les ports 7 ET 9, les deux étant utilisés selon les cartes ;
      · trois fois, un paquet UDP pouvant se perdre sans que personne le sache.
#>
function Send-Reveil([string]$mac, [string]$ipCible) {
    $hex = ($mac -replace '[^0-9A-Fa-f]', '')
    if ($hex.Length -ne 12) { throw "Adresse MAC inexploitable : $mac" }

    $adresse = for ($i = 0; $i -lt 12; $i += 2) { [Convert]::ToByte($hex.Substring($i, 2), 16) }
    $prefixe = [byte[]](0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF)
    $paquet  = [byte[]]($prefixe + ($adresse * 16))

    $interfaces = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' })

    if ($interfaces.Count -eq 0) { Alerte 'Aucune interface IPv4 utilisable.'; return }

    <#
        CHOISIR LA CARTE PLUTÔT QUE D'ARROSER

        Une machine d'administration porte des cartes virtuelles — VirtualBox,
        Hyper-V, VPN — dont les réseaux ne mènent nulle part. Émettre par toutes,
        c'est du bruit sur des réseaux où le poste n'est pas, et cela réveille
        des machines qu'on ne visait pas si un nom se répète ailleurs.

        L'adresse attendue du poste vient du bail DHCP : on ne retient donc que
        la ou les cartes dont le sous-réseau la contient. À défaut d'information,
        on émet partout — mais en le disant.
    #>
    if ($Interface) {
        $choisies = @($interfaces | Where-Object { $_.IPAddress -eq $Interface -or $_.InterfaceAlias -like "*$Interface*" })
        if ($choisies.Count -gt 0) { $interfaces = $choisies; Bien "Interface imposée : $($choisies[0].InterfaceAlias) ($($choisies[0].IPAddress))" }
        else { Alerte "Aucune interface ne correspond à « $Interface » : émission sur toutes." }
    }
    elseif ($ipCible) {
        $choisies = @($interfaces | Where-Object { Test-MemeSousReseau $_.IPAddress $ipCible $_.PrefixLength })
        if ($choisies.Count -gt 0) {
            $interfaces = $choisies
            Bien "Carte retenue pour joindre $ipCible : $($choisies[0].InterfaceAlias) ($($choisies[0].IPAddress)/$($choisies[0].PrefixLength))"
        }
        else {
            Alerte "Aucune carte ne partage le sous-réseau de $ipCible — le poste est probablement sur un autre VLAN."
            Alerte 'Émission sur toutes les cartes, sans grand espoir.'
        }
    }
    else { Alerte 'Adresse du poste inconnue : émission sur toutes les cartes.' }

    $envois = 0
    foreach ($int in $interfaces) {
        $bc = try { Get-BroadcastLocal $int.IPAddress $int.PrefixLength } catch { '255.255.255.255' }
        foreach ($cible in @($bc, '255.255.255.255')) {
            foreach ($portWol in @(9, 7)) {
                try {
                    $local = New-Object System.Net.IPEndPoint ([Net.IPAddress]::Parse($int.IPAddress)), 0
                    $udp = New-Object System.Net.Sockets.UdpClient $local
                    try {
                        $udp.EnableBroadcast = $true
                        for ($n = 0; $n -lt 3; $n++) {
                            [void]$udp.Send($paquet, $paquet.Length, $cible, $portWol)
                            Start-Sleep -Milliseconds 60
                        }
                        $envois++
                    }
                    finally { $udp.Close() }
                }
                catch { }
            }
        }
        Write-Host "    depuis $($int.IPAddress) vers $bc et 255.255.255.255 (ports 9 et 7)" -ForegroundColor DarkGray
    }

    if ($envois -eq 0) { Alerte "Aucun paquet n'a pu être émis." }
    else { Bien "$envois envoi(s) effectué(s) sur $($interfaces.Count) interface(s)." }
}

<#
    Adresse IP réellement utilisée pour joindre ce poste.

    Elle vient de la connexion elle-même (Test-NetConnection), pas d'une saisie
    ni d'un souvenir : c'est la seule qui ne mente pas. Une entrée DNS périmée ou
    une adresse retenue de mémoire produisent un poste « injoignable » dans la
    console, sans que rien n'explique pourquoi — c'est arrivé.
#>
<#
    UNE ADRESSE QUI NE SERT À RIEN NE DOIT PAS ÊTRE PROPOSÉE.

    Constaté le 19/09/2026 : la vérification a rendu « fe80::e6fe:...:ff31%12 ».
    Trois raisons de l'écarter, et elles valent aussi pour ce que le script propose
    de saisir dans la console de parc :

      · l'index de zone (« %12 ») désigne une carte réseau SUR LA MACHINE QUI A
        PRODUIT L'ADRESSE. Recopié ailleurs, il ne veut plus rien dire ;
      · une adresse lien-local — fe80::/10, ou 169.254.0.0/16 en IPv4 — ne traverse
        aucun routeur ;
      · la boucle locale ne désigne jamais un poste distant.

    Mieux vaut RIEN qu'une adresse fausse : sans adresse, on saisit le nom, ce que
    le script recommande de toute façon.
#>
<#
    LA MACHINE LOCALE N'A PAS D'« ADRESSE PAR LAQUELLE ON L'A JOINTE ».

    Interrogée depuis elle-même, elle répond par l'UNE de ses adresses, choisie par
    la pile réseau — et sur un poste d'administration il y en a beaucoup : carte
    physique, VPN, commutateurs virtuels de WSL, d'Hyper-V ou de Docker. Le
    19/09/2026, la vérification a rendu 172.19.240.1, une carte virtuelle, alors
    que la carte utile portait une adresse en 192.168.1.x.

    Aucune de ces adresses n'est « la bonne », et la question n'a pas de sens ici :
    ce script déploie sur des postes DISTANTS. Pour un poste distant, l'adresse
    constatée est celle par laquelle la connexion a réellement abouti, ce qui est
    exactement ce qu'on veut savoir. Pour soi-même, il n'y a rien à constater.
#>
function Test-MachineLocale([string]$nom) {
    $court = ($nom -split '\.')[0]
    return $court -ieq $env:COMPUTERNAME
}

function Test-AdresseUtilisable([string]$ip) {
    if (-not $ip) { return $false }
    if ($ip -like '*%*') { return $false }
    if ($ip -match '^fe[89ab]') { return $false }
    if ($ip -like '169.254.*') { return $false }
    if ($ip -eq '::1' -or $ip -eq '127.0.0.1') { return $false }
    return $true
}

function Get-IpDuPoste([string]$nom) {
    # Mieux vaut RIEN qu'une adresse qui désigne une carte virtuelle de la console.
    if (Test-MachineLocale $nom) { return '' }

    # D'abord ce qui a RÉELLEMENT répondu, puis le DNS — et dans les deux cas, on
    # écarte ce qui ne peut pas servir.
    foreach ($p in @(445, 5985, 58620)) {
        try {
            $t = Test-NetConnection -ComputerName $nom -Port $p -WarningAction SilentlyContinue -ErrorAction Stop
            if ($t.RemoteAddress) {
                $ip = $t.RemoteAddress.IPAddressToString
                if (Test-AdresseUtilisable $ip) { return $ip }
            }
        } catch { }
    }
    try {
        # Un enregistrement CNAME n'a pas d'adresse : le test l'écarte aussi.
        foreach ($a in @(Resolve-DnsName $nom -Type A -ErrorAction Stop)) {
            if (Test-AdresseUtilisable $a.IPAddress) { return $a.IPAddress }
        }
    } catch { }
    return ''
}

function Test-Joignable([string]$nom) {
    # Deux tests, parce qu'un seul ment. Le ping peut être bloqué par le
    # pare-feu ; le port 445 est celui dont on a besoin pour la suite.
    if (Test-Connection -ComputerName $nom -Count 2 -Quiet -ErrorAction SilentlyContinue) { return $true }
    try { return (Test-NetConnection -ComputerName $nom -Port 445 -InformationLevel Quiet -WarningAction SilentlyContinue) }
    catch { return $false }
}

<#
    Le port de la Gestion à distance de Windows (WinRM, 5985) répond-il ?

    POURQUOI CE TEST EXISTE
    Répondre au ping ou au port 445 ne prouve rien sur WinRM : ce sont trois
    protocoles différents. Le script se servait de 445 comme feu vert, puis
    copiait 63 Mo, puis échouait sur 5985. La copie était perdue, et le message
    d'erreur de Windows parlait de pare-feu et de sous-réseau alors que le
    service n'était tout simplement pas démarré. On teste maintenant le port
    dont on a réellement besoin, AVANT la copie.
#>
function Test-WinRM([string]$nom) {
    try { return (Test-NetConnection -ComputerName $nom -Port 5985 -InformationLevel Quiet -WarningAction SilentlyContinue) }
    catch { return $false }
}

<#
    Message unique quand WinRM ne répond pas.

    La cause la plus fréquente n'est ni le réseau ni les droits : c'est un
    service à l'arrêt. Sur un Windows client, WinRM démarre « à la demande » et
    personne ne le demande jamais. Les deux commandes proposées passent par RPC
    (port 445), qui lui fonctionne — c'est précisément pourquoi elles
    aboutissent là où Invoke-Command échoue.
#>
function Write-AideWinRM([string]$nom) {
    Grave "La Gestion à distance de Windows (port 5985) ne répond pas sur $nom."
    Alerte 'Le plus souvent, le service WinRM n''est pas démarré sur le poste.'
    Alerte 'À lancer depuis cette fenêtre, puis relancer le script :'
    Write-Host "    sc.exe \\$nom config winrm start= auto" -ForegroundColor Gray
    Write-Host "    sc.exe \\$nom start winrm" -ForegroundColor Gray
}

# ---------------------------------------------------------------- un poste
function Install-SurUnPoste([string]$nom) {
    $resume = [pscustomobject]@{ Poste = $nom; Etat = 'non traité'; Detail = ''; Ip = ''; Pret = $false }
    $script:ipAttendue = ''   # propre à ce poste : ne doit rien hériter du précédent

    # ---- 1. le nom est-il une machine du domaine ?
    Titre "$nom — 1. Compte d'ordinateur"
    $annuaire = Test-PosteDuDomaine $nom
    if (-not $annuaire.AnnuaireOk) {
        Alerte "Annuaire injoignable : impossible de vérifier. On continue quand même."
        Write-Json $nom 'compte' 'info' 'annuaire injoignable, contrôle non fait'
    }
    elseif (-not $annuaire.Connu) {
        Grave "$nom n'est pas un compte d'ordinateur du domaine."
        Alerte "Téléphone, tablette, machine hors domaine ou faute de frappe : rien n'est tenté."
        $resume.Etat = 'ignoré'; $resume.Detail = 'inconnu de l''annuaire'
        Write-Json $nom 'compte' 'echec' 'inconnu de l''annuaire'
        return $resume
    }
    else {
        Bien 'Compte trouvé dans l''annuaire.'
        Write-Json $nom 'compte' 'ok'
    }

    # ---- 2. répond-il ?
    Titre "$nom — 2. Joignable ?"
    if (Test-Joignable $nom) {
        Bien 'Le poste répond.'
        $ip = Get-IpDuPoste $nom
        if (-not $ip -and (Test-MachineLocale $nom)) {
            Alerte 'Ce poste EST la machine locale : aucune adresse distante à constater.'
            Write-Json $nom 'reponse' 'ok' 'machine locale, pas d''adresse distante'
        }
        else {
            Write-Json $nom 'reponse' 'ok' '' $ip
        }
    }
    else {
        Alerte 'Aucune réponse (ni ping, ni port 445).'
        Write-Json $nom 'reponse' 'echec' 'ni ping, ni port 445'

        # VÉRIFICATION SEULE : ON N'ALLUME PAS.
        # Envoyer un paquet de réveil ne modifie rien SUR la machine, mais ça
        # l'allume. Trente postes qui démarrent parce qu'on a cliqué sur
        # « vérifier » est un effet que personne n'a demandé. On s'arrête ici.
        if ($script:verifierSeul) {
            Alerte 'Vérification seule : pas de réveil réseau.'
            Write-Json $nom 'reveil' 'ignore' 'mode vérifier seulement'
            $resume.Etat = 'éteint'; $resume.Detail = 'ne répond pas'
            return $resume
        }

        $mac = Get-MacDuPoste $nom
        if (-not $mac) {
            Grave 'Adresse MAC inconnue : réveil réseau impossible.'
            Alerte "Ajoute une ligne « $nom;AA-BB-CC-DD-EE-FF » dans postes.csv, à côté du script."
            $resume.Etat = 'échec'; $resume.Detail = 'éteint, MAC inconnue'
            Write-Json $nom 'reveil' 'echec' 'adresse MAC inconnue'
            return $resume
        }

        # Adresse attendue : celle du bail DHCP si on l'a, sinon le DNS. Elle ne
        # sert qu'à choisir la carte réseau — le poste étant éteint, elle n'est
        # évidemment pas joignable à cet instant.
        if (-not $script:ipAttendue) {
            try { $script:ipAttendue = (Resolve-DnsName $nom -Type A -ErrorAction Stop | Select-Object -First 1).IPAddress } catch { }
        }

        Alerte "Réveil réseau vers $mac…"
        Send-Reveil $mac $script:ipAttendue
        # Le paquet de réveil est une DIFFUSION : il ne franchit pas les routeurs.
        # Sans objet tant que console et postes partagent le même VLAN — mais un
        # script survit à ses hypothèses, et cette ligne expliquera l'échec le
        # jour où elle deviendra vraie.
        Alerte '(Le réveil ne traverse pas les routeurs : sans effet sur un autre VLAN.)'

        $limite = (Get-Date).AddSeconds($AttenteReveilSecondes)
        while ((Get-Date) -lt $limite -and -not (Test-Joignable $nom)) {
            Start-Sleep -Seconds 10
            Write-Host '  …'
        }
        if (-not (Test-Joignable $nom)) {
            Grave "$nom ne répond pas après $AttenteReveilSecondes secondes."
            $resume.Etat = 'échec'; $resume.Detail = 'pas de réponse après réveil'
            Write-Json $nom 'reveil' 'echec' "pas de réponse après $AttenteReveilSecondes s"
            return $resume
        }
        Bien 'Le poste a répondu après le réveil.'
        Write-Json $nom 'reveil' 'ok' 'réveillé puis joignable'
    }

    if ($script:reveilSeul) {
        $resume.Etat = 'allumé'
        $resume.Ip = Get-IpDuPoste $nom
        if ($resume.Ip) { Bien "Adresse IP : $($resume.Ip)" }
        return $resume
    }

    if ($script:sansInstall) {
        Alerte 'Installation sautée (-SeulementParc) : on ne fait que la mise en parc.'
        $resume.Etat = 'déjà installé'
        return (Set-ModeParc $nom $resume)
    }

    # ---- 3. accès administratif
    Titre "$nom — 3. Accès administratif"
    if (-not (Test-Path "\\$nom\C$")) {
        Grave "\\$nom\C$ inaccessible : droits insuffisants, ou partages administratifs désactivés."
        $resume.Etat = 'échec'; $resume.Detail = 'partage admin inaccessible'
        Write-Json $nom 'partage' 'echec' 'partage administratif inaccessible'
        return $resume
    }
    Bien 'Partage administratif accessible.'
    Write-Json $nom 'partage' 'ok'

    # Le feu vert de la suite, ce n'est pas 445 : c'est 5985. On le vérifie ici,
    # avant de copier 63 Mo qui seraient perdus.
    if (-not (Test-WinRM $nom)) {
        Write-AideWinRM $nom
        $resume.Etat = 'échec'; $resume.Detail = 'WinRM muet (5985)'
        Write-Json $nom 'winrm' 'echec' 'aucune réponse sur 5985'
        return $resume
    }
    Bien 'Gestion à distance disponible (5985).'
    Write-Json $nom 'winrm' 'ok'

    # VÉRIFICATION SEULE : ON S'ARRÊTE ICI, AVANT LA PREMIÈRE ÉCRITURE.
    # Tout ce qui précède est en lecture — compte d'ordinateur, réponse réseau,
    # partage administratif, gestion à distance. Rien n'a touché ce poste, et rien
    # ne va le toucher : la ligne suivante serait la copie du paquet.
    if ($script:verifierSeul) {
        Bien 'Vérification terminée : ce poste peut recevoir le paquet.'
        $resume.Etat = 'vérifié'; $resume.Detail = 'prêt à recevoir le paquet'
        $resume.Ip = Get-IpDuPoste $nom
        return $resume
    }

    # ---- 4. copie et installation
    Titre "$nom — 4. Installation"
    $nomMsi = Split-Path $Msi -Leaf
    $cible  = "\\$nom\C$\Windows\Temp\$nomMsi"

    # LA COPIE EST LA PREMIÈRE ÉCRITURE SUR LE POSTE. Tout ce qui précède était en
    # lecture ; à partir d'ici, le poste est modifié. Un échec de copie se journalise
    # donc comme tel, et pas comme un échec d'installation : le paquet n'est jamais
    # arrivé, il n'y a rien à nettoyer sur place.
    try {
        Copy-Item $Msi $cible -Force -ErrorAction Stop
        $mo = [math]::Round((Get-Item $cible).Length / 1MB)
        Bien "Paquet copié ($mo Mo)."
        Write-Json $nom 'copie' 'ok' "$mo Mo"
    }
    catch {
        Grave "Copie impossible vers $cible : $($_.Exception.Message)"
        $resume.Etat = 'échec'; $resume.Detail = 'copie impossible'
        Write-Json $nom 'copie' 'echec' $_.Exception.Message
        return $resume
    }

    $r = Invoke-Command -ComputerName $nom -ArgumentList $nomMsi, $Langue -ScriptBlock {
        param($nomMsi, $langue)
        $p = Start-Process msiexec -Wait -PassThru -ArgumentList @(
            '/i', "C:\Windows\Temp\$nomMsi", '/qn', "FTPCLANG=$langue",
            '/l*v', 'C:\Windows\Temp\ftpc-install.log'
        )
        [pscustomobject]@{ Code = $p.ExitCode }
    }

    # LE CODE DE SORTIE EST LE SEUL VERDICT. « /qn » supprime toute interface, y
    # compris les messages d'erreur : une installation qui échoue ne dit rien du
    # tout. C'est le défaut constaté le 30/08/2026 sur un poste où le paquet n'a
    # rien installé sans que rien ne le signale.
    switch ($r.Code) {
        0    {
            Bien 'Installé.'
            Write-Json $nom 'installation' 'ok' 'installe' $null $r.Code
        }
        3010 {
            Bien 'Installé — un redémarrage est demandé (sans urgence).'
            # 3010 EST UNE RÉUSSITE, pas un échec : le poste est installé, il
            # demande seulement un redémarrage. L'état le dit, le code le précise.
            Write-Json $nom 'installation' 'ok' 'installe, redemarrage demande' $null $r.Code
        }
        1638 {
            Grave 'Une autre version est déjà installée (1638) : désinstalle-la d''abord.'
            $resume.Etat = 'échec'; $resume.Detail = '1638 — déjà installé'
            Write-Json $nom 'installation' 'echec' 'une autre version est deja installee' $null $r.Code
            return $resume
        }
        default {
            Grave "Échec de l'installation, code $($r.Code)."
            Alerte "Journal resté sur le poste : C:\Windows\Temp\ftpc-install.log"
            Alerte "Le paquet est laissé en place pour une reprise sur site."
            $resume.Etat = 'échec'; $resume.Detail = "msiexec $($r.Code)"
            Write-Json $nom 'installation' 'echec' 'voir C:\Windows\Temp\ftpc-install.log sur le poste' $null $r.Code
            return $resume
        }
    }

    # Ménage : 66 Mo qui ne servent plus. Effacé UNIQUEMENT après un succès —
    # en cas d'échec, il reste sur place avec son journal.
    Remove-Item $cible -Force -ErrorAction SilentlyContinue
    if (Test-Path $cible) { Alerte "Le paquet n'a pas pu être effacé : $cible" }
    else                  { Bien 'Paquet effacé du poste.' }

    Invoke-Command -ComputerName $nom -ScriptBlock {
        $s = Get-Service FaultTracePCMonitor -ErrorAction SilentlyContinue
        if ($s) { "  Service : $($s.Status)" } else { '  Service ABSENT' }
    }

    # ---- 5. mode parc
    $resume.Etat = 'installé'
    return (Set-ModeParc $nom $resume)
}

<#
    Met le poste en mode parc, puis VÉRIFIE que le port répond.

    La vérification n'est pas une coquetterie : la commande peut rendre 0 et le
    poste rester injoignable — pare-feu fermé ailleurs, service arrêté. Attendre
    puis tester le port est la seule preuve utile, et c'est celle qui manquait
    quand la console ne voyait pas le poste sans qu'on sache pourquoi.
#>
function Set-ModeParc([string]$nom, $resume) {
    if (-not $script:faireParc) { return $resume }

    Titre "$nom — Mode parc"
    if (-not $script:secretParc) {
        Grave 'Aucun secret maître disponible : étape sautée.'
        $resume.Detail = 'parc non configuré'
        # « ignore » et non « echec » : rien n'a raté, l'étape n'a pas été tentée.
        Write-Json $nom 'parc' 'ignore' 'aucun secret maitre disponible'
        return $resume
    }

    if (-not (Test-WinRM $nom)) {
        Write-AideWinRM $nom
        $resume.Detail = 'parc non configuré (WinRM muet)'
        Write-Json $nom 'parc' 'echec' 'gestion a distance muette (5985)'
        return $resume
    }

    <#
        POURQUOI ON PASSE PAR DES FICHIERS ET NON PAR LE TUYAU
        FaultTracePC.Cli.exe écrit ses messages en UTF-8. PowerShell 5.1 décode
        la sortie d'un exécutable natif avec [Console]::OutputEncoding, qui vaut
        la page de codes de la machine : « Règle de pare-feu posée » revenait
        « RÃ¨gle de pare-feu posÃ©e ». Le texte n'était pas faux, il était mal
        relu. Rediriger vers un fichier puis le lire en UTF-8 explicitement
        supprime l'étape qui se trompe. Corrigé le 03/09/2026.
    #>
    $sortie = Invoke-Command -ComputerName $nom -ArgumentList $script:secretParc, $Port -ScriptBlock {
        param($s, $port)
        $exe = "$env:ProgramFiles\FaultTracePC\FaultTracePC.Cli.exe"
        if (-not (Test-Path $exe)) {
            return [pscustomobject]@{ Code = -1; Lignes = @("ERREUR : $exe introuvable — le logiciel n'est pas installé.") }
        }
        # Fichier temporaire : la redirection d'entrée standard vers un exécutable
        # natif n'est pas fiable à travers une session distante.
        $f   = Join-Path $env:TEMP 'ftpc-s.txt'
        $out = Join-Path $env:TEMP 'ftpc-o.txt'
        $err = Join-Path $env:TEMP 'ftpc-e.txt'
        try {
            Set-Content $f $s -NoNewline -Encoding ascii
            $p = Start-Process $exe -Wait -PassThru -NoNewWindow `
                -ArgumentList '--configure-remote', '--master-secret', '-', '--port', $port `
                -RedirectStandardInput $f -RedirectStandardOutput $out -RedirectStandardError $err
            $lignes = @()
            foreach ($fichier in @($out, $err)) {
                if (Test-Path $fichier) {
                    $lignes += [IO.File]::ReadAllLines($fichier, [Text.Encoding]::UTF8)
                }
            }
            return [pscustomobject]@{
                Code   = $p.ExitCode
                Lignes = @($lignes | Where-Object { $_ -ne '' })
            }
        }
        finally {
            # Le secret ne survit pas à l'appel, succès ou échec.
            @($f, $out, $err) | ForEach-Object { Remove-Item $_ -Force -ErrorAction SilentlyContinue }
        }
    }
    $sortie.Lignes | ForEach-Object { Write-Host "  $_" }

    <#
        POURQUOI ON REGARDE LE CODE DE SORTIE
        Le contrôle du port, plus bas, prouve que le poste répond — il ne dit
        pas POURQUOI il ne répond pas. Un secret refusé, un fichier de
        configuration verrouillé et un pare-feu fermé donnaient le même
        message : « port muet ». Le code de sortie du CLI sépare « la
        configuration a échoué » de « la configuration a réussi mais le port
        n'est pas joignable » : deux pannes, deux endroits où chercher.
        Ajouté le 05/09/2026, à la demande.
    #>
    if ($null -ne $sortie.Code -and $sortie.Code -ne 0) {
        Grave "La configuration a échoué : FaultTracePC.Cli.exe a rendu $($sortie.Code)."
        Alerte 'Les lignes ci-dessus viennent du logiciel lui-même : elles disent quoi corriger.'
        Alerte 'Le poste restera INVISIBLE de la console tant que ce n''est pas réglé.'
        $resume.Detail = "parc échoué (code $($sortie.Code))"
        Write-Json $nom 'parc' 'echec' 'configuration refusee par FaultTracePC.Cli.exe' $null $sortie.Code
        return $resume   # inutile d'attendre 35 secondes pour un port qui ne s'ouvrira pas
    }

    # Le service relit sa configuration toutes les 30 secondes : on laisse le
    # temps, puis on vérifie au lieu de supposer.
    Alerte 'Attente de la relecture par le service (35 s)…'
    Start-Sleep -Seconds 35
    $ouvert = try { Test-NetConnection -ComputerName $nom -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue }
              catch { $false }

    if ($ouvert) {
        Bien "Le port $Port répond : le poste est prêt pour la console."
        $resume.Detail = 'parc OK'
        $resume.Pret = $true
        $resume.Ip = Get-IpDuPoste $nom
        Write-Json $nom 'parc' 'ok' "port $Port joignable" $resume.Ip
    }
    else {
        # LA CONFIGURATION A RÉUSSI, LE PORT NE RÉPOND PAS : deux choses
        # différentes, et le journal doit permettre de les distinguer. Le poste est
        # configuré — il restera simplement invisible tant que le chemin réseau ne
        # sera pas ouvert.
        Grave "Le port $Port ne répond pas depuis cette machine."
        Alerte 'Causes possibles : règle de pare-feu absente, service arrêté, ou VLAN filtré.'
        $resume.Detail = 'parc configuré, port muet'
        Write-Json $nom 'parc' 'echec' "configure, mais le port $Port ne repond pas"
    }
    return $resume
}

# ================================================================== une session
<#
    Le corps est une FONCTION et non le corps du script : ainsi « return »
    abandonne la session en cours sans fermer la fenêtre, et l'on peut proposer
    de recommencer. C'est la différence entre « ce poste a échoué » et « la
    soirée est finie ».
#>
function Invoke-Session {
    Titre 'FaultTracePC — déploiement sur postes distants'
    Initialize-Parametres
    Write-Host "  Journal de cette exécution : $journal"
    if ($script:cheminJson) { Write-Host "  Journal JSON : $script:cheminJson" }

    # Le fichier prime sur la saisie, et la saisie n'est proposée que s'il n'y a
    # ni -Poste ni -FichierPostes : c'est ce qui permet de piloter le script sans
    # personne devant.
    if (-not $Poste -and $FichierPostes) {
        $Poste = Get-PostesDuFichier $FichierPostes
        if ($Poste) { Write-Host "  $($Poste.Count) poste(s) lu(s) dans $FichierPostes" }
    }

    if (-not $Poste -and -not $FichierPostes) {
        $saisie = Read-Host 'Nom du ou des postes, séparés par une virgule (ex. PC-1, PC-2)'
        $Poste = $saisie -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }
    $Poste = $Poste | ForEach-Object { $_.ToUpperInvariant() } | Select-Object -Unique
    if (-not $Poste) { Grave 'Aucun poste indiqué.'; return }

    <#
        CE QUE LE DOUBLE-CLIC DOIT POUVOIR FAIRE

        La mise en parc était derrière le commutateur -ConfigurerParc, qu'on ne
        peut PAS passer en double-cliquant sur le .cmd : la fonctionnalité était
        donc inaccessible par le chemin le plus fréquent, et le poste s'installait
        sans jamais devenir visible depuis la console.

        La question est maintenant posée quand aucun choix n'a été passé en
        paramètre. Les commutateurs restent utilisables pour un usage scripté.
    #>
    # Remise à l'état initial : sans ça, un second tour hériterait des choix du
    # premier, ce qui installerait là où l'on ne voulait que réveiller.
    $script:faireParc    = [bool]$ConfigurerParc
    $script:sansInstall  = [bool]$SeulementParc
    $script:reveilSeul   = [bool]$ReveilSeulement
    $script:verifierSeul = [bool]$VerifierSeulement

    # La vérification seule l'emporte sur tout le reste, et ne pose aucune
    # question : elle est faite pour être pilotée par la console.
    if ($script:verifierSeul) {
        $script:faireParc = $false
        $script:sansInstall = $false
        $script:reveilSeul = $false
        Alerte 'Vérification seule : rien ne sera réveillé, copié, installé ni mis en parc.'
    }
    elseif (-not $PSBoundParameters.ContainsKey('ConfigurerParc') -and
        -not $PSBoundParameters.ContainsKey('SeulementParc') -and
        -not $PSBoundParameters.ContainsKey('ReveilSeulement')) {
        Write-Host ''
        Write-Host '  Que faire sur ce ou ces postes ?' -ForegroundColor Cyan
        Write-Host '    1 = installer seulement'
        Write-Host '    2 = installer ET mettre en parc   (poste neuf)'
        Write-Host '    3 = mettre en parc seulement      (poste déjà installé)'
        Write-Host '    4 = réveiller seulement           (allumer le poste, rien d''autre)'
        switch ((Read-Host 'Ton choix [1/2/3/4]').Trim()) {
            '2' { $script:faireParc = $true }
            '3' { $script:faireParc = $true; $script:sansInstall = $true }
            '4' { $script:reveilSeul = $true }
            '1' { }
            default { Alerte 'Choix non reconnu : installation seule.' }
        }
    }

    if (-not $script:verifierSeul -and -not $script:faireParc -and -not $script:reveilSeul) {
        Alerte 'Mode parc non demandé : le poste sera installé mais restera INVISIBLE de la console.'
    }

    # Le paquet n'est nécessaire que si l'on installe : ne pas bloquer un simple
    # réveil ou une mise en parc parce qu'un partage est indisponible.
    if (-not $script:verifierSeul -and -not $script:reveilSeul -and -not $script:sansInstall -and (-not $Msi -or -not (Test-Path $Msi))) {
        Grave $(if ($Msi) { "Paquet introuvable : $Msi" } else { 'Aucun chemin de paquet MSI indiqué.' })
        Alerte 'Vérifie le partage, ou passe -Msi <chemin>.'
        return
    }

    # Secret maître : par fichier, sinon demandé une seule fois pour tout le lot.
    # Read-Host -AsSecureString n'affiche rien à l'écran et ne laisse pas la
    # valeur dans l'historique de la console.
    $script:secretParc = $null
    if ($script:faireParc) {
        if ($SecretFichier -and (Test-Path $SecretFichier)) {
            $script:secretParc = (Get-Content $SecretFichier -Raw).Trim()
            Bien "Secret maître lu dans $SecretFichier."
        }
        else {
            $sec = Read-Host 'Secret maître du parc (rien ne s''affiche)' -AsSecureString
            $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
            try { $script:secretParc = [Runtime.InteropServices.Marshal]::PtrToStringAuto($ptr).Trim() }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
        }
        if (-not $script:secretParc -or $script:secretParc.Length -lt 32) {
            Grave 'Secret maître absent ou trop court (32 caractères au minimum).'
            Alerte 'Il se produit avec « FaultTracePC.Cli.exe --generate-master-secret ».'
            return
        }
        Bien 'Secret maître accepté (il ne sera jamais affiché ni journalisé).'
    }

    Write-Host "  $($Poste.Count) poste(s) à traiter : $($Poste -join ', ')"

    $bilan = foreach ($p in $Poste) {
        try { Install-SurUnPoste $p }
        catch {
            Grave "$p — interruption : $($_.Exception.Message)"
            [pscustomobject]@{ Poste = $p; Etat = 'échec'; Detail = $_.Exception.Message }
        }
    }

    Titre 'Bilan'
    $bilan | Format-Table Poste, Etat, Detail, Ip -AutoSize

    <#
        CE QU'IL FAUT SAISIR DANS LA CONSOLE, écrit noir sur blanc.

        L'adresse affichée est celle par laquelle ce script a RÉELLEMENT joint le
        poste — pas une entrée DNS, pas un souvenir. Une adresse saisie de mémoire
        donne un poste « injoignable » que rien n'explique.

        Et la recommandation est de saisir le NOM, pas l'adresse : en DHCP,
        l'adresse changera, le nom non. L'IP n'est là que pour vérifier, ou pour
        les rares postes dont la résolution de nom est capricieuse.
    #>
    $prets = @($bilan | Where-Object Pret)
    if ($prets.Count -gt 0) {
        Titre 'À saisir dans la console de parc'
        foreach ($m in $prets) {
            Write-Host ''
            Write-Host "  Nom   : $($m.Poste)" -ForegroundColor Green
            Write-Host "  Hôte  : $($m.Poste)   <- le NOM, pas l'adresse : en DHCP l'adresse changera"
            Write-Host "  Port  : $Port"
            Write-Host "  Jeton : (laisser VIDE — il se déduit du secret maître)"
            if ($m.Ip) { Write-Host "          adresse constatée à l'instant : $($m.Ip)" -ForegroundColor DarkGray }
        }
    }

    Write-Host ''
    if ($script:verifierSeul) {
        # « EXAMINÉ », PAS « TRAITÉ ». Un poste éteint a bel et bien été examiné :
        # l'annoncer « 0 traité sur 1 » laisserait croire que la vérification n'a pas
        # eu lieu, alors qu'elle a eu lieu et qu'elle a trouvé quelque chose.
        # Constaté le 19/09/2026 au premier essai réel.
        $prets = @($bilan | Where-Object { $_.Etat -eq 'vérifié' }).Count
        Write-Host "  $($Poste.Count) poste(s) examiné(s), dont $prets prêt(s) à recevoir le paquet."
        Write-Host '  Aucun poste n''a été modifié.' -ForegroundColor DarkGray
    }
    else {
        $ok = @($bilan | Where-Object { $_.Etat -in 'installé','déjà installé','allumé' }).Count
        Write-Host "  $ok poste(s) traité(s) sur $($Poste.Count)."
    }
}

# ================================================================== boucle
$posteInitial = $Poste
try {
    do {
        $Poste = $posteInitial     # la saisie précédente ne doit pas resservir
        try { Invoke-Session }
        catch {
            Grave "Interruption : $($_.Exception.Message)"
            Alerte "Détail complet dans $journal"
        }

        # EN VÉRIFICATION SEULE, ON NE DEMANDE RIEN ET ON SORT.
        # Ce mode est fait pour être lancé par la console : une invite sans personne
        # devant retiendrait la fenêtre ouverte indéfiniment, et le logiciel
        # attendrait la fin d'un processus qui n'arrive jamais.
        if ($script:verifierSeul) { $recommencer = $false }
        else {
            Write-Host ''
            Write-Host '  1 = fermer' -ForegroundColor Cyan
            Write-Host '  2 = recommencer depuis le début'
            $recommencer = (Read-Host 'Ton choix [1/2]').Trim() -eq '2'
        }
    } while ($recommencer)
}
finally {
    Stop-Transcript | Out-Null
}
