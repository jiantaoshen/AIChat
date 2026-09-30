# This script creates a project-local Python 3.10 venv, installs a Windows-compatible CosyVoice3 runtime, clones the official repository, and downloads the Fun-CosyVoice3-0.5B-2512 model.
param(
    [ValidateSet("huggingface", "modelscope")]
    [string]$ModelSource = "huggingface",

    [ValidateSet("auto", "cpu", "cu121")]
    [string]$TorchBackend = "auto"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ToolsDir = Join-Path $ProjectRoot "tools"
$CosyRoot = Join-Path $ToolsDir "cosyvoice"
$RepoDir = Join-Path $CosyRoot "CosyVoice"
$VenvDir = Join-Path $CosyRoot ".venv"
$VenvPython = Join-Path $VenvDir "Scripts\python.exe"
$ModelsDir = Join-Path $CosyRoot "models"
$ModelDir = Join-Path $ModelsDir "Fun-CosyVoice3-0.5B-2512"
$VoiceDir = Join-Path $CosyRoot "voice"
$GeneratedRequirements = Join-Path $CosyRoot "requirements-windows-venv.txt"
$ModelId = "FunAudioLLM/Fun-CosyVoice3-0.5B-2512"

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Assert-Python310 {
    Require-Command "py"

    $version = & py -3.10 -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')" 2>$null
    if ($LASTEXITCODE -ne 0 -or $version.Trim() -ne "3.10") {
        throw "Python 3.10 was not found through the Windows Python Launcher. Install Python 3.10 x64 from python.org with the Python Launcher enabled, then rerun this script."
    }
}

Write-Host "== AI Avatar / CosyVoice3 Python venv setup ==" -ForegroundColor Cyan
Write-Host "Project: $ProjectRoot"
Write-Host "Model:   $ModelId"
Write-Host "Source:  $ModelSource"
Write-Host "Torch:   $TorchBackend"
Write-Host ""

Require-Command "git"
Assert-Python310

New-Item -ItemType Directory -Force -Path $CosyRoot, $ModelsDir, $VoiceDir | Out-Null

if (-not (Test-Path (Join-Path $RepoDir ".git"))) {
    Write-Host "Cloning official CosyVoice repository..." -ForegroundColor Yellow
    git clone --recursive https://github.com/FunAudioLLM/CosyVoice.git $RepoDir
    if ($LASTEXITCODE -ne 0) {
        throw "git clone failed."
    }
} else {
    Write-Host "CosyVoice repository already exists. Updating submodules..." -ForegroundColor Yellow
    Push-Location $RepoDir
    try {
        git submodule update --init --recursive
        if ($LASTEXITCODE -ne 0) {
            throw "git submodule update failed."
        }
    } finally {
        Pop-Location
    }
}

if (-not (Test-Path $VenvPython)) {
    Write-Host "Creating project-local Python 3.10 venv..." -ForegroundColor Yellow
    & py -3.10 -m venv $VenvDir
    if ($LASTEXITCODE -ne 0) { throw "Python venv creation failed." }
} else {
    Write-Host "Python venv already exists at $VenvDir" -ForegroundColor Green
}

Write-Host "Preparing pip / setuptools / wheel..." -ForegroundColor Yellow
# openai-whisper==20231117 still imports pkg_resources while building.
# setuptools 81+ removed pkg_resources, so keep setuptools below 81 in this Windows venv.
& $VenvPython -m pip install --upgrade "pip<27" "setuptools<81" wheel
if ($LASTEXITCODE -ne 0) { throw "pip bootstrap failed." }

$ResolvedTorchBackend = $TorchBackend
if ($ResolvedTorchBackend -eq "auto") {
    if (Get-Command "nvidia-smi" -ErrorAction SilentlyContinue) {
        $ResolvedTorchBackend = "cu121"
    } else {
        $ResolvedTorchBackend = "cpu"
    }
}

if ($ResolvedTorchBackend -eq "cu121") {
    Write-Host "Installing PyTorch 2.3.1 / CUDA 12.1 wheels..." -ForegroundColor Yellow
    & $VenvPython -m pip install torch==2.3.1 torchaudio==2.3.1 --index-url https://download.pytorch.org/whl/cu121
} else {
    Write-Host "Installing PyTorch 2.3.1 CPU wheels..." -ForegroundColor Yellow
    & $VenvPython -m pip install torch==2.3.1 torchaudio==2.3.1 --index-url https://download.pytorch.org/whl/cpu
}
if ($LASTEXITCODE -ne 0) { throw "PyTorch installation failed." }

# The official CosyVoice requirements include wetext -> pynini. Pynini does not provide
# native Windows wheels on PyPI, so a pure venv install cannot use it. Current CosyVoice
# gracefully falls back to its tokenizer path when no text-normalization frontend is present.
# We therefore install the official runtime requirements minus wetext/pynini and the already
# installed torch packages. This keeps the project Conda-free on native Windows.
$OfficialRequirements = Join-Path $RepoDir "requirements.txt"
if (-not (Test-Path $OfficialRequirements)) {
    throw "CosyVoice requirements.txt was not found at $OfficialRequirements"
}

$Filtered = Get-Content $OfficialRequirements | Where-Object {
    $line = $_.Trim()
    if ($line -eq "") { return $true }
    if ($line.StartsWith("#")) { return $true }
    if ($line.StartsWith("--extra-index-url")) { return $false }
    if ($line -match "^(torch|torchaudio)(==|>=|<=|~=|>|<|\s|$)") { return $false }
    if ($line -match "^wetext(==|>=|<=|~=|>|<|\s|$)") { return $false }
    # Install this legacy source distribution separately without build isolation so
    # it uses our setuptools<81 environment (pkg_resources is still available).
    if ($line -match "^openai-whisper(==|>=|<=|~=|>|<|\s|$)") { return $false }
    return $true
}
$Filtered | Set-Content -Encoding UTF8 $GeneratedRequirements

Write-Host "Installing openai-whisper 20231117 with Windows-compatible build settings..." -ForegroundColor Yellow
& $VenvPython -m pip install --prefer-binary --no-build-isolation "openai-whisper==20231117"
if ($LASTEXITCODE -ne 0) {
    throw "openai-whisper installation failed. setuptools is pinned below 81 and build isolation is disabled; if the error now mentions Rust, install the Rust toolchain and rerun setup."
}

Write-Host "Installing CosyVoice runtime dependencies into the venv..." -ForegroundColor Yellow
& $VenvPython -m pip install --prefer-binary -r $GeneratedRequirements
if ($LASTEXITCODE -ne 0) {
    throw "CosyVoice dependency installation failed. If the error mentions a compiler/build tool, install Visual Studio Build Tools with Desktop development with C++ and rerun the setup."
}

# Ensure the small HTTP wrapper dependencies are present even if upstream requirements change.
& $VenvPython -m pip install "fastapi>=0.115,<1" "uvicorn>=0.30,<1" "soundfile>=0.12,<1" "huggingface_hub>=0.24,<1"
if ($LASTEXITCODE -ne 0) { throw "Local CosyVoice service dependencies failed to install." }

if (-not (Test-Path (Join-Path $ModelDir "cosyvoice3.yaml"))) {
    Write-Host "Downloading $ModelId ..." -ForegroundColor Yellow
    $escapedModelDir = $ModelDir.Replace("'", "''")

    if ($ModelSource -eq "huggingface") {
        $code = "from huggingface_hub import snapshot_download; snapshot_download(repo_id='$ModelId', local_dir=r'$escapedModelDir')"
        & $VenvPython -c $code
    } else {
        $code = "from modelscope import snapshot_download; snapshot_download('$ModelId', local_dir=r'$escapedModelDir')"
        & $VenvPython -c $code
    }

    if ($LASTEXITCODE -ne 0) { throw "Model download failed." }
} else {
    Write-Host "CosyVoice3 model already exists at $ModelDir" -ForegroundColor Green
}

$ReferenceExample = Join-Path $VoiceDir "reference.txt.example"
if (-not (Test-Path $ReferenceExample)) {
    "你好，很高兴见到你。今天想聊些什么？" | Set-Content -Encoding UTF8 $ReferenceExample
}

Write-Host ""
Write-Host "Verifying the venv..." -ForegroundColor Yellow
& $VenvPython -c "import sys, torch, fastapi, uvicorn, soundfile; print('Python', sys.version.split()[0]); print('PyTorch', torch.__version__); print('CUDA available:', torch.cuda.is_available())"
if ($LASTEXITCODE -ne 0) { throw "Venv verification failed." }

Write-Host ""
Write-Host "CosyVoice3 venv setup complete." -ForegroundColor Green
Write-Host "Python: $VenvPython"
Write-Host "Torch backend requested: $ResolvedTorchBackend"
Write-Host ""
Write-Host "For a custom avatar voice, add:" -ForegroundColor Cyan
Write-Host "  $VoiceDir\reference.wav"
Write-Host "  $VoiceDir\reference.txt"
Write-Host ""
Write-Host "No Conda installation is required by this project." -ForegroundColor Cyan
Write-Host "The backend can auto-start the TTS service, or you can run RUN_COSYVOICE_WINDOWS.cmd manually." -ForegroundColor Cyan
