using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ServicioRegistroCrm.Datos
{
    /// <summary>Guarda cada <see cref="ResultadoRegistro"/> en la tabla ResultadosRegistro de SQLite.</summary>
    public class RepositorioResultados
    {
        // Mismo esquema que generaba EF Core, para que las bases ya existentes sigan sirviendo.
        private const string SqlCrearTabla = @"
CREATE TABLE IF NOT EXISTS ""ResultadosRegistro"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_ResultadosRegistro"" PRIMARY KEY AUTOINCREMENT,
    ""FechaEjecucion"" TEXT NOT NULL,
    ""NumeroIdentificacion"" TEXT NOT NULL,
    ""Exitoso"" INTEGER NOT NULL,
    ""Etapa"" TEXT NOT NULL,
    ""CodigoHttp"" INTEGER NULL,
    ""Mensaje"" TEXT NOT NULL,
    ""SolicitudJson"" TEXT NULL,
    ""RespuestaJson"" TEXT NULL,
    ""DuracionMs"" INTEGER NOT NULL,
    ""ArchivoTxt"" TEXT NULL
);
CREATE INDEX IF NOT EXISTS ""IX_ResultadosRegistro_NumeroIdentificacion""
    ON ""ResultadosRegistro"" (""NumeroIdentificacion"");";

        private const string SqlInsertar = @"
INSERT INTO ""ResultadosRegistro""
    (""FechaEjecucion"", ""NumeroIdentificacion"", ""Exitoso"", ""Etapa"", ""CodigoHttp"",
     ""Mensaje"", ""SolicitudJson"", ""RespuestaJson"", ""DuracionMs"", ""ArchivoTxt"")
VALUES
    ($fecha, $identificacion, $exitoso, $etapa, $codigoHttp,
     $mensaje, $solicitud, $respuesta, $duracion, $archivo);
SELECT last_insert_rowid();";

        private readonly string _cadenaConexion;

        public RepositorioResultados(string rutaArchivo)
        {
            _cadenaConexion = new SqliteConnectionStringBuilder { DataSource = rutaArchivo }.ToString();
        }

        public void CrearTablaSiNoExiste()
        {
            using (var conexion = new SqliteConnection(_cadenaConexion))
            using (var comando = conexion.CreateCommand())
            {
                conexion.Open();
                comando.CommandText = SqlCrearTabla;
                comando.ExecuteNonQuery();
            }
        }

        /// <summary>Inserta el resultado y le asigna el Id generado.</summary>
        public async Task GuardarAsync(ResultadoRegistro r, CancellationToken ct)
        {
            using (var conexion = new SqliteConnection(_cadenaConexion))
            using (var comando = conexion.CreateCommand())
            {
                await conexion.OpenAsync(ct);
                comando.CommandText = SqlInsertar;
                comando.Parameters.AddWithValue("$fecha", r.FechaEjecucion);
                comando.Parameters.AddWithValue("$identificacion", r.NumeroIdentificacion);
                comando.Parameters.AddWithValue("$exitoso", r.Exitoso);
                comando.Parameters.AddWithValue("$etapa", r.Etapa);
                comando.Parameters.AddWithValue("$codigoHttp", (object)r.CodigoHttp ?? DBNull.Value);
                comando.Parameters.AddWithValue("$mensaje", r.Mensaje);
                comando.Parameters.AddWithValue("$solicitud", (object)r.SolicitudJson ?? DBNull.Value);
                comando.Parameters.AddWithValue("$respuesta", (object)r.RespuestaJson ?? DBNull.Value);
                comando.Parameters.AddWithValue("$duracion", r.DuracionMs);
                comando.Parameters.AddWithValue("$archivo", (object)r.ArchivoTxt ?? DBNull.Value);

                r.Id = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
            }
        }
    }
}
