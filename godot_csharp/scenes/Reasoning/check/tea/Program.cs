using DualOntologyWorldEngine.Game.Reasoning;
using DualOntologyWorldEngine.Core;

static void Require(bool condition, string message) { if (!condition) { Console.Error.WriteLine("FAIL: " + message); Environment.Exit(1); } }
static TeaCase Run(string[] drafts) {
    var game = new TeaCase(); var errors = game.Configure(drafts);
    Require(errors.Count == 0, string.Join("\n", errors)); game.Start();
    for (var n = 0; !game.Finished && n < 10000; n++) Require(game.Step(), "事件队列提前耗尽");
    Require(game.Finished, "没有完成重演"); Require(game.TotalTea == 5, "茶量不守恒"); return game;
}
var valid = new[] {
    "收布通知时：照记忆收起 椅布\n收柜通知时：锁上 器具柜",
    "第一声铃后：用 叶纹杯 替换 缺口杯，旧杯暂放 椅布\n第二声铃后：将 缺口杯 搬到 洗具台\n第二声铃后：收存 素杯 和 续茶碟，方式 叠放",
    "准备时：借用 缺口杯 给 温瓶盆，用后放回 原处",
    "准备时：借用 叶纹杯 给 染纸盘，用后放回 原处"
};
var reference = Run(valid);
Require(reference.Differences.Count == 0, "参考重演冲突：" + string.Join("\n", reference.Differences));
// Another explanation moves the final empty cup by a different person; no author-specific actor gate.
var alternate = valid.ToArray();
alternate[1] = alternate[1].Replace("第二声铃后：将 缺口杯 搬到 洗具台\n", "");
alternate[0] += "\n第二声铃后：将 缺口杯 搬到 洗具台";
Require(Run(alternate).Differences.Count == 0, "另一条合法搬运路线未被接受");
var safe = valid.ToArray(); safe[0] = safe[0].Replace("照记忆", "检查后");
Require(Run(safe).Differences.Any(d => d.Contains("椅面")), "重新检查应避免泼洒并与椅面痕迹冲突");
var separate = valid.ToArray(); separate[1] = separate[1].Replace("叠放", "分开放");
Require(Run(separate).Differences.Any(d => d.Contains("锁柜内")), "同在柜内不等于杯在碟上");
var direct = valid.ToArray(); direct[0] = direct[0].Replace("照记忆收起 椅布", "将 缺口杯 倒入 椅面");
Require(Run(direct).Differences.Any(d => d.StartsWith("椅面痕迹")), "直接倒茶不能冒充抽布拖痕");
var empty = Run(["", "", "", ""]);
Require(empty.Differences.Count > 0, "空方案不应通过");
var atomic = new TeaCase(); Require(atomic.Configure(valid).Count == 0, "任务配置失败");
var bad = valid.ToArray(); bad[1] += "\n准备时：将 叶纹杯 搬到 叶纹杯";
Require(atomic.Configure(bad).Count > 0, "非法配置必须整体拒绝");
atomic.Start(); for(var n=0;!atomic.Finished && n<10000;n++) atomic.Step();
Require(atomic.Differences.Count == 0, "非法配置破坏了原任务");
var memory = new TeaCase();
var roomView = memory.World.Representations(memory.Room).Single();
Require(roomView.Children.Count() == memory.Sites.Count, "房间应拥有地点/家具子物件");
Require(memory.World.Representations(memory.ChipCup).Single().Parent == memory.World.Representations(memory.Saucer).Single(), "现实杯子应通过Core空间属性归属茶碟");
Require(reference.World.Representations(reference.PlainCup).Single().Parent == reference.World.Representations(reference.Saucer).Single(), "重演搬运后Core归属应同步");
Require(memory.People[0].Known[memory.ChipCup].Existence == Truth.True, "初始共同清点应提供有来源的记忆");
foreach(var site in memory.Sites) Require(site == memory.Entrance || TeaRoomGeometry.Path(memory.Points[memory.Entrance], memory.Points[site]).Count > 0, "入口无法到达" + site.TrueName);
CoreSpatialChecks.Run(Require);
Console.WriteLine("PASS: 茶案双解及差异反馈、茶量守恒、原子配置、地点可达；Core嵌套取放/检查、锁与持有人限制、承托环回滚、等待后状态重查、认知隔离。");

