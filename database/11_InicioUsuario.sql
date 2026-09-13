/*
	Archivo: 11_InicioUsuario.sql
	Objetivo: Crear la consulta consolidada que alimenta el módulo Inicio para usuarios autenticados.
	Responsabilidad: Obtener resumen, tickets que requieren atención, tickets recientes, acciones pendientes de cierre y actividad reciente del usuario.
	Dependencias: TI_Incidencia, TI_IncidenciaEstado, TI_Estado y TI_Usuario.
	Orden: Ejecutar después de 10_Autenticacion.sql.
	Consideraciones: Solo realiza lectura; todos los resultados se filtran por UsuarioSolicitante y no expone información interna de otros usuarios.
*/

Use [SistemaTicketsInteligente]
Go

-- Usp_TI_Obtener_InicioUsuario 'USR001'
Create Procedure dbo.Usp_TI_Obtener_InicioUsuario
/*================================================================================
Objetivo            : Obtener toda la información necesaria para el Inicio del usuario en una sola llamada.
Creado Por          : Jhosep S. Lazo
Fecha Creación      : 12/09/2026
SP Anterior         : Ninguno
Comentario Cambios  : Primera versión del dashboard de usuario.
================================================================================*/
	@cUsuario varchar(20)
As
Begin
	Set NoCount On

	Select
		TicketsActivos = Sum(Case When Estado Not In ('RS', 'CA') Then 1 Else 0 End),
		EnAtencion = Sum(Case When Estado In ('DG', 'PA', 'EJ', 'ES') Then 1 Else 0 End),
		RequierenAtencion = Sum(Case When Estado In ('RC', 'PV') Then 1 Else 0 End),
		Resueltos30Dias = Sum(Case When Estado = 'RS' and FechaCierre >= DateAdd(Day, -30, GetDate()) Then 1 Else 0 End),
		PendientesCalificacion = Sum(Case When Estado = 'RS' and Calificacion Is Null Then 1 Else 0 End)
	From dbo.TI_Incidencia
	Where UsuarioSolicitante = @cUsuario

	Select Top (3)
		i.IncidenciaNumero,
		i.Titulo,
		i.Estado,
		e.Descripcion as EstadoDescripcion,
		i.UltimaFechaModif,
		Accion = Case i.Estado When 'RC' Then 'COMPLETAR_INFORMACION' When 'PV' Then 'CONFIRMAR_SOLUCION' Else 'REVISAR' End
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Where i.UsuarioSolicitante = @cUsuario and i.Estado In ('RC', 'PV')
	Order By i.UltimaFechaModif Desc

	Select Top (5)
		i.IncidenciaNumero,
		i.Titulo,
		i.Estado,
		e.Descripcion as EstadoDescripcion,
		Responsable = IsNull(u.NombreCompleto, 'Pendiente de asignación'),
		i.UltimaFechaModif
	From dbo.TI_Incidencia as i
	Inner Join dbo.TI_Estado as e on e.Estado = i.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = i.UsuarioTI
	Where i.UsuarioSolicitante = @cUsuario
	Order By i.UltimaFechaModif Desc

	Select Top (3)
		i.IncidenciaNumero,
		i.Titulo,
		TipoAccion = Case When i.Estado = 'PV' Then 'CONFIRMAR_SOLUCION' Else 'CALIFICAR_ATENCION' End,
		Descripcion = Case When i.Estado = 'PV' Then 'Confirma si la solución aplicada resolvió el problema.' Else 'Califica la atención recibida para cerrar el ciclo del ticket.' End,
		i.UltimaFechaModif
	From dbo.TI_Incidencia as i
	Where i.UsuarioSolicitante = @cUsuario
		and (i.Estado = 'PV' or (i.Estado = 'RS' and i.Calificacion Is Null))
	Order By Case When i.Estado = 'PV' Then 0 Else 1 End, i.UltimaFechaModif Desc

	Select Top (5)
		i.IncidenciaNumero,
		i.Titulo,
		e.Descripcion as EstadoDescripcion,
		ie.FechaCambio,
		Actor = IsNull(u.NombreCompleto, 'Sistema')
	From dbo.TI_IncidenciaEstado as ie
	Inner Join dbo.TI_Incidencia as i on i.IncidenciaNumero = ie.IncidenciaNumero
	Inner Join dbo.TI_Estado as e on e.Estado = ie.Estado
	Left Join dbo.TI_Usuario as u on u.Usuario = ie.UsuarioCambio
	Where i.UsuarioSolicitante = @cUsuario
	Order By ie.FechaCambio Desc
End
Go

/* Ejecuta Procedure */
