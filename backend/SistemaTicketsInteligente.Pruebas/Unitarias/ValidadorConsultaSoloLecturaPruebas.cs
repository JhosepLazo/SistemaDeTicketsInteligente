/**
 * Archivo: ValidadorConsultaSoloLecturaPruebas.cs
 * Objetivo: Comprobar que el agente solo puede ejecutar una lectura acotada sobre la base permitida.
 * Responsabilidad: Aceptar consultas SELECT legítimas y rechazar escritura, lotes, ejecución dinámica, acceso a otras bases o servidores,
 *   funciones de usuario, sugerencias de bloqueo y columnas sensibles, incluidos los intentos de esconderlas.
 * Dependencias: ValidadorConsultaSoloLectura.
 * Flujo: consulta propuesta -> Validar -> válida o rechazada con motivo.
 * Consideraciones: Es una capa de defensa; en ejecución la consulta además corre con el login de lectura y dentro de una transacción
 *   que siempre se revierte.
 */

namespace SistemaTicketsInteligente.Pruebas.Unitarias;

public sealed class ValidadorConsultaSoloLecturaPruebas
{
    private const string BasePermitida = "GestionSistemas";

    private static readonly HashSet<string> ColumnasSensibles = new(StringComparer.OrdinalIgnoreCase) { "Clave", "Contrasena", "Token" };

    private static ValidadorConsultaSoloLectura.Resultado Validar(string sql) =>
        ValidadorConsultaSoloLectura.Validar(sql, ColumnasSensibles.Contains, BasePermitida);

    [Theory]
    [InlineData("Select Top (20) IncidenciaNumero, Estado From dbo.TI_Incidencia Where Estado = 'PA' Order By IncidenciaNumero")]
    [InlineData("Select i.IncidenciaNumero, e.Descripcion From dbo.TI_Incidencia i Join dbo.TI_Estado e on e.Estado = i.Estado")]
    [InlineData("Select Count(*) From dbo.TI_Incidencia With (NoLock)")]
    [InlineData("Select IncidenciaNumero From GestionSistemas.dbo.TI_Incidencia")]
    [InlineData("With Pendientes as (Select IncidenciaNumero From dbo.TI_Incidencia Where Estado = 'PA') Select * From Pendientes")]
    [InlineData("Select IsNull(Max(Secuencia), 0) From dbo.TI_IncidenciaEstado Where IncidenciaNumero = 'INC-000001'")]
    public void LecturaAcotadaEsValida(string sql)
    {
        var resultado = Validar(sql);

        Assert.True(resultado.Valida, resultado.Motivo);
    }

    [Theory]
    [InlineData("", "vacía")]
    [InlineData("Update dbo.TI_Incidencia Set Estado = 'RS'", "SELECT")]
    [InlineData("Delete From dbo.TI_Incidencia", "SELECT")]
    [InlineData("Insert dbo.TI_Estado (Estado) Values ('XX')", "SELECT")]
    [InlineData("Exec dbo.Usp_TI_Cerrar_TicketsSinValidacion @nDias = 0", "SELECT")]
    [InlineData("Exec ('Select 1')", "SELECT")]
    [InlineData("Drop Table dbo.TI_Incidencia", "SELECT")]
    [InlineData("Truncate Table dbo.TI_Auditoria", "SELECT")]
    [InlineData("WaitFor Delay '00:10:00'", "SELECT")]
    [InlineData("Select 1; Delete From dbo.TI_Incidencia", "única")]
    [InlineData("Select 1 -- comentario\n; Drop Table dbo.TI_Incidencia", "única")]
    [InlineData("Select 1\nGo\nSelect 2", "única")]
    [InlineData("With x as (Select IncidenciaNumero From dbo.TI_Incidencia) Delete From x", "SELECT")]
    [InlineData("Select * Into dbo.Copia From dbo.TI_Incidencia", "INTO")]
    [InlineData("Select * From master.dbo.sysdatabases", "base")]
    [InlineData("Select * From [IntranetCalimod].dbo.Usuarios", "base")]
    [InlineData("Select * From ServidorRemoto.GestionSistemas.dbo.TI_Incidencia", "vinculados")]
    [InlineData("Select * From OpenRowSet('SQLNCLI', 'Server=otro;Trusted_Connection=yes;', 'Select 1')", "OPENROWSET")]
    [InlineData("Select * From OpenQuery(ServidorRemoto, 'Select 1')", "OPENQUERY")]
    [InlineData("Select * From OpenDataSource('SQLNCLI', 'Data Source=otro').GestionSistemas.dbo.TI_Incidencia", "OPENDATASOURCE")]
    [InlineData("Select * From OpenRowSet(Bulk 'C:\\Windows\\win.ini', Single_Clob) as archivo", "OPENROWSET")]
    [InlineData("Select dbo.fn_Calcular(IncidenciaNumero) From dbo.TI_Incidencia", "funciones definidas por usuario")]
    [InlineData("Select * From dbo.fn_Tabla()", "funciones de tabla")]
    [InlineData("Select * From dbo.TI_Incidencia With (TabLockX)", "sugerencia")]
    [InlineData("Select * From dbo.TI_Incidencia With (UpdLock, HoldLock)", "sugerencia")]
    [InlineData("Select Clave From dbo.TI_Usuario", "sensibles")]
    [InlineData("Select u.[Clave] From dbo.TI_Usuario u", "sensibles")]
    [InlineData("Select Usuario From dbo.TI_Usuario Where Clave Like 'A%'", "sensibles")]
    [InlineData("Select Len(Token) From dbo.TI_Sesion", "sensibles")]
    [InlineData("Select Usuario From dbo.TI_Usuario Order By Contrasena", "sensibles")]
    [InlineData("Selec * From dbo.TI_Incidencia", "T-SQL válido")]
    public void ConsultaPeligrosaEsRechazada(string sql, string fragmentoMotivo)
    {
        var resultado = Validar(sql);

        Assert.False(resultado.Valida, $"Se aceptó: {sql}");
        Assert.Contains(fragmentoMotivo, resultado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsultaDemasiadoLargaEsRechazada()
    {
        var sql = "Select IncidenciaNumero From dbo.TI_Incidencia Where Titulo = '" + new string('x', 4000) + "'";

        var resultado = Validar(sql);

        Assert.False(resultado.Valida);
        Assert.Contains("4000", resultado.Motivo);
    }
}
