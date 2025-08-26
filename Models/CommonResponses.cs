using System.Collections.Generic;

namespace ProyectoAnalisis.Models
{
    public class ApiResponse<T>
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public T Datos { get; set; }
        public object Debug { get; set; }  // opcional (para pruebas)
    }

    public class PagedResult<T>
    {
        public int Total { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }
}
