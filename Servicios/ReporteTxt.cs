using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServicioRegistroCrm.Datos;

namespace ServicioRegistroCrm.Servicios
{
    public static class ReporteTxt
    {
        public static async Task<string> EscribirAsync(string carpeta, ResultadoRegistro r)
        {
            Directory.CreateDirectory(carpeta);
            var estado = r.Exitoso ? "EXITO" : "FALLO";
            var ruta = Path.Combine(carpeta,
                $"resultado_{r.FechaEjecucion:yyyyMMdd_HHmmss}_{r.NumeroIdentificacion}_{estado}.txt");

            var sb = new StringBuilder()
                .AppendLine("==============================================")
                .AppendLine("  RESULTADO REGISTRO CRM")
                .AppendLine("==============================================")
                .AppendLine($"Fecha ejecución       : {r.FechaEjecucion:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"Identificación        : {r.NumeroIdentificacion}")
                .AppendLine($"Resultado             : {(r.Exitoso ? "ÉXITO" : "SIN ÉXITO")}")
                .AppendLine($"Etapa                 : {r.Etapa}")
                .AppendLine($"Código HTTP           : {r.CodigoHttp?.ToString() ?? "N/A"}")
                .AppendLine($"Mensaje               : {r.Mensaje}")
                .AppendLine($"Duración              : {r.DuracionMs} ms")
                .AppendLine()
                .AppendLine("----- Respuesta de la API -----")
                .AppendLine(Formatear(r.RespuestaJson));

            using (var escritor = new StreamWriter(ruta, false, Encoding.UTF8))
                await escritor.WriteAsync(sb.ToString());
            return ruta;
        }

        private static string Formatear(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "(sin contenido)";
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
            }
            catch (JsonException)
            {
                return json;
            }
        }
    }
}
