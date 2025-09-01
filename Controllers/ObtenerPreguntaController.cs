using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("PasswordQA")]
    public class PasswordQAObtenerPreguntasController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        [HttpGet]
        [Route("ObtenerPreguntas")]
        public IHttpActionResult ObtenerPreguntas(string usuario)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(usuario))
                    return Ok(new { Exito = 0, Mensaje = "Debe enviar el IdUsuario." });

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Password_ObtenerPreguntas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = usuario.Trim();

                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                            return Ok(new { Exito = 0, Mensaje = "Sin respuesta del procedimiento." });

                        int exito = rd["Exito"] == DBNull.Value ? 0 : Convert.ToInt32(rd["Exito"]);
                        string mensaje = rd["Mensaje"] as string ?? "";
                        string pregunta = rd["Pregunta"] as string;
                        int? idStatus = rd["IdStatusUsuario"] == DBNull.Value ? (int?)null : Convert.ToInt32(rd["IdStatusUsuario"]);

                        return Ok(new
                        {
                            Exito = exito,
                            Mensaje = mensaje,
                            Pregunta = pregunta,
                            IdStatusUsuario = idStatus
                        });
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
