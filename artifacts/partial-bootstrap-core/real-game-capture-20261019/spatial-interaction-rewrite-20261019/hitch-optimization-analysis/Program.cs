using Microsoft.Diagnostics.Tracing;
using System.Text.Json;
var pauses = new List<object>();
var starts = new Dictionary<int, (double Time, string Reason)>();
var collections = new List<object>();
using var source = new EventPipeEventSource(args[0]);
source.Clr.GCSuspendEEStart += d => starts[d.ProcessID] = (d.TimeStampRelativeMSec, d.Reason.ToString());
source.Clr.GCRestartEEStop += d => {
 if(starts.Remove(d.ProcessID, out var start)) pauses.Add(new {begin_trace_ms=start.Time,end_trace_ms=d.TimeStampRelativeMSec,duration_ms=d.TimeStampRelativeMSec-start.Time,reason=start.Reason});
};
source.Clr.GCStart += d => collections.Add(new {trace_ms=d.TimeStampRelativeMSec,depth=d.Depth,reason=d.Reason.ToString(),type=d.Type.ToString()});
source.Process();
Console.WriteLine(JsonSerializer.Serialize(new {trace_session_start=source.SessionStartTime.ToString("O"),pauses,collections}));
