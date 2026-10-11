using Microsoft.EntityFrameworkCore;
using ServicioRegistroCrm;
using ServicioRegistroCrm.Configuracion;
using ServicioRegistroCrm.Datos;
using ServicioRegistroCrm.Servicios;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Credenciales y URLs con code: fuera del repositorio (ver appsettings.Local.example.json).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddWindowsService(o => o.ServiceName = "ServicioRegistroCrm");

builder.Services.Configure<CrmOptions>(builder.Configuration.GetSection(CrmOptions.Seccion));

var timeout = builder.Configuration.GetValue($"{CrmOptions.Seccion}:TimeoutSegundos", 60);
builder.Services.AddHttpClient<CrmApiClient>(c => c.Timeout = TimeSpan.FromSeconds(timeout));

var rutaDb = Worker.RutaAbsoluta(builder.Configuration.GetValue("BaseDatos:Archivo", "registro_crm.db")!);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={rutaDb}"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

host.Run();
