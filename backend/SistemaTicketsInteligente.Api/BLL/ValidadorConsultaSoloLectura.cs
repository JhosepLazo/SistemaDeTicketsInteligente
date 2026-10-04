/**
 * Archivo: ValidadorConsultaSoloLectura.cs
 * Objetivo: Decidir si una consulta propuesta por el agente es un SELECT de solo lectura seguro de ejecutar.
 * Responsabilidad: Analizar el T-SQL con el parser oficial (ScriptDom) y rechazar todo lo que no sea una única lectura acotada.
 * Dependencias: Microsoft.SqlServer.TransactSql.ScriptDom.
 * Flujo: ReplicaTecnicaBLL -> Validar -> (ejecución en transacción revertida) o rechazo con motivo.
 * Consideraciones: Es una capa de defensa, no la única: la consulta además se ejecuta con el login de lectura configurado,
 *   dentro de una transacción que siempre se revierte, con tiempo y filas limitados y quedando auditada como evidencia.
 */

using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SistemaTicketsInteligente.Api.BLL;

public static class ValidadorConsultaSoloLectura
{
    public sealed record Resultado(bool Valida, string Motivo);

    public static Resultado Validar(string sql, Func<string, bool> esColumnaSensible, string baseDatosPermitida)
    {
        if (string.IsNullOrWhiteSpace(sql)) return new(false, "La consulta está vacía.");
        if (sql.Length > 4000) return new(false, "La consulta supera los 4000 caracteres permitidos.");

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var lector = new StringReader(sql);
        var arbol = parser.Parse(lector, out var errores);
        if (errores.Count > 0) return new(false, $"La consulta no es T-SQL válido: {errores[0].Message}");

        if (arbol is not TSqlScript script || script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1)
            return new(false, "Solo se permite una única sentencia SELECT.");
        if (script.Batches[0].Statements[0] is not SelectStatement seleccion)
            return new(false, "Solo se permiten consultas SELECT de lectura.");
        if (seleccion.Into is not null) return new(false, "SELECT ... INTO crea tablas y no está permitido.");

        var visitante = new Visitante(esColumnaSensible, baseDatosPermitida);
        seleccion.Accept(visitante);
        return visitante.Motivo is null ? new(true, string.Empty) : new(false, visitante.Motivo);
    }

    private sealed class Visitante(Func<string, bool> esColumnaSensible, string baseDatosPermitida) : TSqlFragmentVisitor
    {
        public string? Motivo { get; private set; }

        private void Rechazar(string motivo) => Motivo ??= motivo;

        public override void Visit(SchemaObjectName nombre)
        {
            if (nombre.ServerIdentifier is not null) Rechazar("No se permiten servidores vinculados.");
            if (nombre.DatabaseIdentifier is not null && !string.Equals(nombre.DatabaseIdentifier.Value, baseDatosPermitida, StringComparison.OrdinalIgnoreCase))
                Rechazar($"Solo se permite consultar la base {baseDatosPermitida}.");
            base.Visit(nombre);
        }

        public override void Visit(OpenRowsetTableReference nodo) { Rechazar("OPENROWSET no está permitido."); base.Visit(nodo); }
        public override void Visit(OpenQueryTableReference nodo) { Rechazar("OPENQUERY no está permitido."); base.Visit(nodo); }
        public override void Visit(AdHocTableReference nodo) { Rechazar("OPENDATASOURCE no está permitido."); base.Visit(nodo); }
        public override void Visit(BulkOpenRowset nodo) { Rechazar("OPENROWSET(BULK) no está permitido."); base.Visit(nodo); }
        public override void Visit(OpenXmlTableReference nodo) { Rechazar("OPENXML no está permitido."); base.Visit(nodo); }

        // Las funciones de usuario (siempre con esquema: dbo.fn...) pueden tener efectos o costos ocultos; solo se admiten las integradas.
        public override void Visit(FunctionCall nodo)
        {
            if (nodo.CallTarget is not null) Rechazar($"No se permiten funciones definidas por usuario ({nodo.FunctionName.Value}).");
            base.Visit(nodo);
        }

        public override void Visit(SchemaObjectFunctionTableReference nodo)
        {
            Rechazar("No se permiten funciones de tabla definidas por usuario.");
            base.Visit(nodo);
        }

        // Las sugerencias de bloqueo podrían bloquear la operación del sistema; solo se admite NOLOCK.
        public override void Visit(TableHint nodo)
        {
            if (nodo.HintKind is not (TableHintKind.NoLock or TableHintKind.ReadUncommitted)) Rechazar($"La sugerencia de tabla {nodo.HintKind} no está permitida.");
            base.Visit(nodo);
        }

        public override void Visit(ColumnReferenceExpression nodo)
        {
            var identificadores = nodo.MultiPartIdentifier?.Identifiers;
            if (identificadores is { Count: > 0 } && esColumnaSensible(identificadores[^1].Value))
                Rechazar($"La columna {identificadores[^1].Value} contiene datos sensibles y no puede consultarse.");
            base.Visit(nodo);
        }
    }
}
