// This file starts and stops the local CosyVoice3 Python service and validates any existing listener before reuse.
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using AiAvatar.Backend.Options;
using Microsoft.Extensions.Options;

namespace AiAvatar.Backend.Services.Speech;

public sealed class CosyVoiceServerHostedService : IHostedService, IDisposable
{
    private readonly CosyVoiceOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CosyVoiceServerHostedService> _logger;
    private Process? _process;
    private bool _ownsProcess;

    public CosyVoiceServerHostedService(
        IOptions<CosyVoiceOptions> options,
        IHostEnvironment environment,
        ILogger<CosyVoiceServerHostedService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Local CosyVoice TTS is disabled.");
            return;
        }

        var existingHealth = await TryReadHealthAsync(cancellationToken);
        if (existingHealth is not null)
        {
            var validation = CosyVoiceServiceHealthPolicy.ValidateForReuse(
                existingHealth,
                BuildExpectedService());

            if (!validation.CanReuse)
            {
                _logger.LogError(
                    "Refusing to reuse the service on {BaseUrl}: {Reason}. Stop the stale listener before using TTS. Text chat remains available.",
                    _options.BaseUrl,
                    validation.Reason);
                return;
            }

            _logger.LogInformation(
                "Reusing verified CosyVoice service on {BaseUrl}. Service={ServiceId}, contract={ContractVersion}, model={Model}, voice={VoiceSource}.",
                _options.BaseUrl,
                existingHealth.Service,
                existingHealth.ContractVersion,
                existingHealth.Model,
                existingHealth.VoiceSource);
            return;
        }

        if (await IsPortOpenAsync(_options.Host, _options.Port, cancellationToken))
        {
            _logger.LogError(
                "Refusing to reuse the listener on {Host}:{Port}: GET {BaseUrl}/health did not return the expected CosyVoice health contract. Stop the process occupying the port before using TTS. Text chat remains available.",
                _options.Host,
                _options.Port,
                _options.BaseUrl);
            return;
        }

        if (!_options.AutoStart)
        {
            _logger.LogInformation(
                "CosyVoice AutoStart is disabled. Start RUN_COSYVOICE_WINDOWS.cmd manually before using TTS.");
            return;
        }

        var pythonExecutable = ResolvePath(_options.PythonExecutable);
        var serviceScript = ResolvePath(_options.ServiceScript);
        var repoPath = ResolvePath(_options.CosyVoiceRepo);
        var modelPath = ResolvePath(_options.ModelPath);
        var referenceAudio = ResolvePath(_options.ReferenceAudioPath);
        var referenceText = ResolvePath(_options.ReferenceTextPath);

        if (!File.Exists(pythonExecutable))
        {
            _logger.LogWarning(
                "CosyVoice Python venv was not found at {Path}. Run SETUP_COSYVOICE_WINDOWS.cmd first.",
                pythonExecutable);
            return;
        }

        if (!File.Exists(serviceScript))
        {
            _logger.LogWarning("CosyVoice service script was not found at {Path}.", serviceScript);
            return;
        }

        if (!Directory.Exists(repoPath) || !Directory.Exists(modelPath))
        {
            _logger.LogWarning(
                "CosyVoice repo or model is missing. Run SETUP_COSYVOICE_WINDOWS.cmd before using TTS.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            WorkingDirectory = Path.GetDirectoryName(serviceScript) ?? _environment.ContentRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add(serviceScript);
        startInfo.ArgumentList.Add("--cosyvoice-repo");
        startInfo.ArgumentList.Add(repoPath);
        startInfo.ArgumentList.Add("--model-dir");
        startInfo.ArgumentList.Add(modelPath);
        startInfo.ArgumentList.Add("--reference-wav");
        startInfo.ArgumentList.Add(referenceAudio);
        startInfo.ArgumentList.Add("--reference-text");
        startInfo.ArgumentList.Add(referenceText);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(_options.Host);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(_options.Port.ToString());

        if (_options.UseOfficialDemoVoiceWhenReferenceMissing)
        {
            startInfo.ArgumentList.Add("--use-official-demo-voice");
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                _logger.LogInformation("cosyvoice: {Message}", eventArgs.Data);
            }
        };
        _process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                _logger.LogInformation("cosyvoice: {Message}", eventArgs.Data);
            }
        };
        _process.Exited += (_, _) =>
        {
            _logger.LogWarning(
                "CosyVoice service exited with code {ExitCode}.",
                SafeExitCode(_process));
        };

        try
        {
            if (!_process.Start())
            {
                _logger.LogWarning("Could not start the CosyVoice service.");
                return;
            }
        }
        catch (Win32Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not start the CosyVoice Python executable at {PythonExecutable}. Run SETUP_COSYVOICE_WINDOWS.cmd or start RUN_COSYVOICE_WINDOWS.cmd manually.",
                pythonExecutable);
            return;
        }

        _ownsProcess = true;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _logger.LogInformation(
            "Started the local CosyVoice3 service on {BaseUrl} using {PythonExecutable}. Model loading continues in the child process.",
            _options.BaseUrl,
            pythonExecutable);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_ownsProcess || _process is null || _process.HasExited)
        {
            return Task.CompletedTask;
        }

        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not stop the CosyVoice service cleanly.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _process?.Dispose();
    }

    private async Task<CosyVoiceHealthSnapshot?> TryReadHealthAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(_options.BaseUrl),
                Timeout = Timeout.InfiniteTimeSpan,
            };

            using var response = await client.GetAsync("/health", timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CosyVoiceHealthSnapshot>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                timeout.Token);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return null;
        }
    }

    private CosyVoiceServiceExpectation BuildExpectedService()
    {
        var repoPath = ResolvePath(_options.CosyVoiceRepo);
        var modelPath = ResolvePath(_options.ModelPath);
        var referenceAudio = ResolvePath(_options.ReferenceAudioPath);
        var referenceText = ResolvePath(_options.ReferenceTextPath);

        var model = Path.GetFileName(
                modelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            ?? throw new InvalidOperationException("CosyVoice:ModelPath does not identify a model directory.");

        if (HasUsableCustomReference(referenceAudio, referenceText))
        {
            return new CosyVoiceServiceExpectation(
                model,
                modelPath,
                "custom-reference",
                referenceAudio,
                referenceText);
        }

        if (_options.UseOfficialDemoVoiceWhenReferenceMissing)
        {
            return new CosyVoiceServiceExpectation(
                model,
                modelPath,
                "official-demo-fallback",
                Path.Combine(repoPath, "asset", "zero_shot_prompt.wav"),
                ReferenceTextPath: null);
        }

        return new CosyVoiceServiceExpectation(
            model,
            modelPath,
            "custom-reference",
            referenceAudio,
            referenceText);
    }

    private static bool HasUsableCustomReference(string audioPath, string textPath)
    {
        if (!File.Exists(audioPath) || !File.Exists(textPath))
        {
            return false;
        }

        try
        {
            return !string.IsNullOrWhiteSpace(File.ReadAllText(textPath));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
    }

    private static async Task<bool> IsPortOpenAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int? SafeExitCode(Process? process)
    {
        try
        {
            return process?.HasExited == true ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }
}
