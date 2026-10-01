param(
    [Parameter(Mandatory = $true)]
    [string]$DocumentosOrigen,

    [Parameter(Mandatory = $true)]
    [string]$Destino
)

$ErrorActionPreference = 'Stop'

$inventario = Join-Path $DocumentosOrigen 'INVENTARIO_BASES_DATOS_Y_TABLAS.md'
$analisis = Join-Path $DocumentosOrigen 'ANALISIS_BASE_DATOS_PROYECTO.md'

foreach ($archivo in @($inventario, $analisis)) {
    if (-not (Test-Path -LiteralPath $archivo -PathType Leaf)) {
        throw "No se encontro el documento requerido: $archivo"
    }
}

$destinoResuelto = [IO.Path]::GetFullPath($Destino)
$raizRepositorio = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $destinoResuelto.StartsWith($raizRepositorio, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'El destino debe permanecer dentro del repositorio.'
}

New-Item -ItemType Directory -Path $destinoResuelto -Force | Out-Null

function Get-SqlBlocks {
    param([string]$Path)

    $lineas = Get-Content -LiteralPath $Path -Encoding UTF8
    $encabezado = ''
    $dentro = $false
    $contenido = [Collections.Generic.List[string]]::new()

    foreach ($linea in $lineas) {
        if ($linea -match '^#{1,6} ') {
            $encabezado = $linea
        }

        if (-not $dentro -and $linea -eq ('`' * 6 + 'sql')) {
            $dentro = $true
            $contenido.Clear()
            continue
        }

        if ($dentro -and $linea -eq ('`' * 6)) {
            [pscustomobject]@{
                Encabezado = $encabezado
                Sql = ($contenido -join [Environment]::NewLine).Trim()
            }
            $dentro = $false
            continue
        }

        if ($dentro) {
            $contenido.Add($linea)
        }
    }
}

function Repair-CreateTableBlock {
    param([string]$Sql)

    $lineas = $Sql -split '\r?\n'
    $inicio = [Array]::FindIndex($lineas, [Predicate[string]] { param($x) $x -match '^CREATE TABLE ' })
    $fin = [Array]::FindIndex($lineas, [Predicate[string]] { param($x) $x -match '^\);\s*$' })
    if ($inicio -lt 0 -or $fin -le $inicio) {
        throw 'No se pudo reconocer un bloque CREATE TABLE del inventario.'
    }

    for ($indice = $inicio + 1; $indice -lt $fin; $indice++) {
        if ([string]::IsNullOrWhiteSpace($lineas[$indice])) { continue }
        $lineas[$indice] = $lineas[$indice].TrimEnd().TrimEnd(',') + ','
    }

    for ($indice = $fin - 1; $indice -gt $inicio; $indice--) {
        if ([string]::IsNullOrWhiteSpace($lineas[$indice])) { continue }
        $lineas[$indice] = $lineas[$indice].TrimEnd(',')
        break
    }

    for ($indice = 0; $indice -lt $lineas.Count; $indice++) {
        if ($lineas[$indice] -match 'PRIMARY KEY') {
            $lineas[$indice] = $lineas[$indice] -replace '\],\s+ASC', '] ASC'
        }
    }

    # La muestra confirmada de Inc00Correlativo contiene Codigo/Serie nulos,
    # aunque el DDL exportado los marca como PK no nula. Se prioriza conservar
    # los registros observados y se omite esa restriccion contradictoria.
    if ($Sql -match 'CREATE TABLE \[dbo\]\.\[Inc00Correlativo\]') {
        $lineas = @($lineas | Where-Object { $_ -notmatch 'CONSTRAINT \[PK_Inc00Correlativo\]' })
        for ($indice = 0; $indice -lt $lineas.Count; $indice++) {
            $lineas[$indice] = $lineas[$indice] -replace '(\[Codigo\]\s+\[varchar\]\(50\)) NOT NULL', '$1 NULL'
            $lineas[$indice] = $lineas[$indice] -replace '(\[Serie\]\s+\[varchar\]\(50\)) NOT NULL', '$1 NULL'
        }
        $cierre = [Array]::FindIndex($lineas, [Predicate[string]] { param($x) $x -match '^\);\s*$' })
        for ($indice = $cierre - 1; $indice -ge 0; $indice--) {
            if ([string]::IsNullOrWhiteSpace($lineas[$indice])) { continue }
            $lineas[$indice] = $lineas[$indice].TrimEnd(',')
            break
        }
    }

    # La muestra de incidencias incluye registros historicos con este valor nulo,
    # aunque el catalogo exportado conserva la declaracion NOT NULL con default.
    if ($Sql -match 'CREATE TABLE \[dbo\]\.\[Inc20Incidencia\]') {
        for ($indice = 0; $indice -lt $lineas.Count; $indice++) {
            $lineas[$indice] = $lineas[$indice] -replace '(\[Inc20FlagLectura\]\s+\[bit\]) NOT NULL', '$1 NULL'
        }
    }

    return ($lineas -join [Environment]::NewLine)
}

function Repair-SampleData {
    param([string]$Sql)

    # El generador documental imprimio siete decimales para columnas datetime.
    # SQL Server datetime conserva milisegundos, por lo que se normaliza a tres.
    $reparado = [regex]::Replace(
        $Sql,
        "'([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3})[0-9]{4}'",
        '''$1''')

    # ExpirarPasswordFlag es char(1), pero fue sustituido por el marcador largo
    # [REDACTADO] durante la anonimizacion. Null conserva que el valor no esta disponible.
    if ($reparado -match 'INSERT INTO \[dbo\]\.\[Usuario\]') {
        $reparado = $reparado -replace "N'\[REDACTADO\]', N'\[REDACTADO\]'", "N'[REDACTADO]', NULL"
    }

    return $reparado
}

function Write-SqlFile {
    param([string]$Name, [Collections.Generic.List[string]]$Parts)

    $cabecera = @(
        'Set NoCount On',
        'Set Xact_Abort On',
        'Go',
        ''
    )
    $contenido = ($cabecera + $Parts) -join [Environment]::NewLine
    Set-Content -LiteralPath (Join-Path $destinoResuelto $Name) -Value $contenido -Encoding UTF8
}

$bloquesInventario = @(Get-SqlBlocks $inventario)
$bloquesAnalisis = @(Get-SqlBlocks $analisis)

$crearBases = [Collections.Generic.List[string]]::new()
$crearBases.Add(@'
Use [master]
Go

If Db_Id(N'GestionSistemas') Is Null Create Database [GestionSistemas]
Go
If Db_Id(N'IntranetCalimod') Is Null Create Database [IntranetCalimod]
Go
If Db_Id(N'Spring') Is Null Create Database [Spring]
Go

Alter Database [GestionSistemas] Set Compatibility_Level = 160
Alter Database [IntranetCalimod] Set Compatibility_Level = 160
Alter Database [Spring] Set Compatibility_Level = 160
Go
'@)
Write-SqlFile '00_CrearBases.sql' $crearBases

$tablas = [Collections.Generic.List[string]]::new()
foreach ($bloque in $bloquesInventario | Where-Object { $_.Encabezado -match '^### 6\.' }) {
    $tablas.Add((Repair-CreateTableBlock $bloque.Sql))
    $tablas.Add('')
}
Write-SqlFile '01_CrearTablas.sql' $tablas

$tablasGestion = @(
    'Calificacion', 'Inc00Correlativo', 'Inc01Linea', 'Inc02Area', 'Inc03Usuario', 'Inc04Item',
    'Inc05Tipo', 'Inc06Estado', 'Inc07Cargo', 'Inc08SubTipo', 'Inc10ItemCategoria', 'Inc20Incidencia',
    'Inc20IncidenciaArchivos', 'Inc21Avance', 'IncComplejidad', 'IncFormato', 'IncImpacto',
    'IncPrioridad', 'IncSLA', 'IncSLAPorcentaje', 'IncVideoTutorial', 'Sis01Menu'
)

$datosGestion = [Collections.Generic.List[string]]::new()
$datosGestion.Add("Use [GestionSistemas]`r`nGo`r`nBegin Transaction`r`nGo")
foreach ($bloque in $bloquesAnalisis) {
    if ($bloque.Sql -notmatch '(?im)^(?:SET IDENTITY_INSERT \[dbo\]\.\[([^\]]+)\]|INSERT INTO \[dbo\]\.\[([^\]]+)\])') { continue }
    $tabla = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
    if ($tabla -notin $tablasGestion) { continue }
    $sqlDatos = Repair-SampleData $bloque.Sql
    if ($tabla -eq 'Inc08SubTipo') {
        $sqlDatos = "Set Identity_Insert [dbo].[Inc08SubTipo] On;`r`n$sqlDatos`r`nSet Identity_Insert [dbo].[Inc08SubTipo] Off;"
    }
    $datosGestion.Add($sqlDatos)
    $datosGestion.Add('Go')
}
$datosGestion.Add("Commit Transaction`r`nGo")
Write-SqlFile '02_CargarDatosGestionSistemas.sql' $datosGestion

$datosExternos = [Collections.Generic.List[string]]::new()
$datosExternos.Add("Begin Transaction`r`nGo")
foreach ($bloque in $bloquesInventario | Where-Object { $_.Encabezado -match '^### 7\.' }) {
    $datosExternos.Add((Repair-SampleData $bloque.Sql))
    $datosExternos.Add('')
}
$datosExternos.Add("Commit Transaction`r`nGo")
Write-SqlFile '03_CargarDatosIntranetSpring.sql' $datosExternos

$objetosSoporte = [Collections.Generic.List[string]]::new()
$objetosSoporte.Add(@'
Use [IntranetCalimod]
Go
Create Or Alter View dbo.vwUsuarioPerfilMenu
As
Select
    pu.UsuarioId,
    pu.SistemaId,
    pu.PerfilId,
    p.Descripcion as PerfilDescripcion,
    p.Habilitado as PerfilHabilitado,
    m.MenuId,
    m.Descripcion as MenuDescripcion,
    m.PadreId,
    m.Posicion,
    m.Icono,
    m.Habilitado as MenuHabilitado,
    m.FormUrl
From dbo.PerfilUsuario as pu
Inner Join dbo.Perfil as p
    on p.PerfilId = pu.PerfilId and p.SistemaId = pu.SistemaId
Inner Join dbo.PerfilMenu as pm
    on pm.PerfilId = pu.PerfilId and pm.SistemaId = pu.SistemaId
Inner Join dbo.Menu as m
    on m.MenuId = pm.MenuId and m.SistemaId = pm.SistemaId
Go

Use [GestionSistemas]
Go
Create Or Alter Function dbo.Fun_Inc_Inc03UsuarioVerifica
(
    @Usuario varchar(20),
    @Clave varchar(20)
)
Returns bit
As
Begin
    Declare @Resultado bit = 0
    If Exists
    (
        Select 1
        From dbo.Inc03Usuario
        Where Inc03Usuario = @Usuario
            and RTrim(Inc03Clave) = @Clave
            and IsNull(Inc03Estado, 'A') = 'A'
    )
        Set @Resultado = 1
    Return @Resultado
End
Go

Use [Spring]
Go
Create Or Alter Function dbo.Verificar_Usuario
(
    @Usuario varchar(20),
    @Clave varchar(20)
)
Returns bit
As
Begin
    Declare @Resultado bit = 0
    If Exists
    (
        Select 1
        From dbo.Usuario
        Where Usuario = @Usuario
            and RTrim(Clave) = @Clave
            and IsNull(Estado, 'A') = 'A'
    )
        Set @Resultado = 1
    Return @Resultado
End
Go

Create Or Alter Function dbo.Fun_Inc_SelectUsuarioRecuerdaPw
(
    @Usuario varchar(20)
)
Returns varchar(20)
As
Begin
    Declare @Clave varchar(20)
    Select @Clave = RTrim(Clave)
    From dbo.Usuario
    Where Usuario = @Usuario
    Return @Clave
End
Go
'@)
Write-SqlFile '04_CrearObjetosSoporte.sql' $objetosSoporte

$procedimientosGestion = [Collections.Generic.List[string]]::new()
$procedimientosIntranet = [Collections.Generic.List[string]]::new()
$procedimientosSpring = [Collections.Generic.List[string]]::new()

foreach ($bloque in $bloquesAnalisis | Where-Object { $_.Encabezado -match '^### 18\.(\d+) ' }) {
    [void]($bloque.Encabezado -match '^### 18\.(\d+) ')
    $numero = [int]$Matches[1]
    $sqlProcedimiento = [regex]::Replace(
        $bloque.Sql,
        '(?im)^\s*CREATE\s+(?:PROCEDURE|PROC)\s+',
        'Create Or Alter Procedure ',
        1)
    if ($numero -in 1, 2) {
        $procedimientosIntranet.Add($sqlProcedimiento)
        $procedimientosIntranet.Add('Go')
    } elseif ($numero -eq 3 -or $numero -in 85, 86, 87, 88, 90) {
        $procedimientosSpring.Add($sqlProcedimiento)
        $procedimientosSpring.Add('Go')
    } else {
        $procedimientosGestion.Add($sqlProcedimiento)
        $procedimientosGestion.Add('Go')
    }
}

$procedimientosIntranet.Insert(0, "Use [IntranetCalimod]`r`nGo")
$procedimientosSpring.Insert(0, "Use [Spring]`r`nGo")
$procedimientosGestion.Insert(0, "Use [GestionSistemas]`r`nGo")

Write-SqlFile '05_CrearProcedimientosIntranet.sql' $procedimientosIntranet
Write-SqlFile '06_CrearProcedimientosSpring.sql' $procedimientosSpring
Write-SqlFile '07_CrearProcedimientosGestionSistemas.sql' $procedimientosGestion

Write-Output "Scripts generados en $destinoResuelto"
