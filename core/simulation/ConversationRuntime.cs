// @mechanism: causal_clock_and_campaign
// @layer: core/simulation
// @contracts: ConversationRuntime

using DuelView.Autonomy;
using DuelView.Compiled;
using DuelView.Core;
using DuelView.Dv;
using DuelView.Runtime;
using DuelView.Spatial;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System;
using static DuelView.Compiled.R;
using static DuelView.Core.DVKernel;
using static DuelView.Core.DVSimulationKernel;

// --- Source: ConversationSession.cs ---
namespace DuelView.Runtime
{


public sealed record ConversationLine(string Speaker, string Label, string Text, double Duration);
public sealed record ConversationView(long Id, string Initiator, string Target, string Phase,
    int LineIndex, ConversationLine? Line, double? LineEndsAt);
public sealed record ConversationState(long Id,string Initiator,string Target,string Phase,int LineIndex,double? LineEndsAt);

/// <summary>Conversation state is owned by the session; the host only reports
/// physical alignment and requests advancement after the authored deadline.</summary>
public sealed class ConversationSession
{
    private readonly ConversationLine[] lines;
    public long Id { get; }
    public string Initiator { get; }
    public string Target { get; }
    public string Phase { get; private set; } = "approaching";
    public int LineIndex { get; private set; } = -1;
    public double? LineEndsAt { get; private set; }

    public ConversationSession(long id, string initiator, string target, JsonElement declaration)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(initiator) || string.IsNullOrWhiteSpace(target) || initiator == target)
            throw new InvalidDataException("Conversation requires two distinct participants.");
        Id = id; Initiator = initiator; Target = target;
        lines = declaration.Get("lines").Items().Select(line => new ConversationLine(
            line.Get("speaker").Text(), line.Get("label").Text(), line.Get("text").Text(), line.Get("duration_seconds").Number(double.NaN))).ToArray();
        if (lines.Length == 0 || lines.Any(line => (line.Speaker != initiator && line.Speaker != target) ||
            string.IsNullOrWhiteSpace(line.Label) || string.IsNullOrWhiteSpace(line.Text) || !double.IsFinite(line.Duration) || line.Duration <= 0))
            throw new InvalidDataException("Invalid conversation lines.");
    }

    public ConversationView View => new(Id, Initiator, Target, Phase, LineIndex,
        LineIndex >= 0 && LineIndex < lines.Length ? lines[LineIndex] : null, LineEndsAt);
    public ConversationState SaveState()=>new(Id,Initiator,Target,Phase,LineIndex,LineEndsAt);
    public static ConversationSession RestoreState(ConversationState state,JsonElement declaration)
    {
        var result=new ConversationSession(state.Id,state.Initiator,state.Target,declaration);
        var valid=state.Phase switch {
            "approaching"=>state.LineIndex==-1 && state.LineEndsAt is null,
            "speaking"=>state.LineIndex>=0 && state.LineIndex<result.lines.Length && state.LineEndsAt is { } end && double.IsFinite(end) && end>=0,
            "finished"=>state.LineIndex==result.lines.Length && state.LineEndsAt is null,
            "interrupted"=>state.LineIndex>=-1 && state.LineIndex<result.lines.Length && state.LineEndsAt is null,
            _=>false
        };
        if(!valid)throw new InvalidDataException("Invalid conversation checkpoint.");
        result.Phase=state.Phase;result.LineIndex=state.LineIndex;result.LineEndsAt=state.LineEndsAt;return result;
    }

    public bool Aligned(double now)
    {
        if (Phase != "approaching") return false;
        Phase = "speaking"; LineIndex = 0; LineEndsAt = now + lines[0].Duration;
        return true;
    }

    public bool Advance(int expectedLine, double now)
    {
        if (Phase != "speaking" || expectedLine != LineIndex || now < LineEndsAt) return false;
        if (++LineIndex == lines.Length) { Phase = "finished"; LineEndsAt = null; }
        else LineEndsAt = now + lines[LineIndex].Duration;
        return true;
    }

    public bool Interrupt()
    {
        if (Phase is "finished" or "interrupted") return false;
        Phase = "interrupted"; LineEndsAt = null; return true;
    }
}
}

// --- Source: RuntimeNarrative.cs ---
namespace DuelView.Runtime
{


public sealed record ActionNarrative(string Kind,string Speaker,string SpeakerLabel,string Text,string[] Recipients,string EvidenceId="");

/// <summary>Records authored presentation at the successful action boundary.
/// Hearing an utterance records that it was said, not that its assertions are true.</summary>
public sealed partial class RuntimeSession
{
    private ActionNarrative[] CompleteNarrative(Decision decision)
    {
        var declaration=decision.Action.Declaration;
        var speaker=actors[decision.Actor];
        var lines=new List<ActionNarrative>();
        var privateText=declaration.Get("completion_text").Text();
        if(privateText.Length>0)lines.Add(new("reflection",speaker.Id,speaker.Declaration.Get("name").Text(speaker.Id),privateText,[speaker.Id]));
        foreach(var line in declaration.Get("completion_lines").Items())
        {
            Func<FieldKey,Fact?> read=World.Read;
            var viewpoint=line.Get("speaker").Text();
            if(viewpoint.Length>0)read=actors[viewpoint].Recall;
            if(line.Get("when").ValueKind==JsonValueKind.Object&&Condition.Compile(line.Get("when"),speaker.Id).Evaluate(read)!=Truth.True)continue;
            lines.Add(new("reflection",speaker.Id,speaker.Declaration.Get("name").Text(speaker.Id),line.Get("text").Text(),[speaker.Id]));
        }
        var publicText=declaration.Get("public_completion_text").Text();
        if(publicText.Length==0)return lines.ToArray();
        var room=World.Read(new(speaker.Id,"state","room"))?.Value.Text()??"";
        var audience=actors.Values.Where(actor=>actor.Id==speaker.Id||room.Length>0&&
            World.Read(new(actor.Id,"state","room"))?.Value.Text()==room&&
            World.Read(new(actor.Id,"phys","asleep")) is {Known:true,Conflicted:false} awake&&awake.Value.ValueKind==JsonValueKind.False&&!Incapacitated(actor.Id))
            .Select(actor=>actor.Id).ToArray();
        var hidden=speaker.Declaration.Get("hidden").Flag();
        var publicName=speaker.Declaration.Get("public_name").Text();
        var label=publicName.Length>0?publicName:hidden?"看不见的说话者":speaker.Declaration.Get("name").Text("在场者");
        var identity=hidden&&publicName.Length==0?"":speaker.Id;
        var evidenceId="utterance/"+(++nextEvidence);
        var key=new FieldKey("utterance:"+decision.Execution,"doc","text");
        var value=JsonSerializer.SerializeToElement(publicText);
        lines.Add(new("speech",identity,label,publicText,audience,evidenceId));
        foreach(var recipient in audience)
        {
            var source=JsonSerializer.SerializeToElement(new {actor=recipient==speaker.Id?speaker.Id:identity,speaker_label=label,recipient,
                action=decision.Action.Id,execution=decision.Execution,method="spoken",channel="hearing",heard_in=room,scope="same_room",utterance_id=evidenceId});
            actors[recipient].ReceiveDirected(new(evidenceId,key,value,1,ContentTime,InformationOrigin.Communication,source));
        }
        return lines.ToArray();
    }
}
}
