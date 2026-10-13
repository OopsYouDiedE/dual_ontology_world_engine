using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace DuelView.VisualStudio;

public static class DvContent
{
    [Export, Name("DuelView"), BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    public static ContentTypeDefinition? ContentType;

    [Export, FileExtension(".dv"), ContentType("DuelView")]
    public static FileExtensionToContentTypeDefinition? Extension;
}

[Export(typeof(ILanguageClient)), ContentType("DuelView")]
public sealed class DvLanguageClient : ILanguageClient
{
    private Process? server;
    public string Name => "DuelView compiler checker";
    public IEnumerable<string> ConfigurationSections => Array.Empty<string>();
    public object InitializationOptions => new { };
    public IEnumerable<string> FilesToWatch => Array.Empty<string>();
    public bool ShowNotificationOnInitializeFailed => true;
    public event AsyncEventHandler<EventArgs>? StartAsync;
    public event AsyncEventHandler<EventArgs>? StopAsync { add { } remove { } }

    public Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string python = Environment.GetEnvironmentVariable("DV_PYTHON") ?? "python";
        string script = Path.Combine(directory, "server", "dv_lsp.py");
        var start = new ProcessStartInfo(python, Quote(script))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory
        };
        start.EnvironmentVariables["PYTHONUTF8"] = "1";
        server = Process.Start(start) ?? throw new InvalidOperationException("Unable to start DV language server");
        server.ErrorDataReceived += (_, e) => { if (e.Data != null) Trace.WriteLine("DV: " + e.Data); };
        server.BeginErrorReadLine();
        return Task.FromResult<Connection?>(new Connection(server.StandardOutput.BaseStream, server.StandardInput.BaseStream));
    }

    private static string Quote(string value) => "\"" + value.Replace("\\\"", "\\\\\\\"").Replace("\"", "\\\"") + "\"";

    public async Task OnLoadedAsync()
    {
        if (StartAsync != null)
            foreach (AsyncEventHandler<EventArgs> callback in StartAsync.GetInvocationList())
                await callback(this, EventArgs.Empty).ConfigureAwait(false);
    }

    public Task OnServerInitializedAsync() => Task.CompletedTask;
    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo info)
        => Task.FromResult<InitializationFailureContext?>(new InitializationFailureContext());
}
