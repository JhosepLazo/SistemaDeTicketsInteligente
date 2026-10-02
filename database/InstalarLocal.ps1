param(
    [switch]$ConfirmarRecreacion
)

$ErrorActionPreference = 'Stop'

if (-not $ConfirmarRecreacion) {
    throw 'La instalación elimina GestionSistemas, IntranetCalimod y Spring. Vuelve a ejecutar con -ConfirmarRecreacion.'
}

$sqlcmd = Get-Command sqlcmd -ErrorAction Stop
$raiz = [IO.Path]::GetFullPath($PSScriptRoot)
$legado = [IO.Path]::GetFullPath((Join-Path $raiz 'legado'))
$aplicacion = [IO.Path]::GetFullPath((Join-Path $raiz 'sistema-inteligente'))

foreach ($ruta in @($legado, $aplicacion)) {
    if (-not $ruta.StartsWith($raiz, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta de scripts fuera de database: $ruta"
    }
}

function Invoke-SqlFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "No se encontró el script requerido: $Path"
    }

    Write-Host "Ejecutando $([IO.Path]::GetFileName($Path))"
    & $sqlcmd.Source -S . -E -C -l 20 -b -i $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Falló el script $Path con código $LASTEXITCODE."
    }
}

$eliminar = @'
Use [master];
If Db_Id(N'GestionSistemas') Is Not Null
Begin
    Alter Database [GestionSistemas] Set Single_User With Rollback Immediate;
    Drop Database [GestionSistemas];
End;
If Db_Id(N'IntranetCalimod') Is Not Null
Begin
    Alter Database [IntranetCalimod] Set Single_User With Rollback Immediate;
    Drop Database [IntranetCalimod];
End;
If Db_Id(N'Spring') Is Not Null
Begin
    Alter Database [Spring] Set Single_User With Rollback Immediate;
    Drop Database [Spring];
End;
'@

& $sqlcmd.Source -S . -E -C -l 20 -b -Q $eliminar
if ($LASTEXITCODE -ne 0) {
    throw "No se pudieron eliminar las bases locales anteriores. Código $LASTEXITCODE."
}

Get-ChildItem -LiteralPath $legado -File -Filter '*.sql' |
    Sort-Object Name |
    ForEach-Object { Invoke-SqlFile $_.FullName }

$scriptsAplicacion = @(
    '00_PrepararGestionSistemas.sql',
    '01_Maestros.sql',
    '02_Incidencias.sql',
    '03_Inteligencia.sql',
    '04_AccionesControl.sql',
    '05_Auditoria.sql',
    '06_Indices.sql',
    '09_InsertDeDatos.sql',
    '10_Autenticacion.sql',
    '11_InicioUsuario.sql',
    '12_InicioTI.sql',
    '13_CredencialesDesarrollo.sql',
    '14_NuevoTicketUsuario.sql',
    '15_MisTicketsUsuario.sql',
    '16_GestionTicketsTI.sql',
    '17_BaseConocimientoTI.sql',
    '18_ReportesTI.sql',
    '19_MejorasFuncionalesSinIA.sql',
    '20_AjustesOperativosSinIA.sql',
    '21_CorreccionesCompatibilidadSinIA.sql',
    '22_CierreMejorasFuncionalesSinIA.sql',
    '08_Validacion.sql',
    '23_SincronizarDatosLegado.sql',
    '24_OptimizacionRendimiento.sql',
    '25_AsistenteIngenieriaAutonomo.sql',
    '26_AgenteDiagnosticoSeguro.sql',
    '27_AgenteFase1Integracion.sql',
    '28_AgenteFase2Integracion.sql'
)

foreach ($nombre in $scriptsAplicacion) {
    Invoke-SqlFile (Join-Path $aplicacion $nombre)
}

Write-Host 'Instalación local completada correctamente.'
