<#
	Archivo: IniciarProyecto.ps1
	Objetivo: Arrancar el proyecto en local con un solo comando y siempre con el código actual.
	Responsabilidad: Verificar SQL Server y el puerto de la API, cargar las claves de IA guardadas como variables de usuario,
		abrir el frontend (si no está corriendo) y compilar e iniciar la API con el perfil Local.
	Uso: .\IniciarProyecto.ps1              API en esta ventana y frontend en otra.
	     .\IniciarProyecto.ps1 -SinFrontend  Solo la API.
	Consideraciones: No detiene procesos ni modifica la base de datos. Si la API anterior sigue abierta, indica cómo cerrarla.
#>
param([switch]$SinFrontend)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot

$sql = Get-Service MSSQLSERVER -ErrorAction SilentlyContinue
if (-not $sql) {
	Write-Warning 'No se encontró el servicio MSSQLSERVER en este equipo.'
} elseif ($sql.Status -ne 'Running') {
	Write-Host 'SQL Server está detenido. Inícialo en una PowerShell como administrador:  Start-Service MSSQLSERVER' -ForegroundColor Yellow
	exit 1
}

$ocupado = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($ocupado) {
	Write-Host "El puerto 5000 ya está en uso (proceso $($ocupado.OwningProcess)): hay una API corriendo, posiblemente con código anterior." -ForegroundColor Yellow
	Write-Host 'Ciérrala con Ctrl+C en su ventana (o desde el Administrador de tareas) y vuelve a ejecutar este script.' -ForegroundColor Yellow
	exit 1
}

# Las claves se guardan como variables de usuario; una ventana abierta antes de guardarlas no las ve.
foreach ($variable in 'GEMINI_API_KEY', 'OPENAI_API_KEY', 'GROQ_API_KEY') {
	if (-not [Environment]::GetEnvironmentVariable($variable, 'Process')) {
		$valor = [Environment]::GetEnvironmentVariable($variable, 'User')
		if ($valor) { Set-Item "env:$variable" $valor }
	}
}
if (-not $env:GEMINI_API_KEY -and -not $env:OPENAI_API_KEY -and -not $env:GROQ_API_KEY) {
	Write-Host 'Sin clave de IA: el agente funcionará sin modelo y sin Live. Ver README (Gemini gratuito).' -ForegroundColor Yellow
}
$env:ASPNETCORE_ENVIRONMENT = 'Local'

if (-not $SinFrontend -and -not (Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue)) {
	Write-Host 'Abriendo el frontend en otra ventana (http://127.0.0.1:5173)...' -ForegroundColor Cyan
	Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$raiz\frontend'; npm run dev -- --host 127.0.0.1"
}

Write-Host 'Compilando e iniciando la API con el perfil Local (Ctrl+C para detenerla)...' -ForegroundColor Cyan
dotnet run --project "$raiz\backend\SistemaTicketsInteligente.Api"
