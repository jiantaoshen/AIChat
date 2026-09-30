// This file starts and stops the local CosyVoice3 Python service from the project-local venv so the TTS model stays loaded between avatar replies.
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
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

        if (await IsPortOpenAsync(_options.Host, _options.Port, cancellationToken))
        {
            _logger.LogInformation(
                "A CosyVoice service is already listening on {Host}:{Port}; the backend will reuse it.",
                _options.Host,
                _options.Port);
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
