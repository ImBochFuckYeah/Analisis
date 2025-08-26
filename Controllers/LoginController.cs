using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Web.Mvc;
using ProyectoAnalisis.Models;

namespace ProyectoAnalisis.Controllers
{
    // DTO que el cliente envía (JSON o form-data)
    public class LoginRequest
    {
        public string Usuario { get; set; }
        public string Password { get; set; }
        public string Ip { get; set; }
        public string UserAgent { get; set; }
        public string SistemaOperativo { get; set; }
        public string Dispositivo { get; set; }
        public string Browser { get; set; }
        public bool Debug { get; set; } = false;
    }

    // DTO de datos de respuesta (cuando el login es OK)
    public class LoginDatos
    {
        public string IdUsuario { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string CorreoElectronico { get; set; }
        public string Sesion { get; set; }
        public string IdSucursal { get; set; }
    }

    public class LoginController : Controller
    {
     
        private static string Clip(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max) : s);

        private static string GetStringOrNull(IDataRecord r, string col)
        {
            var i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetValue(i).ToString();
        }

        private static string GetSqlConnStringFromEF(string efName)
        {
            var ef = ConfigurationManager.ConnectionStrings[efName].ConnectionString;
            var ecb = new EntityConnectionStringBuilder(ef);
            return ecb.ProviderConnectionString; 
        }

        // IP 
        private string GetClientIp()
        {
            string[] hdrs = { "CF-Connecting-IP", "X-Forwarded-For", "X-Real-IP", "X-Original-For" };
            foreach (var h in hdrs)
            {
                var v = Request.Headers[h];
                if (!string.IsNullOrWhiteSpace(v))
                {
                    var ip = v.Split(',')[0].Trim();
                    return ip == "::1" ? "127.0.0.1" : ip;
                }
            }
            var addr = Request.UserHostAddress;
            if (string.IsNullOrWhiteSpace(addr))
                addr = Request.ServerVariables["REMOTE_ADDR"];
            return addr == "::1" ? "127.0.0.1" : addr;
        }

        // Info básica 
        private (string OS, string Device, string Browser) GetAgentInfo()
        {
            var br = Request.Browser;
            var os = br?.Platform ?? "Desconocido";
            var device = (br?.IsMobileDevice ?? false) ? "Mobile" : "Desktop";
            var browser = br != null ? $"{br.Browser} {br.Version}" : "Desconocido";
            return (os, device, browser);
        }
        // -------------------------------------------

        // POST /Login/ValidarCredenciales
       
        [HttpPost]
        [AllowAnonymous]
        public ActionResult ValidarCredenciales(LoginRequest req)
        {
            var resp = new ApiResponse<LoginDatos>
            {
                Exito = false,
                Mensaje = "Error inesperado.",
                Datos = null,
                Debug = null
            };

            try
            {
                // Completar datos desde el servidor si no vinieron en la petición
                var ip = string.IsNullOrWhiteSpace(req.Ip) ? GetClientIp() : Clip(req.Ip, 50);
                var userAgent = string.IsNullOrWhiteSpace(req.UserAgent) ? (Request.UserAgent ?? "") : Clip(req.UserAgent, 200);
                var info = GetAgentInfo();
                var sistemaOperativo = string.IsNullOrWhiteSpace(req.SistemaOperativo) ? info.OS : Clip(req.SistemaOperativo, 50);
                var dispositivo = string.IsNullOrWhiteSpace(req.Dispositivo) ? info.Device : Clip(req.Dispositivo, 50);
                var browser = string.IsNullOrWhiteSpace(req.Browser) ? info.Browser : Clip(req.Browser, 50);

                if (req.Debug)
                {
                    resp.Debug = new
                    {
                        Ip = ip,
                        UserAgent = userAgent,
                        SistemaOperativo = sistemaOperativo,
                        Dispositivo = dispositivo,
                        Browser = browser
                    };
                }


                //conexion a base
                var sqlConnStr = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");

                using (var conn = new SqlConnection(sqlConnStr))
                using (var cmd = new SqlCommand("sp_LoginUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 100).Value = Clip(req.Usuario, 100);
                    cmd.Parameters.Add("@Password", SqlDbType.VarChar, 100).Value = Clip(req.Password, 100);
                    cmd.Parameters.Add("@DireccionIp", SqlDbType.VarChar, 50).Value = ip;
                    cmd.Parameters.Add("@UserAgent", SqlDbType.VarChar, 200).Value = userAgent;
                    cmd.Parameters.Add("@SistemaOperativo", SqlDbType.VarChar, 50).Value = sistemaOperativo;
                    cmd.Parameters.Add("@Dispositivo", SqlDbType.VarChar, 50).Value = dispositivo;
                    cmd.Parameters.Add("@Browser", SqlDbType.VarChar, 50).Value = browser;

                    conn.Open();

                    using (var reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (reader.Read())
                        {
                            bool esError =
                                reader.FieldCount == 2 &&
                                reader.GetName(0).Equals("Resultado", StringComparison.OrdinalIgnoreCase) &&
                                reader.GetName(1).Equals("Mensaje", StringComparison.OrdinalIgnoreCase);

                            if (esError)
                            {
                                resp.Exito = false;
                                resp.Mensaje = Convert.ToString(reader["Mensaje"]);
                                resp.Datos = null;
                            }
                            else
                            {
                                resp.Exito = true;
                                resp.Mensaje = "Login exitoso";
                                resp.Datos = new LoginDatos
                                {
                                    IdUsuario = GetStringOrNull(reader, "IdUsuario"),
                                    Nombre = GetStringOrNull(reader, "Nombre"),
                                    Apellido = GetStringOrNull(reader, "Apellido"),
                                    CorreoElectronico = GetStringOrNull(reader, "CorreoElectronico"),
                                    Sesion = GetStringOrNull(reader, "SesionActual"),
                                    IdSucursal = GetStringOrNull(reader, "IdSucursal")
                                };
                            }
                        }
                        else
                        {
                            resp.Exito = false;
                            resp.Mensaje = "No se obtuvo respuesta del procedimiento.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
                
            }

            
            return Json(resp);
        }
    }
}
