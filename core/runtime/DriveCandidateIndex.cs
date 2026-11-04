namespace DuelView.Runtime;

/// <summary>Compiled once from the DV catalog. Runtime selection visits a fixed
/// number of drive buckets and catalog entries, with persistent fair cursors.</summary>
internal sealed class DriveCandidateIndex
{
    private readonly ActionCandidate[] actions;
    private readonly Dictionary<string,ActionCandidate[]> byDrive;
    private readonly Dictionary<string,int> cursors=[];
    private int fallbackCursor;

    internal DriveCandidateIndex(IEnumerable<ActionCandidate> catalog,DriveCalculator drives)
    {
        actions=catalog.Where(action=>!action.Declaration.Get("manual_only").Flag()).ToArray();
        byDrive=actions.SelectMany(action=>drives.AffectedDrives(action).Select(need=>(need,action)))
            .GroupBy(item=>item.need,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.Select(item=>item.action).ToArray(),StringComparer.Ordinal);
    }

    internal ActionCandidate[] Select(DriveCalculator drives,double now,int limit,int driveLimit,int perDrive)
    {
        var result=new List<ActionCandidate>(limit);
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var drive in drives.Priorities(now).Take(driveLimit))
        {
            if(result.Count==limit)break;
            if(!byDrive.TryGetValue(drive,out var bucket))continue;
            var cursor=cursors.GetValueOrDefault(drive);
            for(var n=0;n<Math.Min(perDrive,bucket.Length)&&result.Count<limit;n++)
            {
                var action=bucket[cursor];cursor=(cursor+1)%bucket.Length;
                if(seen.Add(action.Id))result.Add(action);
            }
            cursors[drive]=cursor;
        }
        for(var n=0;n<Math.Min(limit,actions.Length)&&result.Count<limit;n++)
        {
            var action=actions[fallbackCursor];fallbackCursor=(fallbackCursor+1)%actions.Length;
            if(seen.Add(action.Id))result.Add(action);
        }
        return result.ToArray();
    }
}
