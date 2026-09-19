@echo off
rem ---------------------------------------------------------------------------
rem  FaultTracePC - lanceur du script de deploiement (double-clic)
rem
rem  Demande l'elevation puis lance le .ps1 place A COTE de ce fichier : les
rem  deux se deplacent ensemble, depuis n'importe quel dossier ou une cle USB.
rem
rem  POURQUOI UN ENROBAGE ET PAS UN SIMPLE -File
rem  Un "pause" dans ce .bat ne retient que la fenetre NON elevee : la fenetre
rem  administrateur, elle, se refermait aussitot. Et une erreur d'ANALYSE du
rem  .ps1 survient avant sa premiere ligne : meme un Read-Host ecrit a la fin du
rem  script ne serait jamais atteint. L'enrobage ci-dessous charge le script
rem  DEPUIS l'interieur d'un try/catch/finally : l'erreur s'affiche, et le
rem  finally met en pause dans tous les cas - refus de strategie, erreur de
rem  syntaxe, plantage.
rem ---------------------------------------------------------------------------

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command','& { try { & ''%~dp0Deployer-FaultTracePC.ps1'' } catch { Write-Host $_ -ForegroundColor Red } finally { Read-Host ''Appuyer sur Entree pour fermer'' } }'"
