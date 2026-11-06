using Godot;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using GArray = Godot.Collections.Array;
using GDict = Godot.Collections.Dictionary;

public partial class SessionBridge
{
    // Only this immutable JSON string crosses the UI/worker boundary. It is
    // encoded into BinaryEvidence on disk; it is never a second world state.
    private string casebookJson = "{}";
    private string casebookViewer = "";
    private const int CasebookVersion = 1;
    private static readonly HashSet<string> CasebookEntryFields = new(StringComparer.Ordinal)
    {
        "id", "category", "title", "text", "source", "execution_event", "source_refs",
        "speaker", "speaker_name", "game_time", "place", "action_kind", "voice", "related",
        "acquired", "image", "ownership", "actions", "lines"
    };
    private static readonly HashSet<string> CasebookNoteFields = new(StringComparer.Ordinal)
    {
        "id", "category", "title", "text", "related", "personal", "game_time", "time", "place"
    };
    private static readonly HashSet<string> CasebookLineFields = new(StringComparer.Ordinal)
    {
        "line_id", "source_field", "emitted_line_index", "paragraph_index", "trace_id",
        "execution_event", "source_id", "source_refs", "text", "speaker", "speaker_name",
        "portrait", "portrait_path"
    };
    private static readonly HashSet<string> PreferenceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "settings", "preferences", "locales", "locale", "language", "ui_language",
        "game_settings", "locale_settings", "reduce_motion"
    };

    public void UpdateCasebook(GDict data)
    {
        try
        {
            var serialized = NormalizeCasebook(JsonSerializer.Serialize(JsonSafeDictionary(data)));
            casebookJson = casebookViewer.Length == 0 ? serialized : BindCasebookViewer(serialized, casebookViewer);
        }
        catch (Exception error) { EmitBookmarkError("update", "", error); }
    }

    // Parsing creates a fresh graph: callers cannot mutate the host cache.
    public GDict ReadCasebook() => Parse(casebookJson);

    public async void SaveBookmark(GDict data)
    {
        try
        {
            RequireBookmarkStart();
            var serialized = NormalizeCasebook(JsonSerializer.Serialize(JsonSafeDictionary(data)));
            casebookJson = casebookViewer.Length == 0 ? serialized : BindCasebookViewer(serialized, casebookViewer);
            var savePath = NewBookmarkPath("bookmark");
            maintenance = true;
            var operation = SaveBookmarkAfterAsync(work, savePath);
            work = operation;
            await operation;
        }
        catch (Exception error) { EmitBookmarkError("save", "", error); }
    }

    private async Task SaveBookmarkAfterAsync(Task previous, string path)
    {
        // Publish work/maintenance before a completed previous task can finish
        // this method synchronously or emit a reentrant UI signal.
        await Task.Yield();
        try
        {
            await previous;
            var current = RequireBookmarkBoundary();
            var journal = casebookJson;
            await Task.Run(() => WriteBookmarkPair(current, path, journal));
            Stamp("bookmark_saved", interaction, path);
            if (!closing) EmitSignal(SignalName.EventsReady, new GArray
            {
                new GDict { ["kind"] = "bookmark_saved", ["path"] = path }
            });
        }
        catch (Exception error) { EmitBookmarkError("save", path, error); }
        finally { maintenance = false; StartPump(); }
    }

    public async void LoadBookmark(string path, GDict currentData)
    {
        try
        {
            RequireBookmarkStart();
            var serialized = NormalizeCasebook(JsonSerializer.Serialize(JsonSafeDictionary(currentData)));
            casebookJson = casebookViewer.Length == 0 ? serialized : BindCasebookViewer(serialized, casebookViewer);
            var sourcePath = NormalizeBookmarkPath(path);
            var backupPath = NewBookmarkPath("before-load");
            maintenance = true;
            var operation = LoadBookmarkAfterAsync(work, sourcePath, backupPath);
            work = operation;
            await operation;
        }
        catch (Exception error) { EmitBookmarkError("load", path, error); }
    }

    private async Task LoadBookmarkAfterAsync(Task previous, string path, string backupPath)
    {
        await Task.Yield();
        var backupWritten = false;
        DvSession? candidate = null;
        string? candidateDirectory = null;
        var installed = false;
        try
        {
            await previous;
            var current = RequireBookmarkBoundary();
            var recordingDirectory = NewSessionEvidenceDirectory();
            candidateDirectory = recordingDirectory;
            var restored = await Task.Run(() =>
            {
                // Keep the exact source bytes stable while RestoreFile and the
                // sidecar hash validation read them (including on Windows).
                using var save = OpenBookmarkForRead(path);
                var restoredSession = DvSession.RestoreFile(path);
                candidate = restoredSession;
                StartSessionRecording(restoredSession, recordingDirectory);
                var snapshot = restoredSession.Snapshot(restoredSession.Viewer);
                RequireRestorableSnapshot(snapshot);
                var journal = ReadBoundCasebook(path, restoredSession.Viewer, save);
                return (Session: restoredSession, Snapshot: snapshot, Casebook: journal.Json, Warning: journal.Warning);
            });
            if (!ReferenceEquals(current, RequireBookmarkBoundary()))
                throw new InvalidOperationException("当前会话已改变，请重新打开书签。");
            // Materialize before making a backup or changing the live session.
            var snapshotData = Parse(restored.Snapshot);
            var journalData = Parse(restored.Casebook);
            var journal = casebookJson;
            await Task.Run(() => WriteBookmarkPair(current, backupPath, journal));
            backupWritten = true;
            if (!ReferenceEquals(current, RequireBookmarkBoundary()))
                throw new InvalidOperationException("当前会话已改变；已保留切换前备份。");
            var previousDirectory = evidenceDirectory ?? NewSessionEvidenceDirectory();
            var sealError = await Task.Run(() => SealSessionEvidence(current, previousDirectory, "bookmark-replaced", backupPath));
            InstallRestored((restored.Session, restored.Snapshot), snapshotData, recordingDirectory);
            installed = true;
            casebookJson = restored.Casebook;
            var warning = restored.Warning;
            if (sealError.Length > 0)
            {
                hostErrors.Add(new GDict { ["phase"] = "bookmark-previous-performance", ["directory"] = previousDirectory, ["error"] = sealError });
                warning += (warning.Length == 0 ? "" : "\n") + "切换前会话的统计封存未完整完成：" + sealError + "\n记录位置：" + previousDirectory;
            }
            Stamp("bookmark_loaded", interaction, path);
            if (!closing) EmitSignal(SignalName.EventsReady, new GArray
            {
                new GDict
                {
                    ["kind"] = "bookmark_loaded", ["path"] = path, ["backup_path"] = backupPath,
                    ["snapshot"] = snapshotData, ["casebook"] = journalData,
                    ["casebook_warning"] = warning
                }
            });
            if (!closing) BeginDeliveredDecision();
        }
        catch (Exception error)
        {
            var reportedError = error;
            if (candidate is not null && !installed && candidateDirectory is not null)
            {
                var abandoned = candidate;
                var abandonedDirectory = candidateDirectory;
                var sealError = await Task.Run(() => SealSessionEvidence(abandoned, abandonedDirectory, "bookmark-not-installed"));
                if (sealError.Length > 0)
                {
                    hostErrors.Add(new GDict { ["phase"] = "bookmark-candidate-performance", ["directory"] = abandonedDirectory, ["error"] = sealError });
                    reportedError = new AggregateException(error, new IOException(sealError));
                }
            }
            EmitBookmarkError("load", path, reportedError, backupWritten ? backupPath : "");
        }
        finally { maintenance = false; StartPump(); }
    }

    // Main-thread advisory gate; SaveBookmark still rechecks after acquiring maintenance.
    public bool IsBookmarkBoundaryReady() => !closing && !faulted && !maintenance && !busy
        && commands.Count == 0 && pendingEvent.Length == 0 && session is not null;

    private void RequireBookmarkStart()
    {
        if (closing) throw new InvalidOperationException("会话正在关闭，暂不能操作书签。");
        if (faulted) throw new InvalidOperationException("会话已因宿主错误停止，暂不能操作书签。");
        if (maintenance) throw new InvalidOperationException("另一项保存或载入仍在进行，请稍后重试。");
    }

    private DvSession RequireBookmarkBoundary()
    {
        if (closing || faulted || !maintenance)
            throw new InvalidOperationException("会话已关闭或停止，本次书签操作已取消。");
        if (busy || commands.Count != 0)
            throw new InvalidOperationException("仍有会话命令等待完成，请稍后重试。");
        RequireNoPendingExecution();
        return session ?? throw new InvalidOperationException("尚未打开游戏会话，不能保存或切换书签。");
    }

    private static string NormalizeBookmarkPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("未选择存档文件。");
        var result = Path.GetFullPath(ProjectSettings.GlobalizePath(path));
        if (!string.Equals(Path.GetExtension(result), ".dvsave", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择 .dvsave 游戏存档。");
        return result;
    }

    private static string NewBookmarkPath(string purpose)
    {
        var directory = ProjectSettings.GlobalizePath("user://saves");
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
        return Path.Combine(directory, $"{stamp}-{purpose}-{Guid.NewGuid():N}.dvsave");
    }

    private static FileStream OpenBookmarkForRead(string path) =>
        new(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);

    private static void RequireRestorableSnapshot(string snapshot)
    {
        using var document = JsonDocument.Parse(snapshot);
        if (document.RootElement.TryGetProperty("pending", out var pending) && pending.ValueKind == JsonValueKind.Object
            && pending.TryGetProperty("status", out var status) && status.GetString() == "prepared")
            throw new InvalidOperationException("该存档仍有等待现场回执的动作，不能恢复。");
    }

    private sealed class BookmarkSidecarException(string savePath, Exception inner)
        : IOException("游戏存档已经保存，但案卷记录未能写入；当前会话未切换。", inner)
    {
        internal string SavePath { get; } = savePath;
    }

    private static void WriteBookmarkPair(DvSession current, string path, string journal)
    {
        // Validate the UI owner before writing a world save. WriteSave itself
        // publishes a new .partial atomically and rejects existing destinations.
        var bound = BindCasebookViewer(journal, current.Viewer);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) || File.Exists(path + ".partial"))
            throw new IOException("书签文件已存在，不能覆盖：" + path);
        current.WriteSave(path);
        try { WriteBoundCasebook(path, current.Viewer, bound); }
        catch (Exception error) { throw new BookmarkSidecarException(path, error); }
    }

    private static string CasebookSidecarPath(string savePath) => Path.ChangeExtension(savePath, ".casebook.dvlog");

    private static void WriteBoundCasebook(string savePath, string viewer, string journal)
    {
        var bound = BindCasebookViewer(journal, viewer);
        using var save = OpenBookmarkForRead(savePath);
        var hash = Convert.ToHexString(SHA256.HashData(save));
        using var document = JsonDocument.Parse(bound);
        var envelope = JsonSerializer.Serialize(new
        {
            version = CasebookVersion, viewer, save_sha256 = hash, casebook = document.RootElement
        });
        var sidecar = CasebookSidecarPath(savePath);
        if (File.Exists(sidecar) || File.Exists(sidecar + ".partial"))
            throw new IOException("案卷记录已存在，不能覆盖：" + sidecar);
        DuelView.Compiled.BinaryEvidence.WriteJsonRecords(sidecar + ".partial", new[] { envelope });
        File.Move(sidecar + ".partial", sidecar, overwrite: false);
    }

    private static (string Json, string Warning) ReadBoundCasebook(string savePath, string viewer, FileStream save)
    {
        var sidecar = CasebookSidecarPath(savePath);
        if (!File.Exists(sidecar)) return ("{}", "");
        try
        {
            using var input = OpenBookmarkForRead(sidecar);
            DuelView.Compiled.BinaryEvidence.RequireHeader(input);
            var record = DuelView.Compiled.BinaryEvidence.ReadFrame(input, out var end);
            if (end) throw new InvalidDataException("案卷记录为空。");
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(record));
            var envelope = document.RootElement;
            if (envelope.ValueKind != JsonValueKind.Object || !envelope.TryGetProperty("version", out var version)
                || !version.TryGetInt32(out var number) || number != CasebookVersion)
                throw new InvalidDataException("案卷记录版本不受支持。");
            if (!envelope.TryGetProperty("viewer", out var identity) || identity.ValueKind != JsonValueKind.String
                || !string.Equals(identity.GetString(), viewer, StringComparison.Ordinal))
                throw new InvalidDataException("案卷记录不属于该存档的观察者。");
            save.Position = 0;
            var expected = Convert.ToHexString(SHA256.HashData(save));
            if (!envelope.TryGetProperty("save_sha256", out var hash) || hash.ValueKind != JsonValueKind.String
                || !string.Equals(hash.GetString(), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("案卷记录与该存档的内容校验不一致。");
            if (!envelope.TryGetProperty("casebook", out var journal) || journal.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("案卷记录缺少有效正文。");
            var normalized = BindCasebookViewer(NormalizeCasebook(journal.GetRawText()), viewer);
            DuelView.Compiled.BinaryEvidence.ReadFrame(input, out end);
            if (!end) throw new InvalidDataException("案卷侧车包含多余记录。");
            return (normalized, "");
        }
        catch (Exception error)
        {
            return ("{}", "游戏存档可以载入，但配套案卷未采用：" + error.Message);
        }
    }

    private void EmitBookmarkError(string operation, string path, Exception error, string backupPath = "")
    {
        var partial = error as BookmarkSidecarException;
        if (partial is not null)
        {
            if (operation == "load") backupPath = partial.SavePath;
            else path = partial.SavePath;
        }
        var message = operation == "update" ? "案卷记录未更新：" : operation == "load" ? "无法切换书签：" : "无法保存书签：";
        message += error.Message;
        if (backupPath.Length != 0) message += " 已保留当前游戏备份：" + backupPath;
        Stamp("bookmark_error", interaction, JsonSerializer.Serialize(new { operation, path, backup_path = backupPath, error = error.ToString() }));
        if (!closing) EmitSignal(SignalName.EventsReady, new GArray
        {
            new GDict
            {
                ["kind"] = "bookmark_error", ["operation"] = operation, ["path"] = path,
                ["message"] = message, ["backup_path"] = backupPath,
                ["save_written"] = partial is not null || backupPath.Length != 0,
                ["casebook_written"] = partial is null && backupPath.Length != 0
            }
        });
    }

    private static string BindCasebookViewer(string json, string viewer)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.EnumerateObject().Any())
            return JsonSerializer.Serialize(new { version = CasebookVersion, viewer, entries = Array.Empty<object>(), notes = Array.Empty<object>(), next_note = 1 });
        if (!root.TryGetProperty("viewer", out var identity) || identity.ValueKind != JsonValueKind.String
            || !string.Equals(identity.GetString(), viewer, StringComparison.Ordinal))
            throw new InvalidDataException("案卷与当前观察者不一致，未保存其他人的记录。");
        return json;
    }

    private static string NormalizeCasebook(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("案卷必须是纯数据字典。");
        if (!root.EnumerateObject().Any()) return "{}";
        if (!root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != CasebookVersion)
            throw new InvalidDataException("案卷数据版本不受支持。");
        if (!root.TryGetProperty("viewer", out var identity) || identity.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("案卷缺少观察者身份。");
        var entries = CopyCasebookRecords(root, "entries", CasebookEntryFields);
        var notes = CopyCasebookRecords(root, "notes", CasebookNoteFields);
        var nextNote = root.TryGetProperty("next_note", out var next) && next.TryGetInt64(out var value) ? Math.Max(1, value) : notes.Count + 1L;
        return JsonSerializer.Serialize(new { version = CasebookVersion, viewer = identity.GetString(), entries, notes, next_note = nextNote });
    }

    private static List<Dictionary<string, JsonElement>> CopyCasebookRecords(JsonElement root, string name, HashSet<string> allowed)
    {
        var result = new List<Dictionary<string, JsonElement>>();
        if (!root.TryGetProperty(name, out var values)) return result;
        if (values.ValueKind != JsonValueKind.Array) throw new InvalidDataException("案卷条目必须是数组。");
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("案卷条目必须是纯数据字典。");
            var record = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in item.EnumerateObject())
            {
                if (!allowed.Contains(property.Name)) continue;
                RequireNoPreferences(property.Value);
                if (property.Name == "lines")
                {
                    // Preserve the delivered line/source fields, never arbitrary
                    // runtime presentation nodes or private snapshot properties.
                    var wrapper = JsonSerializer.SerializeToElement(new { lines = property.Value });
                    record[property.Name] = JsonSerializer.SerializeToElement(CopyCasebookRecords(wrapper, "lines", CasebookLineFields));
                }
                else record[property.Name] = property.Value.Clone();
            }
            result.Add(record);
        }
        return result;
    }

    private static void RequireNoPreferences(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (PreferenceFields.Contains(property.Name)) throw new InvalidDataException("案卷不能包含界面设置或语言配置。");
                RequireNoPreferences(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RequireNoPreferences(item);
    }

    private static Dictionary<string, object?> JsonSafeDictionary(GDict value)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in value)
        {
            if (pair.Key.VariantType is not (Variant.Type.String or Variant.Type.StringName))
                throw new InvalidDataException("案卷字典只能使用文本键。");
            result[pair.Key.AsString()] = JsonSafeValue(pair.Value);
        }
        return result;
    }

    private static object? JsonSafeValue(Variant value) => value.VariantType switch
    {
        Variant.Type.Nil => null,
        Variant.Type.Bool => value.AsBool(),
        Variant.Type.Int => value.AsInt64(),
        Variant.Type.Float when double.IsFinite(value.AsDouble()) => value.AsDouble(),
        Variant.Type.String or Variant.Type.StringName => value.AsString(),
        Variant.Type.Dictionary => JsonSafeDictionary(value.AsGodotDictionary()),
        Variant.Type.Array => value.AsGodotArray().Select(JsonSafeValue).ToArray(),
        _ => throw new InvalidDataException("案卷只接受文本、数字、布尔值、数组和字典。")
    };
}
