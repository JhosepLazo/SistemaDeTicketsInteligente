-- Migracion aditiva: no elimina ni recrea bases o datos empresariales.
Use [GestionSistemas]
Go

-- Mismas opciones que 25_AsistenteIngenieriaAutonomo.sql: los SP deben compilarse con QUOTED_IDENTIFIER ON
-- para poder modificar TI_AgenteSesion cuando existen índices filtrados (IX_TI_AgenteSesion_IncidenciaFecha).
Set Ansi_Nulls On
Set Ansi_Padding On
Set Ansi_Warnings On
Set ArithAbort On
Set Concat_Null_Yields_Null On
Set Quoted_Identifier On
Set Numeric_RoundAbort Off
Go
If Col_Length('dbo.TI_AgenteEvento', 'OrigenServidor') Is Null
    Alter Table dbo.TI_AgenteEvento Add OrigenServidor bit Not Null Constraint DF_TI_AgenteEvento_Origen Default 0
Go
If Col_Length('dbo.TI_AgenteSesion', 'SolucionValidada') Is Null
    Alter Table dbo.TI_AgenteSesion Add SolucionValidada bit Not Null Constraint DF_TI_AgenteSesion_Validada Default 0, ConocimientoCodigo varchar(20) Null
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Listar
    @cUsuario varchar(20), @cArea char(3)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Throw 50500, 'El operador TI no se encuentra habilitado.', 1
    Select Top (100) SesionNumero, IsNull(IncidenciaNumero, '') IncidenciaNumero, IdCorrelacion, DescripcionInicial, Estado, FechaInicio,
        SolucionValidada, IsNull(ConocimientoCodigo, '') ConocimientoCodigo
    From dbo.TI_AgenteSesion Where UsuarioTI = @cUsuario and AreaTI = @cArea Order By FechaInicio Desc, SesionNumero Desc
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Cancelar
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
    Set NoCount On
    Set Xact_Abort On
    Begin Try
        Begin Transaction
        Declare @cIncidencia varchar(12), @nSolicitud int, @cCorrelacion uniqueidentifier
        Select @cIncidencia = IncidenciaNumero, @nSolicitud = SolicitudAprobacionSecuencia, @cCorrelacion = IdCorrelacion
        From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
        Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
            and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR','PENDIENTE_TI','PENDIENTE_APROBACION','SIN_EJECUTOR')
        If @@RowCount = 0 Throw 50532, 'La investigacion no puede cancelarse en su estado actual.', 1
        Update dbo.TI_SolicitudAprobacion Set Estado = 'C', FechaRespuesta = SysDateTime(), ComentarioRespuesta = N'Investigacion cancelada por TI.'
        Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and Estado = 'P'
        Update dbo.TI_AgenteSesion Set Estado = 'CANCELADO', FechaCierre = SysDateTime(), UsuarioDecision = @cUsuario, Decision = 'CANCELAR'
        Where SesionNumero = @nSesionNumero
        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
        Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'CANCELAR_INVESTIGACION', 'CANCELADO', @cCorrelacion, SysDateTime())
        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        Throw
    End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Vincular
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint, @cIncidenciaNumero varchar(12)
As
Begin
    Set NoCount On
    If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidenciaNumero) Throw 50502, 'La incidencia indicada no existe.', 1
    Update dbo.TI_AgenteSesion Set IncidenciaNumero = @cIncidenciaNumero
    Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and IncidenciaNumero Is Null
        and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR')
    If @@RowCount = 0 Throw 50533, 'Vincula el ticket antes de generar el diagnostico; una asociacion existente no puede reemplazarse.', 1
End
Go

-- Exclusivamente invocado por instrumentacion interna, nunca por un DTO de eventos.
Create Or Alter Procedure dbo.Usp_TI_Agente_Correlacion
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
    Set NoCount On
    Select s.IdCorrelacion From dbo.TI_AgenteSesion s
    Join dbo.TI_Usuario u on u.Usuario = s.UsuarioTI and u.Area = s.AreaTI
    Where s.SesionNumero = @nSesionNumero and s.UsuarioTI = @cUsuario and s.AreaTI = @cArea
        and s.Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR') and u.Estado = 'A' and u.Perfil In ('TEC','SUP','ADM')
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_Telemetria
    @cUsuario varchar(20), @nSesionNumero bigint, @cContenido nvarchar(max), @cDatosJson nvarchar(max)
As
Begin
    Set NoCount On
    Set Xact_Abort On
    Begin Try
        Begin Transaction
        If Not Exists (Select 1 From dbo.TI_AgenteSesion With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario
            and Estado In ('RECOPILANDO','OBSERVANDO','LISTO_INVESTIGAR'))
        Begin
            Commit Transaction
            Return
        End
        Declare @nSecuencia int
        Select @nSecuencia = IsNull(Max(Secuencia), 0) + 1 From dbo.TI_AgenteEvento With (UpdLock, HoldLock) Where SesionNumero = @nSesionNumero
        Insert dbo.TI_AgenteEvento (SesionNumero, Secuencia, Tipo, Fuente, Contenido, DatosJson, Fecha, OrigenServidor)
        Values (@nSesionNumero, @nSecuencia, 'TRAZA_BACKEND', 'TELEMETRIA', Left(@cContenido, 12000), @cDatosJson, SysDateTime(), 1)
        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        Throw
    End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_DryRun
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
    Set NoCount On
    -- Solo SELECT: no invoca ejecutores, crea aprobaciones ni cambia estados.
    Declare @cIncidencia varchar(12), @cAccion varchar(50), @cParametros nvarchar(max), @nSolicitud int
    Select @cIncidencia = IncidenciaNumero, @cAccion = AccionCodigo, @cParametros = ParametrosJson, @nSolicitud = SolicitudAprobacionSecuencia
    From dbo.TI_AgenteSesion Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
    If @@RowCount = 0 Throw 50503, 'La investigacion no pertenece al operador.', 1
    Select 'IDENTIDAD' Codigo, 'Operador TI habilitado' Descripcion,
        Case When Exists (Select 1 From dbo.TI_Usuario Where Usuario = @cUsuario and Area = @cArea and Estado = 'A' and Perfil In ('TEC','SUP','ADM')) Then 'OK' Else 'ERROR' End Resultado
    Union All Select 'TICKET', 'Ticket vinculado y existente', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia) Then 'OK' Else 'PENDIENTE' End
    Union All Select 'ESTADO', 'Ticket admite intervencion', Case When Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia and Estado Not In ('RS','CA','CF','NP')) Then 'OK' Else 'PENDIENTE' End
    Union All Select 'ACCION', 'Accion correctiva activa', Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and Tipo = 'E' and Estado = 'A') Then 'OK' Else 'PENDIENTE' End
    Union All Select 'PARAMETROS', 'Parametros estructurados validos', Case When IsJson(@cParametros) = 1 and Left(LTrim(@cParametros), 1) = '{' Then 'OK' Else 'PENDIENTE' End
    Union All Select 'APROBACION', 'Aprobacion formal para esta accion y parametros', Case When Exists (Select 1 From dbo.TI_Accion Where AccionCodigo = @cAccion and RequiereAprobacion = 0)
        or Exists (Select 1 From dbo.TI_SolicitudAprobacion Where IncidenciaNumero = @cIncidencia and Secuencia = @nSolicitud and AccionCodigo = @cAccion and Estado = 'A' and IsNull(ParametrosJson, '{}') = IsNull(@cParametros, '{}')) Then 'OK' Else 'PENDIENTE' End
    Union All Select 'EJECUTOR', 'Procedimiento autorizado instalado', Case When Exists (Select 1 From dbo.TI_AgenteAccionEjecutor Where AccionCodigo = @cAccion and Estado = 'A' and Object_Id(Procedimiento, 'P') Is Not Null) Then 'OK' Else 'PENDIENTE' End
    Union All Select 'ERP', 'Validaciones internas ERP: requieren contrato diagnostico del propietario', 'NO_DISPONIBLE'
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_ValidarSolucion
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
    Set NoCount On
    Set Xact_Abort On
    Begin Try
        Begin Transaction
        Declare @cIncidencia varchar(12), @cCorrelacion uniqueidentifier
        Select @cIncidencia = IncidenciaNumero, @cCorrelacion = IdCorrelacion From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
        Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea
            and Estado In ('INFORME_GRABADO','CAMBIO_VALIDADO') and Diagnostico Is Not Null and SolucionPropuesta Is Not Null
        If @@RowCount = 0 Throw 50534, 'Finaliza la investigacion antes de validar su solucion.', 1
        Update dbo.TI_AgenteSesion Set SolucionValidada = 1 Where SesionNumero = @nSesionNumero
        Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
        Values (@cIncidencia, @cUsuario, 'T', 'TI_AgenteSesion', Convert(varchar(30), @nSesionNumero), 'VALIDAR_SOLUCION_AGENTE', 'VALIDADO_TI', @cCorrelacion, SysDateTime())
        Commit Transaction
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        Throw
    End Catch
End
Go

Create Or Alter Procedure dbo.Usp_TI_Agente_CrearBorrador
    @cUsuario varchar(20), @cArea char(3), @nSesionNumero bigint
As
Begin
    Set NoCount On
    Set Xact_Abort On
    Begin Try
        Begin Transaction
        Declare @cCodigo varchar(20), @nNumero int, @cIncidencia varchar(12), @cCorrelacion uniqueidentifier
        Select @cCodigo = ConocimientoCodigo, @cIncidencia = IncidenciaNumero, @cCorrelacion = IdCorrelacion
        From dbo.TI_AgenteSesion With (UpdLock, HoldLock)
        Where SesionNumero = @nSesionNumero and UsuarioTI = @cUsuario and AreaTI = @cArea and SolucionValidada = 1
        If @@RowCount = 0 Throw 50535, 'TI debe validar expresamente la solucion antes de crear conocimiento.', 1
        If @cCodigo Is Null
        Begin
            If Not Exists (Select 1 From dbo.TI_Incidencia Where IncidenciaNumero = @cIncidencia) Throw 50519, 'El borrador requiere un ticket con clasificacion valida.', 1
            Select @nNumero = IsNull(Max(Try_Convert(int, Replace(ConocimientoCodigo, 'KB-', ''))), 0) + 1 From dbo.TI_BaseConocimiento With (UpdLock, HoldLock) Where ConocimientoCodigo Like 'KB-%'
            If @nNumero > 999999 Throw 50536, 'Se alcanzo el limite de numeracion de conocimiento.', 1
            Set @cCodigo = 'KB-' + Right('000000' + Convert(varchar(6), @nNumero), 6)
            Insert dbo.TI_BaseConocimiento (ConocimientoCodigo, Titulo, Problema, Sintomas, MensajeError, Causa, Solucion, Procedimiento, Linea, Item, Tipo, SubTipo, Categoria, IncidenciaOrigen, Estado, FechaCreacion)
            Select @cCodigo, Left(i.Titulo, 250), i.Detalle, Coalesce(NullIf(s.ProcesoObservado, ''), i.Detalle), i.MensajeError,
                s.CausaProbable, s.SolucionPropuesta, s.SolucionPropuesta, i.Linea, i.Item, i.Tipo, i.SubTipo, i.Categoria, i.IncidenciaNumero, 'B', SysDateTime()
            From dbo.TI_AgenteSesion s Join dbo.TI_Incidencia i on i.IncidenciaNumero = s.IncidenciaNumero Where s.SesionNumero = @nSesionNumero
            Update dbo.TI_AgenteSesion Set ConocimientoCodigo = @cCodigo Where SesionNumero = @nSesionNumero
            Insert dbo.TI_Auditoria (IncidenciaNumero, Usuario, TipoActor, Entidad, Registro, Evento, Resultado, IdCorrelacion, Fecha)
            Values (@cIncidencia, @cUsuario, 'T', 'TI_BaseConocimiento', @cCodigo, 'BORRADOR_DESDE_AGENTE', 'BORRADOR', @cCorrelacion, SysDateTime())
        End
        Commit Transaction
        Select @cCodigo ConocimientoCodigo
    End Try
    Begin Catch
        If Xact_State() <> 0 Rollback Transaction
        Throw
    End Catch
End
Go
