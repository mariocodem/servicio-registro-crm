namespace ServicioRegistroCrm.Configuracion;

public class CrmOptions
{
    public const string Seccion = "Crm";

    /// <summary>URL completa del endpoint que genera el token (incluye ?code=...).</summary>
    public string UrlToken { get; set; } = string.Empty;

    /// <summary>URL completa del endpoint que recibe los datos del afiliado.</summary>
    public string UrlRegistro { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public int Servicio { get; set; }

    /// <summary>Archivo JSON con el body que se envía al endpoint de registro.</summary>
    public string ArchivoSolicitud { get; set; } = "Solicitudes/solicitud.json";

    /// <summary>Carpeta donde se escriben los archivos .txt de resultado.</summary>
    public string CarpetaResultados { get; set; } = "Resultados";

    /// <summary>Cada cuántos minutos se repite el proceso. 0 = ejecutar una sola vez.</summary>
    public int IntervaloMinutos { get; set; } = 60;

    public int TimeoutSegundos { get; set; } = 60;
}
