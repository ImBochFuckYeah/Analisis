using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("PasswordQA")]
    public class PasswordQAValidarYActualizarController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        [HttpGet]
        [Route("ValidarYActualizar")]
        public IHttpActionResult ValidarYActualizar(
            string usuario,
            string respuesta,
            string nueva,
            string ip = null,
            string ua = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuario) ||
                    string.IsNullOrWhiteSpace(respuesta) ||
                    string.IsNullOrWhiteSpace(nueva))
                {
                    return Ok(new { Exito = 0, Mensaje = "Debe enviar usuario, respuesta y nueva." });
                }

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_PasswordQA_ValidarYActualizar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = usuario.Trim();
                    cmd.Parameters.Add("@Respuesta", SqlDbType.NVarChar, 200).Value = respuesta;
                    cmd.Parameters.Add("@NuevaPassword", SqlDbType.NVarChar, 200).Value = nueva;
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, 50).Value = (object)ip ?? DBNull.Value;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, 200).Value = (object)ua ?? DBNull.Value;

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            return Ok(new { Exito = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int exito = rd["Exito"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Exito"]);
                        string mensaje = rd["Mensaje"] as string ?? "";

                        return Ok(new { Exito = exito, Mensaje = mensaje });
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
