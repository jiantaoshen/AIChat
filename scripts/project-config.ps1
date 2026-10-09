# This file centralizes Windows script access to application configuration so setup/run scripts do not duplicate model defaults.
function Get-ProjectOllamaConfig {
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

    $model = [string]$settings.Ollama.Model
    $baseUrl = [string]$settings.Ollama.BaseUrl

    # Match ASP.NET Core's standard environment-variable override naming for the
    # two values the Windows launcher needs. appsettings.json remains the
    # committed canonical default; an explicit environment override is intentional.
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
        AppSettingsPath = $appSettingsPath
    }
}
