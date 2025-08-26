using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.Entity.Core.EntityClient;
using System.Web.Mvc;
using ProyectoAnalisis.Models; // ApiResponse<T>, PagedResult<T>

namespace ProyectoAnalisis.Controllers
{
    // ====== DTOs ======
    public class UsuarioDto
    {
        public string IdUsuario { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string CorreoElectronico { get; set; }
        public int? IdSucursal { get; set; }
        public int? IdStatusUsuario { get; set; }
        public int? IdRole { get; set; }
        public string TelefonoMovil { get; set; }
        public DateTime? FechaCreacion { get; set; }
    }

    // Peticiones
    public class UsuarioCrearRequest
    {
        public string IdUsuario { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public int? IdStatusUsuario { get; set; } = 1;
        public string Password { get; set; }   // texto plano; SP hashea
        public int? IdGenero { get; set; }
        public string CorreoElectronico { get; set; }
        public string TelefonoMovil { get; set; }
        public int? IdSucursal { get; set; }
        public string Pregunta { get; set; }
        public string Respuesta { get; set; }
        public int? IdRole { get; set; }   // IdRole
        public string FotografiaBase64 { get; set; }   // opcional (data:image/...;base64,...)
    }

    public class UsuarioActualizarRequest
    {
        public string IdUsuario { get; set; }   // requerido para actualizar
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public int? IdStatusUsuario { get; set; }
        public string Password { get; set; }   // si viene, se reescribe (hasheado)
        public int? IdGenero { get; set; }
        public string CorreoElectronico { get; set; }
        public string TelefonoMovil { get; set; }
        public int? IdSucursal { get; set; }
        public string Pregunta { get; set; }
        public string Respuesta { get; set; }
        public int? IdRole { get; set; }   // IdRole
        public string FotografiaBase64 { get; set; }   // opcional
        public bool LimpiarFoto { get; set; } = false; // true = quitar foto
    }

    public class UsuarioListarRequest
    {
        public string Buscar { get; set; }
        public int Pagina { get; set; } = 1;
        public int TamanoPagina { get; set; } = 10;
    }

    public class UsuarioEliminarRequest
    {
        public string IdUsuario { get; set; }
        public bool HardDelete { get; set; } = false;
    }

    public class CambiarPasswordRequest
    {
        public string IdUsuario { get; set; }
        public string PasswordActual { get; set; }
        public string PasswordNueva { get; set; }
    }

    public class UsuarioController : Controller
    {
        // ===== Helpers =====
        private static string GetSqlConnStringFromEF(string efName)
        {
            var ef = ConfigurationManager.ConnectionStrings[efName].ConnectionString;
            var ecb = new EntityConnectionStringBuilder(ef);
            return ecb.ProviderConnectionString;
        }

        private static void AddParam(SqlCommand cmd, string name, SqlDbType type, int size, object value)
        {
            var p = cmd.Parameters.Add(name, type, size);
            p.Value = value ?? (object)DBNull.Value;
        }
        private static void AddParam(SqlCommand cmd, string name, SqlDbType type, object value)
        {
            var p = cmd.Parameters.Add(name, type);
            p.Value = value ?? (object)DBNull.Value;
        }

        // decodifica "data:image/...;base64,AAAA" → byte[]
        private static byte[] FromBase64(string b64)
        {
            if (string.IsNullOrWhiteSpace(b64)) return null;
            var s = b64.Trim();
            var comma = s.IndexOf(',');
            if (comma >= 0) s = s.Substring(comma + 1); // quitar 'data:image/...;base64,'
            try { return Convert.FromBase64String(s); } catch { return null; }
        }

        // ---- helpers de lectura tolerantes a columnas faltantes ----
        private static string GetStr(IDataRecord r, string col)
        {
            int i; try { i = r.GetOrdinal(col); } catch { return null; }
            return r.IsDBNull(i) ? null : r.GetValue(i).ToString();
        }
        private static int? GetInt(IDataRecord r, string col)
        {
            int i; try { i = r.GetOrdinal(col); } catch { return (int?)null; }
            return r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));
        }
        private static DateTime? GetDt(IDataRecord r, string col)
        {
            int i; try { i = r.GetOrdinal(col); } catch { return (DateTime?)null; }
            return r.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(r.GetValue(i));
        }

        private static UsuarioDto MapUsuario(IDataRecord r) => new UsuarioDto
        {
            IdUsuario = GetStr(r, "IdUsuario"),
            Nombre = GetStr(r, "Nombre"),
            Apellido = GetStr(r, "Apellido"),
            CorreoElectronico = GetStr(r, "CorreoElectronico"),
            IdSucursal = GetInt(r, "IdSucursal"),
            IdStatusUsuario = GetInt(r, "IdStatusUsuario"),
            IdRole = GetInt(r, "IdRole"),
            TelefonoMovil = GetStr(r, "TelefonoMovil"),
            FechaCreacion = GetDt(r, "FechaCreacion")
        };

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "system" : User.Identity.Name;

        // ===== Endpoints =====

        // GET /Usuario/Listar?buscar=...&pagina=1&tamanoPagina=10
        [HttpGet]
        public ActionResult Listar(string buscar = null, int pagina = 1, int tamanoPagina = 10)
        {
            var resp = new ApiResponse<PagedResult<UsuarioDto>>
            {
                Exito = false,
                Mensaje = "Error",
                Datos = new PagedResult<UsuarioDto> { Items = new List<UsuarioDto>(), Total = 0 }
            };

            try
            {
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");
                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "LISTAR");
                    AddParam(cmd, "@Buscar", SqlDbType.VarChar, 100, buscar);
                    AddParam(cmd, "@Pagina", SqlDbType.Int, pagina);
                    AddParam(cmd, "@TamanoPagina", SqlDbType.Int, tamanoPagina);

                    cn.Open();
                    var items = new List<UsuarioDto>();
                    int total = 0;

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) items.Add(MapUsuario(r));
                        if (r.NextResult() && r.Read())
                            total = Convert.ToInt32(r["Total"]);
                    }

                    resp.Exito = true;
                    resp.Mensaje = "OK";
                    resp.Datos = new PagedResult<UsuarioDto> { Items = items, Total = total };
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp, JsonRequestBehavior.AllowGet);
        }

        // GET /Usuario/Obtener?idUsuario=test
        [HttpGet]
        public ActionResult Obtener(string idUsuario)
        {
            var resp = new ApiResponse<UsuarioDto> { Exito = false, Mensaje = "Error", Datos = null };

            try
            {
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");
                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "OBTENER");
                    AddParam(cmd, "@IdUsuario", SqlDbType.VarChar, 100, idUsuario);

                    cn.Open();
                    using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (r.Read())
                        {
                            resp.Exito = true;
                            resp.Mensaje = "OK";
                            resp.Datos = MapUsuario(r);
                        }
                        else
                        {
                            resp.Exito = false;
                            resp.Mensaje = "No encontrado";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp, JsonRequestBehavior.AllowGet);
        }

        // POST /Usuario/Crear  (Body: JSON)
        [HttpPost]
        public ActionResult Crear(UsuarioCrearRequest req)
        {
            var resp = new ApiResponse<UsuarioDto> { Exito = false, Mensaje = "Error", Datos = null };

            try
            {
                var foto = FromBase64(req.FotografiaBase64);
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");

                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "CREAR");
                    AddParam(cmd, "@IdUsuario", SqlDbType.VarChar, 100, req.IdUsuario);
                    AddParam(cmd, "@Nombre", SqlDbType.VarChar, 100, req.Nombre);
                    AddParam(cmd, "@Apellido", SqlDbType.VarChar, 100, req.Apellido);
                    AddParam(cmd, "@FechaNacimiento", SqlDbType.Date, req.FechaNacimiento);
                    AddParam(cmd, "@IdStatusUsuario", SqlDbType.Int, req.IdStatusUsuario);
                    AddParam(cmd, "@Password", SqlDbType.VarChar, 100, req.Password);
                    AddParam(cmd, "@IdGenero", SqlDbType.Int, req.IdGenero);
                    AddParam(cmd, "@CorreoElectronico", SqlDbType.VarChar, 100, req.CorreoElectronico);
                    AddParam(cmd, "@TelefonoMovil", SqlDbType.VarChar, 30, req.TelefonoMovil);
                    AddParam(cmd, "@IdSucursal", SqlDbType.Int, req.IdSucursal);
                    AddParam(cmd, "@Pregunta", SqlDbType.VarChar, 200, req.Pregunta);
                    AddParam(cmd, "@Respuesta", SqlDbType.VarChar, 200, req.Respuesta);
                    AddParam(cmd, "@IdRole", SqlDbType.Int, req.IdRole);
                    AddParam(cmd, "@Fotografia", SqlDbType.VarBinary, foto);
                    AddParam(cmd, "@UsuarioAccion", SqlDbType.VarChar, 100, CurrentUserName);

                    cn.Open();

                    bool ok = true; string msg = "Creado"; UsuarioDto u = null;

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bool hasRes = false, hasMsg = false;
                            for (int i = 0; i < r.FieldCount; i++)
                            {
                                var n = r.GetName(i);
                                if (string.Equals(n, "Resultado", StringComparison.OrdinalIgnoreCase)) hasRes = true;
                                if (string.Equals(n, "Mensaje", StringComparison.OrdinalIgnoreCase)) hasMsg = true;
                            }

                            if (hasRes && hasMsg)
                            {
                                ok = Convert.ToInt32(r["Resultado"]) == 1;
                                msg = Convert.ToString(r["Mensaje"]);

                                if (r.NextResult() && r.Read())
                                    u = MapUsuario(r);
                            }
                            else
                            {
                                // RS#1 ya es la fila del usuario
                                u = MapUsuario(r);
                            }
                        }
                    }

                    resp.Exito = ok;
                    resp.Mensaje = msg;
                    resp.Datos = u;
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp);
        }

        // POST /Usuario/Actualizar  (Body: JSON)
        [HttpPost]
        public ActionResult Actualizar(UsuarioActualizarRequest req)
        {
            var resp = new ApiResponse<UsuarioDto> { Exito = false, Mensaje = "Error", Datos = null };

            try
            {
                var foto = FromBase64(req.FotografiaBase64);
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");

                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "ACTUALIZAR");
                    AddParam(cmd, "@IdUsuario", SqlDbType.VarChar, 100, req.IdUsuario);
                    AddParam(cmd, "@Nombre", SqlDbType.VarChar, 100, req.Nombre);
                    AddParam(cmd, "@Apellido", SqlDbType.VarChar, 100, req.Apellido);
                    AddParam(cmd, "@FechaNacimiento", SqlDbType.Date, req.FechaNacimiento);
                    AddParam(cmd, "@IdStatusUsuario", SqlDbType.Int, req.IdStatusUsuario);
                    AddParam(cmd, "@Password", SqlDbType.VarChar, 100, req.Password);
                    AddParam(cmd, "@IdGenero", SqlDbType.Int, req.IdGenero);
                    AddParam(cmd, "@CorreoElectronico", SqlDbType.VarChar, 100, req.CorreoElectronico);
                    AddParam(cmd, "@TelefonoMovil", SqlDbType.VarChar, 30, req.TelefonoMovil);
                    AddParam(cmd, "@IdSucursal", SqlDbType.Int, req.IdSucursal);
                    AddParam(cmd, "@Pregunta", SqlDbType.VarChar, 200, req.Pregunta);
                    AddParam(cmd, "@Respuesta", SqlDbType.VarChar, 200, req.Respuesta);
                    AddParam(cmd, "@IdRole", SqlDbType.Int, req.IdRole);
                    AddParam(cmd, "@Fotografia", SqlDbType.VarBinary, foto);
                    AddParam(cmd, "@LimpiarFoto", SqlDbType.Bit, req.LimpiarFoto);
                    AddParam(cmd, "@UsuarioAccion", SqlDbType.VarChar, 100, CurrentUserName);

                    cn.Open();

                    bool ok = true; string msg = "Actualizado"; UsuarioDto u = null;

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bool hasRes = false, hasMsg = false;
                            for (int i = 0; i < r.FieldCount; i++)
                            {
                                var n = r.GetName(i);
                                if (string.Equals(n, "Resultado", StringComparison.OrdinalIgnoreCase)) hasRes = true;
                                if (string.Equals(n, "Mensaje", StringComparison.OrdinalIgnoreCase)) hasMsg = true;
                            }

                            if (hasRes && hasMsg)
                            {
                                ok = Convert.ToInt32(r["Resultado"]) == 1;
                                msg = Convert.ToString(r["Mensaje"]);
                                if (r.NextResult() && r.Read())
                                    u = MapUsuario(r);
                            }
                            else
                            {
                                u = MapUsuario(r);
                            }
                        }
                    }

                    resp.Exito = ok;
                    resp.Mensaje = msg;
                    resp.Datos = u;
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp);
        }

        // POST /Usuario/Eliminar  (Body: JSON { IdUsuario, HardDelete })
        [HttpPost]
        public ActionResult Eliminar(UsuarioEliminarRequest req)
        {
            var resp = new ApiResponse<object> { Exito = false, Mensaje = "Error", Datos = null };

            try
            {
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");
                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "ELIMINAR");
                    AddParam(cmd, "@IdUsuario", SqlDbType.VarChar, 100, req.IdUsuario);
                    AddParam(cmd, "@HardDelete", SqlDbType.Bit, req.HardDelete);
                    AddParam(cmd, "@UsuarioAccion", SqlDbType.VarChar, 100, CurrentUserName);

                    cn.Open();

                    bool ok = true; string msg = "Eliminado";

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bool hasRes = false, hasMsg = false;
                            for (int i = 0; i < r.FieldCount; i++)
                            {
                                var n = r.GetName(i);
                                if (string.Equals(n, "Resultado", StringComparison.OrdinalIgnoreCase)) hasRes = true;
                                if (string.Equals(n, "Mensaje", StringComparison.OrdinalIgnoreCase)) hasMsg = true;
                            }
                            if (hasRes && hasMsg)
                            {
                                ok = Convert.ToInt32(r["Resultado"]) == 1;
                                msg = Convert.ToString(r["Mensaje"]);
                            }
                        }
                    }

                    resp.Exito = ok;
                    resp.Mensaje = msg;
                }
            }
            catch (Exception ex)
            {
                resp.Exito = false;
                resp.Mensaje = "Excepción: " + ex.Message;
            }

            return Json(resp);
        }

        // POST /Usuario/CambiarPassword  (Body: JSON)
        [HttpPost]
        public ActionResult CambiarPassword(CambiarPasswordRequest req)
        {
            var resp = new ApiResponse<object> { Exito = false, Mensaje = "Error", Datos = null };

            try
            {
                var sql = GetSqlConnStringFromEF("ProyectoAnalisisEntities1");
                using (var cn = new SqlConnection(sql))
                using (var cmd = new SqlCommand("dbo.sp_Usuario_CRUD", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    AddParam(cmd, "@Accion", SqlDbType.VarChar, 20, "CAMBIAR_PASSWORD");
                    AddParam(cmd, "@IdUsuario", SqlDbType.VarChar, 100, req.IdUsuario);
                    AddParam(cmd, "@PasswordActual", SqlDbType.VarChar, 100, req.PasswordActual);
                    AddParam(cmd, "@PasswordNueva", SqlDbType.VarChar, 100, req.PasswordNueva);
                    AddParam(cmd, "@UsuarioAccion", SqlDbType.VarChar, 100, CurrentUserName);

                    cn.Open();

                    bool ok = true; string msg = "Password actualizado";

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            bool hasRes = false, hasMsg = false;
                            for (int i = 0; i < r.FieldCount; i++)
                            {
                                var n = r.GetName(i);
                                if (string.Equals(n, "Resultado", StringComparison.OrdinalIgnoreCase)) hasRes = true;
                                if (string.Equals(n, "Mensaje", StringComparison.OrdinalIgnoreCase)) hasMsg = true;
                            }
                            if (hasRes && hasMsg)
                            {
                                ok = Convert.ToInt32(r["Resultado"]) == 1;
                                msg = Convert.ToString(r["Mensaje"]);
                            }
                        }
                    }

                    resp.Exito = ok;
                    resp.Mensaje = msg;
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
