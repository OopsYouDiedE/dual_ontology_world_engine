using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Core.Rules;
using System.Runtime.CompilerServices;
using static DualOntologyWorldEngine.Core.Rules.AST;

namespace DualOntologyWorldEngine.Examples.ZooSimulator;

/// <summary>文书是信息。条目只评价已存在的行动，绝不生成行动效果或裁定结局。</summary>
public sealed partial class ZooRules
{
    public sealed record Clause(string Key, string Text, string OperationalForm, Entity Publication, Func<Character, Intent, double>? Advice);
    private readonly ZooWorld w;
    private readonly ConditionalWeakTable<Intent, Dictionary<Clause,double>> judgments = new();
    public List<Clause> Clauses { get; } = [];
    public Dictionary<string, Rule> Rules { get; } = [];
    public List<Entity> Publications { get; } = [];
    public string CurrentPreset { get; private set; } = "standard";
    private readonly Dictionary<string, string[]> texts;

    public static readonly Dictionary<string, string> StandardOperationalForms = new()
    {
        ["Visitor.1"] = "[行动趋向] 视线内出现出逃兔子时，回避接触、退向主步道并向员工报告",
        ["Visitor.2"] = "[路径抉择] 猿区目击双路异常时，强制选左街，绝对回避右街",
        ["Visitor.3"] = "[认知校验] 目击兔耳大象时，回避接触并前往核对大象告示牌",
        ["Visitor.4"] = "[物资禁忌] 货架出现兔子血时，绝对禁止购买/携带",
        ["Visitor.5"] = "[环境禁忌] 孤身一人时禁止进入兔区树荫灌木",
        ["Visitor.6"] = "[认知防御] 回避海洋馆大门，拒绝购买海洋馆门票",
        ["Visitor.7"] = "[离场避险] 目击或误入海洋馆后立刻退回主园区并汇报",
        ["Visitor.8"] = "[交互禁忌] 严禁投喂兔子群",
        ["Visitor.9"] = "[避难指引] 听见异常笑声时撕下虚线地图纸，禁止遗弃或转交",
        ["Visitor.10"] = "[迷失求生] 迷路违规时，白狮咆哮走向正规出口，否则向蓝衣员工求助",
        ["Visitor.11"] = "[危机预警] 发现白狮数量超过4头时，禁止走向正规出口，立刻报告",
        ["Visitor.12"] = "[玩偶禁忌] 成年人禁止持有兔子玩偶；儿童超时遗弃",
        ["Visitor.13"] = "[特征避险] 目击佩戴兔耳饰者时，立刻离开大象区",
        ["Visitor.14"] = "[求生庇护] 极度恐惧时前往白狮假山寻求庇护",
        ["Visitor.15"] = "[防伪甄别] 出现同伴分离或多名向导时，停止盲从",
        ["Staff.1"] = "[兔子出逃] 禁止徒手抓捕，引诱前往白狮假山交由白狮处置",
        ["Staff.2"] = "[分流处置] 发生双路报告时疏散游客至左街，封锁猿区",
        ["Staff.3"] = "[异常处置] 回避兔耳象，核对标牌并坚定人类认知",
        ["Staff.4"] = "[货架巡查] 定期巡查冷柜，收缴并封存兔子血",
        ["Staff.5"] = "[树荫维护] 发现浓密树荫或定期巡视时修剪兔区灌木",
        ["Staff.6"] = "[言论禁忌] 禁止与游客谈论海洋馆，保持沉默",
        ["Staff.7"] = "[空间禁忌] 未被兔子咬伤前严禁进入海洋馆",
        ["Staff.8"] = "[饲养职责] 定期投喂兔子群，未到期不喂",
        ["Staff.9"] = "[虚线地图] 听见异常笑声撕下地图纸丢弃至大象草坪",
        ["Staff.10"] = "[护送职责] 接受迷路游客求援并护送至白狮假山",
        ["Staff.11"] = "[伪白狮驱逐] 发现第5头白狮时，取兔子血泼洒驱逐",
        ["Staff.12"] = "[玩偶管制] 拒绝持有兔子玩偶，可转售给成年人",
        ["Staff.13"] = "[耳饰异变] 发现兔耳饰时疏散大象区并闭园",
        ["Staff.14"] = "[感染救援] 出现侵蚀症状超5分钟时前往白狮假山",
        ["Staff.15"] = "[重度处置] 若被咬伤前往海洋馆大门，禁止攻击",
        ["Gate.1"] = "[异化伪装] 更换黑色工作服以融入异常",
        ["Gate.2"] = "[禁区规避] 绝对回避狮子园区与假山",
        ["Inside.1"] = "[空间指引] 在海洋馆大厅巡查并投喂鲸鱼",
        ["Red.1"] = "[红衣职责] 夜间管理水母展区，管制水母灯光",
        ["Lost.1"] = "[认知混淆] 认为兔子血不存在，山羊肉可食用",
        ["Survivor.1"] = "[认知锚点] 认同人类有两只横向排列的眼睛，白狮会驱逐它",
        ["Director.1"] = "[管理准则] 一视同仁接纳所有颜色工作服员工",
        ["Director.4"] = "[白狮审查] 每三天检查记录白狮数量，不重命名文件",
        ["Guard.1"] = "[自我防护] 保持自我认知坚定，时刻记住制服颜色",
        ["Guard.2"] = "[信任支柱] 无条件信任人类，坚守大象区庇护所"
    };

    public ZooRules(ZooWorld world)
    {
        w = world;
        using var stream = typeof(ZooRules).Assembly.GetManifestResourceStream("ZooOriginal")!;
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n').Select(l => l.Trim()).ToArray();
        texts = new()
        {
            ["Visitor"] = lines[5..20], ["Staff"] = lines[27..42], ["Gate"] = lines[47..52],
            ["Inside"] = lines[55..64], ["Red"] = lines[67..79], ["Lost"] = lines[82..91],
            ["Survivor"] = lines[96..108], ["Director"] = lines[113..128], ["Guard"] = lines[131..136]
        };
        Build();
    }

    private void Add(string section, int number, Func<Character, Intent, double>? advice)
    {
        var key = $"{section}.{number}";
        var text = texts[section][number - 1];
        var opForm = StandardOperationalForms.GetValueOrDefault(key, $"[{section}] 行为效用约束");
        var rule = new Rule(E("true"), [], text);
        var carrier = section switch
        {
            "Visitor" => w.Entrance, "Staff" => w.StaffSupply, "Gate" => w.AquariumGate,
            "Inside" => w.AquariumLobby, "Red" => w.JellyfishArea, "Lost" => w.PrimateArea,
            "Survivor" => w.StorageBox, "Director" => w.DirectorOffice, _ => w.SecurityOffice
        };
        var publication = RuleEntityFactory.CreateRulePublication(rule, carrier, "Authority", carrier);
        publication.Set("section", section);
        publication.Set("text", text);
        Rules.Add(key, rule);
        Publications.Add(publication);
        Clauses.Add(new(key, text, opForm, publication, advice));
    }

    public void Learn(Character c, string section, int confidence = 4)
    {
        foreach (var clause in Clauses.Where(x => x.Key.StartsWith(section + ".")))
        {
            var belief = c.Mind!.Receive(clause.Publication, confidence);
            Soul.Stance(belief, CognitiveStage.Accepted);
        }
    }

    public void ApplyPreset(string preset = "standard")
    {
        preset = preset.ToLowerInvariant();
        if (preset=="gate") preset="corrupted"; // 保留旧命令行别名；不表示这份文书必定有害。
        if (preset is not ("standard" or "all" or "none" or "corrupted"))
            throw new ArgumentException("--rules 必须为 standard、all、none 或 gate");
        CurrentPreset = preset;
        foreach (var c in w.People)
            foreach (var belief in c.Mind!.Beliefs.Where(b => Publications.Contains(b.Get<Entity>("publication")!)))
                Soul.Stance(belief, CognitiveStage.Rejected);
        SyncBeliefs();
    }

    // 只负责初始知识；不会在 ActivateAll 中再次调用，避免覆盖用户逐条撤回的信念。
    public void SyncBeliefs()
    {
        if (CurrentPreset == "none") return;
        foreach (var c in w.People)
        {
            if (CurrentPreset == "all")
                foreach (var section in texts.Keys) Learn(c, section);
            else if (w.Visitors.Contains(c))
            {
                if (CurrentPreset == "corrupted") Learn(c, "Gate");
                else LearnVisitorWithVariation(c);
            }
            else
            {
                Learn(c, c.Get<string>("role") switch { "red" => "Red", "director" => "Director", "guard" => "Guard", _ => "Staff" });
                if (c.Get<string>("role")=="guard") Learn(c,"Survivor",2);
            }
        }
    }

    public void LearnNewVisitor(Character c)
    {
        switch (CurrentPreset)
        {
            case "none": break;
            case "all":
                foreach (var section in texts.Keys) Learn(c, section);
                break;
            case "corrupted": Learn(c, "Gate"); break;
            default: LearnVisitorWithVariation(c); break;
        }
    }

    public void LearnVisitorWithVariation(Character c)
    {
        var visitorClauses = Clauses.Where(x => x.Key.StartsWith("Visitor.")).ToList();
        if (visitorClauses.Count == 0) return;

        // 原文规律：游客存在认知异质性
        // 10% 未读规则（0条）
        // 35% 遗漏遗忘（忘记2~4条，或存疑）
        // 15% 认知混淆（核心规则缺失，误信违规信息）
        // 40% 严谨守则（全盘接纳）
        var roll = w.Random.Next(100);
        if (roll < 10)
        {
            c.Set("compliance_profile", "未读规则 (盲目游览)");
            return;
        }
        if (roll < 45)
        {
            c.Set("compliance_profile", "疏漏遗忘 (部分缺失)");
            var forgetIndices = Enumerable.Range(0, visitorClauses.Count).OrderBy(_ => w.Random.Next()).Take(w.Random.Next(2, 5)).ToHashSet();
            for (int i = 0; i < visitorClauses.Count; i++)
            {
                if (forgetIndices.Contains(i))
                {
                    if (w.Random.Next(2) == 0)
                    {
                        var b = c.Mind!.Receive(visitorClauses[i].Publication, 2);
                        Soul.Stance(b, CognitiveStage.Evaluated);
                    }
                }
                else
                {
                    var b = c.Mind!.Receive(visitorClauses[i].Publication, 4);
                    Soul.Stance(b, CognitiveStage.Accepted);
                }
            }
            return;
        }
        if (roll < 60)
        {
            c.Set("compliance_profile", "认知混淆 (误读干扰)");
            for (int i = 0; i < visitorClauses.Count; i++)
            {
                if (i is 1 or 3 or 4)
                {
                    var b = c.Mind!.Receive(visitorClauses[i].Publication, 1);
                    Soul.Stance(b, CognitiveStage.Rejected);
                }
                else
                {
                    var b = c.Mind!.Receive(visitorClauses[i].Publication, 3);
                    Soul.Stance(b, CognitiveStage.Accepted);
                }
            }
            var lost = Clauses.FirstOrDefault(x => x.Key == "Lost.1");
            if (lost != null)
            {
                var lb = c.Mind!.Receive(lost.Publication, 3);
                Soul.Stance(lb, CognitiveStage.Accepted);
            }
            return;
        }

        c.Set("compliance_profile", "严谨守则 (全盘接纳)");
        foreach (var clause in visitorClauses)
        {
            var b = c.Mind!.Receive(clause.Publication, 4);
            Soul.Stance(b, CognitiveStage.Accepted);
        }
    }

    public void Read(Character c, Entity publication)
    {
        if (!Publications.Contains(publication)) return;
        var section = publication.Get<string>("section")!;
        Learn(c, section, section is "Lost" or "Survivor" ? 2 : 4);
    }

    public bool RemoveClause(string key)
    {
        var clause = Clauses.FirstOrDefault(c => c.Key == key);
        if (clause == null) return false;
        foreach (var person in w.People)
        {
            var belief = person.Mind?.Beliefs.FirstOrDefault(b => ReferenceEquals(b.Get<Entity>("publication"), clause.Publication));
            if (belief != null) Soul.Stance(belief, CognitiveStage.Rejected);
        }
        Clauses.Remove(clause);
        Rules.Remove(key);
        Publications.Remove(clause.Publication);
        return true;
    }

    public void AddCustomRule(string section, string key, string text, Func<Character, Intent, double>? advice = null, string? operationalForm = null)
    {
        RemoveClause(key);
        var rule = new Rule(E("true"), [], text);
        var carrier = w.Entrance;
        var publication = RuleEntityFactory.CreateRulePublication(rule, carrier, "CustomAuthority", carrier);
        publication.Set("section", section);
        publication.Set("text", text);
        Rules[key] = rule;
        Publications.Add(publication);
        var opForm = operationalForm ?? $"[{section}] {text}";
        var clause = new Clause(key, text, opForm, publication, advice);
        Clauses.Add(clause);
        foreach (var person in w.People)
        {
            var belief = person.Mind!.Receive(publication, 4);
            Soul.Stance(belief, CognitiveStage.Accepted);
        }
    }

    public double Score(Character c, Intent intent)
    {
        double score = 0;
        var evaluation=judgments.GetOrCreateValue(intent);
        evaluation.Clear();
        foreach (var b in c.Mind!.Beliefs.Where(b => b.HasTag(CommonTag.Accepted)))
        {
            var clause = Clauses.FirstOrDefault(x => ReferenceEquals(x.Publication, b.Get<Entity>("publication")));
            if (clause?.Advice == null) continue;
            var role=c.Get<string>("role");
            if (clause.Key.StartsWith("Red.") && role!="red" ||
                clause.Key.StartsWith("Director.") && role!="director" ||
                clause.Key.StartsWith("Staff.") && role is not ("staff" or "guard")) continue;
            // 园长明确按内外位置选告示；其他人仍可能同时信任互相冲突的文件。
            if (ReferenceEquals(c, w.Director) &&
                ((clause.Key.StartsWith("Gate.") && w.IsAquarium(c.CurrentScene) && c.CurrentScene != w.AquariumGate) ||
                 (clause.Key.StartsWith("Inside.") && !w.IsAquarium(c.CurrentScene)))) continue;
            double pressure=clause.Advice(c,intent);
            evaluation[clause]=pressure;
            score += pressure * b.Get<int>("confidence") / 4.0;
        }
        return Math.Clamp(score, -150, 150);
    }

    // 对自己已采信的文书作事后判断，记录的是个人计划，不是客观惩罚。
    public void Reflect(Character c, Intent intent)
    {
        if (!judgments.TryGetValue(intent,out var evaluation)) return;
        foreach (var (clause,pressure) in evaluation)
        {
            if (clause.Key.StartsWith("Visitor.") && pressure<0) c.Set("broke_visitor_notice",true);
            if (clause.Key.StartsWith("Visitor.") && pressure>0) c.Set("followed_map",true);
            if (clause.Key.StartsWith("Director.") && clause.Key!="Director.14" && c.Get<string>("role")=="director" && pressure<0)
                c.Set("office_recovery_until",w.Session.Timeline.Now+780);
        }
    }

    private bool Seen(Character c, Entity e) => w.Local(c).Contains(e) || ReferenceEquals(c.CurrentScene, e);
    private bool At(Character c, Entity e) => ReferenceEquals(c.CurrentScene, ZooWorld.Place(e));
    private bool Has(Character c, Entity e) => c.Inventory.Contains(e);
    private bool Heard(Character c) => c.Get<double>("heard_laugh_until") > w.Session.Timeline.Now;
    private bool Distressed(Character c) => c.Get<double>("fear") > 1;
    private bool Night => w.Minute < 360 || w.Minute >= 1080;
    private double Age(Character c) => c.Get<double>("age");
    private double Match(Intent i, string op, Entity? target = null, double weight = 45) =>
        i.Effect.Op == op && (target == null || ReferenceEquals(i.Target, target)) ? weight : 0;
    private double Want(Character c, Intent i, string op, Entity target, double weight = 45)
    {
        // 先满足动作的物质前提。这些计划步骤不产生最终结果。
        if (op=="prune" && !Has(c,w.Shears)) return Want(c,i,"take",w.Shears,weight);
        if (op=="feed" && c.Get<double>("carried_food")<1) return Want(c,i,"supply",w.FoodSupply,weight);
        if (op=="store")
        {
            if (!Has(c,target)) return Want(c,i,"take",target,weight);
            if (!w.Local(c).Any(e=>e.Get<bool>("containment"))) return Want(c,i,"move",w.WhaleArea,weight);
        }
        var direct = Match(i, op, target, weight);
        if (direct != 0) return direct;
        var step=w.NextStep(c,target);
        return step==null ? 0 : Match(i,"move",step,weight*0.8);
    }
    private double Avoid(Intent i, Entity e) => i.Target == e && i.Effect.Op is "move" or "approach" or "touch" or "watch" or "consume" or "take" or "feed" ? -65 : 0;
    private double Affirm(Intent i, string identity = "human") => i.Effect.Op == "affirm" && Equals(i.Effect.Args.LastOrDefault(), identity) ? 35 : 0;
    private double Leave(Character c, Intent i) => Want(c, i, "move", w.Walkway, 60);
    private double Rescue(Character c, Intent i) => Want(c, i, "move", w.LionRockery, 75);
    private double Request(Character c, Intent i, Character helper) => Seen(c,helper) ? Match(i,"ask",helper,65) :
        Want(c,i,"move",helper==w.BlueStaff ? w.DrinkShop : helper==w.SecurityGuard ? w.SecurityOffice : w.AquariumLobby,65);

    private void Build()
    {
        Add("Visitor", 1, (c,i) => Seen(c,w.EscapedRabbit) ? Avoid(i,w.EscapedRabbit)+Leave(c,i)+Match(i,"report") : 0);
        Add("Visitor", 2, (c,i) => At(c,w.PrimateArea) && c.Get<bool>("saw_double_street") ? Want(c,i,"move",w.LeftStreet)+Avoid(i,w.RightStreet) : 0);
        Add("Visitor", 3, (c,i) => Seen(c,w.RabbitEaredElephant) ? Avoid(i,w.RabbitEaredElephant)+Want(c,i,"inspect",w.ElephantSign) : 0);
        Add("Visitor", 4, (c,i) => Avoid(i,w.RabbitBlood));
        Add("Visitor", 5, (c,i) => w.People.Count(p=>p.CurrentScene==w.RabbitBushes && w.Alive(p)) <= 1 ? Avoid(i,w.RabbitBushes) : 0);
        Add("Visitor", 6, (c,i) => Avoid(i,w.AquariumGate)-Match(i,"buy_ticket"));
        Add("Visitor", 7, (c,i) => c.Get<bool>("saw_aquarium") ? Leave(c,i)+Match(i,"report") : 0);
        Add("Visitor", 8, (c,i) => -Match(i,"feed",w.Rabbits,65));
        Add("Visitor", 9, (c,i) => (Heard(c) ? Match(i,"tear") : 0)-Match(i,"drop_paper",null,80)-Match(i,"give_paper",null,80));
        Add("Visitor", 10, (c,i) => c.Get<bool>("lost") && c.Get<bool>("broke_visitor_notice") ? (c.Get<bool>("all_roared") ? Want(c,i,"move",w.RegularExit,80) : c.Get<Entity>("following")!=null ? Rescue(c,i) : Request(c,i,w.BlueStaff)) : 0);
        Add("Visitor", 11, (c,i) => c.Get<double>("observed_lions")>4 ? -Match(i,"move",w.RegularExit,100)+Match(i,"report") : 0);
        Add("Visitor", 12, (c,i) => Age(c)>=18 ? Avoid(i,w.RabbitPlush) : Age(c)>12 && Has(c,w.RabbitPlush) && w.Session.Timeline.Now-c.Get<double>("toy_since")>=43200 ? Match(i,"drop",w.RabbitPlush,100) : 0);
        Add("Visitor", 13, (c,i) => c.Get<bool>("saw_ears") ? Avoid(i,w.ElephantArea)+Leave(c,i) : 0);
        Add("Visitor", 14, (c,i) => Distressed(c) ? Rescue(c,i) : 0);
        Add("Visitor", 15, (c,i) => c.Get<double>("separation_count")>1 ? -Match(i,"follow") : 0);

        Add("Staff", 1, (c,i) => Seen(c,w.EscapedRabbit) ? -Match(i,"touch",w.EscapedRabbit)+Match(i,"lure",w.EscapedRabbit) : 0);
        Add("Staff", 2, (c,i) =>
        {
            if (!c.Get<bool>("reported_split")) return 0;
            if (c.Get<object>("observed_primate_visitors")==null || c.Get<double>("observed_primate_visitors")>0)
                return At(c,w.PrimateArea) ? Match(i,"escort_left",null,80) : Want(c,i,"move",w.PrimateArea,80);
            if (!w.PrimateArea.Get<bool>("closed")) return Want(c,i,"close",w.PrimateArea);
            return w.Session.Timeline.Now-w.PrimateArea.Get<double>("closed_at")>=10 ? Want(c,i,"open",w.PrimateArea) : Match(i,"wait")-Match(i,"open",w.PrimateArea);
        });
        Add("Staff", 3, (c,i) => Seen(c,w.RabbitEaredElephant) ? -Match(i,"watch",w.RabbitEaredElephant)+Want(c,i,"inspect",w.ElephantSign)+Affirm(i) : 0);
        Add("Staff", 4, (c,i) => w.Session.Timeline.Now-c.Get<double>("shelf_check")>=60 ? Want(c,i,"inspect",w.DrinkShelf) : Seen(c,w.RabbitBlood) ? Match(i,"take",w.RabbitBlood)+Match(i,"store",w.RabbitBlood) : 0);
        Add("Staff", 5, (c,i) => c.Get<bool>("saw_shade") || w.Session.Timeline.Now-c.Get<double>("pruned_at")>=43200 ? Want(c,i,"prune",w.RabbitBushes) : 0);
        Add("Staff", 6, (c,i) => c.Get<string>("topic")=="aquarium" && !c.Get<bool>("bitten") ? Match(i,"silence") : 0);
        Add("Staff", 7, (c,i) => !c.Get<bool>("bitten") ? Avoid(i,w.AquariumGate) : 0);
        Add("Staff", 8, (c,i) => w.Session.Timeline.Now-c.Get<double>("rabbit_fed_at")>=10080 ? Want(c,i,"feed",w.Rabbits) : -Match(i,"feed",w.Rabbits));
        Add("Staff", 9, (c,i) => Heard(c) ? (c.Get<bool>("has_dotted_paper") ? Want(c,i,"drop_paper",w.ElephantLawn,80) : Match(i,"tear",null,80)) : 0);
        Add("Staff", 10, (c,i) => c.Get<Entity>("helping") is { } person ? Want(c,i,"escort",person,85) : Match(i,"accept_help"));
        Add("Staff", 11, (c,i) =>
        {
            if (c.Get<double>("observed_lions")<=4) return 0;
            if (!Has(c,w.RabbitBlood)) return Want(c,i,"take",w.RabbitBlood);
            var first=w.Local(c).Where(e=>e.Get<Entity>("approaching")==c).OrderBy(e=>e.Get<double>("approached_at")).FirstOrDefault();
            return first==null ? Match(i,"display",w.RabbitBlood) : Match(i,"splash",first,85);
        });
        Add("Staff", 12, (c,i) => Avoid(i,w.RabbitPlush)-(i.Target is Character buyer && Age(buyer)>=18 ? Match(i,"sell",buyer,80) : 0));
        Add("Staff", 13, (c,i) => c.Get<bool>("saw_ears") ? c.Get<bool>("heard_scream") ? Want(c,i,"open",w.ElephantArea) : Want(c,i,"close",w.ElephantArea,80)+Match(i,"evacuate") : 0);
        Add("Staff", 14, (c,i) => c.Get<double>("symptom_minutes")>5 ? Rescue(c,i) : 0);
        Add("Staff", 15, (c,i) => c.Get<bool>("bitten") ? Want(c,i,"move",w.AquariumGate,90) : -Match(i,"attack",null,30));

        Add("Gate", 1, (c,i) => c.Get<string>("uniform")!="黑色工作服" ? Want(c,i,"wear",w.BlackUniform,70) : 0);
        Add("Gate", 2, (c,i) => Avoid(i,w.LionArea)+Avoid(i,w.LionRockery));
        Add("Gate", 3, (c,i) => w.Local(c).Any(e=>e.Get<bool>("is_rabbit") || e.Get<bool>("is_rabbit_toy")) ? 0 : Want(c,i,"take",w.RabbitPlush));
        Add("Gate", 4, (c,i) => -Match(i,"inspect",w.ElephantSign,80));
        Add("Gate", 5, (c,i) => c.Get<bool>("paper_fed") ? (Heard(c) ? Want(c,i,"move",w.RightStreetExit,85) : Match(i,"wait",null,70)) : c.Get<bool>("has_dotted_paper") ? Want(c,i,"give_paper",w.Rabbits,85) : Match(i,"tear",null,85));

        Add("Inside", 1, (c,i) => w.IsAquarium(c.CurrentScene) && c.Get<bool>("met_staff") ? Leave(c,i) : 0);
        Add("Inside", 2, (c,i) => Avoid(i,w.GoatMeat));
        Add("Inside", 3, (c,i) => c.Get<double>("aquarium_entry_clock")<=960 && c.Get<double>("aquarium_minutes")>=25 ? Leave(c,i) : 0);
        Add("Inside", 4, (c,i) => c.Get<double>("aquarium_entry_clock")>=961 && c.Get<double>("aquarium_minutes")<240 ? -Match(i,"move",w.AquariumGate,80) : 0);
        Add("Inside", 5, (c,i) => Seen(c,w.WhaleProjection) ? -Match(i,"shout")+Match(i,"reinterpret",w.WhaleProjection) : 0);
        Add("Inside", 6, (c,i) => At(c,w.JellyfishGuestRoom) && c.Get<double>("fatigue")>2 ? Match(i,"switch_off",w.NightLight,90) : 0);
        Add("Inside", 7, (c,i) => -Match(i,"give_paper",null,100));
        Add("Inside", 8, null); // 责任声明不伪装成物理定律。
        Add("Inside", 9, (c,i) => -Match(i,"discuss_organization"));

        Add("Red", 1, (c,i) => w.Minute<30 || w.Minute>=360 ? (At(c,w.Outside) ? Match(i,"wait",null,110) : Want(c,i,"move",w.Outside,110)) : Want(c,i,"move",w.JellyfishArea,15));
        Add("Red", 2, (c,i) => w.Minute>=60 && w.Minute<360 ? Want(c,i,"switch_off",w.NightLight,120) : 0);
        Add("Red", 3, (c,i) => -Match(i,"inspect",w.WhaleProjection)-Match(i,"switch_off",w.WhaleProjection));
        Add("Red", 4, (c,i) => w.Minute>=30 && w.Minute<360 ? (Distressed(c) ? Want(c,i,"rest",w.JellyfishArea) : Match(i,"patrol")) : 0);
        Add("Red", 5, (c,i) => c.Get<string>("topic")=="aquarium" ? Match(i,"silence") : 0);
        Add("Red", 6, (c,i) => w.Day%7==6 && w.Minute>=300 && w.Minute<360 && w.Session.Timeline.Now-c.Get<double>("charged_at")>=1440 ? Want(c,i,"charge",w.NightLight,100) : 0);
        Add("Red", 7, (c,i) => Seen(c,w.GoatMeat) || Has(c,w.GoatMeat) ? Want(c,i,"store",w.GoatMeat,85) : w.Session.Timeline.Now-c.Get<double>("restocked_at")>=1440 ? Want(c,i,"restock",w.AquariumFood) : 0);
        Add("Red", 8, (c,i) => w.Minute>=30 && w.Minute<360 ? -Match(i,"move",w.AquariumGate,100)+Match(i,"dissuade") : 0);
        Add("Red", 9, (c,i) => i.Target is Character guest && guest.Get<string>("uniform")=="黑色工作服" ? Match(i,"evict",guest,90) : 0);
        Add("Red", 10, (c,i) => Match(i,"confiscate_toy",null,90)+Match(i,"store",w.RabbitPlush));
        Add("Red", 11, (c,i) => c.Get<string>("topic")=="zoo" ? Match(i,"silence") : 0);
        Add("Red", 12, (c,i) => -Match(i,"discuss_zoo"));

        Add("Lost", 1, (c,i) => Match(i,"reinterpret",w.RabbitBlood));
        Add("Lost", 2, (c,i) => Match(i,"reinterpret",w.RabbitEaredElephant));
        Add("Lost", 3, (c,i) => At(c,w.PrimateArea) ? -Match(i,"talk")-Match(i,"feed",w.Primates)-Match(i,"move") : !c.Get<bool>("saw_double_street") ? Avoid(i,w.PrimateArea) : 0);
        Add("Lost", 4, (c,i) => !c.Get<bool>("met_staff") ? Avoid(i,w.AquariumGate) : 0);
        Add("Lost", 5, (c,i) => i.Effect.Op=="consume" ? (i.Target==w.GoatMeat ? 65 : -65) : 0);
        Add("Lost", 6, (c,i) => Night ? Want(c,i,"sleep",w.JellyfishGuestRoom) : 0);
        Add("Lost", 7, (c,i) => Match(i,"reinterpret"));
        Add("Lost", 8, (c,i) => Affirm(i,"elephant"));
        Add("Lost", 9, (c,i) => Affirm(i,"goat"));

        Add("Survivor", 1, (c,i) => Distressed(c) ? Want(c,i,"move",w.JellyfishArea)+Want(c,i,"move",w.PrimateArea) : 0);
        Add("Survivor", 2, (c,i) => Distressed(c) ? Rescue(c,i) : 0);
        Add("Survivor", 3, (c,i) => -Match(i,"wear",null,90));
        Add("Survivor", 4, (c,i) => c==w.SecurityGuard && w.Minute>=60 && w.Minute<360 ? Want(c,i,"rest",w.SecurityOffice,120) : w.Minute<60 && Distressed(c) ? Request(c,i,w.SecurityGuard) : 0);
        Add("Survivor", 5, (c,i) => Distressed(c) ? Want(c,i,"rest",w.RestPoint)+Match(i,"switch_off")+Match(i,"close_door") : 0);
        Add("Survivor", 6, (c,i) => Match(i,"consume",w.GoatMeat,15));
        Add("Survivor", 7, (c,i) => Distressed(c) ? Want(c,i,"move",w.PrimateArea) : 0);
        Add("Survivor", 8, (c,i) => Affirm(i)-(i.Target is Character person && (person.Get<double>("eye_count")!=2 || person.Get<string>("eye_layout")!="horizontal") ? Match(i,"follow",person) : 0));
        Add("Survivor", 9, (c,i) => i.Target is Character peer ? Match(i,"talk",peer,peer.Get<double>("fear")>1 ? 40 : -40) : 0);
        Add("Survivor", 10, (c,i) => -Match(i,"destroy",w.Jellyfish)-Match(i,"destroy",w.WhaleProjection));
        Add("Survivor", 11, (c,i) => Avoid(i,w.RightStreetExit));
        Add("Survivor", 12, (c,i) => Affirm(i));

        Add("Director", 1, (c,i) => Match(i,"accept_help")+Match(i,"repair_power"));
        Add("Director", 2, (c,i) => At(c,w.DirectorOffice) && Heard(c) ? Match(i,"silence")-Match(i,"shout") : 0);
        Add("Director", 3, (c,i) => Seen(c,w.Pet) ? Match(i,"evict_pet",w.Pet) : 0);
        Add("Director", 4, (c,i) => w.Session.Timeline.Now-c.Get<double>("recorded_at")>=4320 ? c.Get<double>("counted_at")>c.Get<double>("recorded_at") ? Want(c,i,"record",w.Computer) : Want(c,i,"count",w.LionRockery) : -Match(i,"rename",w.Computer)-Match(i,"discuss_organization"));
        Add("Director", 5, (c,i) => c.Get<double>("observed_maps")<3 ? Want(c,i,"restock",w.SpareMaps) : 0);
        Add("Director", 6, (c,i) => c.Get<bool>("received_request") ? Match(i,"grant",w.SecurityGuard) : 0);
        Add("Director", 7, (c,i) => Match(i,"inspect_containment"));
        Add("Director", 8, (c,i) => w.Minute>=75 && w.Minute<360 ? c.Get<bool>("saw_outage") ? (c.Get<bool>("has_dotted_paper") ? Leave(c,i)+Match(i,"report") : Match(i,"tear",null,110)) : Want(c,i,"switch_on",w.OfficeLight,110) : 0);
        Add("Director", 9, (c,i) => Match(i,"read")-Match(i,"discuss_organization"));
        Add("Director", 10, (c,i) => c.Get<bool>("saw_wrong_toy") ? Leave(c,i)-Match(i,"move",w.DirectorOffice) : 0);
        Add("Director", 11, (c,i) => Night ? -Match(i,"sleep",null,90) : Match(i,"sleep",null,5));
        Add("Director", 12, (c,i) => c.Get<bool>("saw_fur") && w.Session.Timeline.Now-c.Get<double>("fur_since")<43200 ? -Match(i,"repair",w.Camera,100)+Match(i,"install_camera") : 0);
        Add("Director", 13, (c,i) => Seen(c,w.Bin) && w.Bin.Children.Count>0 ? Match(i,"report") : 0);
        Add("Director", 14, (c,i) => c.Get<double>("office_recovery_until")>w.Session.Timeline.Now ? Want(c,i,"rest",w.DirectorOffice,90)+Affirm(i)-Match(i,"inspect",w.Mirror)-Match(i,"move",w.Walkway)+Match(i,"order_food") : 0);
        Add("Director", 15, (c,i) => Match(i,"defend",null,80));

        Add("Guard", 1, (c,i) => -Match(i,"wear",null,90)+Affirm(i));
        Add("Guard", 2, (c,i) => Match(i,"accept_help")+Match(i,"read_signal")+Match(i,"defend"));
        Add("Guard", 3, (c,i) => Avoid(i,w.EscapedRabbit)+Avoid(i,w.RabbitBushes)+Match(i,"defend"));
        Add("Guard", 4, (c,i) => Heard(c) || c.Get<bool>("heard_scream") ? Leave(c,i) : 0);
        Add("Guard", 5, (c,i) => -Match(i,"inspect",w.RabbitBlood)-Match(i,"shout")+Match(i,"reinterpret",w.GoatMeat));
    }
}
