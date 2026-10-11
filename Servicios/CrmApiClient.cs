using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ServicioRegistroCrm.Configuracion;

namespace ServicioRegistroCrm.Servicios;

public record RespuestaApi(bool Exitoso, int CodigoHttp, string Mensaje, string Cuerpo);

public record RespuestaToken(bool Exitoso, string? Token, RespuestaApi Respuesta);

public class CrmApiClient(HttpClient http, IOptions<CrmOptions> options, ILogger<CrmApiClient> logger)
{
    private static readonly string[] NombresToken = ["token", "access_token", "accessToken", "jwt", "bearer"];

    private readonly CrmOptions _opciones = options.Value;

    public async Task<RespuestaToken> GenerarTokenAsync(CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["usuario"] = _opciones.Usuario,
            ["clave"] = _opciones.Clave,
            ["servicio"] = _opciones.Servicio
        };

        var respuesta = await EnviarAsync(_opciones.UrlToken, body.ToJsonString(), token: null, ct);
        if (!respuesta.Exitoso)
            return new RespuestaToken(false, null, respuesta);

        var token = BuscarToken(respuesta.Cuerpo);
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("La API de token respondió sin un token reconocible");
            return new RespuestaToken(false, null,
                respuesta with { Exitoso = false, Mensaje = "La respuesta no contiene un token." });
        }

        return new RespuestaToken(true, token, respuesta);
    }

    public Task<RespuestaApi> EnviarRegistroAsync(string solicitudJson, string token, CancellationToken ct)
        => EnviarAsync(_opciones.UrlRegistro, solicitudJson, token, ct);

    private async Task<RespuestaApi> EnviarAsync(string url, string json, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var cuerpo = await response.Content.ReadAsStringAsync(ct);
            var codigo = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
                return new RespuestaApi(false, codigo, $"HTTP {codigo} {response.ReasonPhrase}", cuerpo);

            // Las APIs del CRM responden HTTP 200 aun en error, indicándolo con "estado": 0 y "error.mensaje".
            var (exitoso, mensaje) = InterpretarEstado(cuerpo);
            return new RespuestaApi(exitoso, codigo, mensaje, cuerpo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Error llamando a {Url}", QuitarQuery(url));
            return new RespuestaApi(false, 0, $"Error de comunicación: {ex.Message}", string.Empty);
        }
    }

    private static (bool Exitoso, string Mensaje) InterpretarEstado(string cuerpo)
    {
        JsonNode? nodo;
        try { nodo = JsonNode.Parse(cuerpo); }
        catch (JsonException) { return (true, "OK"); }

        if (nodo is not JsonObject obj)
            return (true, "OK");
        if (obj["estado"] is not JsonValue estadoNodo)
            return (true, obj["mensaje"]?.ToString() ?? "OK");

        var estado = estadoNodo.TryGetValue<int>(out var e) ? e
            : estadoNodo.TryGetValue<bool>(out var b) ? (b ? 1 : 0)
            : 1;
        if (estado != 0)
            return (true, obj["mensaje"]?.ToString() ?? "OK");

        var mensajeError = obj["error"]?["mensaje"]?.ToString()
                           ?? obj["mensaje"]?.ToString()
                           ?? "La API respondió estado 0.";
        return (false, mensajeError);
    }

    /// <summary>Busca el token en cualquier nivel del JSON; si la respuesta es texto plano, la usa tal cual.</summary>
    private static string? BuscarToken(string cuerpo)
    {
        JsonNode? nodo;
        try { nodo = JsonNode.Parse(cuerpo); }
        catch (JsonException) { return cuerpo.Trim().Trim('"'); }

        return nodo switch
        {
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            _ => Buscar(nodo)
        };

        static string? Buscar(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                foreach (var (clave, valor) in o)
                    if (valor is JsonValue v && v.TryGetValue<string>(out var s)
                        && NombresToken.Contains(clave, StringComparer.OrdinalIgnoreCase))
                        return s;
                foreach (var (_, valor) in o)
                    if (Buscar(valor) is { } encontrado)
                        return encontrado;
            }
            else if (n is JsonArray a)
            {
                foreach (var item in a)
                    if (Buscar(item) is { } encontrado)
                        return encontrado;
            }
            return null;
        }
    }

    private static string QuitarQuery(string url) => url.Split('?')[0];
}
