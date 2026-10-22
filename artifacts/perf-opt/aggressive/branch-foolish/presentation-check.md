# 非语言声响有限投影验证

双局正式会话封存后，仅补呈现元数据与通用投影：30透传environmental-sound类别和surface_voice；60以“现场声响”代替日志行动者的主观姓名；40两种开闩声明标注非语言声响。没有改行为、知识或动作链。

当前Standard/Foolish双局证据属于这项呈现补丁之前的源码；不能宣称整局已展示最新中性抬头。根代理最终同源码正式测试另行覆盖。

执行当前源码生成的有限C#投影夹具，不创建正式会话、不启动AI。分别消费c1dep_actions中雀儿和恶魔真实声音声明，构造仅含一次applied执行及一个heard投影的最小夹具，实际调用dvg_presentation；这是投影接口验证，不是游戏实况。

命令：

```
python mcp/build/build_session.py --out .scratch/s4-sound-projection --entry .scratch/s4-sound-projection/fixture.dv
dotnet build .scratch/s4-sound-projection/Program.csproj -c Release --disable-build-servers -p:NuGetAudit=false
dotnet .scratch/s4-sound-projection/bin/Release/net8.0/Program.dll
```

生成和编译成功，0警告0错误；夹具退出码0。两个实际输出均为actual_actor_retained=true、display=现场声响、surface_voice=narration；surface分别为“门外传来几下金属拨动声，接着是一声闩杆碰响。”和“门外响起闩杆碰撞的金属声。”。夹具要求voice也为narration、presentation_kind保留environmental-sound，均未抛错。

临时夹具源码SHA-256：d37997b40a08480a954cd3c4e10a08cd09a3ad388c47af67722e2836f9fe884f

夹具源码（临时工程任务结束时删除）：

```dv
#include ../../dv_core/modules/60-game.dv
fn s4_sound_projection():
    let openers = ["quer", "demon"]
    var i = 0
    while i < rt_count(openers):
        let actions = c1dep_actions(openers[i])
        var found = false
        var j = 0
        while j < rt_count(actions):
            let action = actions[j]
            if dv_find(dv_find(action, "presentation", []), "kind", "") == "environmental-sound":
                found = true
                let state = {"story": {"run_config": {}}, "actors": [], "events": [{"id": "fixture.action", "type": "action_execution", "actor": openers[i], "time": 797, "data": {"action": action, "receipt": {"execution": {"status": "applied"}}}}, {"id": "fixture.heard", "type": "listener_parse", "actor": "detective"}]}
                let result = dvg_presentation(state, "detective")
                dv_require(result.visible and result.speaker_name == "现场声响", "Sound must not identify its actual emitter")
                dv_require(result.surface_voice == "narration" and result.voice == "narration", "Sound must not be presented as dialogue")
                dv_require(result.presentation_kind == "environmental-sound", "Sound kind must survive projection")
                rt_json_print({"fixture": "finite-auditory-presentation", "actual_actor_retained": result.actor == openers[i], "display": result.speaker_name, "surface_voice": result.surface_voice, "surface": result.surface})
            j = j + 1
        dv_require(found, "Expected emitted sound declaration")
        i = i + 1
    return true
(main (s4_sound_projection))
```
