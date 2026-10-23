# 已记录的剧情动作与正文摘录

来源：本目录 console.dvlog 和 engine.dvlog。仅展开实际已有记录，不执行世界、不推测未记录结果。

## 运行边界

记录 1: {"kind": "launch", "real_ns": 0, "command": ["dotnet", "C:\\Users\\iii\\Desktop\\dual_ontology_world_engine\\dv_core\\cli\\bin\\Release\\net8.0\\dv-cli.dll", "--jsonl", "--controller", "ai", "--viewer", "detective", "--planning", "selective", "--trace", "C:\\Users\\iii\\Desktop\\dual_ontology_world_engine\\artifacts\\cli-ai-demo\\current\\session.dvdag", "--save", "C:\\Users\\iii\\Desktop\\dual_ontology_world_engine\\artifacts\\cli-ai-demo\\current\\session.dvsave", "--performance", "C:\\Users\\iii\\Desktop\\dual_ontology_world_engine\\artifacts\\cli-ai-demo\\current\\engine", "--source-revision", "4623e6692834217d756444663f27b84f3f060c5a"], "source_revision": "4623e6692834217d756444663f27b84f3f060c5a", "requested_seconds": 104.06200000000536, "hard_timeout_seconds": 134.06200000000536, "requested_game_minutes": 93.0, "planning": "selective", "shutdown_reserve_seconds": 30.0, "scope": "engine AI decisions only; fixed detective observer; formal-model", "source_validation": "not-run-by-user-request"}
记录 5: {"kind": "window_start", "real_ns": 2578000000, "remaining_seconds": 101.48399999999674, "clock_basis": "process launch; opening included"}
记录 1730: {"kind": "window_end", "real_ns": 17203000000, "reason": "game_window_reached", "elapsed_seconds": 17.203000000008615, "next_inputs": 356, "last_status": {"viewer": "detective", "viewer_name": "侦探", "version": 356, "chapter": 1, "game_time": 575, "location": "乙区 · 活动厅", "room": "zone_yi:hall", "map": "living_floor", "people": [{"id": "huo", "name": "霍铎"}, {"id": "shen", "name": "沈鼎"}, {"id": "su", "name": "苏泠"}], "status": "idle", "active": true, "development_mode": false}, "initial_game_time": 480.0, "actual_game_minutes": 95.0}

## 侦探的实际执行呈现

### 481 / detective.opening.wake

原始控制台记录 23；选择关联 {"actor": "detective", "action_id": "detective.opening.wake", "phase": "confirm", "state_version": 3, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-2"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "private-monologue", "surface_voice": "narration", "action_id": "detective.opening.wake", "action_kind": "inspect", "execution_event": "e145", "game_time": 481, "visible": true, "surface": "", "lines": ["我记得的最后一件事，是上船。", "衣架上挂着一件风衣。右手袖口磨得发亮——我写字时总拿那儿蹭桌沿。是我的。", "先出去，看看这是什么地方。"], "judgment": false, "travel": []}

### 482 / detective.opening.wear-coat

原始控制台记录 162；选择关联 {"actor": "detective", "action_id": "detective.opening.wear-coat", "phase": "confirm", "state_version": 33, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-17"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.opening.wear-coat", "action_kind": "inspect", "execution_event": "e251", "game_time": 482, "visible": true, "surface": "", "lines": ["你披上了这件熟悉的呢料风衣，领口与袖口早已磨平。"], "judgment": false, "travel": []}

### 484 / detective.move.R05.corridor

原始控制台记录 455；选择关联 {"actor": "detective", "action_id": "detective.move.R05.corridor", "phase": "confirm", "state_version": 97, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-49"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.R05.corridor", "action_kind": "move", "execution_event": "e504", "game_time": 484, "visible": true, "surface": "", "lines": ["已抵达乙区 · 宿舍走廊。"], "judgment": false, "travel": {"from": "R05", "to": "corridor"}}

### 486 / detective.move.corridor.hall

原始控制台记录 724；选择关联 {"actor": "detective", "action_id": "detective.move.corridor.hall", "phase": "confirm", "state_version": 155, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-78"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.corridor.hall", "action_kind": "move", "execution_event": "e850", "game_time": 486, "visible": true, "surface": "", "lines": ["已抵达乙区 · 活动厅。"], "judgment": false, "travel": {"from": "corridor", "to": "hall"}}

### 487 / detective.inspect.E7

原始控制台记录 854；选择关联 {"actor": "detective", "action_id": "detective.inspect.E7", "phase": "confirm", "state_version": 183, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-92"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E7", "action_kind": "inspect", "execution_event": "e1027", "game_time": 487, "visible": true, "surface": "", "lines": ["一块小石板，粉笔速记两行。\n第一行：主楼：⊥门 ✕开 ◎\n第二行：一个小小的放大镜图样，后面是“⊥✓”。"], "judgment": false, "travel": []}

### 488 / detective.inspect.E8

原始控制台记录 1004；选择关联 {"actor": "detective", "action_id": "detective.inspect.E8", "phase": "confirm", "state_version": 215, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-108"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E8", "action_kind": "inspect", "execution_event": "e1202", "game_time": 488, "visible": true, "surface": "", "lines": ["铜口嵌在活动厅北墙上，下方铜牌刻着一个符号：◎。\n一根铜管从铜口后面出来：一路沿北墙向西，穿墙进门厅，贴着墙下到隔离门的门框；另一路向东，钻进活动厅与 R-08 之间的墙里。\n那道墙上与门框北侧各有一个管接头，一碰就晃——接口松了。"], "judgment": false, "travel": []}

### 489 / detective.opening.introduce

原始控制台记录 1069；选择关联 {"actor": "detective", "action_id": "detective.opening.introduce", "phase": "confirm", "state_version": 229, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-115"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "detective.opening.introduce", "action_kind": "talk", "execution_event": "e1284", "game_time": 489, "visible": true, "surface": "侦探。……做侦探的。", "lines": [], "judgment": false, "travel": []}

### 513 / detective.sound_trial

原始控制台记录 1177；选择关联 {"actor": "detective", "action_id": "detective.sound_trial", "phase": "confirm", "state_version": 253, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-127"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-action", "surface_voice": "narration", "action_id": "detective.sound_trial", "action_kind": "sound_test", "execution_event": "e1529", "game_time": 513, "visible": true, "surface": "", "lines": ["声路试验完成。各处听见了什么，还需分别询问实际在场的人。"], "judgment": false, "travel": []}

### 515 / detective.move.hall.foyer

原始控制台记录 1195；选择关联 {"actor": "detective", "action_id": "detective.move.hall.foyer", "phase": "confirm", "state_version": 257, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-129"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.hall.foyer", "action_kind": "move", "execution_event": "e1568", "game_time": 515, "visible": true, "surface": "", "lines": ["已抵达乙区 · 门厅。"], "judgment": false, "travel": {"from": "hall", "to": "foyer"}}

### 516 / detective.departure.inspect-door

原始控制台记录 1204；选择关联 {"actor": "detective", "action_id": "detective.departure.inspect-door", "phase": "confirm", "state_version": 259, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-130"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.departure.inspect-door", "action_kind": "inspect", "execution_event": "e1603", "game_time": 516, "visible": true, "surface": "查看门内的插闩和门外的横闩。", "lines": [], "judgment": false, "travel": []}

### 517 / detective.inspect.E1

原始控制台记录 1215；选择关联 {"actor": "detective", "action_id": "detective.inspect.E1", "phase": "confirm", "state_version": 261, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-131"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E1", "action_kind": "inspect", "execution_event": "e1617", "game_time": 517, "visible": true, "surface": "", "lines": ["门内侧的插闩落在闩槽里。\n门外侧另有一道横闩，从这一侧拉不动。\n门上的猫眼，玻璃外侧糊满烟灰，看出去一片黑。\n门内侧压着一张泛黄的旧纸，题头是铅印的《事故隔离规程》，没有署名和落款日期。\n旧纸写着：泄漏后，各寝区的隔离门从外侧落闩。查明本区事故经过，由当值者向铜口陈述前，不得开启。\n末行另写：急病者可经铜口呼叫转移。"], "judgment": false, "travel": []}

### 518 / detective.inspect.E2

原始控制台记录 1226；选择关联 {"actor": "detective", "action_id": "detective.inspect.E2", "phase": "confirm", "state_version": 263, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-132"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E2", "action_kind": "inspect", "execution_event": "e1632", "game_time": 518, "visible": true, "surface": "", "lines": ["门内地面铺着一片扇形的灰，窄端贴着门缝，向门厅里散开。\n东南墙角躺着一枚木楔：上面一层灰，底面干净。楔尖有新的磨痕。\n门底边有一道擦痕。"], "judgment": false, "travel": []}

### 519 / detective.inspect.E3

原始控制台记录 1237；选择关联 {"actor": "detective", "action_id": "detective.inspect.E3", "phase": "confirm", "state_version": 265, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-133"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E3", "action_kind": "inspect", "execution_event": "e1645", "game_time": 519, "visible": true, "surface": "", "lines": ["灰上有一枚马球靴印，踩得很清楚。\n鞋尖朝着门侧的插闩。\n鞋跟一端被什么东西压过。"], "judgment": false, "travel": []}

### 520 / detective.inspect.E4

原始控制台记录 1246；选择关联 {"actor": "detective", "action_id": "detective.inspect.E4", "phase": "confirm", "state_version": 267, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-134"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E4", "action_kind": "inspect", "execution_event": "e1660", "game_time": 520, "visible": true, "surface": "", "lines": ["一条拧过的湿抹布，沿门底压成一条直线，正好贴着闭合的门。\n布的一端压住了靴印的鞋跟边缘。"], "judgment": false, "travel": []}

### 521 / detective.inspect.E5

原始控制台记录 1255；选择关联 {"actor": "detective", "action_id": "detective.inspect.E5", "phase": "confirm", "state_version": 269, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-135"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E5", "action_kind": "inspect", "execution_event": "e1674", "game_time": 521, "visible": true, "surface": "", "lines": ["地上一把纯银音叉，柄上刻着“S.L.”。\n门框齐肩高的地方，有一个很小的凹痕。"], "judgment": false, "travel": []}

### 523 / detective.move.foyer.hall

原始控制台记录 1264；选择关联 {"actor": "detective", "action_id": "detective.move.foyer.hall", "phase": "confirm", "state_version": 271, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-136"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.foyer.hall", "action_kind": "move", "execution_event": "e1688", "game_time": 523, "visible": true, "surface": "", "lines": ["已抵达乙区 · 活动厅。"], "judgment": false, "travel": {"from": "foyer", "to": "hall"}}

### 525 / detective.move.hall.pantry

原始控制台记录 1273；选择关联 {"actor": "detective", "action_id": "detective.move.hall.pantry", "phase": "confirm", "state_version": 273, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-137"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.hall.pantry", "action_kind": "move", "execution_event": "e1712", "game_time": 525, "visible": true, "surface": "", "lines": ["已抵达乙区 · 备餐间。"], "judgment": false, "travel": {"from": "hall", "to": "pantry"}}

### 526 / detective.inspect.E9

原始控制台记录 1282；选择关联 {"actor": "detective", "action_id": "detective.inspect.E9", "phase": "confirm", "state_version": 275, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-138"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E9", "action_kind": "inspect", "execution_event": "e1732", "game_time": 526, "visible": true, "surface": "", "lines": ["水槽里四只用过的杯子，各不相同：\n一只青瓷小杯，杯底没有糖渍；\n一只刻着 H 的凹痕锡杯，杯底一层厚糖渍；\n一只缺口白杯；\n一只粗瓷大杯。"], "judgment": false, "travel": []}

### 527 / detective.inspect.E10

原始控制台记录 1291；选择关联 {"actor": "detective", "action_id": "detective.inspect.E10", "phase": "confirm", "state_version": 277, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-139"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E10", "action_kind": "inspect", "execution_event": "e1747", "game_time": 527, "visible": true, "surface": "", "lines": ["罐里的酵种还活着，冒着细泡。\n罐壁上一层摞一层干结的旧面痕，数不清。"], "judgment": false, "travel": []}

### 529 / detective.move.pantry.hall

原始控制台记录 1300；选择关联 {"actor": "detective", "action_id": "detective.move.pantry.hall", "phase": "confirm", "state_version": 279, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-140"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.pantry.hall", "action_kind": "move", "execution_event": "e1762", "game_time": 529, "visible": true, "surface": "", "lines": ["已抵达乙区 · 活动厅。"], "judgment": false, "travel": {"from": "pantry", "to": "hall"}}

### 531 / detective.move.hall.foyer

原始控制台记录 1318；选择关联 {"actor": "detective", "action_id": "detective.move.hall.foyer", "phase": "confirm", "state_version": 283, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-142"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.hall.foyer", "action_kind": "move", "execution_event": "e1823", "game_time": 531, "visible": true, "surface": "", "lines": ["已抵达乙区 · 门厅。"], "judgment": false, "travel": {"from": "hall", "to": "foyer"}}

### 548 / detective.compare_sole

原始控制台记录 1327；选择关联 {"actor": "detective", "action_id": "detective.compare_sole", "phase": "confirm", "state_version": 285, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-143"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.compare_sole", "action_kind": "experiment", "execution_event": "e1840", "game_time": 548, "visible": true, "surface": "", "lines": ["马球靴的鞋底纹路，与门前灰上的印迹一模一样。靴齿里嵌着同样的炭灰。"], "judgment": false, "travel": []}

### 550 / detective.move.foyer.hall

原始控制台记录 1345；选择关联 {"actor": "detective", "action_id": "detective.move.foyer.hall", "phase": "confirm", "state_version": 289, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-145"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.foyer.hall", "action_kind": "move", "execution_event": "e1870", "game_time": 550, "visible": true, "surface": "", "lines": ["已抵达乙区 · 活动厅。"], "judgment": false, "travel": {"from": "foyer", "to": "hall"}}

### 552 / detective.move.hall.corridor

原始控制台记录 1381；选择关联 {"actor": "detective", "action_id": "detective.move.hall.corridor", "phase": "confirm", "state_version": 297, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-149"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.hall.corridor", "action_kind": "move", "execution_event": "e1940", "game_time": 552, "visible": true, "surface": "", "lines": ["已抵达乙区 · 宿舍走廊。"], "judgment": false, "travel": {"from": "hall", "to": "corridor"}}

### 554 / detective.move.corridor.R08

原始控制台记录 1444；选择关联 {"actor": "detective", "action_id": "detective.move.corridor.R08", "phase": "confirm", "state_version": 311, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-156"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.corridor.R08", "action_kind": "move", "execution_event": "e2022", "game_time": 554, "visible": true, "surface": "", "lines": ["已抵达乙区 · 卧室 R-08。"], "judgment": false, "travel": {"from": "corridor", "to": "R08"}}

### 555 / detective.inspect.E6

原始控制台记录 1480；选择关联 {"actor": "detective", "action_id": "detective.inspect.E6", "phase": "confirm", "state_version": 319, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-160"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "visible-inspection", "surface_voice": "narration", "action_id": "detective.inspect.E6", "action_kind": "inspect", "execution_event": "e2076", "game_time": 555, "visible": true, "surface": "", "lines": ["一页手写乐谱，只有一小节。\n前两拍是弱音，各记着一个问号；后两拍重读，字是“开门”。同样的一小节记了两遍。\n旁注：“门外？”\n下方换了一种墨迹：“他们关了门。”"], "judgment": false, "travel": []}

### 557 / detective.move.R08.corridor

原始控制台记录 1507；选择关联 {"actor": "detective", "action_id": "detective.move.R08.corridor", "phase": "confirm", "state_version": 325, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-163"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.R08.corridor", "action_kind": "move", "execution_event": "e2115", "game_time": 557, "visible": true, "surface": "", "lines": ["已抵达乙区 · 宿舍走廊。"], "judgment": false, "travel": {"from": "R08", "to": "corridor"}}

### 559 / detective.move.corridor.hall

原始控制台记录 1543；选择关联 {"actor": "detective", "action_id": "detective.move.corridor.hall", "phase": "confirm", "state_version": 333, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-167"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "narration", "surface_voice": "narration", "action_id": "detective.move.corridor.hall", "action_kind": "move", "execution_event": "e2155", "game_time": 559, "visible": true, "surface": "", "lines": ["已抵达乙区 · 活动厅。"], "judgment": false, "travel": {"from": "corridor", "to": "hall"}}

### 560 / detective.opening.empty-slot

原始控制台记录 1561；选择关联 {"actor": "detective", "action_id": "detective.opening.empty-slot", "phase": "confirm", "state_version": 337, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-169"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "private-monologue", "surface_voice": "narration", "action_id": "detective.opening.empty-slot", "action_kind": "inspect", "execution_event": "e2192", "game_time": 560, "visible": true, "surface": "", "lines": ["柜上有一格贴着我的名字。小门开着，里面什么也没有。"], "judgment": false, "travel": []}

### 561 / detective.opening.ask-reward

原始控制台记录 1588；选择关联 {"actor": "detective", "action_id": "detective.opening.ask-reward", "phase": "confirm", "state_version": 343, "interaction_id": "cli-df47259e73c4410e84674793c0d16186-172"}

{"status": "applied", "actor": "detective", "speaker": "detective", "speaker_name": "侦探", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "detective.opening.ask-reward", "action_kind": "talk", "execution_event": "e2252", "game_time": 561, "visible": true, "surface": "那我的呢？", "lines": [], "judgment": false, "travel": []}

## 其他实际可见正文

### 487 / huo / huo.opening.introduce

原始控制台记录 863

{"status": "applied", "actor": "huo", "speaker": "huo", "speaker_name": "霍铎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "huo.opening.introduce", "action_kind": "talk", "execution_event": "e1031", "game_time": 487, "visible": true, "surface": "……霍铎。钳工。", "lines": [], "judgment": false, "travel": []}

### 487 / su / su.opening.introduce

原始控制台记录 881

{"status": "applied", "actor": "su", "speaker": "su", "speaker_name": "苏泠", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "su.opening.introduce", "action_kind": "talk", "execution_event": "e1051", "game_time": 487, "visible": true, "surface": "苏泠。弹琴的。", "lines": [], "judgment": false, "travel": []}

### 488 / shen / shen.opening.welcome

原始控制台记录 1013

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "同伴", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.opening.welcome", "action_kind": "talk", "execution_event": "e1206", "game_time": 488, "visible": true, "surface": "哟，又醒一个。进来坐，茶刚泡上。", "lines": [], "judgment": false, "travel": []}

### 489 / shen / shen.opening.introduce

原始控制台记录 1078

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.opening.introduce", "action_kind": "talk", "execution_event": "e1299", "game_time": 489, "visible": true, "surface": "我姓沈，沈鼎，烧菜的。", "lines": [], "judgment": false, "travel": []}

### 490 / shen / shen.opening.serve-tea

原始控制台记录 1105

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "visible-action", "surface_voice": "narration", "action_id": "shen.opening.serve-tea", "action_kind": "inspect", "execution_event": "e1350", "game_time": 490, "visible": true, "surface": "", "lines": ["他把茶一杯一杯推过来。推到苏泠面前的那杯，他没有放糖。"], "judgment": false, "travel": []}

### 491 / shen / shen.opening.memory-report

原始控制台记录 1132

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.opening.memory-report", "action_kind": "talk", "execution_event": "e1393", "game_time": 491, "visible": true, "surface": "都还记得啥？我就记得上船。下了跳板往后，脑子里白茫茫一片。", "lines": [], "judgment": false, "travel": []}

### 491 / su / su.opening.ask-sugar

原始控制台记录 1141

{"status": "applied", "actor": "su", "speaker": "su", "speaker_name": "苏泠", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "su.opening.ask-sugar", "action_kind": "talk", "execution_event": "e1408", "game_time": 491, "visible": true, "surface": "……你怎么知道我喝茶不放糖？", "lines": [], "judgment": false, "travel": []}

### 492 / shen / shen.opening.body-habit

原始控制台记录 1159

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.opening.body-habit", "action_kind": "talk", "execution_event": "e1444", "game_time": 492, "visible": true, "surface": "嗯？……顺手吧。", "lines": [], "judgment": false, "travel": []}

### 551 / demon / demon.opening.reward-invitation

原始控制台记录 1372

{"status": "applied", "actor": "demon", "speaker": "demon", "speaker_name": "同伴", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "demon.opening.reward-invitation", "action_kind": "talk", "execution_event": "e1912", "game_time": 551, "visible": true, "surface": "诸位的报偿，就在铜口下方的领取柜里，各有名签。请亲手核验。", "lines": [], "judgment": false, "travel": []}

### 559 / shen / shen.opening.accept-work

原始控制台记录 1552

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.opening.accept-work", "action_kind": "talk", "execution_event": "e2163", "game_time": 559, "visible": true, "surface": "……钱是真的。这活我接着干。", "lines": [], "judgment": false, "travel": []}

### 560 / huo / huo.reply.slate_code

原始控制台记录 1570

{"status": "applied", "actor": "huo", "speaker": "huo", "speaker_name": "霍铎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "huo.reply.slate_code", "action_kind": "talk", "execution_event": "e2196", "game_time": 560, "visible": true, "surface": "⊥，闩。✕，禁止。✕开，不许开。", "lines": [], "judgment": false, "travel": []}

### 560 / shen / shen.reply.cups_known

原始控制台记录 1579

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.reply.cups_known", "action_kind": "talk", "execution_event": "e2211", "game_time": 560, "visible": true, "surface": "给人倒茶我向来不问，看一眼就知道放几勺。", "lines": [], "judgment": false, "travel": []}

### 561 / huo / huo.reply.slate_person

原始控制台记录 1597

{"status": "applied", "actor": "huo", "speaker": "huo", "speaker_name": "霍铎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "huo.reply.slate_person", "action_kind": "talk", "execution_event": "e2258", "game_time": 561, "visible": true, "surface": "小图代人，我习惯这样——画他手里常拿的家伙。⊥✓，闩好了。", "lines": [], "judgment": false, "travel": []}

### 561 / shen / shen.reply.starter_kept

原始控制台记录 1606

{"status": "applied", "actor": "shen", "speaker": "shen", "speaker_name": "沈鼎", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "shen.reply.starter_kept", "action_kind": "talk", "execution_event": "e2273", "game_time": 561, "visible": true, "surface": "是我喂的。续种，刮壁，盖布——这手法我认得。", "lines": [], "judgment": false, "travel": []}

### 569 / demon / demon.question.prompt

原始控制台记录 1615

{"status": "applied", "actor": "demon", "speaker": "demon", "speaker_name": "同伴", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "demon.question.prompt", "action_kind": "question", "execution_event": "e2305", "game_time": 569, "visible": true, "surface": "此为始业之问：门是谁闩上的？闩门之时，此人可曾听见门外的呼喊？", "lines": [], "judgment": false, "travel": []}

### 570 / demon / demon.opening.other-contract

原始控制台记录 1652

{"status": "applied", "actor": "demon", "speaker": "demon", "speaker_name": "同伴", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "demon.opening.other-contract", "action_kind": "talk", "execution_event": "e2350", "game_time": 570, "visible": true, "surface": "与你立约的，不是我。", "lines": [], "judgment": false, "travel": []}

### 571 / demon / demon.opening.investigate-invitation

原始控制台记录 1689

{"status": "applied", "actor": "demon", "speaker": "demon", "speaker_name": "同伴", "voice": "dialogue", "surface_voice": "dialogue", "action_id": "demon.opening.investigate-invitation", "action_kind": "talk", "execution_event": "e2369", "game_time": 571, "visible": true, "surface": "记不得，并不妨碍作答。痕迹还在。答案备好了，到铜口来。", "lines": [], "judgment": false, "travel": []}

### 575 / su / su.sound_trial

原始控制台记录 1729

{"status": "applied", "actor": "su", "speaker": "su", "speaker_name": "苏泠", "voice": "visible-action", "surface_voice": "narration", "action_id": "su.sound_trial", "action_kind": "sound_test", "execution_event": "e2412", "game_time": 575, "visible": true, "surface": "", "lines": ["声路试验完成。各处听见了什么，还需分别询问实际在场的人。"], "judgment": false, "travel": []}

## 全部执行呈现的状态与动作列表

这里只按已有呈现字段列出，不将缺失字段当作成功。

- 481 | demon | demon.departure.inspect.contract | applied | visible=False | record=14
- 481 | detective | detective.opening.wake | applied | visible=True | record=23
- 481 | shen | shen.opening.prepare-tea | applied | visible=False | record=32
- 481 | quer | quer.follow-own-interest | applied | visible=False | record=41
- 481 | lucian | lucian.follow-own-interest | applied | visible=False | record=50
- 481 | ward | ward.follow-own-interest | applied | visible=False | record=59
- 481 | gu | gu.follow-own-interest | applied | visible=False | record=68
- 481 | ruan | ruan.follow-own-interest | applied | visible=False | record=77
- 481 | elena | elena.follow-own-interest | applied | visible=False | record=86
- 481 | selma | selma.follow-own-interest | applied | visible=False | record=95
- 481 | moran | moran.follow-own-interest | applied | visible=False | record=104
- 481 | mora | mora.follow-own-interest | applied | visible=False | record=114
- 481 | bertrand | bertrand.follow-own-interest | applied | visible=False | record=124
- 481 | veronica | veronica.follow-own-interest | applied | visible=False | record=134
- 481 | margaret | margaret.follow-own-interest | applied | visible=False | record=144
- 482 | demon | demon.departure.inspect.astral | applied | visible=False | record=153
- 482 | detective | detective.opening.wear-coat | applied | visible=True | record=162
- 482 | huo | huo.move.hall.foyer | applied | visible=False | record=171
- 482 | su | su.move.hall.foyer | applied | visible=False | record=181
- 482 | quer | quer.other.inspect-clock | applied | visible=False | record=191
- 482 | lucian | lucian.other.inspect-clock | applied | visible=False | record=200
- 482 | ward | ward.other.inspect-clock | applied | visible=False | record=209
- 482 | gu | gu.other.inspect-clock | applied | visible=False | record=218
- 482 | ruan | ruan.other.inspect-clock | applied | visible=False | record=227
- 482 | elena | elena.other.inspect-clock | applied | visible=False | record=236
- 482 | selma | selma.other.inspect-clock | applied | visible=False | record=246
- 482 | moran | moran.other.inspect-clock | applied | visible=False | record=255
- 482 | mora | mora.other.inspect-clock | applied | visible=False | record=264
- 482 | bertrand | bertrand.other.inspect-clock | applied | visible=False | record=273
- 482 | veronica | veronica.other.inspect-clock | applied | visible=False | record=283
- 482 | margaret | margaret.other.inspect-clock | applied | visible=False | record=292
- 483 | demon | demon.departure.inspect.logistics | applied | visible=False | record=301
- 483 | huo | huo.departure.inspect-door | applied | visible=False | record=310
- 483 | shen | shen.move.hall.foyer | applied | visible=False | record=319
- 483 | su | su.departure.inspect-door | applied | visible=False | record=328
- 483 | quer | quer.inspect.outer-latch | applied | visible=False | record=337
- 483 | lucian | lucian.inspect.outer-latch | applied | visible=False | record=346
- 483 | ward | ward.activity.keys_found | applied | visible=False | record=355
- 483 | gu | gu.inspect.outer-latch | applied | visible=False | record=364
- 483 | ruan | ruan.inspect.outer-latch | applied | visible=False | record=373
- 483 | elena | elena.inspect.outer-latch | applied | visible=False | record=382
- 483 | selma | selma.activity.care_given | applied | visible=False | record=391
- 483 | moran | moran.inspect.outer-latch | applied | visible=False | record=400
- 483 | mora | mora.activity.care_given | applied | visible=False | record=409
- 483 | bertrand | bertrand.inspect.outer-latch | applied | visible=False | record=418
- 483 | veronica | veronica.inspect.outer-latch | applied | visible=False | record=428
- 483 | margaret | margaret.inspect.outer-latch | applied | visible=False | record=437
- 484 | demon | demon.departure.inspect-body | applied | visible=False | record=446
- 484 | detective | detective.move.R05.corridor | applied | visible=True | record=455
- 484 | shen | shen.departure.inspect-door | applied | visible=False | record=464
- 484 | quer | quer.wake-and-introduce | applied | visible=False | record=474
- 484 | lucian | lucian.wake-and-introduce | applied | visible=False | record=483
- 484 | ward | ward.activity.wax_ash_seen | applied | visible=False | record=492
- 484 | gu | gu.wake-and-introduce | applied | visible=False | record=501
- 484 | ruan | ruan.activity.ruan_admitted | applied | visible=False | record=510
- 484 | elena | elena.wake-and-introduce | applied | visible=False | record=519
- 484 | selma | selma.inspect.outer-latch | applied | visible=False | record=529
- 484 | moran | moran.wake-and-introduce | applied | visible=False | record=538
- 484 | mora | mora.inspect.outer-latch | applied | visible=False | record=547
- 484 | bertrand | bertrand.wake-and-introduce | applied | visible=False | record=556
- 484 | veronica | veronica.wake-and-introduce | applied | visible=False | record=565
- 484 | margaret | margaret.wake-and-introduce | applied | visible=False | record=574
- 485 | demon | demon.departure.inspect-outer | applied | visible=False | record=583
- 485 | huo | huo.move.foyer.hall | applied | visible=False | record=592
- 485 | su | su.move.foyer.hall | applied | visible=False | record=602
- 485 | quer | quer.reply.own-material.lucian | applied | visible=False | record=611
- 485 | lucian | lucian.reply.own-material.quer | applied | visible=False | record=620
- 485 | ward | ward.activity.quer_insulted | applied | visible=False | record=629
- 485 | gu | gu.activity.evidence_complete | applied | visible=False | record=638
- 485 | ruan | ruan.wake-and-introduce | applied | visible=False | record=648
- 485 | elena | elena.reply.own-material.ruan | applied | visible=False | record=657
- 485 | selma | selma.activity.evidence_complete | applied | visible=False | record=667
- 485 | moran | moran.reply.own-material.ruan | applied | visible=False | record=677
- 485 | mora | mora.wake-and-introduce | applied | visible=False | record=686
- 485 | bertrand | bertrand.reply.own-material.mora | applied | visible=False | record=696
- 485 | veronica | veronica.reply.own-material.mora | applied | visible=False | record=705
- 485 | margaret | margaret.reply.own-material.mora | applied | visible=False | record=714
- 486 | detective | detective.move.corridor.hall | applied | visible=True | record=724
- 486 | shen | shen.move.foyer.hall | applied | visible=False | record=733
- 486 | quer | quer.reply.own-material.ward | applied | visible=False | record=742
- 486 | lucian | lucian.reply.own-material.ward | applied | visible=False | record=752
- 486 | ward | ward.activity.report_basis | applied | visible=False | record=761
- 486 | gu | gu.reply.own-material.quer | applied | visible=False | record=770
- 486 | ruan | ruan.reply.own-material.elena | applied | visible=False | record=779
- 486 | elena | elena.reply.own-material.selma | applied | visible=False | record=788
- 486 | selma | selma.activity.report_basis | applied | visible=False | record=797
- 486 | moran | moran.reply.own-material.elena | applied | visible=False | record=806
- 486 | mora | mora.reply.own-material.bertrand | applied | visible=False | record=816
- 486 | bertrand | bertrand.reply.own-material.veronica | applied | visible=False | record=825
- 486 | veronica | veronica.reply.own-material.bertrand | applied | visible=False | record=834
- 486 | margaret | margaret.reply.own-material.bertrand | applied | visible=False | record=843
- 487 | detective | detective.inspect.E7 | applied | visible=True | record=854
- 487 | huo | huo.opening.introduce | applied | visible=True | record=863
- 487 | shen | shen.opening.look-arrival | applied | visible=False | record=872
- 487 | su | su.opening.introduce | applied | visible=True | record=881
- 487 | quer | quer.reply.own-material.gu | applied | visible=False | record=890
- 487 | lucian | lucian.reply.own-material.gu | applied | visible=False | record=900
- 487 | ward | ward.inspect.outer-latch | applied | visible=False | record=911
- 487 | gu | gu.reply.own-material.lucian | applied | visible=False | record=920
- 487 | ruan | ruan.reply.own-material.selma | applied | visible=False | record=929
- 487 | elena | elena.reply.own-material.moran | applied | visible=False | record=938
- 487 | selma | selma.wake-and-introduce | applied | visible=False | record=948
- 487 | moran | moran.reply.own-material.selma | applied | visible=False | record=958
- 487 | mora | mora.reply.own-material.veronica | applied | visible=False | record=967
- 487 | bertrand | bertrand.reply.own-material.margaret | applied | visible=False | record=976
- 487 | veronica | veronica.reply.own-material.margaret | applied | visible=False | record=986
- 487 | margaret | margaret.reply.own-material.veronica | applied | visible=False | record=995
- 488 | detective | detective.inspect.E8 | applied | visible=True | record=1004
- 488 | shen | shen.opening.welcome | applied | visible=True | record=1013
- 488 | ward | ward.wake-and-introduce | applied | visible=False | record=1022
- 488 | gu | gu.reply.own-material.ward | applied | visible=False | record=1031
- 488 | ruan | ruan.reply.own-material.moran | applied | visible=False | record=1040
- 488 | selma | selma.reply.own-material.ruan | applied | visible=False | record=1049
- 488 | mora | mora.reply.own-material.margaret | applied | visible=False | record=1058
- 489 | detective | detective.opening.introduce | applied | visible=True | record=1069
- 489 | shen | shen.opening.introduce | applied | visible=True | record=1078
- 489 | ward | ward.reply.own-material.quer | applied | visible=False | record=1087
- 489 | selma | selma.reply.own-material.elena | applied | visible=False | record=1096
- 490 | shen | shen.opening.serve-tea | applied | visible=True | record=1105
- 490 | ward | ward.reply.own-material.lucian | applied | visible=False | record=1114
- 490 | selma | selma.reply.own-material.moran | applied | visible=False | record=1123
- 491 | shen | shen.opening.memory-report | applied | visible=True | record=1132
- 491 | su | su.opening.ask-sugar | applied | visible=True | record=1141
- 491 | ward | ward.reply.own-material.gu | applied | visible=False | record=1150
- 492 | shen | shen.opening.body-habit | applied | visible=True | record=1159
- 500 | demon | other.question.contract | applied | visible=False | record=1168
- 513 | detective | detective.sound_trial | applied | visible=True | record=1177
- 515 | demon | other.question.astral | applied | visible=False | record=1186
- 515 | detective | detective.move.hall.foyer | applied | visible=True | record=1195
- 516 | detective | detective.departure.inspect-door | applied | visible=True | record=1204
- 517 | detective | detective.inspect.E1 | applied | visible=True | record=1215
- 518 | detective | detective.inspect.E2 | applied | visible=True | record=1226
- 519 | detective | detective.inspect.E3 | applied | visible=True | record=1237
- 520 | detective | detective.inspect.E4 | applied | visible=True | record=1246
- 521 | detective | detective.inspect.E5 | applied | visible=True | record=1255
- 523 | detective | detective.move.foyer.hall | applied | visible=True | record=1264
- 525 | detective | detective.move.hall.pantry | applied | visible=True | record=1273
- 526 | detective | detective.inspect.E9 | applied | visible=True | record=1282
- 527 | detective | detective.inspect.E10 | applied | visible=True | record=1291
- 529 | detective | detective.move.pantry.hall | applied | visible=True | record=1300
- 530 | demon | other.question.logistics | applied | visible=False | record=1309
- 531 | detective | detective.move.hall.foyer | applied | visible=True | record=1318
- 548 | detective | detective.compare_sole | applied | visible=True | record=1327
- 550 | demon | demon.opening | applied | visible=False | record=1336
- 550 | detective | detective.move.foyer.hall | applied | visible=True | record=1345
- 551 | huo | huo.inspect.E7 | applied | visible=False | record=1354
- 551 | su | su.inspect.E8 | applied | visible=False | record=1363
- 551 | demon | demon.opening.reward-invitation | applied | visible=True | record=1372
- 552 | detective | detective.move.hall.corridor | applied | visible=True | record=1381
- 552 | huo | huo.opening.inspect-reward-slot | applied | visible=False | record=1390
- 552 | shen | shen.move.hall.pantry | applied | visible=False | record=1399
- 552 | demon | demon.opening.desire-offer | applied | visible=False | record=1408
- 553 | huo | huo.opening.scrape-testpiece | applied | visible=False | record=1417
- 553 | shen | shen.inspect.E9 | applied | visible=False | record=1426
- 553 | demon | demon.opening.public-alarm | applied | visible=False | record=1435
- 554 | detective | detective.move.corridor.R08 | applied | visible=True | record=1444
- 554 | huo | huo.opening.seek-drawing | applied | visible=False | record=1453
- 554 | shen | shen.inspect.E10 | applied | visible=False | record=1462
- 554 | demon | demon.opening.public-latch | applied | visible=False | record=1471
- 555 | detective | detective.inspect.E6 | applied | visible=True | record=1480
- 555 | huo | huo.opening.maintain-interpretation | applied | visible=False | record=1489
- 556 | shen | shen.move.pantry.hall | applied | visible=False | record=1498
- 557 | detective | detective.move.R08.corridor | applied | visible=True | record=1507
- 557 | huo | huo.deep-resonance | applied | visible=False | record=1516
- 557 | shen | shen.opening.inspect-reward-slot | applied | visible=False | record=1525
- 558 | shen | shen.opening.bite-silver | applied | visible=False | record=1534
- 559 | detective | detective.move.corridor.hall | applied | visible=True | record=1543
- 559 | shen | shen.opening.accept-work | applied | visible=True | record=1552
- 560 | detective | detective.opening.empty-slot | applied | visible=True | record=1561
- 560 | huo | huo.reply.slate_code | applied | visible=True | record=1570
- 560 | shen | shen.reply.cups_known | applied | visible=True | record=1579
- 561 | detective | detective.opening.ask-reward | applied | visible=True | record=1588
- 561 | huo | huo.reply.slate_person | applied | visible=True | record=1597
- 561 | shen | shen.reply.starter_kept | applied | visible=True | record=1606
- 569 | demon | demon.question.prompt | applied | visible=True | record=1615
- 570 | demon | demon.opening.other-contract | applied | visible=True | record=1652
- 571 | demon | demon.opening.investigate-invitation | applied | visible=True | record=1689
- 575 | su | su.sound_trial | applied | visible=True | record=1729

## 原始决策采样字段


已列出执行呈现数：178
已列出决策采样数：0
