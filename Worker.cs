using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioRegistroCrm.Configuracion;
using ServicioRegistroCrm.Datos;
using ServicioRegistroCrm.Servicios;

namespace ServicioRegistroCrm
{
    public class Worker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RepositorioResultados _repositorio;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<Worker> _logger;
        private readonly CrmOptions _opciones;

        public Worker(
            IServiceScopeFactory scopeFactory,
            RepositorioResultados repositorio,
            IOptions<CrmOptions> options,
            IHostApplicationLifetime lifetime,
            ILogger<Worker> logger)
        {
            _scopeFactory = scopeFactory;
            _repositorio = repositorio;
            _opciones = options.Value;
            _lifetime = lifetime;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de registro CRM iniciado");

            do
            {
                try
                {
                    await ProcesarAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado en el proceso de registro");
                }

                if (_opciones.IntervaloMinutos <= 0)
                {
                    _lifetime.StopApplication();
                    break;
                }

                await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken)
                    .ContinueWith(_ => { }, CancellationToken.None);
            } while (!stoppingToken.IsCancellationRequested);
        }

        private async Task ProcesarAsync(CancellationToken ct)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var api = scope.ServiceProvider.GetRequiredService<CrmApiClient>();

                var reloj = Stopwatch.StartNew();
                var resultado = new ResultadoRegistro { FechaEjecucion = DateTime.Now };

                var rutaSolicitud = RutaAbsoluta(_opciones.ArchivoSolicitud);
                string solicitudJson;
                using (var lector = new StreamReader(rutaSolicitud))
                    solicitudJson = await lector.ReadToEndAsync();
                resultado.SolicitudJson = solicitudJson;
                resultado.NumeroIdentificacion = LeerIdentificacion(solicitudJson);

                _logger.LogInformation("Solicitando token...");
                var token = await api.GenerarTokenAsync(ct);

                if (!token.Exitoso)
                {
                    resultado.Etapa = "Token";
                    resultado.Exitoso = false;
                    resultado.CodigoHttp = token.Respuesta.CodigoHttp;
                    resultado.Mensaje = "No se pudo obtener el token: " + token.Respuesta.Mensaje;
                    resultado.RespuestaJson = token.Respuesta.Cuerpo;
                }
                else
                {
                    _logger.LogInformation("Token obtenido. Enviando datos de {Identificacion}...", resultado.NumeroIdentificacion);
                    var registro = await api.EnviarRegistroAsync(solicitudJson, token.Token, ct);

                    resultado.Etapa = registro.Exitoso ? "Completado" : "Registro";
                    resultado.Exitoso = registro.Exitoso;
                    resultado.CodigoHttp = registro.CodigoHttp;
                    resultado.Mensaje = registro.Mensaje;
                    resultado.RespuestaJson = registro.Cuerpo;
                }

                resultado.DuracionMs = reloj.ElapsedMilliseconds;
                resultado.ArchivoTxt = await ReporteTxt.EscribirAsync(RutaAbsoluta(_opciones.CarpetaResultados), resultado);

                await _repositorio.GuardarAsync(resultado, ct);

                _logger.Log(resultado.Exitoso ? LogLevel.Information : LogLevel.Warning,
                    "Proceso {Estado} (registro #{Id}): {Mensaje}. Archivo: {Archivo}",
                    resultado.Exitoso ? "EXITOSO" : "SIN ÉXITO", resultado.Id, resultado.Mensaje, resultado.ArchivoTxt);
            }
        }

        private static string LeerIdentificacion(string json)
        {
            using (var doc = JsonDocument.Parse(json))
            {
                return doc.RootElement.TryGetProperty("numero_identificacion", out var id) ? id.ToString() : "SIN_ID";
            }
        }

        // Un servicio de Windows arranca en System32, así que las rutas relativas se resuelven contra la carpeta del ejecutable.
        public static string RutaAbsoluta(string ruta)
            => Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);
    }
}
