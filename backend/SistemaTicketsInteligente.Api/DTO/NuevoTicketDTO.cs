/**
 * Archivo: NuevoTicketDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Nuevo Ticket entre Controller, BLL, DAO y frontend.
 * Responsabilidad: Transportar identidad visible, catálogos del formulario, datos ingresados por el usuario, adjuntos y resultado del registro.
 * Dependencias: ASP.NET Core IFormFile.
 * Flujo: Frontend -> NuevoTicketController -> NuevoTicketBLL -> NuevoTicketDAO -> SQL Server.
 * Consideraciones: No contiene reglas de negocio ni acceso a datos; los campos técnicos que no corresponden al usuario no forman parte de la solicitud.
 */

using Microsoft.AspNetCore.Http;

namespace SistemaTicketsInteligente.Api.DTO;

public sealed class NuevoTicketCatalogoItem
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class NuevoTicketDatosRespuesta
{
    public string Usuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string AreaDescripcion { get; set; } = string.Empty;
    public List<NuevoTicketCatalogoItem> Lineas { get; set; } = [];
    public List<NuevoTicketCatalogoItem> Tipos { get; set; } = [];
}

public sealed class CrearNuevoTicketSolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
    public List<IFormFile> Adjuntos { get; set; } = [];
}

public sealed class NuevoTicketAdjuntoRegistro
{
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string TipoMime { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
}

public sealed class NuevoTicketCreadoRespuesta
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}
