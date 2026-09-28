using Godot;
using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

/// <summary>Godot-facing projection of the engine-independent hall replay.</summary>
public partial class HallReplayBridge : Node
{
    // The Core deliberately keeps its Chinese rule descriptions. Translate only
    // when projecting player-visible text into the Godot scene.
    private static readonly Lazy<(string Source, string Id)[]> ReplayCatalog = new(() =>
    {
        var entries = new List<(string Source, string Id)>();
        using var file = Godot.FileAccess.Open("res://scenes/FirstAwakening/i18n/replay_ui.csv",
            Godot.FileAccess.ModeFlags.Read);
        if (file is null) return Array.Empty<(string Source, string Id)>();
        file.GetCsvLine(); // stable ID, zh_CN, en, ja
        while (!file.EofReached())
        {
            var row = file.GetCsvLine();
            if (row.Length >= 2 && !string.IsNullOrEmpty(row[0]) && !string.IsNullOrEmpty(row[1]))
                entries.Add((row[1], row[0]));
        }
        return entries.OrderByDescending(entry => entry.Source.Length).ToArray();
    });

    private static string Visible(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        var catalog = ReplayCatalog.Value;
        var exact = catalog.FirstOrDefault(entry => entry.Source == source);
        if (exact.Source is not null) return Localized(exact.Id, source);
        var output = new System.Text.StringBuilder();
        for (int i = 0; i < source.Length;)
        {
            var match = catalog.FirstOrDefault(entry => source.AsSpan(i).StartsWith(entry.Source.AsSpan(), StringComparison.Ordinal));
            if (match.Source is null) { output.Append(source[i++]); continue; }
            output.Append(Localized(match.Id, match.Source));
            i += match.Source.Length;
        }
        return output.ToString();
    }

    private static string Localized(string id, string fallback)
    {
        string result = TranslationServer.Translate(id);
        return result == id ? fallback : result;
    }

    public Godot.Collections.Dictionary Compile(Godot.Collections.Array<string> steps)
    {
        var program = HallReplayProgram.Compile(steps.ToArray());
        if (program.Error is not null)
            return new Godot.Collections.Dictionary { ["error"] = Visible(program.Error) };
        var actions = new Godot.Collections.Array<string>();
        var events = new Godot.Collections.Array<string>();
        var states = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var prefix = new List<HallCausalityRules.Action>();
        var previous = HallCausalityRules.Simulate(prefix);
        states.Add(Project(previous, "", true, "推演开始前。"));
        foreach (var action in program.Actions) actions.Add(action.ToString());
        for (int i = 0; i < program.Events.Count; i++)
        {
            string code = program.EventActions[i];
            string caption = program.Events[i];
            var result = previous;
            bool succeeded = true;
            if (HallReplayProgram.TryAction(code, out var action))
            {
                prefix.Add(action);
                result = HallCausalityRules.Simulate(prefix);
                succeeded = result.Failures.Count == previous.Failures.Count;
                if (!succeeded) caption = result.Failures[^1];
                else if (result.Traces.Count > previous.Traces.Count)
                    caption = string.Join("", result.Traces.Skip(previous.Traces.Count).Select(trace => trace.Description));
            }
            events.Add(Visible(caption));
            states.Add(Project(result, code, succeeded, caption));
            previous = result;
        }
        return new Godot.Collections.Dictionary { ["actions"] = actions, ["events"] = events, ["states"] = states };
    }

    // Manuscript tokens are converted at the game boundary. Scene targets and actor
    // references stay here; only physical hall actions are submitted to Core.
    private enum Verb
    {
        GoTo, Inspect, Wait, Talk, Convene, Connect, Disconnect,
        PushDoor, PullDoor, BraceDoorWithWedge, RemoveWedge, WrapWedge,
        UnwrapWedge, CloseDoor, SlideInnerBolt, WithdrawInnerBolt,
        OuterBar, LiftOuterBar, MoveStop, ReturnStop, WipeFloor,
        OpenValve, CloseValve, WipeWindow, Compare
    }

    private sealed record Target(string Key, string Title, string Description, string KnownDialogue);
    private sealed class ReplayActor(string name, Target position, bool outside = false)
    {
        public string Name { get; } = name;
        public Target Position { get; set; } = position;
        public bool Outside { get; set; } = outside;
    }
    private enum Condition
    {
        Smoke, DoorOpen, DoorClosed, InnerBoltSlid, InnerBoltWithdrawn,
        OuterBarDropped, OuterBarLifted, WedgeBraced, WedgeRemoved,
        WedgeWrapped, StopMoved, FloorWiped, WaterRunning
    }
    private sealed record Command(ReplayActor Actor, Verb Verb, Target Target, Condition? Condition);

    public Godot.Collections.Dictionary CompileDraft(Godot.Collections.Array<string> steps,
        Godot.Collections.Dictionary sites)
        => CompileDraftInternal(steps, sites, false);

    public Godot.Collections.Dictionary CompileActorDrafts(Godot.Collections.Dictionary actorSteps,
        Godot.Collections.Dictionary sites)
    {
        var steps = new Godot.Collections.Array<string>();
        foreach (string name in new[] { "侦探", "工程师", "厨师", "音乐家", "门外的人" })
        {
            if (!actorSteps.ContainsKey(name)) continue;
            var personSteps = actorSteps[name].AsGodotArray<string>();
            for (int index = 0; index < personSteps.Count; index++)
            {
                string code = personSteps[index];
                var parts = code.Split('|');
                if (parts.Length != 4 || parts[1] != name)
                    return new Godot.Collections.Dictionary { ["error"] = Visible(name + "的草稿只能规定本人观察和行动。") };
                if (parts[0] == "WHEN" && (index + 1 >= personSteps.Count ||
                    !personSteps[index + 1].StartsWith("ACT|" + name + "|", StringComparison.Ordinal)))
                    return new Godot.Collections.Dictionary { ["error"] = Visible(name + "的观察条件必须接本人行动。") };
                steps.Add(code);
            }
        }
        return CompileDraftInternal(steps, sites, true);
    }

    private Godot.Collections.Dictionary CompileDraftInternal(Godot.Collections.Array<string> steps,
        Godot.Collections.Dictionary sites, bool independent)
    {
        var targets = new Dictionary<string, Target>(StringComparer.Ordinal);
        foreach (var key in sites.Keys)
        {
            string name = key.AsString();
            var data = sites[key].AsGodotDictionary();
            targets[name] = new Target(name, Visible(data.ContainsKey("title") ? data["title"].AsString() : name),
                Visible(data.ContainsKey("description") ? data["description"].AsString() : ""),
                Visible(data.ContainsKey("known_dialogue") ? data["known_dialogue"].AsString() : ""));
        }
        // Public vocabulary aliases describe parts of existing objects, never extra world entities.
        void Add(string key, string title, string source)
        {
            if (!targets.ContainsKey(key)) targets[key] = new Target(key, Visible(title),
                targets.TryGetValue(source, out var found) ? found.Description : "", "");
        }
        Add("outside", "主楼侧门外", "");
        Add("wedge", "木楔", "paper");
        Add("inner_bolt", "生活翼内销", "door");
        Add("outer_bar", "主楼侧横闩", "slit");
        Add("floor", "门厅地面", "floor_wipe");
        Add("cloth", "湿布", "wash");
        Target Start(string key) => targets.TryGetValue(key, out var target) ? target : targets["outside"];
        var actors = new Dictionary<string, ReplayActor>(StringComparer.Ordinal)
        {
            ["侦探"] = new("侦探", Start("notebook")),
            ["工程师"] = new("工程师", Start("study")),
            ["厨师"] = new("厨师", Start("water")),
            ["音乐家"] = new("音乐家", Start("door")),
            ["门外的人"] = new("门外的人", targets["outside"], true)
        };
        var commands = new List<Command>();
        for (int i = 0; i < steps.Count; i++)
        {
            var parts = steps[i].Split('|');
            if (parts.Length != 4 || parts[0] is not ("ACT" or "WHEN") ||
                !actors.TryGetValue(parts[1], out var actor) || !targets.TryGetValue(parts[3], out var target))
                return new Godot.Collections.Dictionary { ["error"] = Visible($"第{i + 1}步包含未公布的人物、对象或句式。") };
            bool when = parts[0] == "WHEN";
            Verb verb = Verb.Wait;
            Condition condition = Condition.Smoke;
            if (when ? !Enum.TryParse(parts[2], out condition) || !Enum.IsDefined(condition) || condition.ToString() != parts[2] :
                !Enum.TryParse(parts[2], out verb) || !Enum.IsDefined(verb) || verb.ToString() != parts[2])
                return new Godot.Collections.Dictionary { ["error"] = Visible($"第{i + 1}步包含未公布的行动或条件。") };
            commands.Add(new(actor, verb, target, when ? condition : null));
        }
        var physical = new List<HallCausalityRules.Action>();
        var result = HallCausalityRules.Simulate(physical);
        var events = new Godot.Collections.Array<string>();
        var states = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var executed = new Godot.Collections.Array<string>();
        var blocked = new Godot.Collections.Array<string>();
        var observations = new Godot.Collections.Dictionary();
        var cleanWindows = new HashSet<string>();
        bool valveOpen = false, connected = false, skipNext = false;
        ReplayActor? holdingDoor = null;
        ReplayActor? holdingConnection = null;
        string SiteKey(string key) => key switch
        {
            "wedge" => "paper", "inner_bolt" => "door", "outer_bar" => "outside",
            "floor" => "floor_wipe", "cloth" => "wash", _ => key
        };
        Godot.Collections.Dictionary Snapshot(string action, ReplayActor? actor, Target? target, bool success, string caption)
        {
            var snapshot = Project(result, action, success, caption);
            var positions = new Godot.Collections.Dictionary();
            var sides = new Godot.Collections.Dictionary();
            foreach (var person in actors.Values) { positions[person.Name] = SiteKey(person.Position.Key); sides[person.Name] = person.Outside ? "outside" : "inside"; }
            snapshot["positions"] = positions; snapshot["sides"] = sides;
            snapshot["actor"] = actor?.Name ?? ""; snapshot["target"] = target?.Key ?? "";
            snapshot["target_site"] = target is null ? "" : SiteKey(target.Key);
            snapshot["valve_open"] = valveOpen; snapshot["transmitter_connected"] = connected;
            snapshot["clean_windows"] = new Godot.Collections.Array<string>(cleanWindows);
            var visibleObservations = new Godot.Collections.Dictionary();
            foreach (var observation in observations.Keys)
                visibleObservations[observation] = Visible(observations[observation].AsString());
            snapshot["observations"] = visibleObservations;
            snapshot["failures"] = new Godot.Collections.Array<string>(blocked);
            return snapshot;
        }
        void Emit(string action, ReplayActor? actor, Target? target, bool success, string caption)
        {
            if (!success)
            {
                caption += actor is null ? "" : "（仍在" + actor.Position.Title + "旁。）";
                blocked.Add(Visible(caption));
            }
            events.Add(Visible(caption));
            states.Add(Snapshot(action, actor, target, success, caption));
        }
        bool Apply(HallCausalityRules.Action action, out string caption)
        {
            var previous = result;
            physical.Add(action);
            result = HallCausalityRules.Simulate(physical);
            executed.Add(action.ToString());
            bool success = previous.Failures.Count == result.Failures.Count;
            caption = success ? string.Join("", result.Traces.Skip(previous.Traces.Count).Select(trace => trace.Description)) : result.Failures[^1];
            return success;
        }
        bool NearDoor(ReplayActor actor) => actor.Position.Key is "door" or "slit" or "hinge" or "weight" or "trace" or "stop" or "floor_wipe" or "floor" or "inner_bolt" or "outer_bar" or "outside";
        bool ConditionMet(Condition condition) => condition switch
        {
            Condition.Smoke => result.SmokeReachedDoor || result.DoorIsBraced,
            Condition.DoorOpen => !result.DoorClosed,
            Condition.DoorClosed => result.DoorClosed,
            Condition.InnerBoltSlid => result.InnerBoltSlid,
            Condition.InnerBoltWithdrawn => !result.InnerBoltSlid,
            Condition.OuterBarDropped => result.OuterBarDropped,
            Condition.OuterBarLifted => !result.OuterBarDropped,
            Condition.WedgeBraced => result.DoorIsBraced,
            Condition.WedgeRemoved => result.WedgeWasRemoved,
            Condition.WedgeWrapped => result.WedgeWrapped,
            Condition.StopMoved => result.StopMoved,
            Condition.FloorWiped => result.FloorWiped,
            Condition.WaterRunning => valveOpen,
            _ => false
        };
        bool CanCross() => !result.DoorClosed && !result.InnerBoltSlid && !result.OuterBarDropped;
        bool AtDoorHandle(Target target) => target.Key is "door" or "slit" or "inner_bolt" or "outer_bar" or "outside" or "trace" or "hinge" or "weight";
        void ReleaseForDestination(ReplayActor actor, Target destination, bool crossesDoor = false)
        {
            if (holdingDoor == actor && (crossesDoor || !AtDoorHandle(destination)))
            {
                Apply(HallCausalityRules.Action.CloseDoor, out string closure);
                holdingDoor = null;
                Emit("CloseDoor", actor, Start("door"), true, actor.Name + "离开门边，松开门扇。" + closure);
            }
            if (holdingConnection == actor && destination.Key != "transmitter")
            {
                connected = false; holdingConnection = null;
                Emit("Disconnect", actor, Start("transmitter"), true, actor.Name + "离开传声口，松开拉柄，回路断开。");
            }
        }
        states.Add(Snapshot("", null, null, true, "推演开始前。"));
        var actorOrder = new[] { "侦探", "工程师", "厨师", "音乐家", "门外的人" };
        var actorQueues = independent
            ? actorOrder.ToDictionary(name => name, name => new Queue<Command>(commands.Where(command => command.Actor.Name == name)))
            : null;
        int legacyIndex = 0, turn = 0;
        string? forcedActor = null;
        Command? NextCommand()
        {
            if (!independent) return legacyIndex < commands.Count ? commands[legacyIndex++] : null;
            if (forcedActor is not null)
            {
                var next = actorQueues![forcedActor].Dequeue();
                forcedActor = null;
                turn = (Array.IndexOf(actorOrder, next.Actor.Name) + 1) % actorOrder.Length;
                return next;
            }
            for (int offset = 0; offset < actorOrder.Length; offset++)
            {
                int index = (turn + offset) % actorOrder.Length;
                var queue = actorQueues![actorOrder[index]];
                if (queue.Count == 0) continue;
                var head = queue.Peek();
                if (head.Condition is { } condition && !ConditionMet(condition)) continue;
                queue.Dequeue();
                if (head.Condition.HasValue && queue.Count > 0) forcedActor = head.Actor.Name;
                else turn = (index + 1) % actorOrder.Length;
                return head;
            }
            // An unresolved condition remains visible as a failed observation, rather than silently passing.
            for (int offset = 0; offset < actorOrder.Length; offset++)
            {
                int index = (turn + offset) % actorOrder.Length;
                var queue = actorQueues![actorOrder[index]];
                if (queue.Count == 0) continue;
                var head = queue.Dequeue();
                if (head.Condition.HasValue && queue.Count > 0) forcedActor = head.Actor.Name;
                else turn = (index + 1) % actorOrder.Length;
                return head;
            }
            return null;
        }
        while (NextCommand() is { } command)
        {
            var actor = command.Actor;
            var target = command.Target;
            string key = target.Key;
            ReplayActor? addressed = key switch
            {
                "engineer" => actors["工程师"], "chef" => actors["厨师"],
                "musician" => actors["音乐家"], _ => null
            };
            // The manuscript's generic wording is resolved against the chosen object.
            var verb = command.Verb switch
            {
                Verb.RemoveWedge when key == "inner_bolt" => Verb.WithdrawInnerBolt,
                Verb.WipeFloor when key.StartsWith("window_", StringComparison.Ordinal) => Verb.WipeWindow,
                Verb.CloseDoor when key == "water" => Verb.CloseValve,
                _ => command.Verb
            };
            string actionName = command.Condition == Condition.Smoke ? "WaitSmoke" : command.Condition.HasValue ? "ObserveCondition" : verb.ToString();
            void Fail(string text) => Emit(actionName, actor, target, false, actor.Name + "：" + text);
            if (command.Condition is { } condition)
            {
                bool near = actor.Position == target || SiteKey(actor.Position.Key) == SiteKey(key);
                bool targetFits = condition switch
                {
                    Condition.Smoke or Condition.DoorOpen or Condition.DoorClosed => key is "door" or "outside",
                    Condition.InnerBoltSlid or Condition.InnerBoltWithdrawn => key is "door" or "inner_bolt",
                    Condition.OuterBarDropped or Condition.OuterBarLifted => key is "slit" or "outer_bar",
                    Condition.WedgeBraced => key is "door" or "wedge" or "paper",
                    Condition.WedgeRemoved or Condition.WedgeWrapped => key is "wedge" or "paper",
                    Condition.StopMoved => key == "stop",
                    Condition.FloorWiped => key is "floor" or "floor_wipe",
                    Condition.WaterRunning => key == "water",
                    _ => false
                };
                bool visible = condition switch
                {
                    Condition.Smoke or Condition.DoorOpen or Condition.DoorClosed => NearDoor(actor),
                    Condition.InnerBoltSlid or Condition.InnerBoltWithdrawn or Condition.WedgeBraced or Condition.StopMoved or Condition.FloorWiped => !actor.Outside && NearDoor(actor),
                    Condition.OuterBarDropped or Condition.OuterBarLifted => actor.Outside
                        ? NearDoor(actor) : actor.Position.Key == "slit",
                    Condition.WedgeRemoved => !actor.Outside && (NearDoor(actor) || near),
                    Condition.WedgeWrapped => !actor.Outside && near,
                    Condition.WaterRunning => !actor.Outside && (near || actor.Position.Key is "wash" or "cloth"),
                    _ => false
                };
                if (!targetFits || !visible)
                {
                    Fail("尚未在可观察的位置看到这个对象的条件；该行后续行动不会发生。"); skipNext = true; continue;
                }
                if (condition == Condition.Smoke)
                {
                    if (independent && !result.SmokeReachedDoor && !result.DoorIsBraced)
                    {
                        Fail("门尚未撑开，此时无法从门缝观察到热烟；后续行动没有发生。");
                        skipNext = true;
                        continue;
                    }
                    Emit(actionName, actor, target, true, actor.Name + (result.SmokeReachedDoor ? "已在门边看见门缝热烟。" : "留在门边等候门缝出现热烟。"));
                    if (!result.SmokeReachedDoor)
                    {
                        Apply(HallCausalityRules.Action.SmokeArrives, out string smokeCaption);
                        Emit("SmokeArrives", null, target, true, "门缝涌入灰黑热烟。" + smokeCaption);
                    }
                    continue;
                }
                bool met = condition switch
                {
                    Condition.DoorOpen => !result.DoorClosed,
                    Condition.DoorClosed => result.DoorClosed,
                    Condition.InnerBoltSlid => result.InnerBoltSlid,
                    Condition.InnerBoltWithdrawn => !result.InnerBoltSlid,
                    Condition.OuterBarDropped => result.OuterBarDropped,
                    Condition.OuterBarLifted => !result.OuterBarDropped,
                    Condition.WedgeBraced => result.DoorIsBraced,
                    Condition.WedgeRemoved => result.WedgeWasRemoved,
                    Condition.WedgeWrapped => result.WedgeWrapped,
                    Condition.StopMoved => result.StopMoved,
                    Condition.FloorWiped => result.FloorWiped,
                    Condition.WaterRunning => valveOpen,
                    _ => false
                };
                string expected = condition switch
                {
                    Condition.DoorOpen => "门扇开启", Condition.DoorClosed => "门扇合拢",
                    Condition.InnerBoltSlid => "内销插入", Condition.InnerBoltWithdrawn => "内销拔出",
                    Condition.OuterBarDropped => "外闩落座", Condition.OuterBarLifted => "外闩抬起",
                    Condition.WedgeBraced => "木楔撑门", Condition.WedgeRemoved => "木楔撤下",
                    Condition.WedgeWrapped => "木楔包起", Condition.StopMoved => "止挡座移开",
                    Condition.FloorWiped => "地面已擦拭", Condition.WaterRunning => "水阀流水", _ => "所写条件"
                };
                if (met) Emit(actionName, actor, target, true, actor.Name + "观察到" + expected + "，条件成立。");
                else { Fail("未观察到“" + expected + "”，该行条件未满足。"); skipNext = true; }
                continue;
            }
            if (skipNext) { Fail("前一观察条件未满足，这个行动没有发生。"); skipNext = false; continue; }
            // A held door closes when its operator leaves it; crossing is resolved before release.
            bool crossing = verb == Verb.GoTo && actor.Outside != (addressed?.Outside ?? key is "outside" or "outer_bar");
            if (holdingDoor == actor && verb is not (Verb.GoTo or Verb.Wait or Verb.Inspect or Verb.PushDoor or Verb.PullDoor or Verb.BraceDoorWithWedge or Verb.CloseDoor) && !crossing)
            {
                Apply(HallCausalityRules.Action.CloseDoor, out string closure);
                holdingDoor = null;
                Emit("CloseDoor", actor, Start("door"), true, actor.Name + "松开门扇。" + closure);
            }
            if (holdingConnection == actor && verb is not (Verb.GoTo or Verb.Inspect or Verb.Connect or Verb.Disconnect or Verb.Talk or Verb.Wait))
            {
                connected = false; holdingConnection = null;
                Emit("Disconnect", actor, Start("transmitter"), true, actor.Name + "松开拉柄，传声回路断开。");
            }
            if (verb == Verb.GoTo)
            {
                bool wantsOutside = addressed?.Outside ?? key is "outside" or "outer_bar";
                if (actor.Outside != wantsOutside && !CanCross()) { Fail("隔离门尚未打开，不能穿过门扇抵达另一侧。"); continue; }
                var moveDestination = addressed?.Position ?? target;
                if (!crossing) ReleaseForDestination(actor, moveDestination);
                actor.Outside = wantsOutside; actor.Position = moveDestination;
                Emit(actionName, actor, target, true, actor.Name + "走到" + target.Title + "旁。");
                if (crossing) ReleaseForDestination(actor, moveDestination, true);
                continue;
            }
            bool throughSlit = !actor.Outside && verb == Verb.Inspect && key == "outer_bar";
            bool needsOutside = addressed?.Outside ?? (verb is Verb.OuterBar or Verb.LiftOuterBar || key is "outside" or "outer_bar");
            if (throughSlit) needsOutside = false;
            bool sharedDoor = key == "door" && verb is Verb.PushDoor or Verb.PullDoor or Verb.CloseDoor or Verb.Inspect or Verb.Wait;
            if (!sharedDoor && actor.Outside != needsOutside)
            {
                Fail(actor.Outside ? "身在主楼侧，够不到生活翼内的这个对象；须先穿过开启的门。" : "身在生活翼，够不到主楼侧的这个对象；须先穿过开启的门。");
                continue;
            }
            bool Matches(params string[] names) => names.Contains(key, StringComparer.Ordinal);
            bool appropriate = verb switch
            {
                Verb.PushDoor or Verb.PullDoor or Verb.CloseDoor or Verb.BraceDoorWithWedge => Matches("door"),
                Verb.RemoveWedge => Matches("wedge", "paper", "door"),
                Verb.WrapWedge or Verb.UnwrapWedge => Matches("wedge", "paper"),
                Verb.SlideInnerBolt or Verb.WithdrawInnerBolt => Matches("inner_bolt", "door"),
                Verb.OuterBar or Verb.LiftOuterBar => Matches("outer_bar", "slit"),
                Verb.MoveStop or Verb.ReturnStop => Matches("stop"),
                Verb.WipeFloor => Matches("floor", "floor_wipe"),
                Verb.OpenValve or Verb.CloseValve => Matches("water"),
                Verb.WipeWindow => key.StartsWith("window_", StringComparison.Ordinal),
                Verb.Connect or Verb.Disconnect => Matches("transmitter"),
                Verb.Convene => Matches("meeting"),
                Verb.Talk => Matches("engineer", "chef", "musician", "transmitter", "meeting", "outside"),
                Verb.Compare => Matches("cup", "tear", "box_mark"),
                _ => true
            };
            if (!appropriate) { Fail("这个行动不适用于" + target.Title + "。"); continue; }
            if (verb is Verb.PushDoor or Verb.PullDoor && (verb == Verb.PushDoor) == actor.Outside)
            {
                Fail("这个方向使门扇压向石砌止口，无法打开外开门。"); continue;
            }
            // Movement here records the abstract destination; the replay layer handles paths and collision presentation.
            Target destination = addressed?.Position ?? (actor.Outside ? targets["outside"] : target);
            if (throughSlit) destination = Start("slit");
            if (verb == Verb.RemoveWedge || verb == Verb.BraceDoorWithWedge) destination = Start("door");
            if (Enum.TryParse<HallCausalityRules.Action>(actionName, out var physicalAction) && Enum.IsDefined(physicalAction))
            {
                bool success = Apply(physicalAction, out string caption);
                if (success)
                {
                    actor.Position = destination;
                    if (verb is Verb.PushDoor or Verb.PullDoor) holdingDoor = actor;
                    if (verb is Verb.CloseDoor or Verb.BraceDoorWithWedge or Verb.RemoveWedge) holdingDoor = null;
                }
                Emit(actionName, actor, target, success, actor.Name + "：" + caption);
                continue;
            }
            bool done = true;
            string feedback;
            switch (verb)
            {
                case Verb.Inspect:
                    string current = key switch
                    {
                        "door" => "推演此刻：门扇" + (result.DoorClosed ? "合拢" : "开启") + "；内销" + (result.InnerBoltSlid ? "插入" : "拔出") + "。门缝" + (result.SmokeReachedDoor ? "已有热烟" : "尚未出现热烟") + "。",
                        "inner_bolt" => "推演此刻：生活翼内销" + (result.InnerBoltSlid ? "插入门框。" : "处于拔出状态。"),
                        "slit" or "outer_bar" => "推演此刻：通过门上的检视孔可见外闩" + (result.OuterBarDropped ? "落座。" : "尚未落座。"),
                        "paper" or "wedge" => "推演此刻：木楔" + (result.DoorIsBraced ? "撑在门底。" : result.WedgeWrapped ? "包在纸中。" : "尚未包起。"),
                        "stop" => "推演此刻：止挡座" + (result.StopMoved ? "已经移开。" : "位于原处。"),
                        "floor" or "floor_wipe" => "推演此刻：地面" + (result.FloorWiped ? "已经擦拭。" : "尚未擦拭。"),
                        "trace" => "推演此刻：门底" + (result.Traces.Any(trace => trace.Id == "door-mask") ? "留有楔形遮蔽痕。" : "尚未生成楔形遮蔽痕。"),
                        "water" => "推演此刻：水阀" + (valveOpen ? "打开，正在流水。" : "关闭。"),
                        "transmitter" => "推演此刻：传声回路" + (connected ? "接通。" : "断开。"),
                        _ => cleanWindows.Contains(key) ? "推演此刻：玻璃浮灰已被擦去。" : ""
                    };
                    if (current.Length == 0 && target.Description.Length == 0) { done = false; feedback = "尚无这处现场记录，脑内推演不能补出未知材料。"; }
                    else
                    {
                        feedback = current + (target.Description.Length > 0 ? "【现场记录】" + target.Description : "");
                        observations[actor.Name + "|" + key] = feedback;
                    }
                    break;
                case Verb.Compare:
                    if (string.IsNullOrEmpty(target.Description)) { done = false; feedback = "尚无这处现场记录，脑内推演不能补出未知材料。"; }
                    else { observations[actor.Name + "|" + key] = target.Description; feedback = "核对" + target.Title + "【现场记录】" + target.Description; }
                    break;
                case Verb.Wait: feedback = "在" + target.Title + "旁等候；没有新增观察。"; break;
                case Verb.OpenValve:
                    done = !valveOpen; valveOpen = true;
                    feedback = done ? "拧开水阀，清水流入洗盆。" : "水阀已经打开。"; break;
                case Verb.CloseValve:
                    done = valveOpen; valveOpen = false;
                    feedback = done ? "关上水阀，流水停止。" : "水阀已经关闭。"; break;
                case Verb.WipeWindow:
                    done = cleanWindows.Add(key);
                    feedback = done ? "擦去玻璃表面的浮灰；窗外景象仍须由现场记录确认。" : "这扇窗上的浮灰已经擦去。"; break;
                case Verb.Connect:
                    done = !connected;
                    if (done) { connected = true; holdingConnection = actor; }
                    feedback = done ? "拉住传声柄，接通传声回路；脑内不会获得新的广播。" : "传声回路已经接通。"; break;
                case Verb.Disconnect:
                    done = connected && holdingConnection == actor;
                    if (done) { connected = false; holdingConnection = null; }
                    feedback = done ? "松开传声柄，切断回路。" : "此人没有握住已接通的传声柄。"; break;
                case Verb.Talk:
                case Verb.Convene:
                    if (string.IsNullOrEmpty(target.KnownDialogue)) { done = false; feedback = "没有已取得的谈话记录可供重述；不会生成新的证词。"; }
                    else
                    {
                        observations[actor.Name + "|" + key] = target.KnownDialogue;
                        feedback = "【已知谈话】" + target.KnownDialogue;
                        if (verb == Verb.Convene)
                            foreach (var person in actors.Values.Where(person => !person.Outside))
                            {
                                ReleaseForDestination(person, target);
                                person.Position = target;
                            }
                    }
                    break;
                default: done = false; feedback = "尚不能执行这个行动。"; break;
            }
            if (done)
            {
                ReleaseForDestination(actor, destination);
                actor.Position = destination;
            }
            Emit(actionName, actor, target, done, actor.Name + "：" + feedback);
        }
        return new Godot.Collections.Dictionary { ["actions"] = executed, ["events"] = events, ["states"] = states };
    }

    public string Probe(string hypothesis)
    {
        if (!Enum.TryParse<HallCausalityRules.Hypothesis>(hypothesis, out var claim)
            || !Enum.IsDefined(claim) || claim.ToString() != hypothesis) return Visible("无法识别这项假设。");
        var observedLocks = HallCausalityRules.Simulate(new[]
        {
            HallCausalityRules.Action.SlideInnerBolt,
            HallCausalityRules.Action.OuterBar
        });
        return Visible(HallCausalityRules.TestHypothesis(claim, observedLocks).Explanation);
    }

    public Godot.Collections.Dictionary Run(Godot.Collections.Array<string> orderedActions)
    {
        var actions = new List<HallCausalityRules.Action>();
        foreach (string code in orderedActions)
        {
            if (!HallReplayProgram.TryAction(code, out var action))
                return new Godot.Collections.Dictionary { ["error"] = Visible($"无法识别行动：{code}") };
            actions.Add(action);
        }
        return Project(HallCausalityRules.Simulate(actions), "", true, "");
    }

    private static Godot.Collections.Dictionary Project(HallCausalityRules.Result result,
        string action, bool succeeded, string caption)
    {
        var ids = new Godot.Collections.Array<string>();
        var descriptions = new Godot.Collections.Array<string>();
        var conflicts = new Godot.Collections.Array<string>();
        var failures = new Godot.Collections.Array<string>();
        foreach (var trace in result.Traces) { ids.Add(trace.Id); descriptions.Add(Visible(trace.Description)); }
        foreach (var conflict in result.Conflicts.Where(item => item.Contradiction)) conflicts.Add(Visible(conflict.Explanation));
        foreach (var failure in result.Failures) failures.Add(Visible(failure));
        return new Godot.Collections.Dictionary
        {
            ["ids"] = ids, ["descriptions"] = descriptions, ["conflicts"] = conflicts,
            ["failures"] = failures, ["action"] = action, ["succeeded"] = succeeded, ["caption"] = Visible(caption),
            ["closed"] = result.DoorClosed, ["inner_bolt"] = result.InnerBoltSlid,
            ["outer_bar"] = result.OuterBarDropped, ["braced"] = result.DoorIsBraced,
            ["wrapped"] = result.WedgeWrapped, ["wedge_removed"] = result.WedgeWasRemoved,
            ["stop_moved"] = result.StopMoved, ["wiped"] = result.FloorWiped,
            ["smoke"] = result.SmokeReachedDoor
        };
    }
}
