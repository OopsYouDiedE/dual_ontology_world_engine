namespace zoo_web;

public enum Lang
{
    En,
    Ja,
    ZhHans,
    ZhHant
}

public static class I18n
{
    public static Lang Current { get; set; } = Lang.ZhHans;

    private static readonly Dictionary<string, (string en, string ja, string zhHans, string zhHant)> Dict = new()
    {
        // Header & Nav
        ["StaffCount"] = ("Active staff", "在園職員", "在园员工", "在園員工"),
        ["Job.Blue"] = ("Blue-coat staff", "青服職員", "蓝衣员工", "藍衣員工"),
        ["Job.Red"] = ("Red-coat staff", "赤服職員", "红衣员工", "紅衣員工"),
        ["Job.Black"] = ("Black-coat", "黒服", "黑衣人", "黑衣人"),
        ["Job.Guard"] = ("Guard", "警備員", "保安", "保安"),
        ["Job.Director"] = ("Director", "園長", "园长", "園長"),
        ["AppTitle"] = ("Zoo · Cognition & Rules Lab", "動物園 · 認知とルール実験室", "动物园 · 认知与规则实验室", "動物園 · 認知與規則實驗室"),
        ["Subtitle"] = ("ESSENCE / PROJECTION · C# CORE", "ESSENCE / PROJECTION · C# CORE", "ESSENCE / PROJECTION · C# CORE", "ESSENCE / PROJECTION · C# CORE"),
        ["MainHeading"] = ("Zoo Rules & Anomalies", "動物園のルール奇譚", "动物园规则怪谈", "動物園規則怪談"),
        ["OpenStatus"] = ("Open 08:00—16:00", "開園中 08:00—16:00", "开放中 08:00—16:00", "開放中 08:00—16:00"),
        ["ClosedStatus"] = ("Closed · Entry Stopped", "閉園中 · 入園終了", "已闭园 · 停止入园", "已閉園 · 停止入園"),
        ["Adv10Min"] = ("Advance 10 min", "10分進める", "推进 10 分钟", "推進 10 分鐘"),
        ["Adv1Hour"] = ("Advance 1 hour", "1時間進める", "推进 1 小时", "推進 1 小時"),
        ["Pause"] = ("Pause", "一時停止", "暂停", "暫停"),
        ["ContinuousRun"] = ("Continuous Run", "連続推論", "连续推演", "連續推演"),
        ["AdvNextDay"] = ("Advance to next day 08:00", "翌日08:00まで進める", "推进至次日08:00", "推進至次日08:00"),
        ["InitialVisitors"] = ("Initial Visitors", "初期入場者", "首批游客", "首批遊客"),
        ["Seed"] = ("Seed", "乱数シード", "种子", "種子"),
        ["RebuildWorld"] = ("Rebuild World", "世界を再構築", "重建世界", "重建世界"),
        ["Language"] = ("Language", "言語", "语言", "語言"),

        // Tabs
        ["Tab1"] = ("📊 1. Overview", "📊 1. 概要サマリー", "📊 1. 总体概览", "📊 1. 總體概覽"),
        ["Tab2"] = ("📜 2. Rules & Utterances", "📜 2. ルールと言霊", "📜 2. 规则与言灵", "📜 2. 規則與言靈"),
        ["Tab3"] = ("🧠 3. Mind & Editor", "🧠 3. 脳内世界・編集器", "🧠 3. 脑内观察与编辑器", "🧠 3. 腦內觀察與編輯器"),
        ["Tab4"] = ("⚖️ 4. Decisions & Compare", "⚖️ 4. 意思決定の比較", "⚖️ 4. 决策优先级与比较", "⚖️ 4. 決策優先級與比較"),

        // Tab 1 Metrics
        ["CurrentVisitors"] = ("Current Visitors", "現在の入場者", "当前游客", "當前遊客"),
        ["PeakVisitors"] = ("Peak Visitors", "ピーク入場者", "峰值游客", "峰值遊客"),
        ["Escaped"] = ("Escaped", "脱出成功", "已逃离", "已逃離"),
        ["ActiveRules"] = ("Active Clauses", "有効な条文数", "当前文书", "當前文書"),
        ["WalkingSpeed"] = ("Walk Speed", "歩行速度", "步行速度", "步行速度"),
        ["SpeedUnit"] = ("60 m/min", "60 m/分", "60 米/分钟", "60 米/分鐘"),

        // Tab 1 Sections
        ["CrossDayStats"] = ("Cross-day Traffic & Staff Return", "日次来園推移と職員復帰", "跨日客流与员工返岗", "跨日客流與員工返崗"),
        ["DayAdmission"] = ("Day {0}: {1} entered", "{0}日目：{1}人入園", "第{0}天入园{1}人", "第{0}天入園{1}人"),
        ["BlackStaff"] = ("New black-coat: {0}", "新規黒服：{0}", "新增黑衣：{0}", "新增黑衣：{0}"),
        ["RedStaffReturn"] = ("Red-coat returns: {0} times", "赤服復帰：{0}回", "红衣返岗：{0} 次", "紅衣返崗：{0} 次"),
        ["ItsBehavior"] = ("\"It\" - Actions & Mechanism", "「それ」の行動とメカニズム", "“它”的行动与机制", "「它」的行動與機制"),
        ["ItsLocation"] = ("Actual Location: ", "実際の位置：", "实际位置：", "實際位置："),
        ["ItsNextAction"] = ("Next Action: ", "次の行動：", "下一行动：", "下一行動："),
        ["ItsNextThink"] = ("Next deliberation: {0:F1} min. Approaching or manifesting awareness increases attention; shaken identity alters its classification; map paper strips and mingling with apes confuse classification. Rabbit transformation comes from physical contact and subsequent progress, snatching paper damages self-identity. Rule documents do not directly cause casualties.",
            "次の思案まで：{0:F1}分。接近と気づきの露出が注意を引き、アイデンティティの揺らぎが分類を狂わせ、ちぎった地図の紙片や猿との混在が識別を妨害します。ウサギ化は肉体接触と事態の進行により発生し、紙片の強奪は自己認識を損ないます。ルール文書自体が直接危害を与えることはありません。",
            "下次考虑行动：{0:F1} 分钟。接近与显露察觉增加关注；身份动摇改变它的分类；纸片及猿猴混处干扰分类。兔化来自身体接触与后续发展，抢夺纸片损伤自我身份。文书不直接制造伤亡。",
            "下次考慮行動：{0:F1} 分鐘。接近與顯露察覺增加關注；身份動搖改變它的分類；紙片及猿猴混處干擾分類。兔化來自身體接觸與後續發展，搶奪紙片損傷自我身份。文書不直接製造傷亡。"),
        ["ItsClassificationLog"] = ("Species Judgment Log Recorded by \"It\"", "「それ」が記憶した種族判定ログ", "它记住的物种判断日志", "它記住的物種判斷日誌"),
        ["ParkSites"] = ("Park Grounds", "園内エリア", "园区现场", "園區現場"),
        ["ObjectiveViewHint"] = ("Objective View · Click room to switch target", "客観視点 · クリックで該当エリアの人物を選択", "客观视角 · 点击区域切换人员", "客觀視角 · 點擊區域切換人員"),
        ["PeopleCount"] = ("{0} people", "{0} 人", "{0} 人", "{0} 人"),
        ["VisibleDanger"] = ("Visible Anomaly", "異常現象あり", "可见异常", "可見異常"),
        ["CalmUtterance"] = ("Utterance: Calm", "言霊：静寂", "言灵：安静", "言靈：安靜"),
        ["EventLog"] = ("Action & Event Log", "行動とイベント履歴", "行动与机制记录", "行動與機制記錄"),
        ["Recent30"] = ("Latest 30 events", "最新30件のストリーム", "最近 30 条事件流", "最近 30 條事件流"),

        // Tab 2 Rules & Utterances
        ["RuleMgmt"] = ("Rule Documents Management", "怪談ルールの管理", "怪谈文书管理", "怪談文書管理"),
        ["ControlledChinese"] = ("Controlled Natural Language Rules", "統制自然言語ルール", "自然语言受控规则", "自然語言受控規則"),
        ["RuleDesc"] = ("Controlled text is parsed into AST. Revisions require reading on-site or park broadcasts for characters to learn. Broadcasts do not cover Aquarium or Closed Dormitory.",
            "統制テキストはASTに構文解析されます。改定後は現地で読むか園内放送を行わないと人物には伝わりません。放送は水族館や密閉寮には届きません。",
            "受控中文会解析成 AST。修订后需要现场阅读或广播，人物才会知道新版本。广播不覆盖海洋馆和封闭宿舍。",
            "受控中文會解析成 AST。修訂後需要現場閱讀或廣播，人物才會知道新版本。廣播不覆蓋海洋館和封閉宿舍。"),
        ["SelectExistingRule"] = ("Select existing document:", "既存の文書を選択：", "选择现有文书：", "選擇現有文書："),
        ["OriginalText"] = ("Original Text: ", "原文：", "原文：", "原文："),
        ["RuleCode"] = ("Document ID: ", "文書番号：", "文书编号：", "文書編號："),
        ["RuleScript"] = ("Controlled Rule Script: ", "受控ルール文：", "受控规则文本：", "受控規則文本："),
        ["ClickRuleSkeleton"] = ("Click to insert rule skeleton:", "クリックして骨格を挿入：", "点击填入规则骨架：", "點擊填入規則骨架："),
        ["Skeleton1"] = ("Evacuate all to Entrance", "全員入口へ避難", "全员疏散至入口", "全員疏散至入口"),
        ["Skeleton2"] = ("Flee danger to Lion Area", "ライオン区へ避難", "避险至狮子园区", "避險至獅子園區"),
        ["Skeleton3"] = ("Prohibit feeding rabbits", "ウサギへの餌やり禁止", "禁止喂兔子", "禁止餵兔子"),
        ["Skeleton4"] = ("Ask help when lost", "迷子時は助けを呼ぶ", "迷路时求助", "迷路時求助"),
        ["ClickCommonCond"] = ("Click to append conditions:", "クリックして頻出条件を追加：", "点击追加常用条件：", "點擊追加常用條件："),
        ["ClickCommonAction"] = ("Click to append actions & targets:", "クリックして動作と対象を追加：", "点击追加常用动作与目标：", "點擊追加常用動作與目標："),
        ["SaveRule"] = ("Save Document", "文書を保存", "保存文书", "保存文書"),
        ["SaveAndBroadcast"] = ("Save & Broadcast", "保存して全園放送", "保存并广播", "保存並廣播"),
        ["WithdrawAndBroadcast"] = ("Withdraw & Broadcast", "撤回して放送", "撤回并广播", "撤回並廣播"),
        ["WithdrawLocalOnly"] = ("Withdraw Local Only", "現地の文書のみ撤去", "仅撤下现场文书", "僅撤下現場文書"),
        ["RuleRememberedTip"] = ("This clause is remembered by {0} people currently in park. Broadcast removes it from memory; unreached people keep it.",
            "この文書は現在園内の {0} 人に記憶されています。撤回放送は記憶を取り消しますが、届かなかった者は保持し続けます。",
            "此条文书当前仍被 {0} 名在园人物记住。撤回广播通过观察输入撤销记忆；未听见者继续保留。",
            "此條文書當前仍被 {0} 名在園人物記住。撤回廣播通過觀察輸入撤銷記憶；未聽見者繼續保留。"),
        ["PersonAttitude"] = ("Person's attitude to this clause:", "選択人物のこの条文への受容：", "所选人物对该条的态度：", "所選人物對該條的態度："),
        ["UnderstandingLevel"] = ("Understanding:", "理解度：", "理解程度：", "理解程度："),
        ["UpdateUnderstanding"] = ("Update Understanding & Compare", "理解度を更新して比較", "更新此人理解并对比", "更新此人理解並對比"),
        ["ViewRememberedClauses"] = ("View all versions remembered by this person", "この人物が記憶している全条文バージョン", "查看当前人物记住的所有文书版本", "查看當前人物記住的所有文書版本"),
        ["UtteranceMgmt"] = ("Utterances · Core AST", "言霊 · Core AST プロセス", "言灵 · Core AST 流程", "言靈 · Core AST 流程"),
        ["DirectIntervention"] = ("Direct on-site rule intervention", "現場ルールの直接介入", "现场规则直接干预", "現場規則直接干預"),
        ["UtteranceDesc"] = ("Consumes 1 mana, executes via character action slot. Temporary states time out independently; combinable with delays.",
            "魔力を1消費し、人物の行動スロットを通じて実行されます。一時的効果は個別に減衰し、複数の効果や待機と組み合わせ可能です。",
            "消耗一次施放机会，经过人物行动槽执行。临时状态独立计时；可组合多个效果及等待。",
            "消耗一次施放機會，經過人物行動槽執行。臨時狀態獨立計時；可組合多個效果及等待。"),
        ["UtteranceAst"] = ("Utterance AST:", "言霊 AST：", "言灵流程 AST：", "言靈流程 AST："),
        ["ClickUtteranceTpl"] = ("Click to insert utterance template:", "言霊テンプレートをクリック：", "可点击填入言灵模板：", "可點擊填入言靈模板："),
        ["Tpl1"] = ("Disable scene shade 15m", "木陰効果を15分無効化", "场景树荫失效15分", "場景樹蔭失效15分"),
        ["Tpl2"] = ("Manifest scene danger 20m", "エリアに20分危険を顕現", "场景显现危险20分", "場景顯現危險20分"),
        ["Tpl3"] = ("Target lost state 15m", "対象を15分迷子にする", "目标陷入迷路15分", "目標陷入迷路15分"),
        ["ParseAndCast"] = ("Parse & Prepare Cast", "構文解析して詠唱準備", "解析并准备施放", "解析並準備施放"),
        ["RemainingMana"] = ("Remaining Mana: {0}", "詠唱可能回数：{0} 回", "剩余施放次数：{0} 次", "剩餘施放次數：{0} 次"),
        ["RoomTempEffects"] = ("Current Scene Core Temporary Effects:", "現在地エリアの Core 一時効果：", "当前所在场景的 Core 临时效果：", "當前所在場景的 Core 臨時效果："),
        ["NoTempEffects"] = ("No active temporary effects", "一時効果なし", "暂无临时效果", "暫無臨時效果"),

        // Tab 3 Mind & Editor
        ["MindMechanismExplain"] = ("💡 Mental Cognition Mechanism:", "💡 脳内認知メカニズム解説：", "💡 脑内认知机制说明：", "💡 腦內認知機制說明："),
        ["MindMechanismText"] = ("Characters in the zoo always act based on their subjective beliefs. Tampering with perceived scene attributes (e.g. fake danger), blocking paths (e.g. forgetting exits), or altering identity (e.g. rabbit suggestion) immediately alters pathfinding and decisions without altering the physical world.",
            "動物園の人物は常に自らの「主観的認識」に基づいて行動します。脳内の場所属性（危険の錯覚など）を改ざんしたり、経路を封鎖したり（出口を忘れるなど）、自己アイデンティティを操作（ウサギ化暗示）すると、現実の物理世界を変えることなく、経路探索と判断が直ちに変化します。",
            "动物园里的人物永远依据自己脑海中的“主观认知”行动。在下方篡改其脑内的场景属性（如制造危险假象）、阻断通道（如误以为出口封死）或改变自身身份（如暗示兔化），将立即改变其寻路与行为决策，而现实物理世界不会因此改变。",
            "動物園裡的人物永遠依據自己腦海中的「主觀認知」行動。在下方篡改其腦內的場景屬性（如製造危險假象）、阻斷通道（如誤以為出口封死）或改變自身身份（如暗示兔化），將立即改變其尋路與行為決策，而現實物理世界不會因此改變。"),
        ["CurrentTrackedPerson"] = ("Target Person to Observe & Intervene:", "観察・介入対象の人物：", "当前观察与干预的人物：", "當前觀察與干預的人物："),
        ["RealityVsMind"] = ("Objective Reality vs Subjective Mind", "客観的現実 vs 主観的認知", "人物主客观对照", "人物主客觀對照"),
        ["SubtitleRealityVsMind"] = ("Physical Truth vs Perceived Belief", "客観の真相 vs 脳内の認識", "客观现实 vs 主观认知", "客觀現實 vs 主觀認知"),
        ["ObjectiveReality"] = ("Objective Reality (Truth)", "客観的現実（真実）", "客观现实（物理真相）", "客觀現實（物理真相）"),
        ["TrueSpecies"] = ("True Species: ", "真の種族：", "真实物种：", "真實物種："),
        ["BodyRabbit"] = ("Physical Rabbit: ", "肉体のウサギ化：", "身体兔化：", "身體兔化："),
        ["PhysLocation"] = ("Physical Location: ", "物理的位置：", "物理位置：", "物理位置："),
        ["PaperProtection"] = ("Paper Strip Protection: ", "破棄地図の防護：", "虚线纸保护：", "虛線紙保護："),
        ["HasPaper"] = ("Holding torn paper strip", "ちぎった紙片を所持", "持有撕下纸片", "持有撕下紙片"),
        ["NoPaper"] = ("Unprotected", "防護なし", "无保护", "無保護"),
        ["ItsClassified"] = ("\"It\"'s Classification: ", "「それ」の分類判定：", "“它”的分类：", "「它」的分類："),
        ["NotClassified"] = ("Unobserved", "未観測", "未观察", "未觀察"),
        ["Unclassifiable"] = ("Unclassifiable", "分類不能", "无法归类", "無法歸類"),
        ["SubjectiveMind"] = ("Subjective Mind (Perception)", "主観的認知（脳内認識）", "主观认知（脑内以为）", "主觀認知（腦內以為）"),
        ["HumanBelief"] = ("Human Identity Belief: ", "人間アイデンティティ：", "人类认同信念：", "人類認同信念："),
        ["BelievedLocation"] = ("Believed Location: ", "自認している場所：", "自认所在位置：", "自認所在位置："),
        ["OngoingAction"] = ("Ongoing Action: ", "実行中のアクション：", "进行中行动：", "進行中行動："),
        ["LastResult"] = ("Last Actual Result: ", "直前の実際の結果：", "最近实际结算：", "最近實際結算："),
        ["CognitionSliderTitle"] = ("Cognitive Clarity Slider (Human Identity)", "認知明晰度スライダー（人間性）", "认知清晰度调节（人类信念）", "認知清晰度調節（人類信念）"),
        ["CognitionClear"] = ("Clear Mind", "認知正常", "认知清晰", "認知清晰"),
        ["CognitionBroken"] = ("Cognitive Collapse", "認識崩壊", "认知崩溃", "認知崩潰"),
        ["SetClearBtn"] = ("Set Clear (100%)", "正気に戻す (100%)", "设为清醒(100%)", "設為清醒(100%)"),
        ["SetLostBtn"] = ("Set Lost (30%)", "迷走させる (30%)", "设为迷失(30%)", "設為迷失(30%)"),
        ["ExitPathfindingStatus"] = ("Exit Pathfinding: ", "出口への経路探索：", "出口寻路状态：", "出口尋路狀態："),
        ["ExitCanFind"] = ("Exit route clear (knows how to exit)", "出口へのルート明瞭（脱出可能）", "出口路线清晰（能找到出口）", "出口路線清晰（能找到出口）"),
        ["ExitCannotFind"] = ("Lost exit route (lacks cognition)", "出口への道筋不明（迷子・忘却）", "不知道怎么走到动物园出口（认知不足或已迷失）", "不知道怎麼走到動物園出口（認知不足或已迷失）"),
        ["ExitDescClear"] = ("Normal Cognition: Exit passages are linked in mind; knows how to reach Zoo Exit.",
            "認知正常：脳内の出口通路が繋がっており、動物園出口への向かい方を把握しています。",
            "认知正常：角色脑内出口通道连通，知道如何走向动物园出口。",
            "認知正常：角色腦內出口通道連通，知道如何走向動物園出口。"),
        ["ExitDescBroken"] = ("Impaired Cognition (<50%): Exit and outer space forgotten; completely lost and cannot find exit!",
            "認知崩壊（<50%）：出口通路と園外の空間を完全に忘却し、園外へ出る方法を見失っています！",
            "认知不足（<50%）：脑内已遗忘出口通道与园外空间，彻底不知道怎么走出动物园！",
            "認知不足（<50%）：腦內已遺忘出口通道與園外空間，徹底不知道怎麼走出動物園！"),
        ["ActualTravelLog"] = ("Actual Movement Trajectory", "実際の移動軌跡ログ", "实际移动路线轨迹", "實際移動路線軌跡"),
        ["MinUnit"] = ("min", "分", "分钟", "分鐘"),

        // Mind Interventions
        ["MindInterventionTitle"] = ("Mental World Interventions", "脳内世界インターベンション", "脑内世界干预器", "腦內世界干預器"),
        ["MindInterventionSub"] = ("Tamper with target's subjective perceptions & memory", "この人物の主観的知覚と記憶を改ざん", "篡改此角色的主观感知与记忆", "篡改此角色的主觀感知與記憶"),
        ["SelectMemoryType"] = ("Filter Memory Type:", "記憶タイプの絞り込み：", "选择记忆类型：", "選擇記憶類型："),
        ["AllMemories"] = ("All Memories ({0})", "すべての記憶 ({0})", "全部记忆 ({0})", "全部記憶 ({0})"),
        ["FilterSpace"] = ("🏢 Spaces & Zones", "🏢 エリア・区画", "🏢 场景园区", "🏢 場景園區"),
        ["FilterBoundary"] = ("🚪 Doors & Paths", "🚪 扉と通路", "🚪 门与通道", "🚪 門與通道"),
        ["FilterBody"] = ("👤 Self & People", "👤 自身と人物", "👤 自身与人物", "👤 自身與人物"),
        ["FilterObject"] = ("📦 Items & Objects", "📦 アイテム・物品", "📦 物品道具", "📦 物品道具"),
        ["FilterConcept"] = ("💡 Rules & Thoughts", "💡 ルール・思考", "💡 规则念头", "💡 規則念頭"),
        ["SelectMentalTarget"] = ("Select mental object to tamper:", "介入する脳内オブジェクトを選択：", "选择要干预的脑内对象：", "選擇要干預的腦內對象："),
        ["CurrentSelected"] = ("Current Selected: ", "現在選択中：", "当前选中：", "當前選中："),
        ["ExistsInMind"] = ("Exists in mind", "脳内に実在", "脑中存在", "腦中存在"),
        ["NotExistsInMind"] = ("Believed non-existent", "脳内で非実在と断定", "脑中断定不存在", "腦中斷定不存在"),
        ["DoubtInMind"] = ("Doubted in mind", "脳内で真偽未定", "脑中存疑", "腦中存疑"),
        ["ActionFakeDanger"] = ("⚠️ Fabricate Danger (makes them avoid this place)", "⚠️ 危険の錯覚を植え付ける（危険と認識して回避）", "⚠️ 制造险情假象（让他以为此处危险而避开）", "⚠️ 製造險情假象（讓他以為此處危險而避開）"),
        ["ActionFakeSafe"] = ("🌳 Fabricate Safety (clears danger, marks shade)", "🌳 安全の錯覚を植え付ける（危険解除、木陰を想起）", "🌳 制造安全假象（清除危险，标记阴凉树荫）", "🌳 製造安全假象（清除危險，標記陰涼樹蔭）"),
        ["ActionBlockDoor"] = ("🚫 Block Passage in Mind (make them think door is impassable/gone)", "🚫 脳内で通路を封鎖（通行不可または消失と錯覚）", "🚫 脑内封死此门（误以为不通或不存在）", "🚫 腦內封死此門（誤以為不通或不存在）"),
        ["ActionUnblockDoor"] = ("🔓 Reopen Passage (remind them path is clear)", "🔓 通路を想起させる（通路が開通していると認識）", "🔓 确认通路（重新回忆起此门通畅）", "🔓 確認通路（重新回憶起此門通暢）"),
        ["ActionRabbitSuggest"] = ("🐰 Believe body is 80% rabbit", "🐰 肉体のウサギ化を80%と認識", "🐰 暗示身体兔化80%", "🐰 暗示身體兔化80%"),
        ["ActionHumanAffirm"] = ("🧑 Affirm Human Identity (Identity restored to 100%)", "🧑 人間性の再強化（自己信念を100%に回復）", "🧑 坚定人类认同（信念恢复至100%）", "🧑 堅定人類認同（信念恢復至100%）"),
        ["ActionForget"] = ("❌ Forget Object (erase from mind)", "❌ 忘却させる（脳内から抹消）", "❌ 遗忘此对象（从脑海中抹去）", "❌ 遺忘此對象（從腦海中抹去）"),
        ["ActionCreateHallucination"] = ("✨ Create Hallucination (fabricate new object)", "✨ 幻覚を捏造（存在しないものを創造）", "✨ 新建幻觉对象（凭空捏造）", "✨ 新建幻覺對象（憑空捏造）"),

        // Advanced details
        ["AdvancedParams"] = ("Advanced Raw Parameter Editor (Tags, Position, Relations & Rules)", "深層パラメータ詳細編集（タグ、位置、関係、ルール）", "高级底层参数编辑（标签、位置、关系与规则）", "高級底層參數編輯（標籤、位置、關係與規則）"),
        ["ParamName"] = ("Name:", "名称：", "名称：", "名稱："),
        ["ParamKind"] = ("Kind:", "分類：", "类别：", "類別："),
        ["ParamExistence"] = ("Existence:", "存在確信度：", "是否存在：", "是否存在："),
        ["ParamParent"] = ("Believed Parent Location:", "脳内における所属先：", "脑内所属位置：", "腦內所屬位置："),
        ["UnknownParent"] = ("Unknown / None", "場所不明 / なし", "不知道位置 / 无所属对象", "不知道位置 / 無所屬對象"),
        ["ParamTags"] = ("Tags (one per line Name=Int, ? for unknown):", "タグ（1行に 名称=数値、? で不明）：", "标签（每行 名称=整数，? 表示未知）：", "標籤（每行 名稱=整數，? 表示未知）："),
        ["ClickAppendTag"] = ("Click to append common cognitive tags:", "認知タグをクリックして追加：", "点击追加常用认知标签：", "點擊追加常用認知標籤："),
        ["EditTopology"] = ("Edit Mental Relation Topology", "脳内関係トポロジーを編集", "修改脑内关系拓扑", "修改腦內關係拓撲"),
        ["WriteAstRule"] = ("Write Core Rule AST for this Mental Object", "この脳内対象にCoreルールASTを付与", "为此脑内对象写 Core 规则 AST", "為此腦內對象寫 Core 規則 AST"),
        ["AcceptAst"] = ("Adopt this AST", "このASTを採信", "采信此 AST", "採信此 AST"),
        ["SaveMentalBtn"] = ("💾 Save Parameters & Recalculate Decisions", "💾 パラメータ保存＆意思決定を再計算", "💾 保存底层参数并重算决策", "💾 保存底層參數並重算決策"),
        ["PreviewDecision"] = ("Preview Decision Diff", "意思決定の差分プレビュー", "仅预览决策对比", "僅預覽決策對比"),
        ["ReplanAction"] = ("⚡ Cancel Current Action & Re-choose via Mind", "⚡ 現行アクションを中断し、現在の脳内認識で再選定", "⚡ 取消旧行动并按当前脑内世界重选", "⚡ 取消舊行動並按當前腦內世界重選"),
        ["ObserveAgain"] = ("👁️ Observe On-site (Correct Misconceptions)", "👁️ 現地を再観察（認識の現実補正）", "👁️ 现场重新观察（纠正认知）", "👁️ 現場重新觀察（糾正認知）"),

        // Tab 4 Decisions
        ["TargetPersonAnalyze"] = ("Current Person for Decision Analysis:", "意思決定分析の対象人物：", "当前分析决策的人物：", "當前分析決策的人物："),
        ["DecisionDiff"] = ("Decision Snapshot Comparison", "意思決定前後のスナップショット比較", "决策前后对比", "決策前後對比"),
        ["DecisionDiffSub"] = ("Before Tampering vs After Tampering", "改ざん前 vs 改ざん後", "认知篡改前 vs 认知篡改后", "認知篡改前 vs 認知篡改後"),
        ["SnapshotBefore"] = ("Before Tampering", "改ざん前のスナップショット", "修改前快照", "修改前快照"),
        ["SnapshotAfter"] = ("After Tampering", "改ざん後のスナップショット", "修改后快照", "修改後快照"),
        ["CandidateActions"] = ("All Candidate Actions", "全候補アクション一覧", "当前所有候选行动", "當前所有候選行動"),
        ["ForbiddenIncluded"] = ("Includes actions forbidden by rules", "ルールで禁止された行動を含む", "含被规则禁止的行动", "含被規則禁止的行動"),
        ["Score"] = ("Score", "スコア", "得分", "得分"),
        ["Forbidden"] = ("Forbidden", "禁止", "禁止", "禁止"),
        ["Available"] = ("Available", "実行可", "可选", "可選"),
        ["Reasons"] = ("Reasons: ", "理由：", "理由：", "理由："),
        ["DecisionTrace"] = ("Mental Decision Tree Trace", "脳内意思決定ツリーの追跡ログ", "脑内决策树链路", "腦內決策樹鏈路"),
        ["DecisionTraceSub"] = ("Observe → Understand → Spatial Cond → Score Weight → Core Select", "観察 → 理解 → 空間条件 → 重み付け → Core選定", "观察 → 理解 → 空间条件 → 建议权重 → Core 选择", "觀察 → 理解 → 空間條件 → 建議權重 → Core 選擇"),
        ["DefaultRoutineTip"] = ("Currently guided by routine touring, resting, or duty shifts.", "現在は通常の観光、休息、またはシフト業務により選択中。", "当前由日常游览、休息或岗位计划选择行动。", "當前由日常遊覽、休息或崗位計劃選擇行動。"),
        ["ObservationHistory"] = ("Recent Observation History", "最近の観察・知覚ログ", "最近观察历史", "最近觀察歷史"),
        ["MentalCountSub"] = ("{0} objects in mind", "脳内オブジェクト数: {0}", "{0} 个脑内对象", "{0} 個腦內對象"),

        // Truth & Actions
        ["TruthTrue"] = ("Accepted", "受容", "采信", "採信"),
        ["TruthFalse"] = ("Rejected", "拒否", "拒绝", "拒絕"),
        ["TruthUnknown"] = ("Undecided", "未定", "未定", "未定"),

        // Status Messages
        ["SelfForgetConsequence"] = ("Forgetting self removes the action subject and stops autonomous decisions.", "自己を忘れると行動主体を指示できず、自主判断が停止します。", "遗忘自身后，思维将无法指代行动主体，自主决策会停止。", "遺忘自身後，思維將無法指代行動主體，自主決策會停止。"),
        ["SelfMissing"] = ("Self is forgotten: no action subject can be bound. Autonomous decisions are stopped; the body remains in the world. Routine observation does not restore self.", "自己を忘れたため行動主体を指示できません。自主判断は停止し、身体は現実に残ります。通常の観察では自己は復元されません。", "已遗忘自身，无法绑定行动主体，自主决策已停止。身体仍留在现实中，常规观察不会自动恢复自身概念。", "已遺忘自身，無法綁定行動主體，自主決策已停止。身體仍留在現實中，常規觀察不會自動恢復自身概念。"),
        ["ForgotNamed"] = ("Forgot “{0}”. Related links and rules have been removed; the view is refreshed.", "「{0}」を忘却しました。関連する関係とルールを削除し、表示を更新しました。", "已遗忘“{0}”，相关关系与引用它的规则已移除，界面已刷新。", "已遺忘「{0}」，相關關係與引用它的規則已移除，介面已重新整理。"),
        ["HumanConceptMissing"] = ("The human concept is missing: reaffirming human identity is unavailable. The numeric belief is a separate memory.", "人間の概念を失っているため、人間性の再確認は選べません。信念の数値は別の記憶です。", "已遗忘“人类”概念，无法选择“坚定人类”。下方信念数值是独立保留的记忆。", "已遺忘「人類」概念，無法選擇「堅定人類」。下方信念數值是獨立保留的記憶。"),
        ["NoCandidates"] = ("No available candidate actions.", "選択できる行動がありません。", "当前没有可选行动。", "目前沒有可選行動。"),
        ["NextDecision"] = ("Next choice from saved cognition: ", "保存済み認識による次の選択：", "按已保存认知的下一选择：", "按已儲存認知的下一選擇："),
        ["NoPendingAction"] = ("No action in progress", "実行中の行動なし", "当前没有正在执行的行动", "目前沒有正在執行的行動"),
        ["EditFailed"] = ("Operation failed: {0}", "操作失敗：{0}", "操作未完成：{0}", "操作未完成：{0}"),
        ["RefreshFailed"] = ("Change saved, but decision refresh failed: {0}", "変更は保存済みですが、判断更新に失敗：{0}", "修改已保存，但决策刷新失败：{0}", "修改已儲存，但決策重新整理失敗：{0}"),
        ["MentalCreated"] = ("Created a new mental object. You can edit it now.", "脳内オブジェクトを作成しました。編集できます。", "已创建脑内对象，可以继续编辑。", "已建立腦內物件，可以繼續編輯。"),
        ["PreviewUpdated"] = ("Decision view refreshed from saved cognition.", "保存済みの認識で判断表示を更新しました。", "已按保存的认知刷新决策视图。", "已按儲存的認知重新整理決策視圖。"),
        ["WorldCreated"] = ("World created; you can pause to edit mental objects or rules, then compare decisions.", "世界を作成しました。一時停止して脳内オブジェクトやルールを編集し、選択を比較できます。", "已创建世界；可以暂停编辑脑内对象或规则，再比较选择。", "已創建世界；可以暫停編輯腦內對象或規則，再比較選擇。"),
        ["AdvancingTo"] = ("Advancing to target, currently {0}.", "目標時刻へ進行中、現在 {0}。", "正在推进至目标时刻，当前 {0}。", "正在推進至目標時刻，當前 {0}。"),
        ["AdvancedTo"] = ("Advanced to {0}.", "{0} まで進めました。", "已推进至 {0}。", "已推進至 {0}。"),
        ["StatusOnlyMindModified"] = ("Only mental world modified. Inspect diff and click re-choose to submit.", "脳内世界のみを変更しました。比較確認後に再選定を実行できます。", "只修改了脑内世界。查看对比后可按当前认知重选行动。", "只修改了腦內世界。查看對比後可按當前認知重選行動。"),
        ["StatusForgotten"] = ("Object forgotten from mind; physical entity remains unchanged.", "対象を脳内から忘却させました。現実のオブジェクトは維持されます。", "对象已从脑内遗忘，现实对象仍然存在。", "對象已從腦內遺忘，現實對象仍然存在。"),
        ["StatusRelationSaved"] = ("Mental relationship updated.", "脳内関係が更新されました。", "脑内关系已更新。", "腦內關係已更新。"),
        ["StatusBeliefSaved"] = ("Core AST rule added to person's belief.", "対象のCoreルールが人物の認知に反映されました。", "此对象的Core规则已进入人物认知。", "此對象的Core規則已進入人物認知。"),
        ["StatusReplanDone"] = ("Cancelled pending phases of old action; new plan submitted based on mind. Sunk costs not refunded.", "旧アクションの未完了段階を中断しました。現在の脳内認知に基づき新行動を開始します（消費コストは返還されません）。", "已取消旧行动的未完成阶段；新行动按当前脑内世界提交，已支付代价不返还。", "已取消舊行動的未完成階段；新行動按當前腦內世界提交，已支付代價不返還。"),
        ["StatusObserved"] = ("On-site observation refreshed mental world.", "現場の観察により脳内認識が更新されました。", "现场观察已更新脑内世界。", "現場觀察已更新腦內世界。"),
        ["StatusRulePublished"] = ("Only on-site version updated; choices unchanged for those unread.", "現地版のみ更新されました。未読の人物の行動選択は変わりません。", "仅更新现场版本；未读到的人选择不变。", "僅更新現場版本；未讀到的人選擇不變。"),
        ["StatusBroadcasted"] = ("Revision entered minds via broadcast. Diff reflects next choice; queued actions proceed.", "改定が放送を通じて認知に伝達されました。比較表示は次回選択に反映されます。", "修订通过广播进入认知。对比显示下一次选择；已排队行动继续原计划。", "修訂通過廣播進入認知。對比顯示下一次選擇；已排隊行動繼續原計劃。"),
        ["StatusWithdrawn"] = ("Clause withdrawn, {0} people heard withdrawal broadcast.", "条文を撤回しました。{0} 人が撤回放送を聞き取りました。", "文书已撤回，{0} 人已听见撤回广播。", "文書已撤回，{0} 人已聽見撤回廣播。"),
        ["StatusWithdrawLocal"] = ("Only removed on-site documents; prior memories kept.", "現地の文書のみ撤去しました。過去の記憶はそのまま保持されます。", "仅撤下现场文书，旧记忆暂时保留。", "僅撤下現場文書，舊記憶暫時保留。"),
        ["StatusUnderstandingChanged"] = ("Modified this person's acceptance & understanding.", "この人物の理解度と受容姿勢を変更しました。", "修改只作用于此人对该条文书的理解。", "修改只作用於此人對該條文書的理解。"),
        ["StatusCast"] = ("Core parsed utterance. Re-planning can trigger immediately, otherwise waits for current action.", "Coreが言霊を構文解析しました。脳内再選定を押せば即時反映されます。", "Core已解析言灵。按脑内重选可立即提交，否则等待现有行动结束。", "Core已解析言靈。按腦內重選可立即提交，否則等待現有行動結束。"),
        ["StatusCognitionDropped"] = ("Cognition reduced: mental map degraded, exit paths forgotten, cannot route exit.", "認知を低下させました：脳内マップが退化し出口通路を忘れ、脱出経路を計算できなくなりました。", "已降低此人认知：脑内地图退化，出口通道已遗忘，无法寻路离园。", "已降低此人認知：腦內地圖退化，出口通道已遺忘，無法尋路離園。"),
        ["StatusCognitionRestored"] = ("Cognition restored: mental map complete, exit paths remembered.", "認知を回復させました：脳内マップが修復され出口通路を思い出しました。", "已恢复此人认知：脑内地图完整，已回忆起出口通道。", "已恢復此人認知：腦內地圖完整，已回憶起出口通道。"),

        // Room and Job translations
        ["Room.Entrance"] = ("Entrance", "正門入口", "入口", "入口"),
        ["Room.MainPath"] = ("Main Boulevard", "メインアベニュー", "主步道", "主步道"),
        ["Room.Lions"] = ("Lion Area", "ライオン区", "狮子园区", "狮子园区"),
        ["Room.Elephants"] = ("Elephant Area", "ゾウ区", "大象园区", "大象园区"),
        ["Room.DrinkShop"] = ("Beverage Shop", "売店", "饮料店", "飲料店"),
        ["Room.Aquarium"] = ("Aquarium", "水族館", "海洋馆", "海洋館"),
        ["Room.Outside"] = ("Outside", "園外", "园外", "園外"),
        ["Room.Jellyfish"] = ("Jellyfish Area", "クラゲ水槽区", "水母区", "水母區"),
        ["Job.Visitor"] = ("Visitor", "観光客", "游客", "遊客"),
        ["Job.Employee"] = ("Employee", "従業員", "员工", "員工"),
        ["Job.Security"] = ("Security Guard", "警備員", "保安", "保安"),
        ["Outcome.InPark"] = ("In Park", "滞在中", "在园", "在園"),
        ["Outcome.Escaped"] = ("Escaped", "脱出成功", "成功逃离", "成功逃離"),
        ["Outcome.Dead"] = ("Deceased", "死亡", "遇难", "遇難"),
        ["Outcome.Rabbit"] = ("Transformed to Rabbit", "ウサギ化", "化为兔子", "化為兔子"),
        ["Outcome.BlackCoat"] = ("Became Black-coat", "黒服化", "加入黑衣", "加入黑衣"),
    };

    public static string T(string key, params object[] args)
    {
        if (!Dict.TryGetValue(key, out var tuple))
        {
            return args.Length > 0 ? string.Format(key, args) : key;
        }

        var template = Current switch
        {
            Lang.Ja => tuple.ja,
            Lang.ZhHans => tuple.zhHans,
            Lang.ZhHant => tuple.zhHant,
            _ => tuple.en
        };

        return args.Length > 0 ? string.Format(template, args) : template;
    }

    public static string TRoom(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name switch
        {
            "入口" => T("Room.Entrance"),
            "主步道" => T("Room.MainPath"),
            "狮子园区" => T("Room.Lions"),
            "大象园区" => T("Room.Elephants"),
            "饮料店" => T("Room.DrinkShop"),
            "海洋馆" => T("Room.Aquarium"),
            "园外" => T("Room.Outside"),
            "水母区" => T("Room.Jellyfish"),
            _ => Vocabulary.Display(name)
        };
    }

    public static string TJob(object? job)
    {
        if (job == null) return "";
        var str = job.ToString();
        return str switch
        {
            "Visitor" or "游客" => T("Job.Visitor"),
            "Blue" or "Red" or "Black" or "Guard" or "Director" => T("Job."+str),
            "Employee" or "员工" => T("Job.Employee"),
            "Security" or "保安" => T("Job.Security"),
            _ => str ?? ""
        };
    }

    public static string TOutcome(string? outcome)
    {
        if (string.IsNullOrEmpty(outcome)) return "";
        return outcome switch
        {
            "在园" => T("Outcome.InPark"),
            "成功逃离" => T("Outcome.Escaped"),
            "遇难" => T("Outcome.Dead"),
            "化为兔子" => T("Outcome.Rabbit"),
            "加入黑衣" => T("Outcome.BlackCoat"),
            _ => outcome
        };
    }

    public static string TClock(double now)
    {
        var day = (int)now / 1440 + 1;
        var hour = (int)now % 1440 / 60;
        var min = (int)now % 60;
        return Current switch
        {
            Lang.Ja => $"{day}日目 {hour:D2}:{min:D2}",
            Lang.ZhHans => $"第{day}天 {hour:D2}:{min:D2}",
            Lang.ZhHant => $"第{day}天 {hour:D2}:{min:D2}",
            _ => $"Day {day} {hour:D2}:{min:D2}"
        };
    }
}
