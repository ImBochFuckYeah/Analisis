using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Usuarios")]
    public class UsuariosCambiarPasswordController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        /// <summary>
        /// Cambia la contraseña del usuario autenticado.
        /// Valida password actual (SHA-256/MD5), aplica políticas y registra bitácora.
        /// </summary>
        [HttpGet]
        [Route("CambiarPassword")]
        public IHttpActionResult CambiarPassword(
            string idUsuario,
            string passwordActual,
            string passwordNueva,
            string usuarioAccion = null,
            string direccionIp = null,
            string userAgent = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idUsuario) ||
                    string.IsNullOrWhiteSpace(passwordActual) ||
                    string.IsNullOrWhiteSpace(passwordNueva))
                {
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar idUsuario, passwordActual y passwordNueva." });
                }

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CambiarPassword", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario.Trim();
                    cmd.Parameters.Add("@PasswordActual", SqlDbType.NVarChar, 200).Value = passwordActual;
                    cmd.Parameters.Add("@PasswordNueva", SqlDbType.NVarChar, 200).Value = passwordNueva;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value = (object)usuarioAccion ?? DBNull.Value;
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, 50).Value = (object)direccionIp ?? DBNull.Value;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, 200).Value = (object)userAgent ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            return Ok(new { Resultado = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int resultado = rd["Resultado"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Resultado"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        return Ok(new { Resultado = resultado, Mensaje = mensaje });
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
