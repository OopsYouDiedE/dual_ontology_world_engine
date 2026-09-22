using DualOntologyWorldEngine.Game.Reasoning;

static void Require(bool value, string message) { if (!value) throw new Exception(message); }
using var game = new BreakfastCase();
var engineer = game.People[0]; var cook = game.People[1];
void Run(string a, string b)
{
    Require(game.Run(engineer, a) is null, "工程师方案解析失败");
    Require(game.Run(cook, b) is null, "主厨方案解析失败");
    for (var n = 0; game.Busy && n < 500; n++) game.Step();
    Require(!game.Busy, "执行未终止");
}
Require(RuleLanguage.Parse("关闭 保温柜", 0).Errors.Count == 1, "未解锁词泄露");
Run("监听 25\n前往 餐厅\n提交记录\n比较记录", "监听 25\n前往 餐厅\n提交记录\n比较记录");
Require(game.Stage == 0, "未校时的记录不能解锁");
Run("前往 走廊\n校时\n监听 25\n前往 餐厅\n提交记录\n比较记录", "前往 厨房\n校时\n监听 25\n前往 餐厅\n提交记录\n比较记录");
Require(game.Stage == 1, "两处有效记录应解锁检查");
Run("等待 5", "前往 厨房\n检查 保温柜");
Require(game.Stage == 2, "检查设备应解锁开关");
Run("前往 走廊\n监听 30\n前往 餐厅\n提交记录\n比较记录", "关闭 保温柜\n监听 30\n前往 餐厅\n提交记录\n比较记录");
Require(game.Stage == 2, "只有关闭条件不能完成对照");
Run("前往 走廊\n监听 30\n前往 餐厅\n提交记录\n比较记录", "前往 厨房\n开启 保温柜\n监听 30\n前往 餐厅\n提交记录\n比较记录");
Require(game.Stage == 3, "开关对照应解锁特定节奏");
Run("前往 走廊\n等待 4\n敲击 两短一长\n等待 8\n前往 餐厅\n提交记录\n比较记录", "前往 厨房\n监听 20\n前往 餐厅\n提交记录\n比较记录");
Require(game.Stage == 4, "节奏实验应解锁言灵");
Require(RuleLanguage.Parse("设立 只接受 本轮 原声", 4).Errors.Count == 1, "行动语言不能混入旧言灵命令");
Require(cook.Soul.World.History.Count > 10, "应通过 Observe 记录认知");
using var reset = new BreakfastCase();
Require(reset.Stage == 0 && reset.Now == 0, "重置不能保留未来词汇");
Console.WriteLine($"PASS: 未校时反例、词汇门槛、开关对照、节奏复现、重置。最终行动值 {game.Now}。");

using var lab = new UtteranceLab();
var speaker = lab.Speakers[0];
const string blockKitchen = "ilo kute pi tomo moku o weka e kalama.";
void Finish()
{
    for (var n = 0; lab.Busy && n < 100; n++) lab.Step();
    Require(!lab.Busy, "言灵执行不能悬挂");
}
void CastAndTrial(string draft)
{
    var error = lab.Cast(speaker, draft); Require(error is null, "发声失败：" + error);
    Require(lab.StartTrial() is not null, "发声结算前不能开始试声");
    Finish();
    Require(lab.StartTrial() is null, "试声未开始"); Finish();
}
Require(UtteranceLanguage.Parse("设立 只接受 本轮 原声", 0).Errors.Count == 1, "两种语言必须独立");
Require(UtteranceLanguage.Parse("ilo o jo e kalama sin.", 0).Errors.Count == 1, "时间词不能预先泄露");
Require(UtteranceLanguage.Parse("ilo o jo kalama.", 0).Errors.Count == 1, "缺少 e 应诊断");
Require(UtteranceLanguage.Parse("ilo kute tomo moku o jo e kalama.", 0).Errors.Count == 1, "修饰组缺少 pi 应诊断");
Require(lab.Cast(lab.Speakers[1], "ilo o weka e kalama.") is not null, "低位格不能约束交谈室");
Require(!lab.Busy, "位格拒绝不能产生部分执行");
CastAndTrial("ilo o weka e kalama.");
Require(lab.VocabularyStage == 0 && lab.Receivers.All(r => r.Accepted == 0), "广义名词必须实际误伤两台器物且不解锁");
CastAndTrial(blockKitchen);
Require(lab.VocabularyStage == 1 && lab.Receivers[1].Accepted == 5, "有效范围实验应解锁序数词");
CastAndTrial(blockKitchen + "\nilo kute pi tomo moku o jo e kalama nanpa wan.");
Require(lab.VocabularyStage == 2 && !lab.Solved && lab.Receivers[0].Accepted == 3, "首次到达不能排除旧声，但应解锁时间词");
var conditional = blockKitchen + "\nkalama li sin la ilo kute pi tomo moku o jo e kalama nanpa wan.";
CastAndTrial(conditional);
Require(lab.Solved && lab.Receivers[0].Accepted == 2 && lab.Receivers[1].Accepted == 5, "情境描述应精确筛选");
var meaning = lab.ActiveMeaning;
Require(lab.Cast(speaker, blockKitchen + "\nilo o jo kalama.") is not null && lab.ActiveMeaning == meaning && !lab.Busy,
    "后句语法错误不能部分覆盖已生效咒文");
CastAndTrial(blockKitchen + "\nilo kute pi tomo moku o jo e kalama nanpa wan pi tenpo ni.");
Require(lab.Solved, "不同句法的等价描述应同样通过");
CastAndTrial("ilo kute pi tomo moku o weka e kalama nanpa wan ala.\nilo kute pi tomo moku o weka e kalama pi tenpo pini.");
Require(lab.Solved, "否定与排除式描述也应通过");
CastAndTrial("ilo kute pi tomo moku o jo e kalama nanpa wan pi tenpo ni.\n" + blockKitchen);
Require(!lab.Solved && lab.Receivers[0].Accepted == 0, "后句覆盖必须影响结果");
Require(UtteranceLanguage.Parse("ilo o jo e kalama pi tenpo ni nanpa wan.", 2).Errors.Count == 1, "pi 后的分组不能静默扁平化");
using var newLab = new UtteranceLab();
Require(newLab.VocabularyStage == 0 && !newLab.Solved && newLab.Now == 0, "言灵重置不能泄露已解锁词");
Console.WriteLine("PASS: 道本语句法、位格、误伤、分阶段词汇、旧声反例、三种等价描述、覆盖顺序与原子提交。");
