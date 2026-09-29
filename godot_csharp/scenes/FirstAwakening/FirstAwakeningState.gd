class_name FirstAwakeningState
extends Resource
## FirstAwakeningState: 纯数据驱动的初醒案核心状态与规则集合

const PAIR_SITES := {
	"engineer": ["hinge", "weight", "trace", "door", "slit", "floor_wipe"],
	"cook": ["wash", "water", "rations", "basin_ring", "cloth_hang", "cold_stove"],
	"musician": ["study", "tear", "box_mark", "table_wedge", "chair_marks", "transmitter"]
}

@export var minute: int = 7 * 60 + 20
@export var player_name: String = "侦探"
@export var investigation_phase: String = "arrival"
@export var paired_companion: String = ""
@export var inner_bolt: bool = true
@export var viewing_outer_bar: bool = false
@export var broadcast_heard: bool = false
@export var meeting_done: bool = false
@export var captain: bool = false
@export var ended: bool = false
@export var ending_title: String = ""

@export var disputed: Dictionary = {"escape": false, "betrayal": false, "fifth_person": false}
@export var npc_searches: Dictionary = {}
@export var evidence_sources: Dictionary = {}
@export var known_dorms: Dictionary = {"detective": true}
@export var deep_inspections: Dictionary = {}
@export var evidence: Dictionary = {}

@export var transmitter_rules: bool = false
@export var transmitter_connected: bool = false
@export var transmitter_answer_seen: bool = false
@export var preliminary_answer_attempted: bool = false
@export var meeting_introduction_seen: bool = false
@export var mind_introduction_seen: bool = false

@export var answer_slots: Dictionary = {"opening_motive": "", "closing_motive": "", "cleanup_and_locks": ""}
@export var answer_slot_display_ids: Dictionary = {"opening_motive": "", "closing_motive": "", "cleanup_and_locks": ""}

# 脑内推演与回放状态
@export var mind_text: String = ""
@export var mind_drafts: Dictionary = {}
@export var mind_person: String = "侦探"
@export var mind_dirty: bool = false
@export var mind_note: String = ""
@export var mind_trace: int = 0
@export var sim_actions: Array[String] = []
@export var sim_actor_actions: Dictionary = {}
@export var sim_result: Dictionary = {}
@export var sim_checks: Dictionary = {}
@export var simulated_draft_snapshot: Dictionary = {}
@export var pending_inspections: Dictionary = {}
@export var conjectures: Dictionary = {}
@export var checks_this_run: int = 0

@export var replay_events: Array[String] = []
@export var replay_states: Array = []
@export var replay_pause_points: Dictionary = {}
@export var replay_pause_display_points: Dictionary = {}
@export var replay_pause_ids: Dictionary = {}
@export var replay_index: int = 0
@export var replay_playing: bool = false
@export var replay_fast: bool = false
@export var replay_accum: float = 0.0
@export var replay_reached: Dictionary = {}

func can_access_dorm(owner: String) -> bool:
	if owner == "detective":
		return true
	if investigation_phase == "paired":
		return paired_companion == owner
	return false

func is_own_dorm_complete() -> bool:
	for id in ["window_0", "bed_0", "drawer", "paper"]:
		if not evidence.has(id):
			return false
	return true

func all_disputed() -> bool:
	return disputed.values().all(func(item): return item)

func paired_actor() -> String:
	return {"engineer": "工程师", "cook": "厨师", "musician": "音乐家"}.get(paired_companion, "")

func record_deep_inspection(id: String) -> void:
	deep_inspections[id] = true
