# This file centralizes Windows script access to backend/appsettings.json so launchers do not duplicate runtime defaults.
function Get-ProjectSettingsDocument {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $appSettingsPath = Join-Path $ProjectRoot "backend\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Canonical backend configuration was not found: $appSettingsPath"
    }

    try {
        $settings = Get-Content -Path $appSettingsPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Could not parse canonical backend configuration '$appSettingsPath': $($_.Exception.Message)"
    }

    [pscustomobject]@{
        Settings = $settings
        AppSettingsPath = $appSettingsPath
        BackendRoot = Join-Path $ProjectRoot "backend"
    }
}

function ConvertTo-ProjectBoolean {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)]$Value
    )

    if ($Value -is [bool]) {
        return $Value
    }

    $parsed = $false
    if ([bool]::TryParse([string]$Value, [ref]$parsed)) {
        return $parsed
    }

    throw "$Name must be 'true' or 'false'. Configured value: '$Value'"
}

function Resolve-BackendConfiguredPath {
    param(
        [Parameter(Mandatory = $true)][string]$BackendRoot,
        [Parameter(Mandatory = $true)][string]$ConfiguredPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        throw "$Name is missing from backend/appsettings.json."
    }

    if ([System.IO.Path]::IsPathRooted($ConfiguredPath)) {
        return [System.IO.Path]::GetFullPath($ConfiguredPath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BackendRoot $ConfiguredPath))
}

function Get-ProjectOllamaConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $document = Get-ProjectSettingsDocument -ProjectRoot $ProjectRoot
    $model = [string]$document.Settings.Ollama.Model
    $baseUrl = [string]$document.Settings.Ollama.BaseUrl

    # Match ASP.NET Core's standard environment-variable override naming for the
    # values Windows scripts need. appsettings.json remains the committed default.
    if (-not [string]::IsNullOrWhiteSpace($env:Ollama__Model)) {
        $model = $env:Ollama__Model
    }
    if (-not [string]::IsNullOrWhiteSpace($env:Ollama__BaseUrl)) {
        $baseUrl = $env:Ollama__BaseUrl
    }

    if ([string]::IsNullOrWhiteSpace($model)) {
        throw "Ollama:Model is missing from backend/appsettings.json. Configure it there instead of adding a model default to a script."
    }
    if ([string]::IsNullOrWhiteSpace($baseUrl)) {
        throw "Ollama:BaseUrl is missing from backend/appsettings.json."
    }

    try {
        $uri = [uri]$baseUrl
        if (-not $uri.IsAbsoluteUri) {
            throw "not absolute"
        }
    }
    catch {
        throw "Ollama:BaseUrl must be an absolute URI. Configured value: '$baseUrl'"
    }

    [pscustomobject]@{
        Model = $model.Trim()
        BaseUrl = $baseUrl.TrimEnd('/')
        AppSettingsPath = $document.AppSettingsPath
    }
}

function Get-ProjectCosyVoiceConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot
    )

    $document = Get-ProjectSettingsDocument -ProjectRoot $ProjectRoot
    $section = $document.Settings.CosyVoice
    if ($null -eq $section) {
        throw "CosyVoice configuration is missing from backend/appsettings.json."
    }

    $enabled = $section.Enabled
    $pythonExecutable = [string]$section.PythonExecutable
    $serviceScript = [string]$section.ServiceScript
    $cosyVoiceRepo = [string]$section.CosyVoiceRepo
    $modelPath = [string]$section.ModelPath
    $referenceAudioPath = [string]$section.ReferenceAudioPath
    $referenceTextPath = [string]$section.ReferenceTextPath
    $useFallback = $section.UseOfficialDemoVoiceWhenReferenceMissing
    $host = [string]$section.Host
    $port = [string]$section.Port

    $overrides = @{
        Enabled = $env:CosyVoice__Enabled
        PythonExecutable = $env:CosyVoice__PythonExecutable
        ServiceScript = $env:CosyVoice__ServiceScript
        CosyVoiceRepo = $env:CosyVoice__CosyVoiceRepo
        ModelPath = $env:CosyVoice__ModelPath
        ReferenceAudioPath = $env:CosyVoice__ReferenceAudioPath
        ReferenceTextPath = $env:CosyVoice__ReferenceTextPath
        UseOfficialDemoVoiceWhenReferenceMissing = $env:CosyVoice__UseOfficialDemoVoiceWhenReferenceMissing
        Host = $env:CosyVoice__Host
        Port = $env:CosyVoice__Port
    }

    if (-not [string]::IsNullOrWhiteSpace($overrides.Enabled)) { $enabled = $overrides.Enabled }
    if (-not [string]::IsNullOrWhiteSpace($overrides.PythonExecutable)) { $pythonExecutable = $overrides.PythonExecutable }
    if (-not [string]::IsNullOrWhiteSpace($overrides.ServiceScript)) { $serviceScript = $overrides.ServiceScript }
    if (-not [string]::IsNullOrWhiteSpace($overrides.CosyVoiceRepo)) { $cosyVoiceRepo = $overrides.CosyVoiceRepo }
    if (-not [string]::IsNullOrWhiteSpace($overrides.ModelPath)) { $modelPath = $overrides.ModelPath }
    if (-not [string]::IsNullOrWhiteSpace($overrides.ReferenceAudioPath)) { $referenceAudioPath = $overrides.ReferenceAudioPath }
    if (-not [string]::IsNullOrWhiteSpace($overrides.ReferenceTextPath)) { $referenceTextPath = $overrides.ReferenceTextPath }
    if (-not [string]::IsNullOrWhiteSpace($overrides.UseOfficialDemoVoiceWhenReferenceMissing)) { $useFallback = $overrides.UseOfficialDemoVoiceWhenReferenceMissing }
    if (-not [string]::IsNullOrWhiteSpace($overrides.Host)) { $host = $overrides.Host }
    if (-not [string]::IsNullOrWhiteSpace($overrides.Port)) { $port = $overrides.Port }

    $enabled = ConvertTo-ProjectBoolean -Name "CosyVoice:Enabled" -Value $enabled
    $useFallback = ConvertTo-ProjectBoolean -Name "CosyVoice:UseOfficialDemoVoiceWhenReferenceMissing" -Value $useFallback

    $parsedPort = 0
    $portIsValid = [int]::TryParse($port, [ref]$parsedPort)
    if (-not $portIsValid -or $parsedPort -lt 1 -or $parsedPort -gt 65535) {
        throw "CosyVoice:Port must be an integer from 1 to 65535. Configured value: '$port'"
    }
    if ([string]::IsNullOrWhiteSpace($host)) {
        throw "CosyVoice:Host is missing from backend/appsettings.json."
    }

    [pscustomobject]@{
        Enabled = $enabled
        PythonExecutable = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $pythonExecutable -Name "CosyVoice:PythonExecutable"
        ServiceScript = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $serviceScript -Name "CosyVoice:ServiceScript"
        CosyVoiceRepo = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $cosyVoiceRepo -Name "CosyVoice:CosyVoiceRepo"
        ModelPath = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $modelPath -Name "CosyVoice:ModelPath"
        ReferenceAudioPath = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $referenceAudioPath -Name "CosyVoice:ReferenceAudioPath"
        ReferenceTextPath = Resolve-BackendConfiguredPath -BackendRoot $document.BackendRoot -ConfiguredPath $referenceTextPath -Name "CosyVoice:ReferenceTextPath"
        UseOfficialDemoVoiceWhenReferenceMissing = $useFallback
        Host = $host.Trim()
        Port = $parsedPort
        AppSettingsPath = $document.AppSettingsPath
    }
}
