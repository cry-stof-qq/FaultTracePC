<div align="center">

<img src="assets/FaultTracePC.png" alt="FaultTracePC" width="120">

# FaultTracePC

**Find out why a Windows 10/11 machine crashed — and what to do about it.**

Crash dump analysis, a real-time flight recorder, alerts before the failure,
a readable report and guided repair. Free, no telemetry, no account.

🇬🇧 English · [🇫🇷 Français](README.fr.md)

[![Build and tests](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/ci.yml/badge.svg)](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/ci.yml)
[![Installer](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/installeur.yml/badge.svg)](https://github.com/cry-stof-qq/FaultTracePC/actions/workflows/installeur.yml)

Every commit is built and tested on a clean Windows runner, and released
packages are produced by that same automated build — not from a workstation.

</div>

---

> **Language.** Since 1.3.0 the interface, the reports, the command line and the
> generated repair script are available **in English and in French**. The language
> follows your Windows display language on first run; a selector in the header
> lets you override it, and `--lang fr|en|auto` does the same from the command
> line. The **installer is still French-only** — that is a separate piece of work.

---

## Why this exists

When a PC crashes, Windows already records everything you need to understand
it — and makes it unreadable. The evidence is scattered across Event Viewer,
Reliability Monitor, `.dmp` files, disk SMART counters and hardware sensors.
Existing tools each read *one* of those sources: one decodes dumps, another
shows temperatures, a third lists crashes. None of them cross-reference, and
none of them tell you what to actually do.

And when you finally get an answer, it usually looks like this: **the faulting
driver is `nvlddmkm.sys`**. Accurate, and useless to anyone who doesn't know
what a driver is.

FaultTracePC collects all of those sources, cross-references them, and produces
**a verdict with an honest confidence level** — then names the software to
install, uninstall or update, and in what order.

## What it does

**Post-mortem analysis.** It reads dump files (`Minidump`, `MEMORY.DMP`,
`LiveKernelReports`), natively extracts the STOP code and its parameters, and —
if WinDbg is installed — runs symbolic analysis to **name the faulting driver**
with its call stack. It then cross-references the event log (BugCheck,
Kernel-Power, WHEA, disk errors, display driver resets, application crashes,
memory exhaustion), Reliability Monitor, the driver inventory, disk SMART health
and running processes. Repeated crashes sharing a signature are flagged as such,
which separates a one-off accident from a systemic fault.

**Every driver gets a name, an owner and an action.** A database of **59
documented drivers** maps a `.sys` file to the hardware or software that
installs it, and to the proven fix — `nvlddmkm.sys` becomes "NVIDIA display
driver, clean reinstall with DDU in safe mode". Drivers absent from the database
are matched to their **platform family** (AMD, Intel, Realtek, Qualcomm,
VirtualBox, Fortinet, OEM vendors…) only when the filename **and** the vendor
recorded in the file agree. The report always states which of the two levels it
reached: a by-name match gives the precise fix, a family match gives generic but
correct advice.

One category matters more than the rest: **files blamed by mistake**.
`ntoskrnl.exe`, `fltmgr.sys` and `dxgkrnl.sys` top most crash analyses because
they *observe* the error rather than cause it — and a beginner following a forum
thread ends up trying to delete a vital Windows component. The report says so
explicitly, and points to where the cause actually lives.

**Real-time monitoring — the flight recorder.** A lightweight Windows service
(< 1 % CPU) continuously records temperatures, memory and critical events. Each
line is *physically flushed* to disk: the last seconds before a crash survive
the crash. That is what makes it possible to say "the CPU was at 97 °C right
before shutdown" — something no post-mortem analysis can reconstruct.

**Alerts before the failure.** The service watches thresholds (temperatures,
virtual memory, WHEA errors, disk health) and raises a Windows notification
*before* the machine goes down.

**Real hardware state.** Disk health counters are read directly from the
hardware, by the path each technology requires:

- **SATA/ATA** — raw SMART attributes via WMI: reallocated sectors, pending
  sectors, uncorrectable sectors, CRC errors, power-on hours, SSD wear. The
  report states plainly whether there are **bad sectors**, and distinguishes a
  dying disk from a **failing SATA cable** — a few-euro fault regularly mistaken
  for a dead drive.
- **NVMe** — health log (log page 0x02) read through `DeviceIoControl`, the way
  dedicated tools do it. Windows does not expose these counters over WMI:
  without this path an NVMe SSD simply cannot be diagnosed. It yields the
  **available spare** compared against the manufacturer's threshold (the real
  end-of-life signal for NVMe), **data integrity errors**, and the critical
  warnings the controller raises itself.

When no counter can be read, the report **says so**, rather than printing an
empty table that would pass a missing measurement off as a clean bill of health.

On laptops, **battery wear** is reported as a percentage with a plain verdict.

**"I don't know what's wrong with it."** A single button, designed for someone
who has no way to arbitrate a technical question. It creates a restore point,
examines the machine, applies the repairs that cannot break anything **on its
own**, re-checks, and concludes **in one sentence**. Anything that reboots,
installs or uninstalls is offered **at the end, one action at a time, with the
reason the assistant did not take it for you**. Where no restore point can be
created — System Protection disabled, common in managed environments — it offers
to enable it, and failing that continues in **reduced mode**, refusing to touch
system files at all rather than performing a quiet irreversible change.

**Guided repair.** Each diagnosis generates a PowerShell script tailored to the
problems found — which starts by creating a restore point and runs nothing
without confirmation. A built-in toolbox gathers the common repairs: restore
point, uninstall a faulty Windows update, reset Windows Update components,
`sfc`, `DISM`, `chkdsk`, memory diagnostic, SMART, disk cleanup, Microsoft
Defender scan, network reset. A dedicated window drives **Windows Update** and
surfaces what the Settings page hides — **optional** and **driver** updates —
with per-row selection and **never an automatic reboot**. Repairs that modify
the system can no longer run concurrently: `sfc` and `DISM` contend for the
component store, so one modifying action runs at a time.

**Is the problem still there?** When software is implicated, the report checks
whether it is still installed, has been removed, or has been **reinstalled or
updated since the last crash** — instead of displaying a solved problem forever.

**Temperature over time.** It isn't the temperature at one instant that predicts
a crash, it's the accumulated time spent too high: *"40 minutes above 90 °C this
week"*, with the longest continuous episodes. Time while the machine was off is
never counted, and the calculation deliberately under-reports rather than
inflating a figure meant to raise an alarm.

**PDF export, on demand.** One button produces a PDF of the **complete** report,
technical details included, to attach to a ticket. No PDF is ever generated
automatically.

**History and fleet.** Every scan is archived, so the next one answers the real
question — *did the repair work?* In fleet mode, a console shows the state of
several machines and can trigger a remote diagnosis without walking to the desk.

**Fleet comparator.** What no individual diagnosis can see: an identical old
driver on six machines is not a suspect, it's a deployment image to fix — once,
for the whole fleet. The comparator reports what is **shared** (driver, stop
code, degrading disk model), what **diverges** (same driver at several versions:
the laggards are named) and what is **isolated** (one machine accumulating
problems alone, needing individual treatment). Below two machines it produces
nothing, and says so.

**Am I up to date?** The `🔄` button compares the version actually embedded in
the executable against the latest published on
[the releases page](https://github.com/cry-stof-qq/FaultTracePC/releases/latest).
If there is something new it shows the changes and offers to open the download
page — **it downloads nothing and installs nothing by itself**: on a
GPO-deployed fleet, an executable that updates itself unasked is a risk, not a
service. The startup check is **off by default**: unless you tick `au démarrage`,
FaultTracePC never contacts the Internet on its own.

## Install

| Format | For whom | How |
|---|---|---|
| **MSI** | Permanent install, GPO deployment | `msiexec /i FaultTracePC-1.5.1.msi` (or double-click) |
| **Portable (.zip)** | USB-stick troubleshooting, nothing to install | Unzip, run `FaultTracePC.exe` |

Both are on the [Releases page](../../releases). No prerequisites: the .NET
runtime is bundled. **Windows 10 or 11, 64-bit, administrator rights** (required
to read dumps and full system logs).

To set the language for a whole machine at deployment time:
`msiexec /i FaultTracePC-1.5.1.msi FTPCLANG=en /qn`. It is a default — a user's
own choice in the application still wins.

**Deploying to several machines:** the full procedure — master secret,
headless configuration, firewall, the checks to run on the day and the known
limits — is in [docs/DEPLOIEMENT.md](docs/DEPLOIEMENT.md) (in French).

Optional but recommended — symbolic dump analysis, which names the faulting
driver, needs WinDbg:

```powershell
winget install Microsoft.WinDbg
```

**These files are not code-signed.** On first run Windows SmartScreen will show
"Unknown publisher" — expected for free software without a code-signing
certificate. Click "More info", then "Run anyway". The full source is here and
you can rebuild it yourself with `dotnet build`.

## Usage

1. Launch FaultTracePC (it requests administrator elevation).
2. **Analyse this machine** — the HTML report opens in your browser. It starts
   in simple mode; a button reveals the technical detail.
3. **Real-time monitoring** — installs the flight recorder in one click. It
   keeps running with the application closed, and restarts with the PC.
4. **Toolbox** — one-click repairs, in a visible PowerShell window.

From the command line, for a fleet or a scheduled task:

```powershell
FaultTracePC.Cli.exe --quiet --json --days 90 --output \\server\Diagnostics$
# exit codes: 0 healthy · 1 warnings · 2 critical · 3 error
```

## Limits — stated honestly

- **The installer is French-only.** The application and its reports are not —
  see the note at the top.
- **Without WinDbg**, the STOP code is read but the faulting driver often stays
  unidentified: the diagnosis is less precise, and the report lowers its stated
  confidence accordingly rather than hiding the gap.
- **CPU temperatures depend on the machine.** The legacy driver used by sensor
  libraries is blocked on recent Windows 11; installing [PawnIO](https://pawnio.eu)
  restores the reading. GPU temperatures work without it.
- **A diagnosis is not a certainty.** Every conclusion carries a confidence
  level — "low" marks a lead to check, not a proof.
- **Nothing is sent anywhere.** No telemetry, no account. Reports stay in
  `Documents\FaultTracePC`. The optional network mode accepts private addresses
  only, and signed requests only.
- Some antivirus products react to it, because FaultTracePC does exactly what a
  diagnostic tool does: read memory dumps, query hardware at a low level, and —
  if you enable fleet mode — listen on a local network port. **Do not disable
  your antivirus for it.** Read the source instead.

## Network mode (optional)

A machine can publish its state **read-only** for an administration console.
Two locks: only private addresses (RFC 1918) are accepted, **and** every request
must carry an HMAC-SHA256 signature — the secret never travels over the network,
and replaying a captured request is refused. A firewall rule restricted to the
same ranges is added on top. Nothing is reachable from the Internet.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/cry-stof-qq/FaultTracePC.git
cd FaultTracePC
dotnet build
dotnet test                                    # 250 tests
dotnet run --project src\FaultTracePC.App
```

Produce the distributables:

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Zip
powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1 -Version 1.5.1
```

## Architecture

```
src/
  FaultTracePC.Core      Collection (WMI, events, dumps, sensors), rules engine,
                         STOP code catalogue, driver knowledge base, HTML report
                         generation
  FaultTracePC.App       WPF interface: scan, viewer, fleet console, toolbox,
                         network configuration
  FaultTracePC.Monitor   Windows service: flight recorder, preventive alerts,
                         signed telemetry API
  FaultTracePC.Cli       Command-line diagnosis (fleet, GPO)
tests/                   xUnit tests: security, dump parsing, rules
```

## Contributing

Issues and pull requests are welcome, in English or French.

The most useful thing you can report right now: **whether you want an English
interface**. The translation is a real piece of work and it will only be done if
there is demand — an issue saying so is what makes that decision.

## Licence

[MIT](LICENSE) — free to use, modify and redistribute, including commercially
and in schools and businesses.

This software comes with no warranty. It reads system state and changes nothing
without explicit user confirmation.
