# Starts the local CosyVoice service from the same canonical backend configuration used by ASP.NET Core.
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "project-config.ps1")

try {
    $config = Get-ProjectCosyVoiceConfig -ProjectRoot $projectRoot

    if (-not $config.Enabled) {
        throw "CosyVoice:Enabled is false in backend/appsettings.json. The manual launcher will not override canonical runtime configuration."
    }

    if (-not (Test-Path $config.PythonExecutable -PathType Leaf)) {
        throw "CosyVoice Python venv was not found at '$($config.PythonExecutable)'. Run SETUP_COSYVOICE_WINDOWS.cmd first."
    }
    if (-not (Test-Path $config.ServiceScript -PathType Leaf)) {
        throw "CosyVoice service script was not found at '$($config.ServiceScript)'."
    }
    if (-not (Test-Path $config.CosyVoiceRepo -PathType Container)) {
        throw "CosyVoice repository was not found at '$($config.CosyVoiceRepo)'. Run SETUP_COSYVOICE_WINDOWS.cmd first."
    }
    if (-not (Test-Path $config.ModelPath -PathType Container)) {
        throw "CosyVoice model was not found at '$($config.ModelPath)'. Run SETUP_COSYVOICE_WINDOWS.cmd first."
    }

    Write-Host "Starting CosyVoice from canonical configuration:" -ForegroundColor Cyan
    Write-Host "  config: $($config.AppSettingsPath)"
    Write-Host "  host:   $($config.Host):$($config.Port)"
    Write-Host "  model:  $($config.ModelPath)"
    Write-Host "  voice:  $($config.ReferenceAudioPath)"

    $arguments = @(
        $config.ServiceScript,
        "--cosyvoice-repo", $config.CosyVoiceRepo,
        "--model-dir", $config.ModelPath,
        "--reference-wav", $config.ReferenceAudioPath,
        "--reference-text", $config.ReferenceTextPath,
        "--host", $config.Host,
        "--port", [string]$config.Port
    )

    if ($config.UseOfficialDemoVoiceWhenReferenceMissing) {
        $arguments += "--use-official-demo-voice"
    }

    & $config.PythonExecutable @arguments
    exit $LASTEXITCODE
}
catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
