Use [GestionSistemas]
Go

Set Xact_Abort On
Go

Begin Transaction

-- Conserva los catálogos y accesos de desarrollo, pero retira la transacción sintética.
Update dbo.TI_BaseConocimiento Set IncidenciaOrigen = Null Where IncidenciaOrigen Is Not Null;
Delete From dbo.TI_IncidenciaDiagnosticoEvidencia;
Delete From dbo.TI_IncidenciaDiagnostico;
Delete From dbo.TI_EjecucionAccion;
Delete From dbo.TI_Notificacion;
Delete From dbo.TI_SolicitudAprobacion;
Delete From dbo.TI_IncidenciaAdjunto;
Delete From dbo.TI_IncidenciaAvance;
Delete From dbo.TI_IncidenciaDocumento;
Delete From dbo.TI_IncidenciaEstado;
Delete From dbo.TI_IncidenciaMensaje;
Delete From dbo.TI_Auditoria;
Delete From dbo.TI_Incidencia;

Insert dbo.TI_Area (Area, Descripcion, Estado, Telefono, UltimoUsuario, UltimaFechaModif)
Select RTRIM(a.Inc02Area), Coalesce(NullIf(RTRIM(a.Inc02Descripcion), ''), RTRIM(a.Inc02Area)),
       Coalesce(NullIf(RTRIM(a.Estado), ''), 'A'), a.Inc02Telefono,
       a.Inc02UsuarioModifica, a.Inc02FechaModifica
From dbo.Inc02Area a
Where Not Exists (Select 1 From dbo.TI_Area t Where t.Area = a.Inc02Area);

;With Areas As (
    Select RTRIM(Inc02Area) Area From dbo.Inc20Incidencia Where Inc02Area Is Not Null
    Union Select RTRIM(Inc02AreaTI) From dbo.Inc20Incidencia Where Inc02AreaTI Is Not Null
    Union Select RTRIM(Inc02AreaCausante) From dbo.Inc20Incidencia Where Inc02AreaCausante Is Not Null
    Union Select RTRIM(Inc02Area) From dbo.Inc03Usuario Where Inc02Area Is Not Null
)
Insert dbo.TI_Area (Area, Descripcion, Estado)
Select a.Area, Concat('Área legado ', a.Area), 'A'
From Areas a Where Not Exists (Select 1 From dbo.TI_Area t Where t.Area = a.Area);

Insert dbo.TI_Linea (Linea, Area, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
Select RTRIM(l.Inc01Linea), Coalesce(NullIf(RTRIM(l.Inc02Area), ''), '002'),
       Coalesce(NullIf(RTRIM(l.Inc01Descripcion), ''), RTRIM(l.Inc01Linea)),
       Coalesce(NullIf(RTRIM(l.Estado), ''), 'A'), l.Inc01UsuarioModifica, l.Inc01FechaModifica
From dbo.Inc01Linea l
Where Exists (Select 1 From dbo.TI_Area a Where a.Area = Coalesce(NullIf(RTRIM(l.Inc02Area), ''), '002'))
  And Not Exists (Select 1 From dbo.TI_Linea t Where t.Linea = l.Inc01Linea);

Insert dbo.TI_Item (Item, Linea, Descripcion, Estado, UltimoUsuario, UltimaFechaModif)
Select RTRIM(i.Inc04Item), RTRIM(i.Inc01Linea),
       Coalesce(NullIf(RTRIM(i.Inc04Descripcion), ''), RTRIM(i.Inc04Item)),
       Coalesce(NullIf(RTRIM(i.Estado), ''), 'A'), i.Inc04UsuarioModifica, i.Inc04FechaModifica
From dbo.Inc04Item i
Where Exists (Select 1 From dbo.TI_Linea l Where l.Linea = i.Inc01Linea)
  And Not Exists (Select 1 From dbo.TI_Item t Where t.Item = i.Inc04Item);

;With ItemsTicket As (
    Select RTRIM(i.Inc01Linea) Linea, RTRIM(i.Inc04Item) Item,
           Max(Coalesce(NullIf(RTRIM(m.Inc04Descripcion), ''), RTRIM(i.Inc04Item))) Descripcion
    From dbo.Inc20Incidencia i
    Left Join dbo.Inc04Item m On m.Inc04Item=i.Inc04Item And m.Inc01Linea=i.Inc01Linea
    Where i.Inc01Linea Is Not Null And i.Inc04Item Is Not Null
    Group By i.Inc01Linea, i.Inc04Item
)
Insert dbo.TI_Item (Item, Linea, Descripcion, Estado)
Select Concat('L', t.Linea, '-', t.Item), t.Linea, t.Descripcion, 'A'
From ItemsTicket t
Where Not Exists (Select 1 From dbo.TI_Item x Where x.Item=Concat('L', t.Linea, '-', t.Item));

Insert dbo.TI_Tipo (Tipo, Descripcion, Abreviatura, Item, Estado, UltimoUsuario, UltimaFechaModif)
Select RTRIM(t.Inc05Tipo), Coalesce(NullIf(RTRIM(t.Inc05Descripcion), ''), RTRIM(t.Inc05Tipo)),
       t.Inc05Abreviatura, Null, Coalesce(NullIf(RTRIM(t.Estado), ''), 'A'),
       t.Inc05UsuarioModifica, t.Inc05FechaModifica
From dbo.Inc05Tipo t
Where Not Exists (Select 1 From dbo.TI_Tipo x Where x.Tipo = t.Inc05Tipo);

Insert dbo.TI_Estado (Estado, Descripcion, Orden, UltimoUsuario, UltimaFechaModif)
Select RTRIM(e.Inc06Estado), Coalesce(NullIf(RTRIM(e.Inc06Descripcion), ''), RTRIM(e.Inc06Estado)),
       100 + Row_Number() Over (Order By e.Inc06Estado), e.Inc06UsuarioModifica, e.Inc06FechaModifica
From dbo.Inc06Estado e
Where Not Exists (Select 1 From dbo.TI_Estado x Where x.Estado = e.Inc06Estado);

;With Categorias As (
    Select Distinct IdCategoria From dbo.Inc20Incidencia Where IdCategoria Is Not Null
)
Insert dbo.TI_Categoria (Categoria, Descripcion, Abreviatura, Estado)
Select Concat('LEG-', IdCategoria), Concat('Categoría legado ', IdCategoria), Right(Concat('000', IdCategoria), 3), 'A'
From Categorias c
Where Not Exists (Select 1 From dbo.TI_Categoria x Where x.Categoria = Concat('LEG-', c.IdCategoria));

;With Pares As (
    Select Distinct Concat('L', RTRIM(Inc01Linea), '-', RTRIM(Inc04Item)) Item, Concat('LEG-', IdCategoria) Categoria,
           Try_Convert(int, IncPrioridad) Prioridad, Try_Convert(int, IncImpacto) Impacto,
           Try_Convert(int, IncComplejidad) Complejidad
    From dbo.Inc20Incidencia
    Where Inc04Item Is Not Null And IdCategoria Is Not Null
)
Insert dbo.TI_ItemCategoria (Item, Categoria, Prioridad, Impacto, Complejidad, Estado)
Select p.Item, p.Categoria, Max(p.Prioridad), Max(p.Impacto), Max(p.Complejidad), 'A'
From Pares p
Where Exists (Select 1 From dbo.TI_Item i Where i.Item = p.Item)
  And Not Exists (Select 1 From dbo.TI_ItemCategoria x Where x.Item = p.Item And x.Categoria = p.Categoria)
Group By p.Item, p.Categoria;

;With SubTipos As (
    Select RTRIM(Inc05Tipo) Tipo, RTRIM(Inc08SubTipo) SubTipo,
           Concat('LEG-', IdCategoria) Categoria
    From dbo.Inc20Incidencia
    Where Inc05Tipo Is Not Null And Inc08SubTipo Is Not Null And IdCategoria Is Not Null
    Group By Inc05Tipo, Inc08SubTipo, IdCategoria
)
Insert dbo.TI_SubTipo (Tipo, SubTipo, Categoria, Descripcion, Abreviatura, Estado)
Select s.Tipo, s.SubTipo, s.Categoria,
       Coalesce(NullIf(RTRIM(Max(st.Inc08Descripcion)), ''), Concat('Subtipo legado ', s.SubTipo)),
       Max(st.Inc08Abreviatura), 'A'
From SubTipos s
Left Join dbo.Inc08SubTipo st On st.Inc05Tipo = s.Tipo And st.Inc08SubTipo = s.SubTipo And st.IdCategoria = Try_Convert(int, Replace(s.Categoria, 'LEG-', ''))
Where Not Exists (Select 1 From dbo.TI_SubTipo x Where x.Tipo=s.Tipo And x.SubTipo=s.SubTipo And x.Categoria=s.Categoria)
Group By s.Tipo, s.SubTipo, s.Categoria;

;With Nombres As (
    Select RTRIM(Inc03Usuario) Usuario From dbo.Inc03Usuario
    Union Select RTRIM(Inc03Usuario) From dbo.Inc20Incidencia Where Inc03Usuario Is Not Null
    Union Select RTRIM(Inc03UsuarioTI) From dbo.Inc20Incidencia Where Inc03UsuarioTI Is Not Null
    Union Select RTRIM(Inc03UsuarioAsigno) From dbo.Inc20Incidencia Where Inc03UsuarioAsigno Is Not Null
    Union Select RTRIM(Inc20UsuarioCrea) From dbo.Inc20Incidencia Where Inc20UsuarioCrea Is Not Null
    Union Select RTRIM(Inc20UsuarioModifica) From dbo.Inc20Incidencia Where Inc20UsuarioModifica Is Not Null
    Union Select RTRIM(Inc20UsuarioGC) From dbo.Inc20Incidencia Where Inc20UsuarioGC Is Not Null
    Union Select RTRIM(Inc03UsuarioTI) From dbo.Inc21Avance Where Inc03UsuarioTI Is Not Null
), Usuarios As (
    Select n.Usuario, Coalesce(NullIf(RTRIM(u.Inc03Descripcion), ''), n.Usuario) NombreCompleto,
           Coalesce(NullIf(RTRIM(u.Inc02Area), ''), oa.Area, '002') Area,
           u.Inc03Correo, u.Inc03Anexo, u.Telefono,
           Case When Exists (Select 1 From dbo.Inc20Incidencia i Where RTRIM(i.Inc03UsuarioTI)=n.Usuario)
                  Or Exists (Select 1 From dbo.Inc21Avance a Where RTRIM(a.Inc03UsuarioTI)=n.Usuario)
                Then 'TEC' Else 'USR' End Perfil
    From Nombres n
    Left Join dbo.Inc03Usuario u On RTRIM(u.Inc03Usuario)=n.Usuario
    Outer Apply (Select Top (1) RTRIM(i.Inc02Area) Area From dbo.Inc20Incidencia i Where RTRIM(i.Inc03Usuario)=n.Usuario And i.Inc02Area Is Not Null) oa
    Where n.Usuario Is Not Null And n.Usuario <> ''
)
Insert dbo.TI_Usuario (Usuario, NombreCompleto, Clave, Area, Cargo, Perfil, Correo, Anexo, Telefono, Estado, TipoUsuario, FuenteIdentidad)
Select u.Usuario, u.NombreCompleto, 'HASH_DEMO_NO_VALIDO', u.Area, Null, u.Perfil,
       u.Inc03Correo, u.Inc03Anexo, u.Telefono, 'A', 'INTERNO', 'LEGADO'
From Usuarios u
Where Exists (Select 1 From dbo.TI_Area a Where a.Area=u.Area)
  And Not Exists (Select 1 From dbo.TI_Usuario x Where x.Usuario=u.Usuario);

-- Acceso local para comprobar la vista normal con tickets de la muestra empresarial.
Update usuarioEmpresa
Set Clave = usuarioPrueba.Clave
From dbo.TI_Usuario usuarioEmpresa
Cross Join dbo.TI_Usuario usuarioPrueba
Where usuarioEmpresa.Usuario = 'YPENALOZA'
  And usuarioPrueba.Usuario = 'USR001';

Insert dbo.TI_Incidencia (
    IncidenciaNumero, FechaRegistro, UsuarioSolicitante, AreaSolicitante, AreaTI, UsuarioTI, UsuarioAsigno,
    Linea, Item, Tipo, SubTipo, Categoria, Estado, AreaCausante, Titulo, Detalle,
    FechaAsignacion, FechaAtencion, FechaCierre, SlaObjetivoMinutos, Prioridad, Impacto, Complejidad,
    CanalRegistro, SolucionTecnica, UltimoUsuario, UltimaFechaModif, UsuarioRegistro
)
Select RTRIM(i.Inc20Incidencia), Coalesce(i.Inc20Fecha, GetDate()), RTRIM(i.Inc03Usuario), RTRIM(i.Inc02Area),
       RTRIM(i.Inc02AreaTI), RTRIM(i.Inc03UsuarioTI), RTRIM(i.Inc03UsuarioAsigno), RTRIM(i.Inc01Linea),
       Concat('L', RTRIM(i.Inc01Linea), '-', RTRIM(i.Inc04Item)), RTRIM(i.Inc05Tipo), RTRIM(i.Inc08SubTipo),
       Case When i.IdCategoria Is Null Then Null Else Concat('LEG-', i.IdCategoria) End,
       RTRIM(i.Inc20Estado), RTRIM(i.Inc02AreaCausante),
       Coalesce(NullIf(Convert(nvarchar(500), i.Inc20Titulo), ''), Left(i.Inc20Detalle, 500), RTRIM(i.Inc20Incidencia)),
       Coalesce(NullIf(i.Inc20Detalle, ''), Convert(nvarchar(max), i.Inc20Titulo), N'Sin detalle'),
       i.Inc20FechaAsignado, i.Inc20FechaAtencion, i.Inc20FechaCierre, i.IncSLA,
       i.IncPrioridad, i.IncImpacto, i.IncComplejidad, 'LEGADO', i.Respuesta,
       Coalesce(RTRIM(i.Inc20UsuarioModifica), RTRIM(i.Inc20UsuarioCrea), RTRIM(i.Inc03Usuario)),
       Coalesce(i.Inc20FechaModifica, i.Inc20Fecha, GetDate()),
       Coalesce(RTRIM(i.Inc20UsuarioCrea), RTRIM(i.Inc03Usuario))
From dbo.Inc20Incidencia i;

Insert dbo.TI_IncidenciaEstado (IncidenciaNumero, Secuencia, Estado, UsuarioCambio, FechaCambio, Observacion)
Select RTRIM(i.Inc20Incidencia), 1, RTRIM(i.Inc20Estado),
       Coalesce(RTRIM(i.Inc20UsuarioModifica), RTRIM(i.Inc20UsuarioCrea), RTRIM(i.Inc03Usuario)),
       Coalesce(i.Inc20FechaModifica, i.Inc20Fecha, GetDate()), N'Estado importado desde GestionSistemas legado.'
From dbo.Inc20Incidencia i;

;With Avances As (
    Select RTRIM(a.Inc20Incidencia) IncidenciaNumero,
           Row_Number() Over (Partition By a.Inc20Incidencia Order By a.Inc21Fecha, a.Inc21Avance) Secuencia,
           Coalesce(RTRIM(a.Inc03UsuarioTI), RTRIM(i.Inc03UsuarioTI), 'TEC001') UsuarioTI,
           Coalesce(a.Inc21Fecha, i.Inc20Fecha, GetDate()) FechaAvance,
           Coalesce(NullIf(a.Inc21Detalle, ''), N'Avance importado desde el sistema legado.') Detalle,
           a.Inc21TiempoUtilizado TiempoUtilizado, a.Inc21Porcentaje PorcentajeAvance
    From dbo.Inc21Avance a
    Join dbo.Inc20Incidencia i On i.Inc20Incidencia=a.Inc20Incidencia
)
Insert dbo.TI_IncidenciaAvance (IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado, PorcentajeAvance)
Select IncidenciaNumero, Secuencia, UsuarioTI, FechaAvance, Detalle, TiempoUtilizado,
       Case When PorcentajeAvance Between 0 And 100 Then PorcentajeAvance Else Null End
From Avances;

Commit Transaction
Go

Select Count(*) As TicketsEmpresariales From dbo.TI_Incidencia Where CanalRegistro='LEGADO';
Go
