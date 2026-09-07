using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using Spectre.Console;
using System.Text;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

public sealed class DashboardUi
{
    private readonly int _seed;
    private readonly int _visitorCount;
    private readonly double _startMinute;
    private readonly string _initialPreset;

    private ZooWorld _world = null!;
    private ZooRules _rules = null!;
    private ZooAnomalies _anomalies = null!;
    private ZooSimulation _simulation = null!;

    private Character _trackedCharacter = null!;
    private int _trackedIndex;
    private bool _isPlaying;
    private double _timeStepMinutes = 1.0;
    private int _totalSteps;
    private readonly List<string> _eventLog = [];
    private const int MaxLogCount = 10;
    private int _lastWidth;
    private int _lastHeight;

    public DashboardUi(int seed = 42, int visitorCount = 4, double startMinute = 540, string preset = "standard")
    {
        _seed = seed;
        _visitorCount = visitorCount;
        _startMinute = startMinute;
        _initialPreset = preset;
        ResetWorld();
    }

    public void ResetWorld()
    {
        _simulation?.Dispose();
        _totalSteps = 0;

        _simulation = new ZooSimulation(_seed, _visitorCount, _startMinute, _initialPreset);
        _world = _simulation.World;
        _rules = _simulation.Rules;
        _anomalies = _simulation.Anomalies;

        _world.Session.Runtime.Executed += (intent, result) =>
        {
            if (result.Result?.Succeeded == true && intent.Effect.Op != "evolve")
            {
                var time = _world.Session.Timeline.Now;
                var log = $"[{time:F1}m] {Markup.Escape(intent.Actor.Name)}: {Markup.Escape(intent.Label)}";
                lock (_eventLog)
                {
                    _eventLog.Add(log);
                    if (_eventLog.Count > MaxLogCount) _eventLog.RemoveAt(0);
                }
            }
        };


        var people = _world.People.ToList();
        _trackedIndex = Math.Clamp(_trackedIndex, 0, Math.Max(0, people.Count - 1));
        _trackedCharacter = people.Count > 0 ? people[_trackedIndex] : _world.BlueStaff;

        lock (_eventLog)
        {
            _eventLog.Clear();
            _eventLog.Add($"[系统] 世界重置完成，初始时刻 {_startMinute:F1} 分钟，人数 {_world.People.Count()} 人。");
        }
    }

    public void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        _lastWidth = Console.WindowWidth;
        _lastHeight = Console.WindowHeight;
        Console.Clear();

        try
        {
            while (true)
            {
                // 检测窗口缩放
                int curWidth = Console.WindowWidth;
                int curHeight = Console.WindowHeight;
                if (curWidth != _lastWidth || curHeight != _lastHeight)
                {
                    _lastWidth = curWidth;
                    _lastHeight = curHeight;
                    Console.Clear();
                }

                // 处理按键输入
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.P:
                            _isPlaying = !_isPlaying;
                            break;
                        case ConsoleKey.S:
                            StepSimulation(_timeStepMinutes);
                            break;
                        case ConsoleKey.R:
                            ResetWorld();
                            Console.Clear();
                            break;
                        case ConsoleKey.T:
                            CycleTrackedCharacter();
                            break;
                        case ConsoleKey.V:
                            ShowRulesModal();
                            Console.Clear();
                            break;
                        case ConsoleKey.A:
                            ShowAddRuleModal();
                            Console.Clear();
                            break;
                        case ConsoleKey.D:
                            ShowDeleteRuleModal();
                            Console.Clear();
                            break;
                        case ConsoleKey.C:
                            SelectTrackedCharacterModal();
                            Console.Clear();
                            break;
                        case ConsoleKey.Q:
                            return;
                    }
                }

                // 推进模拟时钟
                if (_isPlaying)
                {
                    StepSimulation(_timeStepMinutes);
                }

                // 渲染主仪表盘
                RenderDashboard();
                Thread.Sleep(120);
            }
        }
        finally
        {
            Console.CursorVisible = true;
            _anomalies?.Dispose();
            _world?.Dispose();
        }
    }

    private void StepSimulation(double minutes)
    {
        var targetTime = _world.Session.Timeline.Now + minutes;
        _totalSteps += _world.Run(targetTime, maxSteps: 300);
    }

    private void CycleTrackedCharacter()
    {
        var people = _world.People.ToList();
        if (people.Count == 0) return;
        _trackedIndex = (_trackedIndex + 1) % people.Count;
        _trackedCharacter = people[_trackedIndex];
    }

    private void SelectTrackedCharacterModal()
    {
        Console.Clear();
        var people = _world.People.ToList();
        var prompt = new SelectionPrompt<Character>()
            .Title("[yellow]请选择要跟踪监控的人物：[/]")
            .PageSize(10)
            .UseConverter(c => $"{c.Name} (身份: {c.Get<string>("role")}, 场景: {c.CurrentScene?.Name ?? "无"})");

        foreach (var p in people) prompt.AddChoice(p);

        _trackedCharacter = AnsiConsole.Prompt(prompt);
        _trackedIndex = people.IndexOf(_trackedCharacter);
    }

    private void ShowRulesModal()
    {
        Console.Clear();
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("[cyan]编号[/]");
        table.AddColumn("[yellow]生效形式 / 行为效用指引[/]");
        table.AddColumn("[white]文书正文[/]");
        table.AddColumn($"[magenta]{_trackedCharacter.Name}接纳状态[/]");
        table.AddColumn("[green]全员采信[/]");

        foreach (var clause in _rules.Clauses)
        {
            var acceptedCount = _world.People.Count(p =>
                p.Mind?.Beliefs.Any(b => b.HasTag(CommonTag.Accepted) && ReferenceEquals(b.Get<Entity>("publication"), clause.Publication)) == true);
            var charBelief = _trackedCharacter.Mind?.Beliefs.FirstOrDefault(b => ReferenceEquals(b.Get<Entity>("publication"), clause.Publication));
            var stanceStr = charBelief == null ? "[grey]未感知(未读)[/]" :
                charBelief.HasTag(CommonTag.Accepted) ? $"[green]已采信(★{charBelief.Get<int>("confidence")})[/]" :
                (charBelief.HasTag(CommonTag.Evaluated) || charBelief.HasTag(CommonTag.Understood)) ? "[yellow]存疑动摇[/]" : "[red]排斥/忽略[/]";

            table.AddRow(clause.Key, Markup.Escape(clause.OperationalForm), Markup.Escape(clause.Text.Length > 28 ? clause.Text[..28] + "..." : clause.Text), stanceStr, $"{acceptedCount}/{_world.People.Count()}");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]按任意键返回主界面...[/]");
        Console.ReadKey(intercept: true);
    }

    private void ShowAddRuleModal()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold cyan]=== 添加新规则 ===[/]");
        var section = AnsiConsole.Ask<string>("请输入规则分区（例如 Custom / Visitor / Staff）：");
        var num = AnsiConsole.Ask<string>("请输入规则编号（例如 99）：");
        var text = AnsiConsole.Ask<string>("请输入规则正文内容：");

        var key = $"{section}.{num}";
        _rules.AddCustomRule(section, key, text);

        lock (_eventLog)
        {
            _eventLog.Add($"[规则添加] 新增条目 {key}：{Markup.Escape(text)}");
        }

        AnsiConsole.MarkupLine($"[green]成功添加并广播规则 {key}！[/]");
        Thread.Sleep(800);
    }

    private void ShowDeleteRuleModal()
    {
        Console.Clear();
        if (_rules.Clauses.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]当前没有任何规则可供删除。[/]");
            Thread.Sleep(1000);
            return;
        }

        var prompt = new SelectionPrompt<ZooRules.Clause>()
            .Title("[bold red]请选择要废除/删除的规则：[/]")
            .PageSize(12)
            .UseConverter(c => $"{c.Key} - {Markup.Escape(c.Text.Length > 40 ? c.Text[..40] + "..." : c.Text)}");

        foreach (var clause in _rules.Clauses) prompt.AddChoice(clause);

        var selected = AnsiConsole.Prompt(prompt);
        _rules.RemoveClause(selected.Key);

        lock (_eventLog)
        {
            _eventLog.Add($"[规则废除] 已撤销条目 {selected.Key}");
        }

        AnsiConsole.MarkupLine($"[yellow]规则 {selected.Key} 已被废除并从所有人信念中撤销。[/]");
        Thread.Sleep(800);
    }

    private void RenderDashboard()
    {
        Console.SetCursorPosition(0, 0);

        var rootGrid = new Grid().Expand();
        rootGrid.AddColumn(new GridColumn());

        // 1. 顶部 Header
        var statusColor = _isPlaying ? "green" : "yellow";
        var statusText = _isPlaying ? "● 运行中 (PLAYING)" : "⏸ 已暂停 (PAUSED)";
        var headerTable = new Table().Border(TableBorder.Square).Expand();
        headerTable.AddColumn(new TableColumn($"[bold cyan]动物园规则怪谈世界引擎[/] (Dual-Ontology World Engine)") { Alignment = Justify.Left });
        headerTable.AddColumn(new TableColumn($"[{statusColor}]{statusText}[/] | 种子: {_seed} | 园区时刻: [bold white]{(int)(_world.Minute / 60):D2}:{(int)(_world.Minute % 60):D2}[/] (+{_world.Session.Timeline.Now:F1}m) | 步数: {_totalSteps}") { Alignment = Justify.Right });
        rootGrid.AddRow(headerTable);

        // 2. 主区：左右分栏
        var mainGrid = new Grid().Expand();
        mainGrid.AddColumn(new GridColumn().Width(42));
        mainGrid.AddColumn(new GridColumn());

        // 左侧：园区角色概况与日志
        var leftTable = new Table().Border(TableBorder.Rounded).Title("[bold blue]园区场景与在场角色[/]").Expand();
        leftTable.AddColumn("[white]人物[/]");
        leftTable.AddColumn("[white]所在场景[/]");
        leftTable.AddColumn("[white]结局/状态[/]");

        foreach (var p in _world.People)
        {
            var isTracked = ReferenceEquals(p, _trackedCharacter);
            var prefix = isTracked ? "[bold yellow]▶ " : "  ";
            var suffix = isTracked ? "[/]" : "";
            var outcome = _world.Outcome(p);
            var outcomeColor = outcome switch
            {
                "离园" => "green",
                "仍在园内" => "cyan",
                "认知同化" => "magenta",
                "死亡" => "red",
                _ => "yellow"
            };
            leftTable.AddRow($"{prefix}{p.Name}{suffix}", p.CurrentScene?.Name ?? "未知", $"[{outcomeColor}]{outcome}[/]");
        }

        var logPanel = new Panel(BuildLogMarkup())
            .Header("[bold cyan]实时因果与心智日志[/]")
            .Border(BoxBorder.Rounded)
            .Expand();

        var leftGrid = new Grid().Expand();
        leftGrid.AddColumn(new GridColumn());
        leftGrid.AddRow(leftTable);
        leftGrid.AddRow(logPanel);

        // 右侧：选定人物精神状态监控面板
        var mentalPanel = BuildMentalStatePanel(_trackedCharacter);

        mainGrid.AddRow(leftGrid, mentalPanel);
        rootGrid.AddRow(mainGrid);

        // 3. 底部操作栏
        var footerPanel = new Panel(
            "[bold white][Space/P][/] 播放/暂停  |  [bold white][S][/] 单步推进  |  [bold white][R][/] 重置世界  |  " +
            "[bold white][T/C][/] 切换人物  |  [bold white][V][/] 查看规则  |  [bold white][A][/] 添加规则  |  [bold white][D][/] 删除规则  |  [bold white][Q][/] 退出")
            .Border(BoxBorder.Square)
            .Header("[bold green]交互控制指令[/]")
            .Expand();
        rootGrid.AddRow(footerPanel);

        AnsiConsole.Write(rootGrid);
    }

    private Panel BuildMentalStatePanel(Character c)
    {
        var panelGrid = new Grid().Expand();
        panelGrid.AddColumn(new GridColumn());

        // 基础信息
        var role = c.Get<string>("role") ?? "未知";
        var uniform = c.Get<string>("uniform") ?? "默认服装";
        var scene = c.CurrentScene?.Name ?? "无";
        var identity = c.Get<string>("identity") ?? "human";
        var identityStrength = c.Get<double>("identity_strength");

        var infoTable = new Table().Border(TableBorder.None).HideHeaders().Expand();
        infoTable.AddColumn("key");
        infoTable.AddColumn("val");
        var compliance = c.Get<string>("compliance_profile") ?? "标准认知";
        infoTable.AddRow("[bold yellow]目标角色:[/] " + c.Name, $"[bold]职务/角色:[/] {role}  |  [bold]场景:[/] {scene}");
        infoTable.AddRow("[bold]守则倾向:[/] [magenta]" + compliance + "[/]", $"[bold]自我认同:[/] {identity} ({identityStrength:P0})");
        panelGrid.AddRow(infoTable);
        panelGrid.AddRow(new Rule());

        // 核心生理与精神数值进度条
        var health = Math.Clamp(c.Get<double>("health"), 0, 10);
        var stability = Math.Clamp(c.Get<double>("stability"), 0, 20);
        var fear = Math.Clamp(c.Get<double>("fear"), 0, 10);
        var erosion = Math.Clamp(c.Get<double>("erosion"), 0, 10);
        var attention = Math.Clamp(c.Get<double>("attention"), 0, 10);
        var uncertainty = Math.Clamp(c.Get<double>("uncertainty"), 0, 5);
        var thirst = Math.Clamp(c.Get<double>("thirst"), 0, 10);
        var fatigue = Math.Clamp(c.Get<double>("fatigue"), 0, 10);

        var statsTable = new Table().Border(TableBorder.None).HideHeaders().Expand();
        statsTable.AddColumn(new TableColumn("label").Width(14));
        statsTable.AddColumn(new TableColumn("bar"));
        statsTable.AddColumn(new TableColumn("value").Width(8));

        void AddGauge(string label, double val, double max, string color)
        {
            int totalBars = 20;
            int filled = (int)Math.Round((val / max) * totalBars);
            filled = Math.Clamp(filled, 0, totalBars);
            string bar = $"[{color}]{new string('█', filled)}[/][grey]{new string('░', totalBars - filled)}[/]";
            statsTable.AddRow(label, bar, $"{val:F1}/{max:F0}");
        }

        AddGauge("生命健康 (Health)", health, 10, health > 5 ? "green" : "red");
        AddGauge("精神稳定 (Stability)", stability, 15, stability > 5 ? "cyan" : "red");
        AddGauge("侵蚀程度 (Erosion)", erosion, 10, erosion > 5 ? "bold red" : (erosion > 2 ? "magenta" : "grey"));
        AddGauge("恐惧数值 (Fear)", fear, 10, fear > 4 ? "red" : "yellow");
        AddGauge("受到关注 (Attention)", attention, 10, attention > 4 ? "yellow" : "grey");
        AddGauge("认知困惑 (Uncertain)", uncertainty, 5, uncertainty > 2 ? "purple" : "grey");
        AddGauge("口渴疲劳 (Thirst/Fat)", thirst + fatigue, 20, "blue");

        panelGrid.AddRow(statsTable);
        panelGrid.AddRow(new Rule());

        // 持有物品与心智决策
        var inventory = c.Inventory.Any() ? string.Join(", ", c.Inventory.Select(i => i.Name)) : "（空）";
        var lastOp = c.Get<string>("last_op") ?? "无";
        var lastTarget = c.Get<Entity>("last_target")?.Name ?? "无";
        var acceptedBeliefsCount = c.Mind?.Beliefs.Count(b => b.HasTag(CommonTag.Accepted)) ?? 0;
        var doubtedCount = c.Mind?.Beliefs.Count(b => b.HasTag(CommonTag.Evaluated) || b.HasTag(CommonTag.Understood)) ?? 0;

        var detailTable = new Table().Border(TableBorder.None).HideHeaders().Expand();
        detailTable.AddColumn("k");
        detailTable.AddColumn("v");
        detailTable.AddRow("[bold]持有物品:[/] ", inventory);
        detailTable.AddRow("[bold]最近行动:[/] ", $"{lastOp} -> {lastTarget}");
        detailTable.AddRow("[bold]已采信规则:[/] ", $"[green]{acceptedBeliefsCount} 条采信生效[/]" + (doubtedCount > 0 ? $" | [yellow]{doubtedCount} 条存疑/动摇[/]" : ""));

        panelGrid.AddRow(detailTable);

        return new Panel(panelGrid)
            .Header($"[bold yellow]人物精神与心智监控：{c.Name}[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private string BuildLogMarkup()
    {
        lock (_eventLog)
        {
            if (_eventLog.Count == 0) return "[grey]暂无动态事件[/]";
            return string.Join("\n", _eventLog);
        }
    }
}
