using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace V00.PublicPlayer;

// Serialized data only. No callbacks, object references, root session, or observer path.
// Clearing env/cwd is hygiene; same-user filesystem/network rights remain unrestricted.
public sealed class ControllerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ObserverLog _observer;
    private readonly Task<byte[]> _stderr;
    private bool _closed;
    private readonly int _timeoutMs;

    public ControllerProcess(string python, string script, string workingDirectory,
        ObserverLog observer, string mode = "neutral", int timeoutMs = 2500)
    {
        _observer = observer;
        _timeoutMs = timeoutMs;
        var start = new ProcessStartInfo(python) {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workingDirectory
        };
        foreach (var argument in new[] { "-I", "-S", "-u", script, "--mode", mode }) start.ArgumentList.Add(argument);
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        start.Environment.Clear();
        if (systemRoot is not null) start.Environment["SystemRoot"] = systemRoot;
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        // -I ignores PYTHON* environment; explicit UTF-8 mode is therefore necessary.
        start.ArgumentList.Insert(0, "-X");
        start.ArgumentList.Insert(1, "utf8");
        _process = Process.Start(start) ?? throw new InvalidOperationException("Controller did not start.");
        _stderr = CaptureStderrAsync();
        _observer.Record("controller_started", new { pid = _process.Id, mode, timeoutMs,
            process_rights = "DETECTION_ONLY: inherited same-user rights; no OS sandbox" });
    }

    private async Task<byte[]> CaptureStderrAsync()
    {
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int length;
        while ((length = await _process.StandardError.BaseStream.ReadAsync(buffer)) != 0)
        {
            output.Write(buffer, 0, length);
            if (output.Length > 65536)
            {
                Kill();
                _observer.Bytes("controller_stderr", output.ToArray(), "terminated_at_stderr_limit");
                return [];
            }
        }
        return output.ToArray();
    }

    private void Kill()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    public async Task<string> DecideAsync(PublicObservation observation)
    {
        if (_closed) throw new BoundaryRejected("controller_closed");
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(observation, CandidateBoundary.WireJson) + "\n");
        _observer.Bytes("host_to_controller", bytes);
        using var timeout = new CancellationTokenSource(_timeoutMs);
        using var output = new MemoryStream();
        try
        {
            await _process.StandardInput.BaseStream.WriteAsync(bytes, timeout.Token);
            await _process.StandardInput.BaseStream.FlushAsync(timeout.Token);
            // Bounded, byte-preserving read; do not ReadLine an unbounded stream.
            var one = new byte[1];
            while (true)
            {
                var length = await _process.StandardOutput.BaseStream.ReadAsync(one, timeout.Token);
                if (length == 0) throw new BoundaryRejected("controller_eof");
                output.WriteByte(one[0]);
                if (output.Length > 16384) throw new BoundaryRejected("controller_output_limit");
                if (one[0] == '\n') break;
            }
            _observer.Bytes("controller_to_host", output.ToArray());
            return new UTF8Encoding(false, true).GetString(output.ToArray()).TrimEnd('\r', '\n');
        }
        catch (OperationCanceledException)
        {
            _closed = true; Kill();
            _observer.Bytes("controller_to_host", output.ToArray(), "terminated_at_timeout");
            _observer.Record("transport_rejected", new { code = "controller_timeout" });
            throw new BoundaryRejected("controller_timeout");
        }
        catch (Exception error) when (error is BoundaryRejected or IOException or DecoderFallbackException)
        {
            _closed = true; Kill();
            _observer.Bytes("controller_to_host", output.ToArray(), "terminated_at_transport_failure");
            _observer.Record("transport_rejected", new { code = error is BoundaryRejected rejected ? rejected.Code : "controller_transport_error" });
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _closed = true;
        _process.StandardInput.Close();
        try
        {
            using var timeout = new CancellationTokenSource(500);
            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) { Kill(); await _process.WaitForExitAsync(); }
        var stderr = await _stderr;
        if (stderr.Length != 0) _observer.Bytes("controller_stderr", stderr);
        // Capture all emitted surplus bytes AFTER the process terminates, so
        // extra stdout is evidence, never another silently accepted action.
        using var remainder = new MemoryStream();
        await _process.StandardOutput.BaseStream.CopyToAsync(remainder);
        if (remainder.Length != 0) _observer.Bytes("controller_stdout_surplus", remainder.ToArray(), "not_accepted");
        _observer.Record("controller_ended", new { exit_code = _process.ExitCode });
        _process.Dispose();
    }
}
