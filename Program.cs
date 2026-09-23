using PlcMonitor.Data;
using PlcMonitor.Monitoring;
using PlcMonitor.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PlcOptions>(builder.Configuration.GetSection("Plc"));

var dbPath = builder.Configuration.GetValue<string>("Database:Path") ?? "stoppages.db";
builder.Services.AddSingleton(new StoppageRepository(dbPath));
builder.Services.AddSingleton<StoppageDetector>();
builder.Services.AddHostedService<MonitoringService>();

var app = builder.Build();

// ── THESE TWO LINES MUST BE PRESENT AND IN THIS ORDER ──
app.UseDefaultFiles();      // serves index.html when "/" is requested
app.UseStaticFiles();       // serves /css/style.css, /js/app.js, etc.

app.MapDashboard();

app.Run();