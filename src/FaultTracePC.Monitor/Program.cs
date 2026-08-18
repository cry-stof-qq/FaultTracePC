using FaultTracePC.Core;
using FaultTracePC.Monitor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Service « boîte noire » FaultTracePC : enregistre en continu l'état de la machine
// (charge, températures, mémoire, top processus) et les événements critiques,
// dans un journal écrit et synchronisé sur disque à chaque ligne — pour que les
// dernières secondes AVANT un crash survivent au crash.
//
// Exécutable aussi en console pour test : FaultTracePC.Monitor.exe (Ctrl+C pour arrêter).

// Le service tourne sous le compte SYSTEM : il n'a pas de préférence utilisateur
// à lire et suivra donc la langue par défaut de la machine, sauf si le service
// est installé avec « --lang ». Ses journaux et ses notifications sont les seuls
// textes concernés.
// Gardes d'abord. Ce service est le composant le plus difficile à observer : il
// tourne sous SYSTEM, sans interface, souvent sans personne devant l'écran. Une
// exception non rattrapée ne laissait jusqu'ici aucune trace exploitable.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    ErrorLog.Write("monitor", e.ExceptionObject as Exception);

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    ErrorLog.Write("monitor background task", e.Exception);
    e.SetObserved();
};

Lang.Initialize(args);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "FaultTracePCMonitor");
builder.Services.AddHostedService<FlightRecorderService>();
// API de télémétrie (mode Client uniquement — s'endort en mode Local).
builder.Services.AddHostedService<TelemetryService>();
builder.Build().Run();
