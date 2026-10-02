using DualOntologyWorldEngine.Core;

namespace DualOntologyWorldEngine.Game.FirstAwakening;

internal static class RedTokenHypothesis
{
    internal static string UnprovenDetail(string id) => id switch
    {
        "E01" => "报偿核验属于现场材料，候选历史未据此推断昨夜动作。",
        "E05" => "尚未提交楔的处置候选，无法核对案脚痕迹。",
        "E06" => "尚未提交楔与合门候选，无法核对门侧灰痕。",
        "E07" => "尚未提交楔与包纸候选，无法核对纸内痕迹。",
        "E08" => "尚未提交通流来源，无法核对布上灰与擦地纤维的先后。",
        "E09" => "尚未形成可执行的通流与持柄候选，无法核对盆内痕迹。",
        "E09.deep" => "尚未形成可执行的通流与持柄候选，无法核对上游当班捕集痕迹。",
        "E09.completion" => "尚未提交释放与收班记录候选，无法核对同号转清、确认与停流。",
        "E10" => "尚未提交持柄岗位候选，无法核对本人接掌记录。",
        "E11" => "尚未提交本人是否读取具名特例，无法核对签收。",
        "E12" => "尚未提交门侧和台侧的声路判断，无法核对两人转述。",
        "E13" or "E14" or "E15" => "尚未提交器具交回候选，无法核对交接后留下的材料。",
        "E16" => "当前身份候选尚未得到已取得移物材料的支持。",
        _ => "候选尚未说明此材料。"
    };

    internal static string ConflictDetail(string id, RedTokenSimulation s, IReadOnlyDictionary<string, string> f)
    {
        bool Is(string obj, Tag tag, int value = 1) => s.Value(obj, tag) == value;
        var gaps = new List<string>();
        switch (id)
        {
            case "E05":
                if (!Is("wedge", RedTokenSimulation.Wrapped)) gaps.Add("同一楔未形成包裹状态");
                break;
            case "E06":
                if (!Is("wedge", RedTokenSimulation.Dirty)) gaps.Add("楔未留下灰痕");
                if (!Is("door", RedTokenSimulation.Closed)) gaps.Add("门未形成合拢状态");
                break;
            case "E07":
                if (!Is("paper", RedTokenSimulation.Dirty)) gaps.Add("包纸内未生成灰转印");
                break;
            case "E09":
                if (!Is("water", RedTokenSimulation.Basin)) gaps.Add("盆内未留下候选水痕");
                break;
            case "E09.deep":
                if (!s.HistoricalGray) gaps.Add("上游当班捕集片未生成新灰，盆内洗布痕迹不能替代这份上游材料");
                break;
            case "E09.completion":
                if (!Is("confirmation", RedTokenSimulation.Clear)) gaps.Add("同号转清状态未生成");
                if (!Is("confirmation", RedTokenSimulation.Confirmed)) gaps.Add("具名确认未提交");
                if (s.Flowing) gaps.Add("候选仍在持柄通流，未形成停流状态");
                break;
            case "E10":
                if (!Is("工程师霍铎", RedTokenSimulation.Accepted)) gaps.Add("工程师本人未生成接掌手柄记录，其他人的持柄不能替代本人记录");
                break;
            case "E11":
                if (!Is("工程师霍铎", RedTokenSimulation.Read)) gaps.Add("工程师本人未生成读取具名特例的记录");
                break;
            case "E12":
                string Sound(string value) => value == "quiet" ? "静" : value == "ringing" ? "响" : "未知";
                if ((f.GetValueOrDefault("sound_gap", "quiet") == "quiet") != Is("door", RedTokenSimulation.Sound, 0))
                    gaps.Add("门侧候选为" + Sound(f.GetValueOrDefault("sound_gap", "quiet")) + "，合门敲击模型实际为" + (Is("door", RedTokenSimulation.Sound, 0) ? "静" : "响"));
                if ((f.GetValueOrDefault("sound_pipe", "ringing") == "ringing") != Is("pipe", RedTokenSimulation.Sound))
                    gaps.Add("台侧候选为" + Sound(f.GetValueOrDefault("sound_pipe", "ringing")) + "，铜管敲击模型实际为" + (Is("pipe", RedTokenSimulation.Sound) ? "响" : "静"));
                break;
            case "E13":
                if (!Is("red", RedTokenSimulation.Received)) gaps.Add("同一红筹未形成单向接收记录，与已见接收印不符");
                break;
            case "E14":
                if (!Is("white", RedTokenSimulation.Taken)) gaps.Add("白片未被领取");
                if (!Is("room_sign", RedTokenSimulation.Taken, 4)) gaps.Add("交接后返房签条仅生成" + s.Value("room_sign", RedTokenSimulation.Taken) + "/4份");
                break;
            case "E15":
                if (!Is("chair", RedTokenSimulation.Taken)) gaps.Add("椅上未生成红筹放置痕迹");
                if (!Is("receiver", RedTokenSimulation.Broken)) gaps.Add("前抽屉原封未断开");
                if (f.GetValueOrDefault("identity") == "student") gaps.Add("学生身份候选与此材料的身份边界不相容");
                break;
        }
        return gaps.Count > 0 ? "候选差异：" + string.Join("；", gaps) + "。" : "候选未生成这份已见材料要求的痕迹。";
    }

    internal static void Compile(RedTokenSimulation s, IReadOnlyDictionary<string, string> f)
    {
        string Get(string key, string fallback = "unknown") => f.GetValueOrDefault(key, fallback);
        void Do(string actor, string action) { var r = s.Perform(actor, action); if (!r.Ok) s.Failures.Add(r.Message); }
        const string engineer = "工程师霍铎", chef = "主厨沈鼎", music = "音乐家苏泠", detective = "侦探";
        // 共同行动中的楔始终是同一引用；门底、手上、纸内不能并存。
        if (Get("wedge") != "unknown")
        {
            Do(music, "take_wedge");
            Do(music, "release_wedge");
            if (Get("wedge") == "dirty_then_wrap") Do(music, "dirty_wedge");
            Do(detective, "take_wedge"); Do(detective, "close_door"); Do(detective, "wrap_wedge");
        }
        if (Get("sound_gap") != "unknown" || Get("sound_pipe") != "unknown") { Do(detective, "close_door"); Do(music, "knock_gap"); Do(engineer, "knock_pipe"); }
        if (Get("engineer_exception") == "read") Do(engineer, "read_exception");
        if (Get("wipe") == "before") Do(chef, "wipe");
        if (Get("flow") == "ash")
        {
            if (Get("cloth_before_flow") == "yes" || Get("cook_filter") == "yes") Do(chef, "clip_filter");
            var holder = Get("holder") == "cook" ? chef : Get("holder") == "engineer" ? engineer : null;
            if (holder is not null)
            {
                Do(holder, "take_handle");
                if (Get("cook_filter") == "yes") Do(chef, "confirm_filter");
                Do(holder, "hold");
                if (s.Flowing)
                {
                    Do(chef, "capture");
                    if (Get("release") == "after_confirmation") { Do(chef, "inspect_clear"); Do(chef, "confirm"); }
                    if (Get("release") != "unknown") Do(holder, "release");
                }
            }
        }
        else if (Get("flow") == "water")
        {
            Do(chef, "wash");
            if (Get("completion_record") == "mistaken") Do(chef, "misrecord_clear");
        }
        if (Get("wipe") == "after") { Do(chef, "wipe"); Do(chef, "wash"); }
        if (Get("returned") == "yes")
        {
            Do(engineer, "deposit_other"); Do(engineer, "push_other");
            Do("值守者", "deposit_red"); Do("值守者", "push_red");
            if (Get("return_before_room") == "yes") Do(detective, "take_white");
        }
        if (Get("return_before_room") == "yes") foreach (var actor in new[] { detective, engineer, chef, music }) Do(actor, "return_room");
        if (Get("return_before_room") == "no") foreach (var actor in new[] { detective, engineer, chef, music }) Do(actor, "go_room_without_sign");
        if (Get("returned") == "no") Do("值守者", "carry_red_without_return");
        if (Get("take") == "after_return")
        {
            var mover = Get("identity") == "receptionist" ? "接待者" : Get("identity") == "student" ? detective : "未知后取者";
            Do(mover, "open_drawer"); Do(mover, "move_red_to_chair");
        }
    }
    internal static Dictionary<string, string> Predict(RedTokenSimulation s, IReadOnlyDictionary<string, string> f)
    {
        string Check(bool ok) => ok ? "compatible" : "conflict";
        bool Has(string k) => f.GetValueOrDefault(k, "unknown") != "unknown";
        var p = new Dictionary<string, string>();
        p["E01"] = "unknown"; p["E02"] = "compatible"; p["E03"] = "compatible"; p["E04"] = "compatible";
        p["E05"] = Has("wedge") ? Check(s.Value("wedge", RedTokenSimulation.Wrapped) == 1) : "unknown";
        p["E06"] = Has("wedge") ? Check(s.Value("wedge", RedTokenSimulation.Dirty) == 1 && s.Value("door", RedTokenSimulation.Closed) == 1) : "unknown";
        p["E07"] = Has("wedge") ? Check(s.Value("paper", RedTokenSimulation.Dirty) == 1) : "unknown";
        p["E08"] = Has("flow") ? s.HistoricalGray && s.Value("cloth", RedTokenSimulation.Wiped) == 1 ? "compatible" : "auxiliary" : "unknown";
        p["E09"] = Has("flow") && (f.GetValueOrDefault("flow") == "water" || Has("holder")) ? Check(s.Value("water", RedTokenSimulation.Basin) == 1) : "unknown";
        p["E09.deep"] = Has("flow") && (f.GetValueOrDefault("flow") == "water" || Has("holder")) ? Check(s.HistoricalGray) : "unknown";
        p["E09.completion"] = Has("release") || f.GetValueOrDefault("completion_record") == "mistaken" ? Check(s.Value("confirmation", RedTokenSimulation.Clear) == 1 && s.Value("confirmation", RedTokenSimulation.Confirmed) == 1 && !s.Flowing) : "unknown";
        p["E10"] = Has("holder") ? Check(s.Value("工程师霍铎", RedTokenSimulation.Accepted) == 1) : "unknown";
        p["E11"] = Has("engineer_exception") ? Check(s.Value("工程师霍铎", RedTokenSimulation.Read) == 1) : "unknown";
        p["E12"] = Has("sound_gap") || Has("sound_pipe") ? Check((f.GetValueOrDefault("sound_gap", "quiet") == "quiet") == (s.Value("door", RedTokenSimulation.Sound) == 0) && (f.GetValueOrDefault("sound_pipe", "ringing") == "ringing") == (s.Value("pipe", RedTokenSimulation.Sound) == 1)) : "unknown";
        p["E13"] = Has("returned") ? Check(s.Value("red", RedTokenSimulation.Received) == 1) : "unknown";
        p["E14"] = Has("returned") ? Check(s.Value("white", RedTokenSimulation.Taken) == 1 && s.Value("room_sign", RedTokenSimulation.Taken) == 4) : "unknown";
        p["E15"] = Has("returned") ? Check(s.Value("chair", RedTokenSimulation.Taken) == 1 && s.Value("receiver", RedTokenSimulation.Broken) == 1 && f.GetValueOrDefault("identity") != "student") : "unknown";
        p["E16"] = f.GetValueOrDefault("identity") == "receptionist" || f.GetValueOrDefault("identity") == "unknown" ? "compatible" : "unknown";
        return p;
    }
}
