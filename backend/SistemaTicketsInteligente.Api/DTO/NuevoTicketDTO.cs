/**
 * Archivo: NuevoTicketDTO.cs
 * Objetivo: Definir los contratos utilizados por el módulo Nuevo Ticket entre Controller, BLL y frontend.
 * Responsabilidad: Transportar identidad visible, catálogos del formulario, la ficha por tipo de ticket, datos ingresados por el usuario,
 *   adjuntos y resultado del registro.
 * Dependencias: ASP.NET Core IFormFile.
 * Flujo: Frontend -> NuevoTicketController -> NuevoTicketBLL -> SQL Server.
 * Consideraciones: No contiene reglas de negocio ni acceso a datos; los campos técnicos que no corresponden al usuario no forman parte de la solicitud.
 */

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
    /// <summary>Campos activos de la ficha de cada tipo de ticket (el formulario muestra los del tipo elegido).</summary>
    public List<CampoFichaTicket> Ficha { get; set; } = [];
}

/// <summary>Campo de la ficha estructurada de un tipo de ticket (TI_PlantillaCampo).</summary>
public sealed class CampoFichaTicket
{
    public string Tipo { get; set; } = string.Empty;
    public string Campo { get; set; } = string.Empty;
    public string Bloque { get; set; } = string.Empty;
    public int Orden { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string Ayuda { get; set; } = string.Empty;
    /// <summary>TEXTO, TEXTO_LARGO, FECHA (aaaa-mm-dd) o SI_NO.</summary>
    public string TipoDato { get; set; } = string.Empty;
    public bool Obligatorio { get; set; }
    public int LongitudMinima { get; set; }
    public int LongitudMaxima { get; set; }
}

/// <summary>Respuesta registrada en la ficha de un ticket (TI_IncidenciaDato), visible para el solicitante y para TI.</summary>
public sealed class DatoFichaTicket
{
    public string Campo { get; set; } = string.Empty;
    public string Bloque { get; set; } = string.Empty;
    public int Orden { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string TipoDato { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}

public sealed class CrearNuevoTicketSolicitud
{
    public string Linea { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? MensajeError { get; set; }
    public List<IFormFile> Adjuntos { get; set; } = [];
    /// <summary>Evidencia que el colaborador mostró en pantalla al Asistente TI (pasos, error, conversación); activa la investigación automática.</summary>
    public string? EvidenciaAsistenteJson { get; set; }
    /// <summary>Ficha del tipo de ticket: objeto JSON con un texto por campo ({"OBJETIVO_NEGOCIO": "..."}).</summary>
    public string? FichaJson { get; set; }
}

public sealed class NuevoTicketCreadoRespuesta
{
    public string IncidenciaNumero { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}
