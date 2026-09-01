#nullable enable

using DualOntologyEngine.Core;
using Godot;
using Expression = DualOntologyEngine.Core.Expression;

namespace DualOntologyEngine.Game;

public partial class GameRoot : Control
{
	private static readonly string[] AspectNames =
		["Form", "Name", "Law", "Mind", "Bond", "Threshold", "Preservation", "Death", "Vestige"];

	private static readonly Dictionary<string, string> AspectLabels = new()
	{
		["Form"] = "形质", ["Name"] = "名契", ["Law"] = "律令", ["Mind"] = "心识",
		["Bond"] = "缘系", ["Threshold"] = "门隙", ["Preservation"] = "存续",
		["Death"] = "终寂", ["Vestige"] = "遗痕",
	};

	private readonly WorldSpace world = new("抄名室");
	private readonly TurnSequenceManager timeline = new();
	private readonly RuleEnvironment ruleEnvironment = new();
	private readonly RuleSystem ruleSystem = new();
	private Entity inventory = null!;
	private Character playerCharacter = null!;
	private Character borrowerCharacter = null!;
	private Soul playerSoul = null!;
	private Soul borrowerSoul = null!;
	private WordSpiritSystem wordSpirits = null!;
	private Rule falseMirrorRule = null!;
	private Rule trueMirrorRule = null!;
	private Rule trueSealRule = null!;
	private WordSpirit sealingWordSpirit = null!;
	private readonly HashSet<GameCard> selected = [];
	private readonly Dictionary<string, int> disciplines = new()
	{
		["Name"] = 0, ["Law"] = 0, ["Mind"] = 0, ["Threshold"] = 0, ["Preservation"] = 0,
	};
	private readonly List<string> journal = [];
	private readonly List<string> log = [];

	private Label resourceLabel = null!;
	private Label objectiveLabel = null!;
	private Label selectedLabel = null!;
	private Label queueLabel = null!;
	private Label insightLabel = null!;
	private Label soulLabel = null!;
	private FlowContainer hand = null!;
	private FlowContainer locationBoard = null!;
	private VBoxContainer disciplineList = null!;
	private RichTextLabel journalText = null!;
	private RichTextLabel logText = null!;
	private ColorRect overlay = null!;
	private PanelContainer overlayPanel = null!;

	private int stability
	{
		get => playerSoul.Stability;
		set => playerSoul.changeTag("Stability", value - playerSoul.Stability);
	}
	private int identity
	{
		get => playerSoul.Identity;
		set => playerSoul.changeTag("Identity", value - playerSoul.Identity);
	}
	private int exposure
	{
		get => playerSoul.getTag("Exposure");
		set => playerSoul.changeTag("Exposure", value - playerSoul.getTag("Exposure"));
	}
	private int turn;
	private int insight;
	private int anomalyPulses;
	private int anchorUses;
	private int mainRuleInfluences;
	private bool requiredIntentGenerated;
	private bool playerBusy;
	private bool awaitingReaction;
	private bool gameEnded;
	private bool playerWon;

	private readonly Color background = new("0b0d12");
	private readonly Color panelColor = new("141822");
	private readonly Color panelLight = new("1b2130");
	private readonly Color ink = new("d7d1c1");
	private readonly Color dimInk = new("8d92a1");
	private readonly Color gold = new("c4a86a");
	private readonly Color red = new("b85c62");
	private readonly Color teal = new("5d9c91");

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildInterface();
		StartGame();
		if (OS.GetCmdlineUserArgs().Contains("--prototype-smoke"))
			RunSmokePath();
	}

	private void StartGame()
	{
		ClearEntity(world);
		world.clear_connections();
		ruleEnvironment.clear();
		selected.Clear();
		timeline.reset();
		journal.Clear();
		log.Clear();
		foreach (var key in disciplines.Keys.ToArray())
			disciplines[key] = 0;

		playerSoul = new Soul("守名者", world, 8, 6);
		borrowerSoul = new Soul("借名者意识", world, 8, 4);
		exposure = 0;
		turn = 0;
		insight = 0;
		anomalyPulses = 0;
		anchorUses = 0;
		mainRuleInfluences = 0;
		requiredIntentGenerated = false;
		playerBusy = false;
		awaitingReaction = false;
		gameEnded = false;
		playerWon = false;
		overlay.Visible = false;

		inventory = new Entity("随身物与记录", world, []);

		AddCard("《夜班访客守则》", "记录 · 来源不明", "墨水尚新。文本声称：镜中人询问姓名时必须如实回答。",
			new() { ["Record"] = 1, ["Law"] = 1, ["Claim"] = 1, ["Cognitohazard"] = 1 });
		AddCard("铜质呼铃", "工具 · 可重复使用", "铃舌已被取走，但敲击外壳仍会发出短促回声。",
			new() { ["Tool"] = 1, ["Law"] = 1, ["Mind"] = 1, ["Catalyst"] = 1 });
		AddCard("母亲的来信", "记录 · 身份锚点", "信上使用了只有家人才知道的乳名。它证明你曾经是谁。",
			new() { ["Record"] = 1, ["Bond"] = 2, ["Preservation"] = 2, ["Offering"] = 1 });
		AddCard("褪色入馆证", "工具 · 制度身份", "照片是你，姓名栏却正在缓慢褪色。",
			new() { ["Tool"] = 1, ["Name"] = 2, ["Law"] = 1, ["Authority"] = 1 });
		AddCard("库房粉笔", "工具 · 边界标记", "能标记真实门框。粉末无法附着在伪造的入口上。",
			new() { ["Tool"] = 1, ["Form"] = 1, ["Threshold"] = 1 });

		var documentVault = AddLocation("文书库", "地点 · 门框不稳定", "库门在相邻墙面间移动，内部保存今晚产生的纸面记录。", new(-4, 0, 0));
		_ = new GameCard("入库调拨单", documentVault, "记录 · 行为证据", "今晚的入库单与守则纸张使用同一批水印。",
			new() { ["Record"] = 1, ["Vestige"] = 2, ["Law"] = 1 });
		var mirrorHall = AddLocation("镜廊", "地点 · 认知危险", "镜面会复述访客的社会身份，并尝试取得姓名回应。", new(0, 0, -3));
		_ = new GameCard("镜廊回声记录", mirrorHall, "记录 · 异常反应", "铜铃回声与异常复述之间存在半拍延迟。",
			new() { ["Record"] = 1, ["Mind"] = 2, ["Vestige"] = 1 });
		var personnelRoom = AddLocation("人事室", "地点 · 制度封锁", "门口贴着互相冲突的告示；其中至少一条没有发行担保。", new(0, 0, 3));
		_ = new GameCard("旧值班名册", personnelRoom, "记录 · 身份遗痕", "被刮去的姓名下方，仍残留职位与旧事件。",
			new() { ["Record"] = 1, ["Name"] = 1, ["Vestige"] = 2, ["Mind"] = 1 });
		var westGate = AddLocation("西门", "地点 · 馆舍边界", "守则称其为出口，门缝中却持续渗入不属于馆舍的回声。", new(5, 0, 0));
		_ = new GameCard("西门闭馆封条", westGate, "工具 · 仪式收束", "真正的西门不是出口，而是把馆舍边界缝合起来的封口。",
			new() { ["Tool"] = 1, ["Law"] = 3, ["Threshold"] = 2, ["Seal"] = 1 });

		world.connect(documentVault, mirrorHall, 2);
		world.connect(documentVault, personnelRoom, 1);
		world.connect(mirrorHall, westGate, 3);
		world.connect(personnelRoom, westGate, 2);
		world.connect(mirrorHall, personnelRoom, 2);

		playerCharacter = new Character("夜班访客", documentVault, new() { ["Character"] = 1 });
		borrowerCharacter = new Character("借名者", mirrorHall, new() { ["Character"] = 1, ["Authority"] = 2 });
		playerCharacter.bind(playerSoul);
		borrowerCharacter.bind(borrowerSoul);

		var parser = new RuleParser();
		falseMirrorRule = parser.Parse(
			"WHEN ALL:\n- visitor is challenged_by mirror\nREQUIRE:\n- visitor answers identity");
		trueMirrorRule = parser.Parse(
			"WHEN ALL:\n- visitor is challenged_by mirror\nFORBID:\n- visitor answers identity\nREQUIRE:\n- visitor remains_silent mirror");
		trueSealRule = parser.Parse(
			"WHEN ALL:\n- visitor is located_at west_gate\nREQUIRE:\n- visitor seals west_gate");
		sealingWordSpirit = parser.ParseWordSpirit(
			"WORD SPIRIT:\n" +
			"CONTRACT COST:\n- visitor sacrifices identity_offering\n" +
			"ACTIVATION RULE:\nWHEN ALL:\n- borrower is bound_by true_name\n- west_gate is sealed\n" +
			"REQUIRE:\n- visitor keeps_holding identity\nEND ACTIVATION RULE\n" +
			"CONSTRAINT RULE:\nWHEN ALL:\n- west_gate is sealed\n" +
			"FORBID:\n- borrower leaves archive\nEND CONSTRAINT RULE\n" +
			"ACTUAL EFFECTS IN ORDER:\n1. archive removes borrower\n2. visitor keeps_holding identity");

		ruleEnvironment.alias("visitor", playerCharacter);
		ruleEnvironment.alias("borrower", borrowerCharacter);
		ruleEnvironment.alias("mirror", mirrorHall);
		ruleEnvironment.alias("west_gate", westGate);
		ruleEnvironment.alias("archive", world);
		ruleEnvironment.assert(new Expression("located_at", playerCharacter, documentVault));

		wordSpirits = new WordSpiritSystem(ruleEnvironment, ruleSystem);
		wordSpirits.register("sacrifice", ValidateIdentityOffering, ApplyIdentityOffering);
		wordSpirits.register("remove", ValidateRemoval, ApplyRemoval);

		var persistenceRule = new Rule(new Expression("true"),
			[new Directive(DirectiveMode.Prefer, new Effect("erode", playerCharacter, "stability"))]);
		var replacementRule = new Rule(new Expression("all",
			new Expression("exposed", playerCharacter, "high"),
			new Expression("ready_to_replace", borrowerCharacter)),
			[new Directive(DirectiveMode.Require, new Effect("replace", playerCharacter, "identity"))]);
		borrowerSoul.consider(persistenceRule, borrowerCharacter, 2);
		borrowerSoul.consider(replacementRule, borrowerCharacter, 2);

		journal.Add("[color=#c4a86a]目标[/color]　在第 15 回合结束前找出异常的真称，并封闭西门。");
		journal.Add("[color=#8d92a1]认知分层[/color]　卡牌上的文字只是记录，不自动等于客观规则。");
		journal.Add("[color=#8d92a1]地点实体[/color]　探查必须投入一个地点；工具或知识决定你能在该地点发现什么。");
		journal.Add("[color=#8d92a1]空间[/color]　角色实际位于地点实体中；探查会沿空间图最短路线移动，并消耗相应行动值。");
		journal.Add("[color=#8d92a1]Soul 与规则[/color]　记录先进入 Soul 的认知；只有被接受的规则才影响决策，普通规则不会直接修改现实。");
		journal.Add("[color=#8d92a1]言灵[/color]　封印是契约：身份献礼必须与保存身份的效果匹配，代价签约时立即支付。");
		journal.Add("[color=#8d92a1]仪式[/color]　需要真称、封条和一件与自身身份有关的献礼。");
		log.Add("[color=#d7d1c1]你在闭馆后醒来。入馆证上的姓名正逐笔消失。[/color]");
		log.Add("选择卡牌，再提交一个行动。每个行动与异常反应会在同一回合队列中依次结算。");

		var anomalyClock = new RecurringAction("异常：借名者", world, 75, ResolveAnomalyPulse);
		timeline.add(new SortedDictionary<float, Entity> { [75] = anomalyClock });

		RefreshAll();
	}

	private void BuildInterface()
	{
		AddChild(new ColorRect
		{
			Color = background,
			MouseFilter = MouseFilterEnum.Ignore,
			LayoutMode = 1,
			AnchorsPreset = (int)LayoutPreset.FullRect,
			AnchorRight = 1,
			AnchorBottom = 1,
		});

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		AddChild(margin);

		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 12);
		margin.AddChild(page);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 18);
		page.AddChild(header);
		var title = MakeLabel("抄名室：第七夜", 28, gold);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(title);
		resourceLabel = MakeLabel("", 17, ink);
		resourceLabel.HorizontalAlignment = HorizontalAlignment.Right;
		header.AddChild(resourceLabel);
		var restart = MakeButton("重新开始", () => StartGame(), new("252b38"));
		restart.CustomMinimumSize = new(104, 40);
		header.AddChild(restart);

		objectiveLabel = MakeLabel("", 14, dimInk);
		objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		page.AddChild(objectiveLabel);

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 14);
		page.AddChild(body);

		var left = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.7f,
		};
		left.AddThemeConstantOverride("separation", 10);
		body.AddChild(left);
		left.AddChild(BuildActionPanel());
		left.AddChild(BuildSelectionPanel());
		left.AddChild(MakeLabel("地点实体", 18, ink));
		locationBoard = new FlowContainer
		{
			CustomMinimumSize = new(0, 104),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		locationBoard.AddThemeConstantOverride("h_separation", 10);
		left.AddChild(locationBoard);

		left.AddChild(MakeLabel("持有物与记录", 18, ink));
		var handScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		handScroll.AddThemeStyleboxOverride("panel", Flat(new("0e1118"), new("242a37")));
		left.AddChild(handScroll);
		hand = new FlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		hand.AddThemeConstantOverride("h_separation", 10);
		hand.AddThemeConstantOverride("v_separation", 10);
		handScroll.AddChild(hand);

		var right = new VBoxContainer
		{
			CustomMinimumSize = new(410, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		right.AddThemeConstantOverride("separation", 10);
		body.AddChild(right);
		right.AddChild(BuildDisciplinePanel());
		right.AddChild(BuildTextPanel("规则簿：记录 / 求证 / 客观结论", out journalText, 0.92f));
		right.AddChild(BuildTextPanel("因果记录", out logText, 1.08f));

		BuildOverlay();
	}

	private Control BuildActionPanel()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Flat(panelColor, new("2a3040"), 10, 1));
		var margin = InnerMargin(14);
		panel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 9);
		margin.AddChild(box);
		box.AddChild(MakeLabel("行动：先选卡，再提交意图", 18, gold));

		var actions = new GridContainer { Columns = 5 };
		actions.AddThemeConstantOverride("h_separation", 8);
		actions.AddThemeConstantOverride("v_separation", 8);
		box.AddChild(actions);
		actions.AddChild(ActionButton("研读 · 100", "1 张记录/知识\n获得洞见，辨认内容", () => RunAction("study")));
		actions.AddChild(ActionButton("探查 · 50＋路线", "1 个地点＋可选工具\n沿最短路线前往并查询", () => RunAction("explore")));
		actions.AddChild(ActionButton("校验 · 90", "2 张已研读记录\n寻找可重复证据", () => RunAction("verify")));
		actions.AddChild(ActionButton("锚定 · 60", "1 张缘系/存续卡\n恢复自我连续", () => RunAction("anchor")));
		actions.AddChild(ActionButton("封印 · 140", "3 张卡牌\n提交真称、封条、献礼", () => RunAction("rite"), true));

		queueLabel = MakeLabel("行动值队列：空", 13, dimInk);
		box.AddChild(queueLabel);
		return panel;
	}

	private Control BuildSelectionPanel()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Flat(panelLight, new("30384a"), 8, 1));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);
		var margin = InnerMargin(10);
		margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(margin);
		selectedLabel = MakeLabel("投入槽：未选择", 14, ink);
		selectedLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		margin.AddChild(selectedLabel);
		var clear = MakeButton("清空投入", () => { selected.Clear(); RefreshAll(); }, new("252b38"));
		clear.CustomMinimumSize = new(100, 42);
		row.AddChild(clear);
		return panel;
	}

	private Control BuildDisciplinePanel()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", Flat(panelColor, new("2a3040"), 9, 1));
		var margin = InnerMargin(12);
		panel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		margin.AddChild(box);
		insightLabel = MakeLabel("洞见 0", 17, gold);
		box.AddChild(insightLabel);
		soulLabel = MakeLabel("Soul：尚未建立认知", 12, dimInk);
		soulLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		box.AddChild(soulLabel);
		var hint = MakeLabel("洞见投入后不可撤回；修习值会加入相关行动的 Tag 总量。", 12, dimInk);
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		box.AddChild(hint);
		disciplineList = new VBoxContainer();
		disciplineList.AddThemeConstantOverride("separation", 4);
		box.AddChild(disciplineList);
		return panel;
	}

	private Control BuildTextPanel(string title, out RichTextLabel text, float stretch)
	{
		var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = stretch };
		panel.AddThemeStyleboxOverride("panel", Flat(panelColor, new("2a3040"), 9, 1));
		var margin = InnerMargin(12);
		panel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 7);
		margin.AddChild(box);
		box.AddChild(MakeLabel(title, 16, gold));
		text = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		text.AddThemeColorOverride("default_color", ink);
		text.AddThemeFontSizeOverride("normal_font_size", 13);
		box.AddChild(text);
		return panel;
	}

	private void BuildOverlay()
	{
		overlay = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.76f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(overlay);
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		overlay.AddChild(center);
		overlayPanel = new PanelContainer { CustomMinimumSize = new(620, 350) };
		overlayPanel.AddThemeStyleboxOverride("panel", Flat(new("171b25"), gold, 12, 2));
		center.AddChild(overlayPanel);
		overlay.Visible = false;
	}

	private void RunAction(string action)
	{
		if (gameEnded || awaitingReaction || playerBusy)
			return;

		Action? resolution = action switch
		{
			"study" => PrepareStudy(),
			"explore" => PrepareExplore(),
			"verify" => PrepareVerify(),
			"anchor" => PrepareAnchor(),
			"rite" => PrepareRite(),
			_ => null,
		};
		if (resolution is null)
			return;

		var duration = ActionDuration(action);
		var intent = new Intent(playerCharacter, playerSoul, BuildActionEffect(action), duration, resolution, ActionName(action));
		if (!AuthorizeIntent(intent, "主回合"))
			return;

		playerBusy = true;
		var playerEvent = new ScheduledAction($"玩家：{intent.Label}", world, () =>
		{
			intent.Resolve();
			if (!awaitingReaction && !gameEnded)
				CompletePlayerAction();
		});
		timeline.add(new SortedDictionary<float, Entity> { [intent.MotionCost] = playerEvent });
		selected.Clear();
		ContinueTimeline();
	}

	private Effect BuildActionEffect(string action)
	{
		var inputs = selected.Cast<object>().ToArray();
		return action switch
		{
			"rite" => new Effect("seal", playerCharacter, LocationCards.First(card => card.name == "西门")),
			_ => new Effect(action, (object[])[playerCharacter, .. inputs]),
		};
	}

	private bool AuthorizeIntent(Intent intent, string context)
	{
		if (!wordSpirits.allows(intent.Effect))
		{
			AddLog($"{context}意图“{intent.Label}”被已生效言灵约束阻止。", red);
			RefreshAll();
			return false;
		}
		var decision = ruleSystem.evaluate_intent(intent.Mind, intent.Effect, ruleEnvironment);
		if (decision.Basis is not null)
		{
			if (context == "主回合")
				mainRuleInfluences++;
			AddLog($"{context}规则评价“{intent.Label}”：{decision.Reason}，决策值 {decision.Score}。普通规则不代替主体选择。",
				decision.Allowed ? dimInk : red);
		}
		return true;
	}

	private Action? PrepareStudy()
	{
		if (!Exactly(1, "研读需要投入 1 张卡牌。")) return null;
		var card = selected.First();
		if (!(card.Has("Record") || card.Has("Knowledge"))) return Invalid("只能研读记录或知识。");
		if (card.Studied) return Invalid("这张卡已经研读完成。可将它用于校验或其他行动。");

		return () =>
		{
			card.Studied = true;
			insight++;
			AddLog($"研读《{card.name}》：获得 1 点洞见。", gold);
			switch (card.name)
			{
				case "《夜班访客守则》":
					stability--;
					exposure++;
					var claim = playerSoul.consider(falseMirrorRule, card, 1);
					AddJournal("[color=#b85c62]记录声称[/color]　镜中人询问姓名时，必须如实回答。");
					AddJournal("[color=#b85c62]记录声称[/color]　西门是唯一可以安全离开的出口。");
					AddLog($"文字进入 Soul 的认知，但来源没有发行担保；规则{(claim.Accepted ? "被接受" : "未被接受")}。认知冲突使稳定 -1。", red);
					break;
				case "入库调拨单":
					AddJournal("[color=#d7d1c1]证据[/color]　守则的纸张今晚才从空白表格调入，落款却写着十年前。可与守则校验。");
					break;
				case "镜廊回声记录":
					AddJournal("[color=#d7d1c1]证据[/color]　回声只重复社会身份，从未重复亲属使用的乳名。可与旧值班名册校验。");
					break;
				case "旧值班名册":
					AddJournal("[color=#d7d1c1]证据[/color]　十年前有一名没有姓名的值班员，只留下称号“借名者”。");
					break;
				case "伪造规则的批注":
					playerSoul.reject(falseMirrorRule);
					var verified = playerSoul.consider(trueMirrorRule, card, 2);
					var sealInstruction = playerSoul.consider(trueSealRule, card, 2);
					AddJournal("[color=#5d9c91]客观结论[/color]　普通记录没有发行担保。服从伪规则只会主动交出名字。");
					AddLog($"Soul 撤回伪规则，并{(verified.Accepted && sealInstruction.Accepted ? "接受" : "保留怀疑")}由独立证据担保的镜廊与西门规则。", teal);
					break;
				case "母亲的来信":
					AddJournal("[color=#d7d1c1]身份锚点[/color]　乳名连接了记忆、关系和自我确认；它可作为高价值献礼，但会被仪式永久消耗。");
					break;
			}
		};
	}

	private Action? PrepareExplore()
	{
		if (selected.Count is < 1 or > 2)
			return Invalid("探查需要投入 1 个地点，并可额外投入 1 张工具或知识。");
		var locations = selected.Where(card => card.Has("Location")).ToArray();
		if (locations.Length != 1)
			return Invalid("探查槽必须恰好包含 1 个地点实体。");
		var location = locations[0];
		var method = selected.FirstOrDefault(card => card != location);
		var route = world.find_route(playerCharacter.parent!, location);
		if (route is null)
			return Invalid($"从“{playerCharacter.parent!.name}”无法到达“{location.name}”。空间图中不存在路线。");
		Action AtLocation(Action action) => () =>
		{
			MoveCharacter(route, location);
			action();
		};
		if (location.Has("Recovered"))
		{
			return AtLocation(() =>
			{
				stability = Math.Min(8, stability + 1);
				AddLog($"你再次探查“{location.name}”。地点状态没有变化，也没有凭空生成新物品；稳定 +1。", dimInk);
			});
		}

		return location.name switch
		{
			"文书库" when AspectTotal(selected, "Form") + AspectTotal(selected, "Threshold") >= 2 => AtLocation(() =>
			{
				exposure++;
				RevealLocationContent(location);
				AddLog($"你用《{method?.name ?? "徒手观察"}》辨出伪造门框，从文书库取得“入库调拨单”。", teal);
			}),
			"文书库" => FailedExplore(location, route, "文书库需要投入能提供形质与门隙合计 2 的工具。"),
			"镜廊" when method?.Has("Catalyst") == true &&
				AspectTotal(selected, "Law") + AspectTotal(selected, "Mind") >= 2 => AtLocation(() =>
			{
				exposure += 2;
				ShowMirrorReaction(location);
			}),
			"镜廊" => FailedExplore(location, route, "镜廊需要催化工具，并要求投入组合的律令与心识合计 2。"),
			"人事室" when method?.Studied == true && method.Has("Truth") &&
				AspectTotal(selected, "Law") + AspectTotal(selected, "Mind") >= 4 => AtLocation(() =>
			{
				exposure++;
				RevealLocationContent(location);
				AddLog("已求证的知识使伪造告示失效。你从人事室取得“旧值班名册”。", teal);
			}),
			"人事室" => FailedExplore(location, route, "人事室需要已研读的 Truth 知识，且组合的律令与心识合计 4。"),
			"西门" when method?.Has("Authority") == true && AspectTotal(selected, "Name") >= 3 => AtLocation(() =>
			{
				exposure += 2;
				RevealLocationContent(location);
				AddJournal("[color=#5d9c91]客观结论[/color]　西门是异常入口。封条必须留在门上，不能把西门当作出口。");
				AddLog("授权工具与名契强度使西门承认你的操作权限。你取得“西门闭馆封条”。", teal);
			}),
			"西门" => FailedExplore(location, route, "西门需要 Authority 工具与名契 3。地点不会因剧情阶段自动开启。"),
			_ => FailedExplore(location, route, "这个地点暂时没有可执行的探查规则。"),
		};
	}

	private Action? PrepareVerify()
	{
		if (!Exactly(2, "校验需要投入 2 张卡牌。")) return null;
		var pair = selected.ToArray();
		if (pair.Any(card => !card.Studied || !(card.Has("Record") || card.Has("Knowledge"))))
			return Invalid("校验只接受 2 张已经研读的记录或知识。");

		var names = pair.Select(card => card.name).ToHashSet();
		if (names.SetEquals(["《夜班访客守则》", "入库调拨单"]) && !HasCard("伪造规则的批注"))
		{
			return () =>
			{
				exposure++;
				insight++;
				AddCard("伪造规则的批注", "知识 · 已求证", "守则缺乏发行担保；其中关于回答姓名与西门出口的指令均为诱导。",
					new() { ["Knowledge"] = 1, ["Law"] = 2, ["Mind"] = 2, ["Truth"] = 1, ["Guarantee"] = 2 });
				AddJournal("[color=#5d9c91]已求证[/color]　访客守则是异常伪造的 Claim，不具有客观强制力。沉默不会违规。");
				AddLog("两条独立证据形成可重复结论。获得“伪造规则的批注”和 1 点洞见。", teal);
			};
		}

		if (names.SetEquals(["镜廊回声记录", "旧值班名册"]) && !HasCard("借名者真称"))
		{
			return () =>
			{
				exposure += 2;
				insight++;
				AddCard("借名者真称", "知识 · 高精度寻址", "它没有自己的姓名；“借名者”是由职务、行为历史与受害者关系共同固定的真称。",
					new() { ["Knowledge"] = 1, ["Name"] = 3, ["Mind"] = 2, ["TrueName"] = 1 });
				AddJournal("[color=#5d9c91]已求证[/color]　真称不是单一字符串，而是“无名值班员 + 借用访客身份 + 困于馆舍”的连续关系。");
				AddLog("回声、职位与行为历史闭合为同一目标。获得“借名者真称”和 1 点洞见。", teal);
			};
		}

		return () =>
		{
			insight++;
			stability = Math.Min(8, stability + 1);
			AddLog("两份材料没有形成新的客观结论，但排除了一个错误解释。洞见 +1，稳定 +1。", dimInk);
		};
	}

	private Action? PrepareAnchor()
	{
		if (!Exactly(1, "锚定需要投入 1 张卡牌。")) return null;
		var card = selected.First();
		if (Total(card, "Bond") + Total(card, "Preservation") < 2)
			return Invalid("锚定需要缘系与存续合计 2。修习值也会计入。");
		return () =>
		{
			anchorUses++;
			stability = Math.Min(8, stability + (anchorUses == 1 ? 3 : 1));
			if (anchorUses == 1) identity = Math.Min(6, identity + 1);
			exposure = Math.Max(0, exposure - 1);
			AddLog($"你借《{card.name}》重新连接自我、记忆与关系。稳定恢复，暴露 -1。", teal);
		};
	}

	private Action? PrepareRite()
	{
		if (!Exactly(3, "封印仪式需要恰好 3 张卡牌。")) return null;
		var offered = selected.ToArray();
		var missing = new List<string>();
		if (!offered.Any(card => card.Has("TrueName"))) missing.Add("真称");
		if (!offered.Any(card => card.Has("Seal"))) missing.Add("封条");
		if (!offered.Any(card => card.Has("Offering"))) missing.Add("身份献礼");
		if (playerCharacter.parent?.name != "西门") missing.Add("角色必须实际位于西门");
		foreach (var (aspect, threshold) in new[] { ("Name", 4), ("Law", 4), ("Threshold", 3), ("Preservation", 2) })
		{
			var value = offered.Sum(card => card.getTag(aspect)) + disciplines.GetValueOrDefault(aspect);
			if (value < threshold) missing.Add($"{AspectLabels[aspect]} {value}/{threshold}");
		}
		if (missing.Count > 0)
			return Invalid($"仪式结构尚未闭合：{string.Join("、", missing)}。");

		return () =>
		{
			var offering = offered.First(card => card.Has("Offering"));
			var trueName = offered.First(card => card.Has("TrueName"));
			var westGate = LocationCards.First(card => card.name == "西门");
			ruleEnvironment.alias("identity_offering", offering);
			ruleEnvironment.alias("true_name", trueName);
			var offer = new WordSpiritOffer(
				borrowerCharacter,
				offering,
				[trueName, offered.First(card => card.Has("Seal"))],
				6,
				new Dictionary<string, int> { ["Offering"] = 1, ["Preservation"] = 2 }
			);
			var binding = wordSpirits.sign("西门守名契", sealingWordSpirit, playerSoul, offer);
			if (binding is null)
			{
				AddLog("言灵拒绝签约：献礼与保存身份的效果不匹配，或支付后无法维持主体。", red);
				return;
			}
			AddLog($"言灵签约：献礼《{offering.name}》立即支付，约束“真称绑定＋西门封闭”成立。", gold);
			ApplyWorldEffect(new Effect("bound_by", borrowerCharacter, trueName));
			ApplyWorldEffect(new Effect("seal", playerCharacter, westGate));
			ApplyWorldEffect(new Effect("keep_holding", playerCharacter, "identity"));
			var fired = wordSpirits.trigger().Contains(binding);
			if (!fired)
			{
				AddLog("言灵已建立约束，但客观触发事实尚未闭合。", red);
				return;
			}
			EndGame(true, "封印成立", "真称完成寻址，闭馆封条固定空间边界，匹配的身份献礼已经支付。言灵改变了客观事实：借名者实体被移出馆舍，而你的 Soul 仍与身体及身份连续性相连。");
		};
	}

	private Action FailedExplore(GameCard location, Route route, string reason) => () =>
	{
		MoveCharacter(route, location);
		stability--;
		exposure++;
		AddLog($"探查失败：{reason} 稳定 -1，暴露 +1。", red);
	};

	private void ShowMirrorReaction(GameCard location)
	{
		var challenge = new Expression("challenged_by", playerCharacter, location);
		ruleEnvironment.assert(challenge);
		playerSoul.observe(challenge);
		var answer = new Effect("answer", playerCharacter, "identity");
		var decision = playerSoul.decide([answer], ruleSystem, ruleEnvironment,
			intent => intent.Operator == "remain_silent" ? 5 : 0);
		requiredIntentGenerated = decision.Intent.Operator == "remain_silent";
		awaitingReaction = true;
		overlay.Visible = true;
		ReplaceChildren(overlayPanel);
		var margin = InnerMargin(26);
		overlayPanel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 16);
		margin.AddChild(box);
		box.AddChild(MakeLabel("反应子回合：镜廊询名", 24, gold));
		var choice = decision.Intent.Operator == "remain_silent" ? "保持沉默" : "回答姓名";
		var text = MakeLabel($"所有镜面同时出现你的背影。它用你的声音问：\n\n“夜班访客，请说出你的姓名。”\n\nSoul 决策：{choice}（{decision.Reason}）。规则只影响判断，你仍可违背它。", 17, ink);
		text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		text.SizeFlagsVertical = SizeFlags.ExpandFill;
		box.AddChild(text);
		box.AddChild(MakeButton("说出入馆证上的姓名", () => FinishMirrorReaction(location, false), new("512d34")));
		box.AddChild(MakeButton("敲击铜铃，保持沉默", () => FinishMirrorReaction(location, true), new("24433f")));
	}

	private void FinishMirrorReaction(GameCard location, bool resisted)
	{
		var effect = resisted
			? new Effect("remain_silent", playerCharacter, location)
			: new Effect("answer", playerCharacter, "identity");
		var label = resisted ? "保持沉默" : "回答姓名";
		var intent = new Intent(playerCharacter, playerSoul, effect, 0, () => { }, label);
		if (!AuthorizeIntent(intent, "反应子回合"))
			return;
		overlay.Visible = false;
		awaitingReaction = false;
		ruleEnvironment.assert_effect(effect);
		ruleEnvironment.retract(new Expression("challenged_by", playerCharacter, location));
		var record = location.children.Keys.OfType<GameCard>().First();
		if (resisted)
		{
			AddLog("你没有接受伪规则。铜铃把模仿暴露为延迟回声；取得“镜廊回声记录”。", teal);
		}
		else
		{
			identity -= 2;
			exposure += 2;
			record.changeTag("Tainted", 1);
			AddLog("你主动交出了制度姓名。伪规则没有强迫你，但你的选择建立了目标绑定。身份 -2。", red);
		}
		RevealLocationContent(location);
		CompletePlayerAction();
		RefreshAll();
		ContinueTimeline();
	}

	private void ContinueTimeline()
	{
		while (playerBusy && !awaitingReaction && !gameEnded && timeline.motionQueue.Count > 0)
		{
			RefreshQueue();
			timeline.next();
			RefreshAll();
		}
		RefreshQueue();
	}

	private void CompletePlayerAction()
	{
		turn++;
		playerBusy = false;
		CheckEndState();
	}

	private bool ResolveAnomalyPulse()
	{
		anomalyPulses++;
		AddLog($"行动值 {timeline.currentMotionValue:0.##}：借名者完成第 {anomalyPulses} 次常驻行动。", dimInk);
		var exposedFact = new Expression("exposed", playerCharacter, "high");
		if (exposure >= 4)
			ruleEnvironment.assert(exposedFact);
		else
			ruleEnvironment.retract(exposedFact);

		List<Effect> candidates = [];
		var replacementReady = new Expression("ready_to_replace", borrowerCharacter);
		if (anomalyPulses % 3 == 0 || exposure >= 10 && anomalyPulses % 4 == 0)
			ruleEnvironment.assert(replacementReady);
		else
			ruleEnvironment.retract(replacementReady);
		if (anomalyPulses % 2 == 0 && exposure >= 4)
			candidates.Add(new Effect("erode", playerCharacter, "stability"));
		if (ruleEnvironment.evaluate(replacementReady))
			candidates.Add(new Effect("replace", playerCharacter, "identity"));
		if (candidates.Count > 0)
		{
			var decision = borrowerSoul.decide(candidates, ruleSystem, ruleEnvironment);
			var intent = new Intent(borrowerCharacter, borrowerSoul, decision.Intent, 0, () => { }, "借名者异常行动");
			if (!AuthorizeIntent(intent, "异常主回合"))
				return !gameEnded;
			if (decision.Intent.Operator == "erode")
			{
				stability--;
				AddLog($"借名者的 Soul 依“{decision.Reason}”选择侵蚀解释结构。稳定 -1。", red);
			}
			else
			{
				identity--;
				AddLog($"借名者的 Soul 依“{decision.Reason}”选择覆盖身份记录。身份 -1。", red);
			}
		}
		CheckEndState();
		return !gameEnded;
	}

	private void CheckEndState()
	{
		if (identity <= 0)
			EndGame(false, "身份被替代", "你的身体仍在行动，馆舍记录和你的自我模型却已经一致认定：你是下一任无名值班员。");
		else if (stability <= 0)
			EndGame(false, "认知结构崩解", "你仍能看见卡牌，却无法再判断记录、信念和客观规则之间的区别。");
		else if (turn >= 15)
			EndGame(false, "闭馆广播结束", "第十五次广播完成了社会记录的覆盖。你没能在期限内提交封印仪式。");
	}

	private void EndGame(bool won, string title, string detail)
	{
		gameEnded = true;
		playerBusy = false;
		playerWon = won;
		selected.Clear();
		overlay.Visible = true;
		ReplaceChildren(overlayPanel);
		var margin = InnerMargin(28);
		overlayPanel.AddChild(margin);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 18);
		margin.AddChild(box);
		box.AddChild(MakeLabel(won ? $"结局：{title}" : $"失败：{title}", 26, won ? teal : red));
		var label = MakeLabel(detail, 17, ink);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.SizeFlagsVertical = SizeFlags.ExpandFill;
		box.AddChild(label);
		box.AddChild(MakeLabel($"最终状态　回合 {turn}/15　稳定 {stability}/8　身份 {identity}/6　暴露 {exposure}/12", 14, dimInk));
		box.AddChild(MakeButton("重新开始", StartGame, won ? new("24433f") : new("512d34")));
	}

	private void RunSmokePath()
	{
		SelectAndRun("study", "《夜班访客守则》");
		SelectAndRun("explore", "文书库", "库房粉笔");
		SelectAndRun("study", "入库调拨单");
		SelectAndRun("verify", "《夜班访客守则》", "入库调拨单");
		SpendInsight("Name");
		SpendInsight("Law");
		SpendInsight("Threshold");
		SelectAndRun("study", "伪造规则的批注");
		SelectAndRun("explore", "镜廊", "铜质呼铃");
		FinishMirrorReaction(LocationCards.First(card => card.name == "镜廊"), true);
		SelectAndRun("study", "镜廊回声记录");
		SelectAndRun("explore", "人事室", "伪造规则的批注");
		SelectAndRun("study", "旧值班名册");
		SelectAndRun("verify", "镜廊回声记录", "旧值班名册");
		SelectAndRun("explore", "西门", "褪色入馆证");
		SelectAndRun("rite", "借名者真称", "西门闭馆封条", "母亲的来信");

		var constraintBlocksEscape = !wordSpirits.allows(new Effect("leave", borrowerCharacter, world));
		var triggerlessSupported = new RuleParser().ParseWordSpirit(
			"WORD SPIRIT:\nCONTRACT COST:\n- visitor sacrifices identity_offering\n" +
			"ACTUAL EFFECTS IN ORDER:\n1. visitor keeps_holding identity").Rules.Length == 0;
		var corePassed = playerCharacter.parent?.name == "西门" && borrowerCharacter.parent is null &&
			playerSoul.Beliefs.Count == 3 && playerSoul.Beliefs.Count(belief => belief.Accepted) == 2 &&
			mainRuleInfluences == 1 && wordSpirits.Bindings.Count == 1 && wordSpirits.Bindings[0].Fired &&
			requiredIntentGenerated && triggerlessSupported && constraintBlocksEscape && wordSpirits.BlockedIntents > 0;
		if (playerWon && corePassed)
		{
			var result = $"PROTOTYPE_SMOKE_PASS turn={turn} stability={stability} identity={identity} exposure={exposure} core=PASS";
			StartGame();
			var restartPassed = turn == 0 && InventoryCards.Count() == 5 && LocationCards.Count() == 4 &&
				timeline.motionQueue.Count == 1 && !gameEnded && playerSoul.Characters.Count == 1 &&
				world.find_route(LocationCards.First(), LocationCards.Last()) is not null;
			result += restartPassed ? " restart=PASS" : " restart=FAIL";
			using var output = Godot.FileAccess.Open("res://smoke-result.txt", Godot.FileAccess.ModeFlags.Write);
			output.StoreString(result);
			GD.Print(result);
			GetTree().Quit(restartPassed ? 0 : 2);
		}
		else
		{
			GD.PushError($"PROTOTYPE_SMOKE_FAIL: won={playerWon} location={playerCharacter.parent?.name} " +
				$"borrowerRemoved={borrowerCharacter.parent is null} beliefs={playerSoul.Beliefs.Count}/" +
				$"{playerSoul.Beliefs.Count(belief => belief.Accepted)} mainRule={mainRuleInfluences} " +
				$"requiredGenerated={requiredIntentGenerated} bindings={wordSpirits.Bindings.Count} " +
				$"fired={wordSpirits.Bindings.FirstOrDefault()?.Fired} blocked={wordSpirits.BlockedIntents} " +
				$"constraintBlocks={constraintBlocksEscape}");
			GetTree().Quit(2);
		}
	}

	private void SelectAndRun(string action, params string[] names)
	{
		selected.Clear();
		foreach (var name in names)
			selected.Add(VisibleCards.First(card => card.name == name));
		RunAction(action);
	}

	private void SpendInsight(string aspect)
	{
		if (insight <= 0 || gameEnded) return;
		insight--;
		disciplines[aspect]++;
		AddLog($"你把 1 点洞见投入{AspectLabels[aspect]}。该值会加入对应行动检定。", gold);
		RefreshAll();
	}

	private void ToggleCard(GameCard card)
	{
		if (gameEnded || awaitingReaction) return;
		if (!selected.Remove(card))
		{
			if (selected.Count >= 3)
			{
				AddLog("投入槽最多容纳 3 张卡牌。", red);
				RefreshLog();
				return;
			}
			selected.Add(card);
		}
		RefreshAll();
	}

	private void RefreshAll()
	{
		resourceLabel.Text = $"稳定 {Math.Max(0, stability)}/8　身份 {Math.Max(0, identity)}/6　暴露 {exposure}/12　回合 {turn}/15　位置 {playerCharacter.parent?.name}　时间 {timeline.currentMotionValue:0.##}";
		var unresolved = LocationCards.Where(card => !card.Has("Recovered")).Select(card => card.name).ToArray();
		objectiveLabel.Text = unresolved.Length == 0
			? "所有地点内容都已取出。使用证据求证真称，再构造封印仪式。"
			: $"未完成地点：{string.Join("、", unresolved)}。探查槽接受地点实体，以及一张可选的工具或知识。";
		selectedLabel.Text = selected.Count == 0 ? "投入槽：未选择" : $"投入槽：{string.Join("　/　", selected.Select(card => card.name))}";
		insightLabel.Text = $"可用洞见　{insight}";
		var accepted = playerSoul.Beliefs.Count(belief => belief.Accepted);
		soulLabel.Text = $"Soul：{playerSoul.name}　绑定身体 {playerSoul.Characters.Count}　观察 {playerSoul.Observations.Count}　规则 {accepted}/{playerSoul.Beliefs.Count} 已接受";
		RefreshLocations();
		RefreshHand();
		RefreshDisciplines();
		RefreshJournal();
		RefreshLog();
		RefreshQueue();
	}

	private void RefreshHand()
	{
		ReplaceChildren(hand);
		foreach (var card in InventoryCards)
		{
			hand.AddChild(CreateCardButton(card, new(188, 142)));
		}
	}

	private void RefreshLocations()
	{
		ReplaceChildren(locationBoard);
		foreach (var location in LocationCards)
			locationBoard.AddChild(CreateCardButton(location, new(172, 96)));
	}

	private Button CreateCardButton(GameCard card, Vector2 minimumSize)
	{
		var chosen = selected.Contains(card);
		var button = new Button
		{
			Text = CardText(card),
			TooltipText = CardTooltip(card),
			CustomMinimumSize = minimumSize,
			Alignment = HorizontalAlignment.Left,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", ink);
		var baseColor = card.Has("Location") ? new Color("252b25")
			: card.Has("Knowledge") ? new Color("1c3030")
			: card.Has("Record") ? new Color("272432") : new Color("202733");
		button.AddThemeStyleboxOverride("normal", Flat(baseColor, chosen ? gold : new Color("343b4b"), 8, chosen ? 3 : 1));
		button.AddThemeStyleboxOverride("hover", Flat(baseColor.Lightened(0.07f), gold, 8, 2));
		button.AddThemeStyleboxOverride("pressed", Flat(baseColor.Darkened(0.08f), teal, 8, 3));
		button.Pressed += () => ToggleCard(card);
		return button;
	}

	private void RefreshDisciplines()
	{
		ReplaceChildren(disciplineList);
		foreach (var aspect in disciplines.Keys)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			var label = MakeLabel($"{AspectLabels[aspect]}　{disciplines[aspect]}", 14, ink);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(label);
			var key = aspect;
			var add = MakeButton("＋", () => SpendInsight(key), new("293246"));
			add.Disabled = insight <= 0 || gameEnded;
			add.CustomMinimumSize = new(42, 30);
			row.AddChild(add);
			disciplineList.AddChild(row);
		}
	}

	private void RefreshJournal() => journalText.Text = string.Join("\n\n", journal.TakeLast(12));
	private void RefreshLog()
	{
		logText.Text = string.Join("\n\n", log.TakeLast(10));
		logText.ScrollToLine(Math.Max(0, logText.GetLineCount() - 1));
	}

	private void RefreshQueue()
	{
		queueLabel.Text = timeline.motionQueue.Count == 0
			? $"行动值 {timeline.currentMotionValue:0}　队列：空"
			: $"行动值 {timeline.currentMotionValue:0.##}　队列：{string.Join(" → ", timeline.motionQueue.Select(item => $"{item.Key:0.##}:{item.Value.name}"))}";
	}

	private GameCard AddCard(string name, string subtitle, string description, Dictionary<string, int> tags) =>
		new(name, inventory, subtitle, description, tags);

	private GameCard AddLocation(string name, string subtitle, string description, Vector3 position)
	{
		var location = new GameCard(name, world, subtitle, description, new() { ["Location"] = 1, ["Domain"] = 1 });
		location.set_relative_position(position);
		return location;
	}

	private IEnumerable<GameCard> InventoryCards => inventory.children.Keys
		.OfType<GameCard>().OrderBy(card => inventory.children[card]);

	private IEnumerable<GameCard> LocationCards => world.children.Keys
		.OfType<GameCard>().Where(card => card.Has("Location")).OrderBy(card => world.children[card]);

	private IEnumerable<GameCard> VisibleCards => LocationCards.Concat(InventoryCards);

	private bool HasCard(string name) => InventoryCards.Any(card => card.name == name);
	private int Total(GameCard card, string aspect) => card.getTag(aspect) + disciplines.GetValueOrDefault(aspect);
	private int AspectTotal(IEnumerable<GameCard> inputs, string aspect) =>
		inputs.Sum(card => card.getTag(aspect)) + disciplines.GetValueOrDefault(aspect);

	private void RevealLocationContent(GameCard location)
	{
		var content = location.children.Keys.OfType<GameCard>().FirstOrDefault();
		if (content is null)
			return;
		content.set_parent(inventory);
		location.changeTag("Recovered", 1);
	}

	private void MoveCharacter(Route route, GameCard destination)
	{
		var origin = playerCharacter.parent!;
		if (origin == destination)
			return;
		ruleEnvironment.retract(new Expression("located_at", playerCharacter, origin));
		playerCharacter.set_parent(destination);
		ruleEnvironment.assert(new Expression("located_at", playerCharacter, destination));
		AddLog($"空间导航：{route.describe()}，路线边权 {route.Cost:0.##}。", dimInk);
	}

	private bool ValidateIdentityOffering(Effect effect)
	{
		if (effect.Arguments.Length < 2 || ruleEnvironment.resolve(effect.Arguments[1]) is not GameCard offering)
			return false;
		return offering.parent == inventory && offering.Has("Offering") &&
			offering.getTag("Bond") + offering.getTag("Preservation") >= 2 && identity > 1;
	}

	private void ApplyIdentityOffering(Effect effect)
	{
		var offering = (GameCard)ruleEnvironment.resolve(effect.Arguments[1]);
		offering.set_parent(null);
		identity--;
		ruleEnvironment.assert_effect(effect);
	}

	private void ApplyWorldEffect(Effect effect)
	{
		if (effect.Operator == "seal" && effect.Arguments.Length >= 2 &&
			ruleEnvironment.resolve(effect.Arguments[1]) is Entity target)
			target.changeTag("sealed", 1);
		ruleEnvironment.assert_effect(effect);
	}

	private bool ValidateRemoval(Effect effect) => effect.Arguments.Length >= 2 &&
		ruleEnvironment.resolve(effect.Arguments[1]) is Entity target && target.parent is not null;

	private void ApplyRemoval(Effect effect)
	{
		var target = (Entity)ruleEnvironment.resolve(effect.Arguments[1]);
		target.set_parent(null);
		ruleEnvironment.assert_effect(effect);
	}

	private static void ClearEntity(Entity entity)
	{
		foreach (var child in entity.children.Keys.ToArray())
		{
			ClearEntity(child);
			child.set_parent(null);
		}
	}

	private float ActionDuration(string action) => action switch
	{
		"anchor" => 60,
		"explore" => selected.FirstOrDefault(card => card.Has("Location")) is { } location &&
			world.find_route(playerCharacter.parent!, location) is { } route ? 50 + route.Cost * 15 : 80,
		"verify" => 90,
		"study" => 100,
		"rite" => 140,
		_ => 100,
	};

	private bool Exactly(int count, string message)
	{
		if (selected.Count == count) return true;
		Invalid(message);
		return false;
	}

	private Action? Invalid(string message)
	{
		AddLog(message, red);
		RefreshLog();
		return null;
	}

	private void AddJournal(string value)
	{
		if (!journal.Contains(value)) journal.Add(value);
	}

	private void AddLog(string value, Color? color = null)
	{
		var actual = color ?? ink;
		log.Add($"[color=#{actual.ToHtml(false)}]{value}[/color]");
	}

	private string CardText(GameCard card)
	{
		var tags = AspectNames
			.Where(tag => card.getTag(tag) > 0)
			.Select(tag => $"{AspectLabels[tag]} {card.getTag(tag)}");
		var marker = card.Has("Location")
			? card.Has("Recovered") ? "■ 已取出" : "□ 未探明"
			: card.Studied ? "◆ 已研读" : "◇ 未研读";
		return $"{marker}　{card.name}\n{card.Subtitle}\n\n{string.Join("　", tags)}";
	}

	private string CardTooltip(GameCard card)
	{
		var tags = card.tagValues.Select(pair => $"{pair.Key} {pair.Value}");
		var route = card.Has("Location") ? world.find_route(playerCharacter.parent!, card) : null;
		var navigation = route is null ? "" : $"\n路线：{route.describe()}（边权 {route.Cost:0.##}）";
		return $"{card.Description}{navigation}\n\nTag：{string.Join(" · ", tags)}";
	}

	private static string ActionName(string action) => action switch
	{
		"study" => "研读", "explore" => "探查", "verify" => "校验", "anchor" => "锚定", "rite" => "封印仪式", _ => action,
	};

	private Button ActionButton(string title, string detail, Action callback, bool primary = false)
	{
		var button = MakeButton($"{title}\n{detail}", callback, primary ? new("4b3e29") : new("252b38"));
		button.CustomMinimumSize = new(152, 82);
		button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		button.AddThemeFontSizeOverride("font_size", 13);
		return button;
	}

	private Button MakeButton(string text, Action callback, Color color)
	{
		var button = new Button { Text = text };
		button.AddThemeColorOverride("font_color", ink);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", Flat(color, new("394153"), 7, 1));
		button.AddThemeStyleboxOverride("hover", Flat(color.Lightened(0.1f), gold, 7, 1));
		button.AddThemeStyleboxOverride("pressed", Flat(color.Darkened(0.08f), teal, 7, 2));
		button.Pressed += callback;
		return button;
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static MarginContainer InnerMargin(int amount)
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", amount);
		margin.AddThemeConstantOverride("margin_right", amount);
		margin.AddThemeConstantOverride("margin_top", amount);
		margin.AddThemeConstantOverride("margin_bottom", amount);
		return margin;
	}

	private static StyleBoxFlat Flat(Color fill, Color border, int radius = 6, int width = 1) => new()
	{
		BgColor = fill,
		BorderColor = border,
		BorderWidthLeft = width,
		BorderWidthTop = width,
		BorderWidthRight = width,
		BorderWidthBottom = width,
		CornerRadiusTopLeft = radius,
		CornerRadiusTopRight = radius,
		CornerRadiusBottomLeft = radius,
		CornerRadiusBottomRight = radius,
		ContentMarginLeft = 10,
		ContentMarginRight = 10,
		ContentMarginTop = 8,
		ContentMarginBottom = 8,
	};

	private static void ReplaceChildren(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}
