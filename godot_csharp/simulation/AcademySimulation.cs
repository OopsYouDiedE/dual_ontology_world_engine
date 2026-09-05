using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Kotodama;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using DualOntologyWorldEngine.Core.Timeline;
using DualOntologyWorldEngine.Core.Simulation;

namespace DualOntologyWorldEngine.Simulation;

/// <summary>
/// 十六人与恶魔的校园五日自主认知决策推演引擎。
/// </summary>
public partial class AcademySimulation
{
    public Entity World { get; }
    public RuleEnvironment Env { get; }
    public RuleSystem Rules { get; }
    public KotodamaSystem Words { get; }
    public Timeline Timeline { get; }
    public double CurrentTime => Timeline.Now;
    public ActionRuntime Runtime { get; }

    public Dictionary<string, Entity> Scenes { get; } = new();
    public Dictionary<string, Character> Students { get; } = new();
    public Dictionary<string, Soul> Souls { get; } = new();
    public Character Demon { get; }
    public Soul DemonSoul { get; }

    public List<Entity> RulePublications { get; } = new();
    public List<Entity> Facilities { get; } = new();
    public List<Entity> SceneItems { get; } = new();
    public List<Entity> StudentItems { get; } = new();

    private (Character Accused, Character Instigator, string AccusationReason, string AccusedLocation, string AccusedAction)? _activeAccusation;
    public List<string> SimulationLogs { get; } = new();
    public TrajectoryRecorder Trajectory { get; } = new();
    public CommandLineController CLI { get; } = new();
    public Dictionary<Character, SimsMotiveProfile> Motives { get; } = new();

    private readonly Random _rng = new(42);
    private int _simDay = 1;
    private int _maxDays = 5;
    public SimulationSession Session { get; }
    private bool _dayStarted;
    private SimulationStep? _lastStep;
    private readonly Dictionary<Entity, List<string>> _actionHistory = new();
    private bool _simFinished = false;

    public int CurrentDay => _simDay;
    public bool ExportOnCompletion { get; set; } = true;

    public AcademySimulation()
    {
        World = new Entity("异常感知学园");
        Env = new RuleEnvironment();
        Rules = new RuleSystem();
        Words = new KotodamaSystem(Env, Rules);
        Timeline = new Timeline();
        Runtime = new ActionRuntime(Timeline, Env, Rules, Words, World);
        Session = new SimulationSession(Runtime, World);

        BuildCampusTopology();
        BuildSceneFacilities();

        Demon = new Character("恶魔（人形具象）");
        DemonSoul = new Soul("恶魔心智");
        Demon.Bind(DemonSoul);
        Demon.SetTag("Demon", 1);
        Demon.SetTag("Reliability", 1);
        Demon.Reparent(Scenes["中央厅"]);

        InstallCampusCovenants();
        BuildStudents();
        BuildSceneItems();
        BuildStudentItems();

        InstallStudentDecisions();
        InstallDemonDecisions();

        foreach (var student in Students.Values) Motives[student] = new SimsMotiveProfile();
        Motives[Demon] = new SimsMotiveProfile { Hunger = 100f, Energy = 100f, Comfort = 100f };
        CLI.RegisterCharacters(Students.Values.Concat([Demon]));

        ConfigureActionRuntimeHooks();
        Timeline.Advanced += elapsed =>
        {
            foreach (var profile in Motives.Values) profile.Decay(elapsed);
        };
    }

    public sealed record SimulationStep(
        double Time,
        int Day,
        Character Actor,
        Intent? Intent,
        ActionResolution? Resolution,
        string Narrative,
        bool IsNight);

    public SimulationStep? StepSimulation()
    {
        if (_simFinished || _simDay > _maxDays) return null;
        double daylightEndAv = (_simDay - 1) * 200.0 + 120.0;
        double nightEndAv = _simDay * 200.0;
        if (!_dayStarted)
        {
            _dayStarted = true;
            SimulationLogs.Add($"☀️ 学园第 {_simDay} 日开始，角色依当前状态自主选择。");
            foreach (var actor in Students.Values.Concat([Demon]).OrderBy(_ => _rng.Next()))
            {
                var soul = actor.Mind!;
                if (!_actionHistory.TryGetValue(actor, out var history))
                    _actionHistory[actor] = history = new List<string>();
                int logStart = 0;
                Session.Activate(actor, soul, idleCost: 20,
                    utility: intent => CalculateActionUtility(intent, actor, Motives[actor], history),
                    chooser: decisions =>
                    {
                        logStart = SimulationLogs.Count;
                        return CLI.IsInteractive && CLI.IsControlled(actor)
                            ? CLI.InteractiveChoose(actor, decisions, Motives[actor], Timeline.Now, _simDay) ?? decisions[0].Action!
                            : decisions[0].Action!;
                    },
                    completed: (intent, result) =>
                    {
                        if (intent == null)
                        {
                            _lastStep = new SimulationStep(Timeline.Now, _simDay, actor, null, null,
                                $"【{actor.Name}】暂时没有可执行行动，等待 20 AV 后重试。", false);
                            return;
                        }
                        history.Add(intent.Effect.Op);
                        if (result?.Result?.Succeeded == true) Motives[actor].ApplyActionReward(intent.Effect.Op);
                        var narrative = string.Join("\n", SimulationLogs.Skip(logStart));
                        if (string.IsNullOrWhiteSpace(narrative)) narrative = $"【{actor.Name}】执行「{intent.Label}」。";
                        var profile = Motives[actor];
                        var role = actor.Get<string>("profession") ?? (actor == Demon ? "学园监管者/恶魔" : "学员");
                        Trajectory.Record(Timeline.Now, intent, result!, actor.CurrentScene?.Name ?? "未知",
                            $"动机: [饱腹:{profile.Hunger:F0} 精力:{profile.Energy:F0} 社交:{profile.Social:F0} 娱乐:{profile.Fun:F0}] | 产出: {result?.Reason}",
                            narrative, intent.Label, role);
                        _lastStep = new SimulationStep(Timeline.Now, _simDay, actor, intent, result, narrative, false);
                    });
            }
        }
        _lastStep = null;
        while (Timeline.Queue.Count > 0 && Timeline.Queue[0].Time < daylightEndAv)
        {
            Timeline.Step();
            if (_lastStep != null) return _lastStep;
        }
        // 日程属于学园内容；停止角色槽不会清除核心记录的剩余行动代价。
        foreach (var actor in Students.Values.Concat([Demon])) Session.Deactivate(actor);
        Timeline.RunUntil(nightEndAv);
        if (Timeline.Now < nightEndAv)
            throw new InvalidOperationException("夜间时间线仍有待执行任务，请检查周期任务的步数预算。");
        ProcessNightPhase(_simDay);
        SettleNightlyEmotions(_simDay);
        var nightStep = new SimulationStep(nightEndAv, _simDay, Demon, null, null,
            $"🌙 第 {_simDay} 日夜间作息结算完成。", true);
        _simDay++;
        _dayStarted = false;
        if (_simDay > _maxDays)
        {
            _simFinished = true;
            if (ExportOnCompletion) SaveSimulationOutputs();
        }
        return nightStep;
    }

    public void RunSimulationFiveDays(int days = 5)
    {
        _maxDays = days;
        while (StepSimulation() != null)
        {
        }
    }

    public void SaveSimulationOutputs()
    {
        try
        {
            var mdText = Trajectory.GenerateMarkdown();
            File.WriteAllText("five_days_trajectory.md", mdText, System.Text.Encoding.UTF8);

            var rootCandidates = new[]
            {
                Path.GetFullPath("."),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../")),
            };
            foreach (var r in rootCandidates)
            {
                if (File.Exists(Path.Combine(r, "AGENTS.md")))
                {
                    File.WriteAllText(Path.Combine(r, "five_days_trajectory.md"), mdText, System.Text.Encoding.UTF8);
                    break;
                }
            }

            var docsDir = Path.GetFullPath("docs");
            if (Directory.Exists(docsDir))
            {
                File.WriteAllText(Path.Combine(docsDir, "five_days_trajectory.md"), mdText, System.Text.Encoding.UTF8);
            }

            var csharpCode = Trajectory.GenerateCSharpClass("FiveDaysRecordedTrajectory");
            var outDir = Path.GetFullPath("output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "FiveDaysRecordedTrajectory.cs"), csharpCode);
        }
        catch (Exception ex)
        {
            SimulationLogs.Add($"[Warning] 导出轨迹与生成 C# 类发生提示: {ex.Message}");
        }
    }
}
