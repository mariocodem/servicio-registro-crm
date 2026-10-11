using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ServicioRegistroCrm.Configuracion;
using ServicioRegistroCrm.Datos;
using ServicioRegistroCrm.Servicios;

namespace ServicioRegistroCrm;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<CrmOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly CrmOptions _opciones = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Servicio de registro CRM iniciado");

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
                logger.LogError(ex, "Error inesperado en el proceso de registro");
            }

            if (_opciones.IntervaloMinutos <= 0)
            {
                lifetime.StopApplication();
                break;
            }

            await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken)
                .ContinueWith(_ => { }, CancellationToken.None);
        } while (!stoppingToken.IsCancellationRequested);
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<CrmApiClient>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reloj = Stopwatch.StartNew();
        var resultado = new ResultadoRegistro { FechaEjecucion = DateTime.Now };

        var rutaSolicitud = RutaAbsoluta(_opciones.ArchivoSolicitud);
        var solicitudJson = await File.ReadAllTextAsync(rutaSolicitud, ct);
        resultado.SolicitudJson = solicitudJson;
        resultado.NumeroIdentificacion = LeerIdentificacion(solicitudJson);

        logger.LogInformation("Solicitando token...");
        var token = await api.GenerarTokenAsync(ct);

        if (!token.Exitoso)
        {
            resultado.Etapa = "Token";
            resultado.Exitoso = false;
            resultado.CodigoHttp = token.Respuesta.CodigoHttp;
            resultado.Mensaje = $"No se pudo obtener el token: {token.Respuesta.Mensaje}";
            resultado.RespuestaJson = token.Respuesta.Cuerpo;
        }
        else
        {
            logger.LogInformation("Token obtenido. Enviando datos de {Identificacion}...", resultado.NumeroIdentificacion);
            var registro = await api.EnviarRegistroAsync(solicitudJson, token.Token!, ct);

            resultado.Etapa = registro.Exitoso ? "Completado" : "Registro";
            resultado.Exitoso = registro.Exitoso;
            resultado.CodigoHttp = registro.CodigoHttp;
            resultado.Mensaje = registro.Mensaje;
            resultado.RespuestaJson = registro.Cuerpo;
        }

        resultado.DuracionMs = reloj.ElapsedMilliseconds;
        resultado.ArchivoTxt = await ReporteTxt.EscribirAsync(RutaAbsoluta(_opciones.CarpetaResultados), resultado, ct);

        db.ResultadosRegistro.Add(resultado);
        await db.SaveChangesAsync(ct);

        logger.Log(resultado.Exitoso ? LogLevel.Information : LogLevel.Warning,
            "Proceso {Estado} (registro #{Id}): {Mensaje}. Archivo: {Archivo}",
            resultado.Exitoso ? "EXITOSO" : "SIN ÉXITO", resultado.Id, resultado.Mensaje, resultado.ArchivoTxt);
    }

    private static string LeerIdentificacion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("numero_identificacion", out var id) ? id.ToString() : "SIN_ID";
    }

    // Un servicio de Windows arranca en System32, así que las rutas relativas se resuelven contra la carpeta del ejecutable.
    public static string RutaAbsoluta(string ruta)
        => Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);
}
