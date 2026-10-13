using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServicioRegistroCrm.Configuracion;

namespace ServicioRegistroCrm.Servicios
{
    public class RespuestaApi
    {
        public RespuestaApi(bool exitoso, int codigoHttp, string mensaje, string cuerpo)
        {
            Exitoso = exitoso;
            CodigoHttp = codigoHttp;
            Mensaje = mensaje;
            Cuerpo = cuerpo;
        }

        public bool Exitoso { get; }
        public int CodigoHttp { get; }
        public string Mensaje { get; }
        public string Cuerpo { get; }
    }

    public class RespuestaToken
    {
        public RespuestaToken(bool exitoso, string token, RespuestaApi respuesta)
        {
            Exitoso = exitoso;
            Token = token;
            Respuesta = respuesta;
        }

        public bool Exitoso { get; }
        public string Token { get; }
        public RespuestaApi Respuesta { get; }
    }

    public class CrmApiClient
    {
        private static readonly string[] NombresToken = { "token", "access_token", "accessToken", "jwt", "bearer" };

        private readonly HttpClient _http;
        private readonly ILogger<CrmApiClient> _logger;
        private readonly CrmOptions _opciones;

        public CrmApiClient(HttpClient http, IOptions<CrmOptions> options, ILogger<CrmApiClient> logger)
        {
            _http = http;
            _opciones = options.Value;
            _logger = logger;
        }

        public async Task<RespuestaToken> GenerarTokenAsync(CancellationToken ct)
        {
            var body = new JsonObject
            {
                ["usuario"] = _opciones.Usuario,
                ["clave"] = _opciones.Clave,
                ["servicio"] = _opciones.Servicio
            };

            var respuesta = await EnviarAsync(_opciones.UrlToken, body.ToJsonString(), null, ct);
            if (!respuesta.Exitoso)
                return new RespuestaToken(false, null, respuesta);

            var token = BuscarToken(respuesta.Cuerpo);
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("La API de token respondió sin un token reconocible");
                return new RespuestaToken(false, null,
                    new RespuestaApi(false, respuesta.CodigoHttp, "La respuesta no contiene un token.", respuesta.Cuerpo));
            }

            return new RespuestaToken(true, token, respuesta);
        }

        public Task<RespuestaApi> EnviarRegistroAsync(string solicitudJson, string token, CancellationToken ct)
            => EnviarAsync(_opciones.UrlRegistro, solicitudJson, token, ct);

        private async Task<RespuestaApi> EnviarAsync(string url, string json, string token, CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })
            {
                if (token != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                try
                {
                    using (var response = await _http.SendAsync(request, ct))
                    {
                        var cuerpo = await response.Content.ReadAsStringAsync();
                        var codigo = (int)response.StatusCode;

                        if (!response.IsSuccessStatusCode)
                            return new RespuestaApi(false, codigo, $"HTTP {codigo} {response.ReasonPhrase}", cuerpo);

                        // Las APIs del CRM responden HTTP 200 aun en error, indicándolo con "estado": 0 y "error.mensaje".
                        var (exitoso, mensaje) = InterpretarEstado(cuerpo);
                        return new RespuestaApi(exitoso, codigo, mensaje, cuerpo);
                    }
                }
                catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error llamando a {Url}", QuitarQuery(url));
                    return new RespuestaApi(false, 0, "Error de comunicación: " + ex.Message, string.Empty);
                }
            }
        }

        private static (bool Exitoso, string Mensaje) InterpretarEstado(string cuerpo)
        {
            JsonNode nodo;
            try { nodo = JsonNode.Parse(cuerpo); }
            catch (JsonException) { return (true, "OK"); }

            var obj = nodo as JsonObject;
            if (obj == null)
                return (true, "OK");
            var estadoNodo = obj["estado"] as JsonValue;
            if (estadoNodo == null)
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
        private static string BuscarToken(string cuerpo)
        {
            JsonNode nodo;
            try { nodo = JsonNode.Parse(cuerpo); }
            catch (JsonException) { return cuerpo.Trim().Trim('"'); }

            if (nodo is JsonValue valor && valor.TryGetValue<string>(out var texto))
                return texto;
            return Buscar(nodo);
        }

        private static string Buscar(JsonNode n)
        {
            if (n is JsonObject o)
            {
                foreach (var par in o)
                    if (par.Value is JsonValue v && v.TryGetValue<string>(out var s)
                        && NombresToken.Contains(par.Key, StringComparer.OrdinalIgnoreCase))
                        return s;
                foreach (var par in o)
                {
                    var encontrado = Buscar(par.Value);
                    if (encontrado != null)
                        return encontrado;
                }
            }
            else if (n is JsonArray a)
            {
                foreach (var item in a)
                {
                    var encontrado = Buscar(item);
                    if (encontrado != null)
                        return encontrado;
                }
            }
            return null;
        }

        private static string QuitarQuery(string url) => url.Split('?')[0];
    }
}
