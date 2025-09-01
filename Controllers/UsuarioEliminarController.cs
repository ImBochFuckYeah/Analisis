using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Http;

namespace ProyectoAnalisis.Controllers
{
    [RoutePrefix("Usuarios")]
    public class UsuariosEliminarController : ApiController
    {
        private static string Cnx => ConfigurationManager.ConnectionStrings["ConexionBD"].ConnectionString;

        /// <summary>
        /// Elimina un usuario.
        /// hardDelete=false -> inactiva (IdStatusUsuario = 3)
        /// hardDelete=true  -> elimina físicamente
        /// </summary>
        [HttpGet]
        [Route("Eliminar")]
        public IHttpActionResult Eliminar(
            string idUsuario,
            bool hardDelete = false,
            string usuarioAccion = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idUsuario))
                    return Ok(new { Resultado = 0, Mensaje = "Debe enviar IdUsuario." });

                using (var conn = new SqlConnection(Cnx))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_Eliminar", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IdUsuario", SqlDbType.VarChar, 100).Value = idUsuario.Trim();
                    cmd.Parameters.Add("@HardDelete", SqlDbType.Bit).Value = hardDelete;
                    cmd.Parameters.Add("@UsuarioAccion", SqlDbType.VarChar, 100).Value =
                        (object)usuarioAccion ?? DBNull.Value;

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

                        // Si es soft delete, el SP devuelve un 2do resultset con datos del usuario.
                        object data = null;
                        if (!hardDelete && rd.NextResult() && rd.Read())
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
