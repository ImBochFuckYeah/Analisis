using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Usuarios")]
    public class UsuariosCrearController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        [HttpGet]
        [Route("Crear")]
        public IHttpActionResult Crear(
            string idUsuario,
            string nombre,
            string apellido,
            string fechaNacimiento,      // esperado: "yyyy-MM-dd" o "yyyy-MM-ddTHH:mm:ss"
            int? idGenero,
            string correoElectronico = null,
            string telefonoMovil = null,
            int? idSucursal = null,
            string pregunta = null,
            string respuesta = null,
            int? idRole = null,
            string password = null,
            int? idStatusUsuario = null,
            string usuarioAccion = null
        )
        {
            try
            {
                // --- Parseo de fecha de nacimiento (acepta varios formatos ISO) ---
                if (string.IsNullOrWhiteSpace(fechaNacimiento))
                    return Ok(new { Resultado = 0, Mensaje = "FechaNacimiento es requerida (yyyy-MM-dd)." });

                var formatos = new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss" };
                if (!DateTime.TryParseExact(fechaNacimiento, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fn))
                    return Ok(new { Resultado = 0, Mensaje = "FechaNacimiento inválida. Use formato yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss." });

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Crear", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Parámetros requeridos
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = (object)idUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = (object)nombre ?? DBNull.Value;
                    cmd.Parameters.Add("@Apellido", SqlDbType.VarChar, 100).Value = (object)apellido ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value = fn.Date;
                    cmd.Parameters.Add("@IdGenero", SqlDbType.Int).Value = (object)idGenero ?? DBNull.Value;
                    cmd.Parameters.Add("@IdSucursal", SqlDbType.Int).Value = (object)idSucursal ?? DBNull.Value;
                    cmd.Parameters.Add("@Pregunta", SqlDbType.VarChar, 200).Value = (object)pregunta ?? DBNull.Value;
                    cmd.Parameters.Add("@Respuesta", SqlDbType.VarChar, 200).Value = (object)respuesta ?? DBNull.Value;
                    cmd.Parameters.Add("@IdRole", SqlDbType.Int).Value = (object)idRole ?? DBNull.Value;
                    cmd.Parameters.Add("@Password", SqlDbType.NVarChar, 200).Value = (object)password ?? DBNull.Value;

                    // Parámetros opcionales
                    cmd.Parameters.Add("@CorreoElectronico", SqlDbType.VarChar, 100).Value = (object)correoElectronico ?? DBNull.Value;
                    cmd.Parameters.Add("@TelefonoMovil", SqlDbType.VarChar, 30).Value = (object)telefonoMovil ?? DBNull.Value;
                    cmd.Parameters.Add("@IdStatusUsuario", SqlDbType.Int).Value = (object)idStatusUsuario ?? DBNull.Value;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value = (object)usuarioAccion ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.HasRows)
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        // Primer resultset: Resultado / Mensaje
                        rd.Read();
                        int resultado = rd["Resultado"] != DBNull.Value ? Convert.ToInt32(rd["Resultado"]) : 0;
                        string mensaje = rd["Mensaje"] as string ?? "";

                        // Si no fue exitoso, devolvemos solo eso
                        if (resultado != 1)
                            return Ok(new { Resultado = resultado, Mensaje = mensaje });

                        // Si hay un segundo resultset, leer datos del usuario creado
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
