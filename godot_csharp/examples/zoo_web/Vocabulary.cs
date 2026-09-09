using System.Text.RegularExpressions;

namespace zoo_web;

// 可调用标识符使用无空格的词形，保持受控语法与 AST 的分词边界。
public static class Vocabulary
{
    private const string Words = """
当|when|条件|當
时|then|なら|時
优先|prefer|優先|優先
必须|require|必須|必須
禁止|forbid|禁止|禁止
允许|permit|許可|允許
权重|weight|重み|權重
前往|go|移動|前往
总是|always|常時|總是
出逃兔子|escaped_rabbit|脱走ウサギ|出逃兔子
双路|split_path|分岐路|雙路
象异常|elephant_anomaly|ゾウ異常|象異常
独处|alone|一人|獨處
兔区|in_rabbit_area|ウサギ区内|兔區
馆外|at_aquarium_gate|水族館前|館外
笑声|laughter|笑い声|笑聲
有地图|has_map|地図所持|有地圖
有纸|has_paper|紙片所持|有紙
迷路|lost|迷子|迷路
危险|danger|危険|危險
获救|rescued|救助済み|獲救
多余白狮|extra_lion|余分な白ライオン|多餘白獅
成年|adult|成人|成年
玩偶过期|toy_expired|玩具期限切れ|玩偶過期
兔耳入象区|rabbit_ears_at_elephants|ゾウ区のウサギ耳|兔耳入象區
反复走散|repeatedly_lost|繰り返す迷子|反覆走散
查货到期|stock_check_due|在庫点検時刻|查貨到期
食品可见|food_visible|食品あり|食品可見
修剪到期|trimming_due|剪定時刻|修剪到期
黑衣|black_coat|黒服|黑衣
喂食到期|feeding_due|給餌時刻|餵食到期
象区|in_elephant_area|ゾウ区内|象區
有求助|help_requested|救助依頼あり|有求助
有血|has_blood|血液所持|有血
持续症状|persistent_symptoms|症状持続|持續症狀
无人|nobody|無人|無人
猿区|in_ape_area|サル区内|猿區
馆内|inside_aquarium|水族館内|館內
夜间|night|夜間|夜間
员工在场|staff_present|職員在場|員工在場
白天|daytime|昼間|白天
日间停留到期|day_visit_due|昼間滞在期限|日間停留到期
夜间不足四小时|night_under_four_hours|夜間四時間未満|夜間不足四小時
鲸区|in_whale_area|クジラ区内|鯨區
客房|GuestRoom|客室|客房
灯亮|lamp_on|点灯中|燈亮
疲倦|tired|疲労|疲倦
红衣|red_coat|赤服|紅衣
上班|on_duty|勤務中|上班
下班|off_duty|勤務終了|下班
巡逻到期|patrol_due|巡回時刻|巡邏到期
周日|sunday|日曜日|週日
有离馆者|departing_visitor|退館者あり|有離館者
黑衣借宿|black_coat_lodging|黒服宿泊|黑衣借宿
有兔玩偶|has_rabbit_toy|ウサギ玩具所持|有兔玩偶
动摇|shaken|動揺|動搖
查狮到期|lion_check_due|ライオン点検時刻|查獅到期
狮区|in_lion_area|ライオン区内|獅區
停电|power_outage|停電|停電
办公室|in_office|事務室内|辦公室
水母死亡|jellyfish_dead|クラゲ死亡|水母死亡
隔离中|isolating|隔離中|隔離中
保安在岗|guard_on_duty|警備員勤務中|保安在崗
猿区已空|apes_empty|サル区無人|猿區已空
封闭到期|closure_due|閉鎖期限|封閉到期
游览|tour|見学|遊覽
休息|rest|休憩|休息
阅读|read|読む|閱讀
撕地图|tear_map|地図を破る|撕地圖
喂虚线纸|feed_paper|紙片を与える|餵虛線紙
喂兔子|feed_rabbit|ウサギに給餌|餵兔子
购买兔子血|buy_blood|ウサギ血を買う|購買兔子血
吃山羊肉|eat_meat|ヤギ肉を食べる|吃山羊肉
换黑衣|wear_black|黒服に着替える|換黑衣
求助|ask_help|助けを求める|求助
坚定人类|reaffirm_human|人間性を確認|堅定人類
无视|ignore|無視|無視
混入猿猴|mix_with_apes|サルに混じる|混入猿猴
维修|repair|修理|維修
关灯|switch_off|消灯|關燈
修剪|trim|剪定|修剪
引兔至狮区|lure_rabbit|ウサギを誘導|引兔至獅區
泼兔子血|splash_blood|血を撒く|潑兔子血
丢弃纸片|discard_paper|紙片を捨てる|丟棄紙片
投纸信号|signal_paper|紙片で合図|投紙信號
查货|inspect_stock|在庫点検|查貨
巡逻|patrol|巡回|巡邏
辞职|resign|辞職|辭職
隔离十三小时|isolate|十三時間隔離|隔離十三小時
抢夺纸片|steal_paper|紙片を奪う|搶奪紙片
驱离客房|evict|客室から退去|驅離客房
收存食品|collect_food|食品回収|收存食品
充电|charge|充電|充電
报告|report|報告|報告
跟随|follow|追従|跟隨
买兔玩偶|buy_toy|ウサギ玩具を買う|買兔玩偶
丢弃玩偶|discard_toy|玩具を捨てる|丟棄玩偶
护送|escort|護送|護送
疏散|evacuate|避難誘導|疏散
封猿区|close_apes|サル区閉鎖|封猿區
值守|stand_guard|当直|值守
误认大象|misidentify_elephant|ゾウを誤認|誤認大象
认同山羊|identify_goat|ヤギと自認|認同山羊
交出地图|give_map|地図を渡す|交出地圖
劝阻|dissuade|制止|勸阻
收缴玩偶|collect_toy|玩具回収|收繳玩偶
交谈|talk|会話|交談
记录白狮|count_lions|白ライオンを数える|記錄白獅
领取地图|get_map|地図を受け取る|領取地圖
清理|clean|清掃|清理
开猿区|open_apes|サル区開放|開猿區
认同大象|identify_elephant|ゾウと自認|認同大象
园外|Outside|園外|園外
入口|Entrance|正門入口|入口
主步道|MainPath|メイン通路|主步道
兔子园区|RabbitArea|ウサギ園区|兔子園區
猿猴园区|ApeArea|サル園区|猿猴園區
猿猴混处区|MixedApeArea|サル混在区|猿猴混處區
左街|LeftStreet|左通路|左街
后勤区|ServiceArea|管理区域|後勤區
大象园区|ElephantArea|ゾウ園区|大象園區
狮子园区|LionArea|ライオン園区|獅子園區
饮料店|DrinkShop|飲料店|飲料店
海洋馆门口|AquariumGate|水族館入口|海洋館門口
海洋馆|Aquarium|水族館|海洋館
水母区|JellyfishArea|クラゲ区|水母區
鲸鱼区|WhaleArea|クジラ区|鯨魚區
内部宿舍|StaffDormitory|職員寮|內部宿舍
园长办公室|DirectorOffice|園長室|園長辦公室
安保室|SecurityOffice|警備室|安保室
生命|health|体力|生命
兔化|rabbit_change|ウサギ化|兔化
关注|attention|注目|關注
人类信念|human_belief|人間信念|人類信念
虚线纸|paper_strip|切取紙片|虛線紙
完整地图|whole_map|完全地図|完整地圖
异常食品|anomalous_food|異常食品|異常食品
动作|deed|動作|動作
灯光|light|照明|燈光
树荫|shade|木陰|樹蔭
言灵安静|utterance_calm|言霊静寂|言靈安靜
岗位|job|職務|崗位
分钟|minutes|分|分鐘
路长|distance|距離|路長
开放|open|開放|開放
言灵次数|mana|言霊回数|言靈次數
症状分钟|symptom_minutes|症状時間|症狀分鐘
离园|escape|退園|離園
兔耳饰|rabbit_ears|ウサギ耳飾り|兔耳飾
兔玩偶|rabbit_toy|ウサギ玩具|兔玩偶
年龄|age|年齢|年齡
吼叫|roaring|咆哮|吼叫
白狮数量|lion_count|白ライオン数|白獅數量
猿猴混处|mixed_apes|サル混在|猿猴混處
危险征兆|danger_sign|危険兆候|危險徵兆
时刻|clock|時刻|時刻
客房灯|guest_lamp|客室灯|客房燈
办公室供电|office_power|事務室電源|辦公室供電
理解程度|understanding|理解度|理解程度
普通离园资格|exit_eligible|通常退園資格|普通離園資格
海馆离园资格|aquarium_exit_eligible|水族館退園資格|海館離園資格
通往|leads_to|行き先|通往
起点|from|出発点|起點
岗位地点|duty_at|担当場所|崗位地點
兔化稳定|rabbit_stable|ウサギ化安定|兔化穩定
写入关系|SaveRelation|関係を保存|寫入關係
""";
    private static readonly string[][] Entries = Words.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line=>line.Trim().Split('|')).ToArray();
    private static readonly Dictionary<string,string> Canonical = BuildCanonical();
    private static readonly Dictionary<string,string>[] Displays = Enumerable.Range(0,4)
        .Select(column=>Entries.ToDictionary(row=>row[0],row=>row[column])).ToArray();
    private static Dictionary<string,string> BuildCanonical()
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in Entries)
        foreach(var word in row)
        {
            if(result.TryGetValue(word,out var old)&&old!=row[0])throw new InvalidOperationException($"词汇歧义：{word}");
            result[word]=row[0];
        }
        return result;
    }
    private static readonly Regex Token = new(@"[\p{L}\p{N}_]+", RegexOptions.CultureInvariant);
    public static string ToCanonical(string text)
    {
        var ast=text.Contains('(');
        return Token.Replace(text,m=>ast && m.Value.ToLowerInvariant() is "require" or "forbid" or "prefer" or "permit"
            ? m.Value : Canonical.GetValueOrDefault(m.Value,m.Value));
    }
    public static string Display(string text)
    {
        var column=I18n.Current switch {Lang.En=>1,Lang.Ja=>2,Lang.ZhHant=>3,_=>0};
        var map=Displays[column];
        return Token.Replace(text,m=>map.GetValueOrDefault(m.Value,m.Value));
    }
}
