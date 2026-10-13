using System;

namespace ServicioRegistroCrm.Datos
{
    /// <summary>Entidad que representa una ejecución del proceso de registro en el CRM.</summary>
    public class ResultadoRegistro
    {
        public int Id { get; set; }
        public DateTime FechaEjecucion { get; set; }
        public string NumeroIdentificacion { get; set; } = string.Empty;
        public bool Exitoso { get; set; }

        /// <summary>Paso en el que terminó el proceso: Token, Registro o Completado.</summary>
        public string Etapa { get; set; } = string.Empty;

        public int? CodigoHttp { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string SolicitudJson { get; set; }
        public string RespuestaJson { get; set; }
        public long DuracionMs { get; set; }
        public string ArchivoTxt { get; set; }
    }
}
