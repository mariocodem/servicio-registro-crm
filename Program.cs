using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServicioRegistroCrm.Configuracion;
using ServicioRegistroCrm.Datos;
using ServicioRegistroCrm.Servicios;

namespace ServicioRegistroCrm
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });

            // Credenciales y URLs con code: fuera del repositorio (ver appsettings.Local.example.json).
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

            builder.Services.AddWindowsService(o => o.ServiceName = "ServicioRegistroCrm");

            builder.Services.Configure<CrmOptions>(builder.Configuration.GetSection(CrmOptions.Seccion));

            var timeout = builder.Configuration.GetValue(CrmOptions.Seccion + ":TimeoutSegundos", 60);
            builder.Services.AddHttpClient<CrmApiClient>(c => c.Timeout = TimeSpan.FromSeconds(timeout));

            var rutaDb = Worker.RutaAbsoluta(builder.Configuration.GetValue("BaseDatos:Archivo", "registro_crm.db"));
            builder.Services.AddSingleton(new RepositorioResultados(rutaDb));

            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();

            host.Services.GetRequiredService<RepositorioResultados>().CrearTablaSiNoExiste();

            host.Run();
        }
    }
}
