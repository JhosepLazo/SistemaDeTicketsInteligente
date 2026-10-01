/*
	Archivo: 05_Auditoria.sql
	Objetivo: Crear la estructura de auditoría transversal para registrar eventos relevantes generados por usuarios, técnicos, inteligencia artificial o procesos del sistema.
	Responsabilidad: Mantener trazabilidad técnica sobre entidad afectada, registro, evento, resultado, actor, fecha y correlación de la operación.
	Dependencias: Requiere TI_Incidencia y TI_Usuario para las relaciones opcionales de contexto.
	Orden: Ejecutar después de la creación del núcleo operativo.
	Consideraciones: La auditoría complementa los historiales funcionales y no los reemplaza; no utiliza eliminación en cascada para preservar trazabilidad histórica.
*/

Use [GestionSistemas]
Go

Set Xact_Abort on

Begin Try
	Begin Transaction

	Create Table dbo.TI_Auditoria (
		AuditoriaNumero			bigint Identity(1,1) Not Null,
		IncidenciaNumero		varchar(12)			Null,
		Usuario					varchar(20)			Null,
		TipoActor				char(1)				Not Null,
		Entidad					varchar(100)		Not Null,
		Registro				varchar(200)		Not Null,
		Evento					varchar(100)		Not Null,
		Resultado				varchar(20)			Not Null,
		DetalleJson				nvarchar(max)		Null,
		IdCorrelacion			uniqueidentifier	Not Null,
		Fecha					datetime2(0)		Not Null,

		Constraint PK_TI_Auditoria
			Primary Key (AuditoriaNumero),
		Constraint FK_TI_Auditoria_Incidencia
			Foreign Key (IncidenciaNumero)
			References dbo.TI_Incidencia (IncidenciaNumero),
		Constraint FK_TI_Auditoria_Usuario
			Foreign Key (Usuario)
			References dbo.TI_Usuario (Usuario)
	)

	Commit Transaction
End Try
Begin Catch
	If Xact_State() <> 0
	Begin
		Rollback Transaction
	End

	;Throw
End Catch
Go
