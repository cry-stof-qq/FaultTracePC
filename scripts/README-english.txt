Deployer-FaultTracePC - install FaultTracePC on remote computers
================================================================
Version 1.1 - 5 September 2026

IMPORTANT - THIS SCRIPT IS IN FRENCH
Its prompts, menus and messages are French only. Nothing else about it is
language-specific: it drives msiexec and FaultTracePC.Cli.exe, which are
translated. This file exists so that you know what you are downloading before
you run it. An English version may follow; it does not exist today.

WHAT IT IS
A PowerShell script that installs FaultTracePC on Windows computers over the
network, one or several at a time, and optionally registers them with the
fleet console. Four files, meant to stay in the SAME folder - they run from
anywhere, including a USB stick.

  Deployer-FaultTracePC.cmd   double-click this one (asks for elevation)
  Deployer-FaultTracePC.ps1   the script itself
  LISEZMOI.txt                the full manual, in French
  postes.csv                  MAC address directory, for Wake-on-LAN

UNBLOCK THE FILES FIRST
Windows marks anything downloaded from the Internet (Mark of the Web) and
refuses to run a script so marked. In the extracted folder:

  Get-ChildItem . -Recurse | Unblock-File

REQUIREMENTS
- A Windows domain, and administrator rights on the target computers.
- WinRM answering on the target (port 5985). On a Windows client the service
  starts "on demand" and nobody ever demands it: the computer answers ping and
  port 445 while 5985 stays silent. The script checks this before copying
  anything, and prints the two commands that fix it remotely over RPC:
      sc.exe \\<computer> config winrm start= auto
      sc.exe \\<computer> start winrm
  On a fleet, the durable answer is a group policy setting the "Windows Remote
  Management (WS-Management)" service to Automatic.
- Access to \\<computer>\C$.

WHAT IT DOES, FOR EACH COMPUTER YOU NAME
  1. checks the name is a domain computer account;
  2. checks it answers - ping OR port 445;
  3. checks WinRM answers, BEFORE copying 63 MB that would be wasted;
  4. wakes it with Wake-on-LAN if it is off;
  5. copies the MSI locally and installs it, checking the EXIT CODE, then
     deletes the package;
  6. optionally configures fleet mode, CHECKS the tool's exit code, then
     verifies the port really answers.

The package is COPIED to the target rather than installed from a share: a
remote session cannot present your identity to a THIRD computer (the Kerberos
"double hop"). Being a domain administrator does not change this.

WHAT IT SENDS ANYWHERE
Nothing. No telemetry, no account, no Internet access. On first run it asks for
your MSI path and your DHCP server and offers to save them next to the script
in parametres.json - that file names your servers, so do not redistribute it.
The fleet master secret is asked every time and is never written, displayed or
logged.

NO WARRANTY
MIT licence. This script is not signed, not supported, and was written for one
school's fleet before being published as-is. Read it before running it - it is
commented throughout, in French, including the reasons behind each choice.

https://palisser.fr - https://github.com/cry-stof-qq/FaultTracePC
