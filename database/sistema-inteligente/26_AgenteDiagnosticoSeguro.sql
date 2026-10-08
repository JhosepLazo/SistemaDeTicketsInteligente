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

-- dbo.Usp_TI_Agente_Listar: la versión vigente está en 28_AgenteFase2Integracion.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_Cancelar: la versión vigente está en 27_AgenteFase1Integracion.sql (aquí había una versión anterior que ese script reemplaza).
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
-- dbo.Usp_TI_Agente_Correlacion: la versión vigente está en 29_AgenteFase3ReproduccionUsuario.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_Telemetria: la versión vigente está en 29_AgenteFase3ReproduccionUsuario.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_DryRun: la versión vigente está en 32_AgenteFase6ReplicaTecnica.sql (aquí había una versión anterior que ese script reemplaza).
Go

-- dbo.Usp_TI_Agente_ValidarSolucion: la versión vigente está en 34_DominiosYMaquinaEstados.sql (aquí había una versión anterior que ese script reemplaza).
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
