using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Usuarios")]
    public class UsuariosActualizarController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        [HttpGet]
        [Route("Actualizar")]
        public IHttpActionResult Actualizar(
            string idUsuario,                     // requerido
            string nombre = null,
            string apellido = null,
            string fechaNacimiento = null,        // "yyyy-MM-dd" o "yyyy-MM-ddTHH:mm:ss"
            int? idStatusUsuario = null,
            string password = null,
            int? idGenero = null,
            string correoElectronico = null,
            string telefonoMovil = null,
            int? idSucursal = null,
            string pregunta = null,
            string respuesta = null,
            int? idRole = null,
            string fotografiaBase64 = null,       // opcional (data:image/...;base64,xxxx o solo base64)
            bool limpiarFoto = false,             // true para borrar la foto
            string usuarioAccion = null
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idUsuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdUsuario." });

                // Parseo de fecha (si viene)
                DateTime? fechaNac = null;
                if (!string.IsNullOrWhiteSpace(fechaNacimiento))
                {
                    var formatos = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss" };
                    if (!DateTime.TryParseExact(fechaNacimiento, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fn))
                        return Ok(new { Resultado = 0, Mensaje = "FechaNacimiento inválida. Use yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss." });
                    fechaNac = fn.Date;
                }

                // Foto (si viene y no se va a limpiar)
                byte[] fotoBytes = null;
                if (!limpiarFoto && !string.IsNullOrWhiteSpace(fotografiaBase64))
                {
                    var b64 = fotografiaBase64.Trim();
                    var comma = b64.IndexOf(',');
                    if (comma >= 0) b64 = b64.Substring(comma + 1); // remover encabezado data:
                    try
                    {
                        fotoBytes = Convert.FromBase64String(b64);
                    }
                    catch
                    {
                        return Ok(new { Resultado = 0, Mensaje = "fotografiaBase64 no es una cadena Base64 válida." });
                    }
                }

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Actualizar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Requerido
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario.Trim();

                    // Opcionales (solo se actualizan si no son null en el SP)
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = (object)nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 100).Value = (object)apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = (object)fechaNac ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = (object)idStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = (object)password ?? DBNull.Value;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)idGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value = (object)correoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@TelefonoMovil", SqlDbType.VarChar, 30).Value = (object)telefonoMovil ?? DBNull.Value;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = (object)idSucursal ?? DBNull.Value;
                    cmd.Parameters.Add("@Pregunta", SqlDbType.VarChar, 200).Value = (object)pregunta ?? DBNull.Value;
                    cmd.Parameters.Add("@Respuesta", SqlDbType.VarChar, 200).Value = (object)respuesta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)idRole ?? DBNull.Value;

                    // Foto / limpiar
                    var pFoto = cmd.Parameters.Add("@Fotografia", SqlDbType.VarBinary, -1);
                    pFoto.Value = (object)fotoBytes ?? DBNull.Value;
                    cmd.Parameters.Add("@LimpiarFoto", SqlDbType.Bit).Value = limpiarFoto;

                    // Auditoría
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value = (object)usuarioAccion ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        // 1er resultset: Resultado / Mensaje
                        rd.Read();
                        int resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                        string mensaje = rd["Mensaje"] as string ?? "";

                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // 2do resultset: datos del usuario
                        object data = null;
                        if (rd.NextResult() && rd.Read())
                        {
                            data = new
                            {
                                IdUsuario = rd["IdUsuario"] as string,
                                Nombre = rd["Nombre"] as string,
                                Apellido = rd["Apellido"] as string,
                                CorreoElectronico = rd["CorreoElectronico"] as string,
                                IdSucursal = rd["IdSucursal"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdSucursal"]),
                                IdStatusUsuario = rd["IdStatusUsuario"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdStatusUsuario"]),
                                IdRole = rd["IdRole"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdRole"])
                            };
                        }

                        return Ok(new { Resultado = resultado, Mensaje = mensaje, Data = data });
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(new Exception("Error interno: " + e.Message));
            }
        }
    }
}
