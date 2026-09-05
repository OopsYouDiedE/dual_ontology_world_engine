using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DualOntologyWorldEngine.Core;
using DualOntologyWorldEngine.Core.Mind;
using DualOntologyWorldEngine.Simulation;

namespace DualOntologyWorldEngine.Presentation;

/// <summary>
/// 异常感知学园 · 真实生活与动机轨迹可视化层 (Presentation Layer)。
/// 严格遵循引擎与表现分离原则：空间坐标、地图瓦片与 UI 渲染完全由表现层负责。
/// 真实展示十六位学员（侦探、主厨、机械师、药师等）与人形恶魔瓦沙克在学园八大设施中的日常生活。
/// </summary>
public partial class SmallvilleDemo : Node2D
{
	private const int TileSize = 16;
	private const int MapColumns = 70;
	private const int MapRows = 37;
	private const float MemberSpacing = 28f;
	private static readonly Vector2 MapOrigin = new(24, 68);
	private static readonly Rect2 MapRect = new(MapOrigin, new Vector2(MapColumns * TileSize, MapRows * TileSize));
	private static readonly Rect2 SideRect = new(1168, 68, 288, 768);

	// 学园成员实体定义
	public sealed class AcademyMember
	{
		public required string Name;
		public required string Romaji;
		public required string Title;
		public required string Faculty;
		public required Color Color;
		public required int SpriteRow;
		public required Vector2 DormPosition;
		public required Vector2 DutyPosition;
		public required string DutyActivity;
		public required string DutyPlace;

		// 模拟人生 5 大需求动机 (0~100)
		public float Hunger = 80f;
		public float Energy = 90f;
		public float Social = 75f;
		public float Fun = 70f;
		public float Comfort = 85f;

		// 运行时状态
		public Vector2 Position;
		public Vector2 TargetPosition;
		public string CurrentActivity = "清晨苏醒";
		public string CurrentPlace = "生活院宿舍";
		public float DwellLeft;
		public float InteractionCooldown;
		public int FacingColumn = 23;
		public bool IsMoving;
		public readonly List<Vector2> Trail = [];
		public float DemonVigilance = 10f; // 对恶魔警戒度
		public string? BubbleText;
		public float BubbleTimeLeft;
	}

	private sealed record EventLog(string Time, string Text, Color Color);
	private sealed record FacilityZone(Rect2I TileBounds, string Name, string Code, Vector2 Waypoint, Color ThemeColor);

	private readonly List<AcademyMember> _members = [];
	private readonly List<EventLog> _logs = [];
	private readonly List<(AcademyMember A, AcademyMember B, string Dialogue, float Remaining)> _activeTalks = [];
	private readonly List<FacilityZone> _facilities = [];

	private TileMapLayer? _ocean;
	private TileMapLayer? _shore;
	private TileMapLayer? _campusGround;
	private TileMapLayer? _campusObjects;
	private Texture2D _urbanAtlas = null!;
	private AcademyMember? _selected;
	private float _worldMinutes = 8 * 60; // 默认从专职活动阶段开始，避免全员在早餐点堆叠
	private int _day = 1;
	private float _speed = 1f;
	private float _trailAccumulator;
	private bool _paused;
	private bool _showTrails = true;
	private int _listPage; // 0: 前 8 人, 1: 后 9 人
	private Font _font = null!;

	// 学园关键路网节点
	private static readonly Vector2 WaypointCentralHall = TilePoint(35, 19);
	private static readonly Vector2 WaypointDormitory = TilePoint(14, 25);
	private static readonly Vector2 WaypointLibrary = TilePoint(18, 14);
	private static readonly Vector2 WaypointWorkshop = TilePoint(54, 25);
	private static readonly Vector2 WaypointRefectory = TilePoint(27, 28);
	private static readonly Vector2 WaypointInfirmary = TilePoint(43, 28);
	private static readonly Vector2 WaypointDojo = TilePoint(52, 14);
	private static readonly Vector2 WaypointOutpost = TilePoint(59, 8);

	private static Vector2 TilePoint(int x, int y) => MapOrigin + new Vector2(x * TileSize + TileSize / 2f, y * TileSize + TileSize / 2f);

	private static Vector2 SceneToWaypoint(string sceneName) => sceneName switch
	{
		"中央厅" or "契约审议室" or "公共娱乐室" or "中央草场与露天运动场" => WaypointCentralHall,
		"生活院" => WaypointDormitory,
		"教学图书馆" or "星盘讲堂" or "美术工房与冬园" => WaypointLibrary,
		"动力总站" or "化学实验室" => WaypointWorkshop,
		"后勤院" or "博物与生态室" => WaypointRefectory,
		"公共诊疗所" => WaypointInfirmary,
		"训练院" or "共鸣厅" => WaypointDojo,
		"门楼与誓约庭" => WaypointOutpost,
		_ => WaypointCentralHall
	};

	private AcademySimulation _simulation = null!;
	private float _stepTimer;

	public override void _Ready()
	{
		TextureFilter = TextureFilterEnum.Nearest;
		_font = ThemeDB.FallbackFont;
		BuildFacilitiesMeta();
		BuildTileMap();
		BuildAcademyArchitecture();
		CreateAcademyMembers();
		_simulation = new AcademySimulation();
		SyncMembersFromSimulation();
		_selected = _members[0];
		AddLog(ClockText(), "学园晨钟敲响，真实模拟引擎启动，十六位学员与恶魔开启日常生活。", new Color("8fd7c1"));
		QueueRedraw();
	}

	private void SyncMembersFromSimulation()
	{
		_day = _simulation.CurrentDay;
		double dayAv = _simulation.CurrentTime % 200.0;
		_worldMinutes = (float)(dayAv / 200.0 * 1440.0);

		foreach (var m in _members)
		{
			Character? actor = m.Name == "瓦沙克"
				? _simulation.Demon
				: _simulation.Students.Values.FirstOrDefault(s => s.Name == m.Name);

			if (actor != null)
			{
				if (_simulation.Motives.TryGetValue(actor, out var profile))
				{
					m.Hunger = profile.Hunger;
					m.Energy = profile.Energy;
					m.Social = profile.Social;
					m.Fun = profile.Fun;
					m.Comfort = profile.Comfort;
				}

				var currentSceneName = actor.CurrentScene?.Name;
				if (!string.IsNullOrEmpty(currentSceneName))
				{
					m.CurrentPlace = currentSceneName;
					m.TargetPosition = FacilitySlot(m, SceneToWaypoint(currentSceneName));
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!_paused)
		{
			var dt = (float)delta * _speed;
			_stepTimer += dt;

			// 随物理时间驱动底层真实仿真推进一步
			if (_stepTimer >= 0.28f)
			{
				_stepTimer = 0f;
				var step = _simulation.StepSimulation();
				if (step != null)
				{
					var m = _members.FirstOrDefault(x => x.Name == step.Actor.Name);
					if (m != null)
					{
						m.CurrentActivity = step.Intent?.Label ?? (step.IsNight ? "夜间安寝" : "原地观察");
						if (step.Intent != null)
						{
							m.BubbleText = step.Intent.Label;
							m.BubbleTimeLeft = 3.2f;
						}
					}

					SyncMembersFromSimulation();

					if (!string.IsNullOrWhiteSpace(step.Narrative))
					{
						var firstLine = step.Narrative.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? step.Narrative;
						AddLog(ClockText(), firstLine, new Color("8fd7c1"));
					}
				}
			}

			_trailAccumulator += dt;
			foreach (var m in _members)
			{
				UpdateMemberMovement(m, dt);
			}

			ResolveMemberSpacing();
			UpdateInteractions(dt);

			if (_trailAccumulator >= 0.18f)
			{
				_trailAccumulator = 0;
				foreach (var m in _members)
				{
					if (m.Trail.Count == 0 || m.Trail[^1].DistanceTo(m.Position) > 6)
						m.Trail.Add(m.Position);
					if (m.Trail.Count > 60)
						m.Trail.RemoveAt(0);
				}
			}
		}
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
		{
			switch (key.Keycode)
			{
				case Key.Space: _paused = !_paused; break;
				case Key.Key1: _speed = 0.5f; break;
				case Key.Key2: _speed = 1f; break;
				case Key.Key3: _speed = 2f; break;
				case Key.Key4: _speed = 4f; break;
				case Key.T: _showTrails = !_showTrails; break;
				case Key.R: ResetSimulation(); break;
				case Key.Tab:
					var currentIndex = _selected is null ? 0 : _members.IndexOf(_selected);
					_selected = _members[(currentIndex + 1) % _members.Count];
					_listPage = _members.IndexOf(_selected) < 8 ? 0 : 1;
					break;
				case Key.Pageup:
					_listPage = 0;
					break;
				case Key.Pagedown:
					_listPage = 1;
					break;
			}
		}

		if (inputEvent is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
		{
			var clickedMember = _members.OrderBy(c => c.Position.DistanceTo(mouse.Position)).FirstOrDefault();
			if (clickedMember is not null && clickedMember.Position.DistanceTo(mouse.Position) <= 36)
			{
				_selected = clickedMember;
				_listPage = _members.IndexOf(_selected) < 8 ? 0 : 1;
				return;
			}

			// 检查是否点击了右侧栏列表
			if (SideRect.HasPoint(mouse.Position) && mouse.Position.Y is >= 120 and <= 480)
			{
				var row = (int)((mouse.Position.Y - 120) / 44);
				var idx = _listPage * 8 + row;
				if (idx >= 0 && idx < _members.Count)
					_selected = _members[idx];
			}
			// 检查是否点击了翻页按钮
			else if (mouse.Position.X is >= 1342 and <= 1444 && mouse.Position.Y is >= 80 and <= 110)
			{
				_listPage = _listPage == 0 ? 1 : 0;
			}
		}
	}

	private void BuildFacilitiesMeta()
	{
		_facilities.Add(new FacilityZone(new Rect2I(8, 21, 12, 8), "生活院与公共浴场", "DORM", WaypointDormitory, new Color("6c757d")));
		_facilities.Add(new FacilityZone(new Rect2I(13, 10, 12, 8), "文契院与教学图书馆", "LIB", WaypointLibrary, new Color("5aa4df")));
		_facilities.Add(new FacilityZone(new Rect2I(49, 21, 12, 8), "铁工院与动力总站", "PWR", WaypointWorkshop, new Color("d4a373")));
		_facilities.Add(new FacilityZone(new Rect2I(29, 14, 13, 10), "中央厅与誓约庭", "HALL", WaypointCentralHall, new Color("f4d58d")));
		_facilities.Add(new FacilityZone(new Rect2I(22, 25, 10, 8), "后勤院与食堂", "DINE", WaypointRefectory, new Color("ff595e")));
		_facilities.Add(new FacilityZone(new Rect2I(38, 25, 10, 8), "白塔异象院与诊疗所", "MED", WaypointInfirmary, new Color("52b788")));
		_facilities.Add(new FacilityZone(new Rect2I(47, 10, 12, 8), "星仪院与共鸣训练场", "DOJO", WaypointDojo, new Color("e63946")));
		_facilities.Add(new FacilityZone(new Rect2I(55, 4, 9, 9), "海崖信号塔", "GATE", WaypointOutpost, new Color("70e000")));
	}

	private void BuildTileMap()
	{
		_urbanAtlas = GD.Load<Texture2D>("res://assets/kenney_rpg_urban/source/Tilemap/tilemap_packed.png");

		var islandTexture = GD.Load<Texture2D>("res://assets/island_campus/overworld_island_tiles_16x16.png");
		var islandAtlas = new TileSetAtlasSource
		{
			Texture = islandTexture,
			TextureRegionSize = new Vector2I(16, 16),
			UseTexturePadding = false
		};
		for (var y = 0; y < 7; y++)
		for (var x = 0; x < 6; x++)
			islandAtlas.CreateTile(new Vector2I(x, y));
		var islandTileSet = new TileSet { TileSize = new Vector2I(16, 16) };
		islandTileSet.AddSource(islandAtlas, 0);

		var townTexture = GD.Load<Texture2D>("res://assets/island_campus/kenney_tiny_town/Tilemap/tilemap_packed.png");
		var townAtlas = new TileSetAtlasSource
		{
			Texture = townTexture,
			TextureRegionSize = new Vector2I(16, 16),
			UseTexturePadding = false
		};
		for (var y = 0; y < 11; y++)
		for (var x = 0; x < 12; x++)
			townAtlas.CreateTile(new Vector2I(x, y));
		var townTileSet = new TileSet { TileSize = new Vector2I(16, 16) };
		townTileSet.AddSource(townAtlas, 0);

		_ocean = CreateMapLayer("Ocean", islandTileSet, -40);
		_shore = CreateMapLayer("SandShore", islandTileSet, -39);
		_campusGround = CreateMapLayer("CampusGroundAndPaths", townTileSet, -38);
		_campusObjects = CreateMapLayer("CampusBuildingsAndNature", townTileSet, -37);

		for (var y = 0; y < MapRows; y++)
		for (var x = 0; x < MapColumns; x++)
		{
			var cell = new Vector2I(x, y);
			_ocean.SetCell(cell, 0, new Vector2I(4, 1));
			var isReservoir = x is >= 31 and <= 39 && y is >= 4 and <= 7;

			var nx = (x - 34.5f) / 33f;
			var ny = (y - 18f) / 17f;
			var islandDistance = nx * nx + ny * ny;
			if (islandDistance <= 1f && !isReservoir)
				_shore.SetCell(cell, 0, new Vector2I(1, 4));

			var gx = (x - 34.5f) / 30f;
			var gy = (y - 18f) / 14.5f;
			if (gx * gx + gy * gy <= 1f && !isReservoir)
				_campusGround.SetCell(cell, 0, new Vector2I((x + y) % 7 == 0 ? 1 : 0, 0));
		}
	}

	private TileMapLayer CreateMapLayer(string name, TileSet tileSet, int zIndex)
	{
		var layer = new TileMapLayer
		{
			Name = name,
			TileSet = tileSet,
			Position = MapOrigin,
			ShowBehindParent = true,
			ZIndex = zIndex,
			TextureFilter = TextureFilterEnum.Nearest
		};
		AddChild(layer);
		return layer;
	}

	private void BuildAcademyArchitecture()
	{
		if (_campusGround is null || _campusObjects is null)
			return;

		// 环岛步道、南岸门楼至中央厅的主轴，以及四院之间的十字回廊。
		for (var x = 7; x <= 62; x++)
		{
			var y = 18 + Mathf.RoundToInt(Mathf.Sin(x * 0.18f) * 11f);
			PlaceTownTile(_campusGround, x, y, 43);
		}
		for (var y = 10; y <= 33; y++) PlaceTownTile(_campusGround, 35, y, 43);
		for (var x = 14; x <= 56; x++) PlaceTownTile(_campusGround, x, 19, 43);
		for (var x = 14; x <= 54; x++) PlaceTownTile(_campusGround, x, 27, 43);

		// 四院、生活设施与中央厅。所有建筑均由 Kenney Tiny Town 的 16×16 瓦片逐格组成。
		BuildBuilding(10, 22, 9, false); // 生活院
		BuildBuilding(14, 11, 9, true);  // 文契院与图书馆
		BuildBuilding(49, 11, 9, false); // 星仪院与训练场
		BuildBuilding(50, 22, 9, true);  // 铁工院
		BuildBuilding(23, 26, 9, true);  // 后勤院
		BuildBuilding(39, 26, 9, false); // 白塔诊疗所
		BuildBuilding(30, 15, 11, true); // 中央厅

		// 南岸门楼与誓约庭。
		for (var x = 31; x <= 39; x++) PlaceTownTile(_campusObjects, x, 33, x is 34 or 35 or 36 ? 99 : 97);
		PlaceTownTile(_campusObjects, 34, 32, 112);
		PlaceTownTile(_campusObjects, 35, 32, 113);
		PlaceTownTile(_campusObjects, 36, 32, 114);

		// 西北种植梯田与温室。
		for (var y = 8; y <= 13; y += 2)
		for (var x = 7; x <= 13; x++)
			PlaceTownTile(_campusObjects, x, y, (x + y) % 2 == 0 ? 2 : 1);
		BuildBuilding(8, 14, 7, false);

		// 东北海崖信号塔、上水库和水务站。
		BuildBuilding(56, 6, 7, false);
		PlaceTownTile(_campusObjects, 59, 5, 95);
		PlaceTownTile(_campusObjects, 60, 5, 95);
		BuildBuilding(32, 8, 7, false); // 上水库管理与水务站
		for (var y = 8; y <= 14; y++) PlaceTownTile(_campusGround, 35, y, 43);

		// 外缘林地与观景庭园，保留南岸潮湾和西湾沙滩的开阔视线。
		for (var x = 5; x <= 64; x += 3)
		{
			var topY = 5 + (x * 7 % 4);
			var bottomY = 30 - (x * 5 % 3);
			if (x is < 29 or > 41) PlaceNatureIfLand(x, topY, 3 + x % 7);
			if (x is < 29 or > 41) PlaceNatureIfLand(x, bottomY, 3 + (x + 2) % 7);
		}
		for (var y = 10; y <= 27; y += 3)
		{
			PlaceNatureIfLand(5 + y % 2, y, 4 + y % 6);
			PlaceNatureIfLand(64 - y % 2, y, 4 + (y + 3) % 6);
		}
	}

	private void PlaceNatureIfLand(int x, int y, int tileId)
	{
		if (_campusGround is not null && _campusObjects is not null && _campusGround.GetCellSourceId(new Vector2I(x, y)) >= 0)
			PlaceTownTile(_campusObjects, x, y, tileId);
	}

	private void BuildBuilding(int left, int top, int width, bool redRoof)
	{
		if (_campusObjects is null || width < 5)
			return;

		var roofTop = redRoof ? new[] { 52, 53, 54 } : new[] { 48, 49, 50 };
		var roofBottom = redRoof ? new[] { 64, 65, 66 } : new[] { 60, 61, 62 };
		var gable = redRoof ? 67 : 63;
		for (var x = 0; x < width; x++)
		{
			PlaceTownTile(_campusObjects, left + x, top, roofTop[x == 0 ? 0 : x == width - 1 ? 2 : 1]);
			PlaceTownTile(_campusObjects, left + x, top + 1, x == width / 2 ? gable : roofBottom[x == 0 ? 0 : x == width - 1 ? 2 : 1]);
			PlaceTownTile(_campusObjects, left + x, top + 2, x == 0 ? 72 : x == width - 1 ? 75 : x == width / 2 ? 85 : x % 3 == 0 ? 84 : 73);
		}
	}

	private static void PlaceTownTile(TileMapLayer layer, int x, int y, int tileId) =>
		layer.SetCell(new Vector2I(x, y), 0, new Vector2I(tileId % 12, tileId / 12));

	private void CreateAcademyMembers()
	{
		// 16位学员 + 1位恶魔真实数据映射
		AddMember("雾岛律", "Ritsu", "真理探求 · 侦探", "智识调查科", new Color("3a86ff"), 0,
			Dorm(1), WaypointLibrary, "阅览异常卷宗并推演线索", "教学图书馆");

		AddMember("榊火澄", "Hizumi", "万物调味 · 主厨", "生活保障科", new Color("ff595e"), 2,
			Dorm(2), WaypointRefectory, "掌勺烹饪学员三餐饮食", "后勤院食堂");

		AddMember("真壁铸", "Iruka", "百炼构件 · 机械师", "动力工程科", new Color("d4a373"), 4,
			Dorm(3), WaypointWorkshop, "检修总站发电机与回路", "动力机工坊");

		AddMember("灰原朔", "Saku", "无影匿步 · 潜行者", "斥候安全科", new Color("7b68ee"), 6,
			Dorm(4), WaypointOutpost + new Vector2(-40, 20), "潜行巡视外围防线死角", "门楼哨站");

		AddMember("鸣海澄", "Kiyomi", "生命律脉 · 医师", "医疗保障科", new Color("52b788"), 8,
			Dorm(5), WaypointInfirmary, "调配凝神药剂与记录脉象", "公共诊疗所");

		AddMember("久世弦", "Gen", "共振天籁 · 乐师", "礼仪心智科", new Color("8338ec"), 10,
			Dorm(6), WaypointDojo + new Vector2(20, -20), "校准音叉与谱写祝词", "战术演武场");

		AddMember("野野宫蕾", "Tsubomi", "沃野生息 · 园艺学者", "生殖环境科", new Color("70e000"), 12,
			Dorm(7), WaypointOutpost + new Vector2(-15, 60), "照料中庭药草与温室花圃", "中庭温室");

		AddMember("冬城史", "Fumi", "纪元碑刻 · 记录者", "史料编纂科", new Color("90e0ef"), 14,
			Dorm(8), WaypointLibrary + new Vector2(40, 20), "拓印古代遗迹符文拓片", "教学图书馆");

		AddMember("月见里鸢", "Tobi", "千步追芒 · 狙击哨兵", "防卫反制科", new Color("ffb703"), 0,
			Dorm(1, 20), WaypointOutpost + new Vector2(10, -40), "登高瞭望校门地平线动静", "门楼哨站");

		AddMember("御影章", "Akira", "天元残局 · 战术家", "战术总括科", new Color("48cae4"), 2,
			Dorm(2, 20), WaypointCentralHall + new Vector2(-30, 0), "在大厅推演全域防御阵势", "中央议事大厅");

		AddMember("白峰局", "Tsubone", "明镜规仪 · 行政长", "行政监察科", new Color("e0aaff"), 4,
			Dorm(3, 20), WaypointCentralHall + new Vector2(30, 0), "审查全员物资配给与出入许可", "中央议事大厅");

		AddMember("赤星砾", "Tsubute", "崩岩破军 · 武道家", "近卫突击科", new Color("e63946"), 6,
			Dorm(4, 20), WaypointDojo + new Vector2(-30, 0), "演练破岩体术与格斗身段", "战术演武场");

		AddMember("苍井砥", "Togi", "秋水砺锋 · 刀工", "武备维护科", new Color("0077b6"), 8,
			Dorm(5, 20), WaypointWorkshop + new Vector2(30, 20), "精细研磨战备锋刃与构件", "动力机工坊");

		AddMember("绯宫巡", "Meguru", "赫耀铁卫 · 守门人", "要隘镇守科", new Color("fb8500"), 10,
			Dorm(6, 20), WaypointOutpost + new Vector2(-10, 0), "核验门禁信物并戒备结界", "门楼哨站");

		AddMember("青栖绘", "Kai", "万象丹青 · 画家", "艺术留存科", new Color("5e60ce"), 12,
			Dorm(7, 20), WaypointLibrary + new Vector2(-30, 30), "手绘学园防御工事构造图谱", "教学图书馆");

		AddMember("方城础", "Ishizue", "不动基岩 · 建造学者", "工构筑造科", new Color("adb5bd"), 14,
			Dorm(8, 20), WaypointWorkshop + new Vector2(-35, 10), "测算中央厅承重梁与结界锚点", "动力机工坊");

		// 人形恶魔瓦沙克
		AddMember("瓦沙克", "Vassago", "隐秘低语 · 人形恶魔", "深渊受审者", new Color("c77dff"), 16,
			WaypointCentralHall + new Vector2(0, 50), WaypointCentralHall, "游弋于阴影中窥探学员心智", "中央大厅暗隅");
	}

	private static Vector2 Dorm(int room, float offsetY = 0)
	{
		var baseX = 100 + (room - 1) % 4 * 48;
		var baseY = 140 + (room - 1) / 4 * 40 + offsetY;
		return new Vector2(baseX, baseY);
	}

	private void AddMember(string name, string romaji, string title, string faculty, Color color, int spriteRow,
		Vector2 dormPos, Vector2 dutyPos, string dutyActivity, string dutyPlace)
	{
		var index = _members.Count;
		var spawnOffset = new Vector2((index % 3 - 1) * 20, (index / 3 % 3 - 1) * 16);
		_members.Add(new AcademyMember
		{
			Name = name, Romaji = romaji, Title = title, Faculty = faculty, Color = color, SpriteRow = spriteRow,
			DormPosition = dormPos, DutyPosition = dutyPos, DutyActivity = dutyActivity, DutyPlace = dutyPlace,
			Position = dutyPos + spawnOffset, TargetPosition = dutyPos + spawnOffset,
			CurrentPlace = dutyPlace, CurrentActivity = dutyActivity, DwellLeft = 4f
		});
	}

	/// <summary>
	/// 为共同设施分配稳定站位。成员仍然前往同一功能区，但不会争抢同一个像素坐标。
	/// 这是游戏表现层的空间安排，不进入核心回合制引擎。
	/// </summary>
	private Vector2 FacilitySlot(AcademyMember member, Vector2 center, float spacingX = 34f, float spacingY = 28f)
	{
		var index = _members.IndexOf(member);
		var column = index % 5;
		var row = index / 5;
		return center + new Vector2((column - 2) * spacingX, (row - 1.5f) * spacingY);
	}

	/// <summary>
	/// 对移动中的角色做轻量分离，处理交叉路径和同一设施内的临时拥挤。
	/// </summary>
	private void ResolveMemberSpacing()
	{
		for (var pass = 0; pass < 2; pass++)
		for (var i = 0; i < _members.Count; i++)
		for (var j = i + 1; j < _members.Count; j++)
		{
			var a = _members[i];
			var b = _members[j];
			var delta = b.Position - a.Position;
			var distance = delta.Length();
			if (distance >= MemberSpacing)
				continue;

			var direction = distance > 0.01f
				? delta / distance
				: Vector2.FromAngle((i * 17 + j * 31) * 0.37f);
			var correction = direction * ((MemberSpacing - distance) * 0.5f + 0.1f);
			a.Position = ClampToMap(a.Position - correction);
			b.Position = ClampToMap(b.Position + correction);
		}
	}

	private static Vector2 ClampToMap(Vector2 position) => new(
		Mathf.Clamp(position.X, MapRect.Position.X + 18f, MapRect.End.X - 18f),
		Mathf.Clamp(position.Y, MapRect.Position.Y + 24f, MapRect.End.Y - 18f));

	private static void UpdateMemberMovement(AcademyMember m, float dt)
	{
		m.InteractionCooldown = Math.Max(0, m.InteractionCooldown - dt);
		if (m.BubbleTimeLeft > 0)
		{
			m.BubbleTimeLeft -= dt;
			if (m.BubbleTimeLeft <= 0)
				m.BubbleText = null;
		}

		var dist = m.Position.DistanceTo(m.TargetPosition);
		if (dist < 4)
		{
			m.Position = m.TargetPosition;
			m.IsMoving = false;
			return;
		}

		var movement = m.TargetPosition - m.Position;
		m.FacingColumn = Math.Abs(movement.X) > Math.Abs(movement.Y)
			? movement.X < 0 ? 24 : 26
			: movement.Y < 0 ? 25 : 23;
		m.IsMoving = true;
		m.Position = m.Position.MoveToward(m.TargetPosition, dt * 55f);
	}

	private void UpdateInteractions(float dt)
	{
		for (var i = _activeTalks.Count - 1; i >= 0; i--)
		{
			var talk = _activeTalks[i];
			talk.Remaining -= dt;
			if (talk.Remaining <= 0)
				_activeTalks.RemoveAt(i);
			else
				_activeTalks[i] = talk;
		}

		for (var i = 0; i < _members.Count; i++)
		for (var j = i + 1; j < _members.Count; j++)
		{
			var a = _members[i];
			var b = _members[j];

			if (a.InteractionCooldown > 0 || b.InteractionCooldown > 0 || a.Position.DistanceTo(b.Position) > 34)
				continue;
			if (_activeTalks.Any(t => t.A == a || t.B == a || t.A == b || t.B == b))
				continue;

			a.InteractionCooldown = b.InteractionCooldown = 18;
			var dialogue = GenerateDialogue(a, b);
			_activeTalks.Add((a, b, dialogue, 5.0f));

			a.BubbleText = dialogue;
			a.BubbleTimeLeft = 4.5f;

			a.Social = Math.Min(100f, a.Social + 25f);
			b.Social = Math.Min(100f, b.Social + 25f);

			if (a.Name == "瓦沙克" || b.Name == "瓦沙克")
			{
				var human = a.Name == "瓦沙克" ? b : a;
				human.DemonVigilance = Math.Min(100f, human.DemonVigilance + 15f);
				AddLog(ClockText(), $"【恶魔试探】{dialogue}", new Color("c77dff"));
			}
			else
			{
				AddLog(ClockText(), $"{a.Name} 与 {b.Name}：“{dialogue}”", a.Color.Lerp(b.Color, 0.5f));
			}
		}
	}

	private static string GenerateDialogue(AcademyMember a, AcademyMember b)
	{
		// 恶魔对话逻辑
		if (a.Name == "瓦沙克" || b.Name == "瓦沙克")
		{
			var human = a.Name == "瓦沙克" ? b : a;
			var demonLines = new[]
			{
				$"{human.Name}，你真的确信‘界标规则’能永远庇护这座学园吗？",
				"当第三声钟鸣响起时，你的言灵还能对抗深渊的低语吗？",
				"不必压抑内心的怀疑……契约的另一端早已有你的名字。",
				"越是严谨的构架，崩塌时的声响便越是悦耳呢。"
			};
			return demonLines[Random.Shared.Next(demonLines.Length)];
		}

		// 学员之间基于身份与专长的特定台词
		if ((a.Name == "雾岛律" && b.Name == "灰原朔") || (b.Name == "雾岛律" && a.Name == "灰原朔"))
			return "灰原，昨夜外围巡视记录里有八秒的盲区，你在遮掩什么？";

		if ((a.Name == "榊火澄" && b.Name == "野野宫蕾") || (b.Name == "榊火澄" && a.Name == "野野宫蕾"))
			return "蕾，温室新培育的凝神草叶，今晚可以加入学员热汤吗？";

		if ((a.Name == "真壁铸" && b.Name == "苍井砥") || (b.Name == "真壁铸" && a.Name == "苍井砥"))
			return "砥，动力炉的高温轴承，唯有你的研磨精度能确保闭合！";

		if ((a.Name == "鸣海澄" && b.Name == "赤星砾") || (b.Name == "鸣海澄" && a.Name == "赤星砾"))
			return "赤星同学，体术切磋留下的筋骨暗伤必须按时敷药，不准硬撑！";

		if ((a.Name == "久世弦" && b.Name == "冬城史") || (b.Name == "久世弦" && a.Name == "冬城史"))
			return "冬城，古代碑文上的押韵韵律，与当今言灵的谐振频率完全一致。";

		if ((a.Name == "御影章" && b.Name == "月见里鸢") || (b.Name == "御影章" && a.Name == "月见里鸢"))
			return "鸢，门楼西北角的三处视野死角，需要重新调整交叉哨位。";

		if ((a.Name == "白峰局" && b.Name == "绯宫巡") || (b.Name == "白峰局" && a.Name == "绯宫巡"))
			return "巡逻队长，夜禁巡查切勿漏检中央厅侧门，异常指数在上升。";

		if ((a.Name == "青栖绘" && b.Name == "方城础") || (b.Name == "青栖绘" && a.Name == "方城础"))
			return "方城学者，你绘制的承重受力结构图在构图美感上堪称完美。";

		// 常规学园生活交流
		var genericLines = new[]
		{
			"今日的界标能量读数相当平稳，希望能平安度过夜禁。",
			"刚才在后勤食堂尝了火澄调配的新热饮，精力恢复了不少。",
			"去图书馆阅览典籍时，记得别翻开未授权的暗红封皮。",
			"白峰行政长刚更新了巡查条令，大家都务必遵守时限。"
		};
		return genericLines[Random.Shared.Next(genericLines.Length)];
	}

	private string FindFacilityName(Vector2 pos)
	{
		var f = _facilities.OrderBy(fac => fac.Waypoint.DistanceTo(pos)).FirstOrDefault();
		return f?.Name ?? "学园回廊";
	}

	private void AddLog(string time, string text, Color color)
	{
		_logs.Insert(0, new EventLog(time, text, color));
		if (_logs.Count > 10)
			_logs.RemoveAt(_logs.Count - 1);
	}

	private void ResetSimulation()
	{
		_simulation = new AcademySimulation();
		_logs.Clear();
		_activeTalks.Clear();
		SyncMembersFromSimulation();
		foreach (var m in _members)
		{
			m.Position = m.TargetPosition;
			m.Trail.Clear();
			m.BubbleText = null;
		}
		AddLog(ClockText(), "学园推演已重置至第 1 日，真实仿真核心与表现层同步就绪。", new Color("8fd7c1"));
	}

	public override void _Draw()
	{
		// 顶栏背景与系统状态
		DrawRect(new Rect2(0, 0, 1480, 60), new Color("172228"));
		Text("异常感知学园：灰潮禁闭 · 荒岛校园轨迹观察站", new Vector2(24, 38), 22, new Color("eef4ef"));

		var phaseName = GetSchedulePhaseName();
		Text($"第 {_day} 日 · {ClockText()} [{phaseName}]", new Vector2(820, 38), 18, new Color("f4d58d"));
		Pill(new Rect2(1090, 16, 64, 28), _paused ? "暂停" : $"×{_speed:0.#}", _paused ? new Color("a76363") : new Color("3c806f"));

		// 地图外边框与设施标签
		DrawRect(MapRect.Grow(3), new Color("0a1014"), false, 6);
		DrawIslandLandmarks();
		DrawFacilitiesInfo();
		DrawRoutesAndMembers();
		DrawSidebar();
		DrawFooter();
		DrawMapAttributionStrip();
	}

	private string GetSchedulePhaseName()
	{
		var m = _worldMinutes;
		if (m is >= 6.5f * 60 and < 8f * 60) return "晨起早餐配给";
		if (m is >= 8f * 60 and < 12f * 60) return "午前专职研习执勤";
		if (m is >= 12f * 60 and < 13.5f * 60) return "午间午膳休整";
		if (m is >= 13.5f * 60 and < 17.5f * 60) return "午后跨科演武巡视";
		if (m is >= 17.5f * 60 and < 19f * 60) return "傍晚晚膳配给";
		if (m is >= 19f * 60 and < 21.5f * 60) return "晚间例会与棋乐交流";
		return "夜禁安寝防备";
	}

	private void DrawFacilitiesInfo()
	{
		foreach (var f in _facilities)
		{
			var wp = f.Waypoint.Y > 580 ? new Vector2(f.Waypoint.X, 572) : f.Waypoint;
			var width = f.Name.Length * 15 + 16;
			var rect = new Rect2(wp.X - width / 2f, wp.Y - 26, width, 20);
			DrawRect(rect, new Color(0.08f, 0.12f, 0.15f, 0.85f));
			DrawRect(new Rect2(rect.Position.X, rect.Position.Y, 3, 20), f.ThemeColor);
			Text(f.Name, new Vector2(rect.Position.X + 8, rect.Position.Y + 14), 12, new Color("f3f0df"));
		}
	}

	private void DrawIslandLandmarks()
	{
		LandmarkBadge("西湾沙滩与海浴场", TilePoint(3, 18), new Color("e9c46a"));
		LandmarkBadge("南岸潮湾", TilePoint(35, 35), new Color("90e0ef"));
		LandmarkBadge("种植梯田与温室", TilePoint(9, 7), new Color("70e000"));
		LandmarkBadge("上水库与水务站", TilePoint(35, 4), new Color("48cae4"));
		LandmarkBadge("林地石场与潮间观测站", TilePoint(61, 28), new Color("adb5bd"));
	}

	private void LandmarkBadge(string label, Vector2 point, Color color)
	{
		var width = label.Length * 12 + 14;
		var rect = new Rect2(point.X - width / 2f, point.Y - 9, width, 18);
		DrawRect(rect, new Color(0.035f, 0.075f, 0.095f, 0.78f));
		DrawRect(new Rect2(rect.Position.X, rect.Position.Y, 3, rect.Size.Y), color);
		Text(label, rect.Position + new Vector2(7, 13), 10, new Color("eaf2ef"));
	}

	private void DrawMapAttributionStrip()
	{
		Text("TileMap：Kenney Tiny Town + OpenGameArt Island Terrain · CC0",
			new Vector2(652, 802), 11, new Color("8fd7c1"));
	}

	private void DrawRoutesAndMembers()
	{
		if (_showTrails)
		{
			foreach (var m in _members)
			{
				if (m.Trail.Count > 1)
					DrawPolyline(m.Trail.ToArray(), new Color(m.Color, 0.40f), 2.5f, true);
				foreach (var point in m.Trail.Where((_, idx) => idx % 8 == 0))
					DrawCircle(point, 2.2f, new Color(m.Color, 0.65f));
			}
		}

		// 正在对话的连接线与气泡
		foreach (var talk in _activeTalks)
		{
			DrawDashedLine(talk.A.Position, talk.B.Position, new Color("fff2ae"), 2, 4);
			var center = (talk.A.Position + talk.B.Position) / 2 + new Vector2(0, -32);
			var bubbleW = Math.Min(320, talk.Dialogue.Length * 13 + 24);
			DrawRect(new Rect2(center.X - bubbleW / 2f, center.Y - 12, bubbleW, 24), new Color("141e24"), true);
			DrawRect(new Rect2(center.X - bubbleW / 2f, center.Y - 12, bubbleW, 24), new Color("f4d58d"), false, 1.5f);
			Text(Trim(talk.Dialogue, 22), new Vector2(center.X - bubbleW / 2f + 8, center.Y + 5), 11, Colors.White);
		}

		// 渲染 16 学员 + 恶魔
		foreach (var m in _members.OrderBy(c => c.Position.Y))
			DrawMember(m);
	}

	private void DrawMember(AcademyMember m)
	{
		var p = m.Position;

		// 选中标记光环
		if (m == _selected)
		{
			DrawCircle(p + new Vector2(0, 7), 24, new Color(1, 1, 1, 0.18f));
			DrawArc(p + new Vector2(0, 7), 24, 0, Mathf.Tau, 24, new Color("fff0a8"), 2.5f);
		}

		// 投影
		DrawEllipse(p + new Vector2(0, 15), new Vector2(13, 5), new Color(0, 0, 0, 0.35f));

		// 精灵动画帧绘制
		var animationFrame = m.IsMoving && ((int)(_worldMinutes * 4) & 1) == 1 ? 1 : 0;
		var source = new Rect2(m.FacingColumn * 16, (m.SpriteRow + animationFrame) * 16, 16, 16);
		DrawTextureRectRegion(_urbanAtlas, new Rect2(p.X - 16, p.Y - 23, 32, 32), source);

		// 大量角色同处一地时只标记选中者或交谈者，避免姓名牌遮住整个场景。
		var shouldLabel = m == _selected || _activeTalks.Any(t => t.A == m || t.B == m);
		if (shouldLabel)
		{
			var labelWidth = Math.Max(50, m.Name.Length * 15 + 14);
			DrawRect(new Rect2(p.X - labelWidth / 2f, p.Y - 40, labelWidth, 18), new Color(0.08f, 0.12f, 0.15f, 0.88f));
			DrawRect(new Rect2(p.X - labelWidth / 2f, p.Y - 40, 4, 18), m.Color);
			Text(m.Name, new Vector2(p.X - labelWidth / 2f + 8, p.Y - 26), 12, Colors.White);
		}
	}

	private void DrawSidebar()
	{
		DrawRect(SideRect, new Color("182329"));

		// 成员列表标题与分页切换
		Text("学园成员生活动向", new Vector2(1184, 98), 18, new Color("eef4ef"));
		var pageBtnText = _listPage == 0 ? "下一页 (9-17) ▶" : "◀ 上一页 (1-8)";
		DrawRect(new Rect2(1342, 82, 102, 22), new Color("2d3d46"));
		Text(pageBtnText, new Vector2(1346, 97), 11, new Color("90e0ef"));

		// 列表项目渲染 (每页展示 8-9 人)
		var startIndex = _listPage * 8;
		var displayMembers = _members.Skip(startIndex).Take(8).ToList();
		var y = 126f;

		foreach (var m in displayMembers)
		{
			var isSelected = m == _selected;
			if (isSelected)
				DrawRect(new Rect2(1176, y - 14, 272, 40), new Color("2c3e47"));

			DrawCircle(new Vector2(1190, y + 4), 6, m.Color);
			Text($"{m.Name} · {m.Title.Split('·')[0].Trim()}", new Vector2(1202, y + 5), 13, new Color("e7eeee"));
			Text($"[{m.CurrentPlace}] {Trim(m.CurrentActivity, 14)}", new Vector2(1202, y + 20), 11, new Color("9fb1b7"));

			y += 44;
		}

		// 分隔线
		y = 485f;
		DrawLine(new Vector2(1184, y), new Vector2(1440, y), new Color("364952"));

		// 选中对象的深度动机观察卡片
		if (_selected is not null)
		{
			y += 24;
			Text($"【观察】{_selected.Name} ({_selected.Romaji})", new Vector2(1184, y), 16, _selected.Color.Lightened(0.2f));
			y += 18;
			Text($"{_selected.Title}  ·  {_selected.Faculty}", new Vector2(1184, y), 12, new Color("90e0ef"));
			y += 18;
			Text($"当前活动：{_selected.CurrentActivity}", new Vector2(1184, y), 12, new Color("e2ecef"));
			y += 18;
			Text($"所在设施：{_selected.CurrentPlace}", new Vector2(1184, y), 12, new Color("e2ecef"));

			// 5 大动机槽进度条 (Hunger, Energy, Social, Fun, Comfort)
			y += 22;
			DrawMotiveBar(1184, y, "饱腹", _selected.Hunger, new Color("ff595e"));
			y += 16;
			DrawMotiveBar(1184, y, "精力", _selected.Energy, new Color("52b788"));
			y += 16;
			DrawMotiveBar(1184, y, "社交", _selected.Social, new Color("3a86ff"));
			y += 16;
			DrawMotiveBar(1184, y, "娱乐", _selected.Fun, new Color("ffb703"));
			y += 16;
			DrawMotiveBar(1184, y, "舒适", _selected.Comfort, new Color("e0aaff"));

			// 恶魔戒备值
			y += 16;
			DrawMotiveBar(1184, y, "警戒", _selected.DemonVigilance, new Color("c77dff"));
		}

		// 底部最近生活事件流
		y = 705f;
		DrawLine(new Vector2(1184, y), new Vector2(1440, y), new Color("364952"));
		Text("学园因果事件流", new Vector2(1184, y + 20), 14, new Color("eef4ef"));
		y += 36;
		foreach (var log in _logs.Take(4))
		{
			DrawCircle(new Vector2(1190, y - 3), 3.5f, log.Color);
			Text(log.Time, new Vector2(1198, y), 11, new Color("f4d58d"));
			Text(Trim(log.Text, 17), new Vector2(1238, y), 11, new Color("d9e2e3"));
			y += 21;
		}
	}

	private void DrawMotiveBar(float x, float y, string label, float value, Color fill)
	{
		Text(label, new Vector2(x, y + 9), 11, new Color("b3c4c8"));
		var barX = x + 38;
		var barW = 210f;
		DrawRect(new Rect2(barX, y, barW, 10), new Color("10161a"));
		var filledW = barW * Math.Clamp(value / 100f, 0f, 1f);
		DrawRect(new Rect2(barX, y, filledW, 10), fill);
		Text($"{value:0}", new Vector2(barX + barW + 5, y + 9), 10, new Color("cad7da"));
	}

	private void DrawFooter()
	{
		// 用轻量信息带收束地图下方空间，使观察界面保持完整的仪表盘构图。
		DrawRect(new Rect2(24, 680, 1120, 156), new Color("111a1f"));
		DrawLine(new Vector2(24, 680), new Vector2(1144, 680), new Color("2f424b"), 1.5f);
		Text("观察说明", new Vector2(42, 710), 15, new Color("eef4ef"));
		Text("成员依据时段、职责与当前动机选择目的地；相遇后会产生对话，彩色轨迹保留其最近移动历史。",
			new Vector2(42, 738), 13, new Color("a9bbc0"));
		Text("当前观察：自动推演中", new Vector2(42, 774), 13, new Color("77c7b2"));
		Text($"角色 {_members.Count}  ·  设施 {_facilities.Count}  ·  活跃交谈 {_activeTalks.Count}",
			new Vector2(42, 802), 13, new Color("d4e2e5"));

		Text("【操作】单击角色/设施：聚焦观察  |  SPACE：暂停/恢复  |  1/2/3/4：调速 (0.5x~4x)  |  TAB：轮询角色  |  T：轨迹开关  |  R：重置推演",
			new Vector2(24, 874), 13, new Color("8fa4aa"));
		var trailText = _showTrails ? "轨迹: 开启" : "轨迹: 关闭";
		Text(trailText, new Vector2(1376, 874), 13, new Color("77c7b2"));
	}

	private string ClockText()
	{
		var m = (int)_worldMinutes;
		return $"{m / 60:00}:{m % 60:00}";
	}

	private void Text(string value, Vector2 position, int size, Color color) =>
		DrawString(_font, position, value, HorizontalAlignment.Left, -1, size, color);

	private void Pill(Rect2 rect, string value, Color color)
	{
		DrawStyleBox(new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12
		}, rect);
		Text(value, rect.Position + new Vector2(13, 19), 12, Colors.White);
	}

	private void DrawEllipse(Vector2 center, Vector2 radius, Color color)
	{
		var points = new Vector2[20];
		for (var i = 0; i < points.Length; i++)
		{
			var angle = Mathf.Tau * i / points.Length;
			points[i] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
		}
		DrawColoredPolygon(points, color);
	}

	private static string Trim(string value, int count) => value.Length <= count ? value : value[..(count - 1)] + "…";
}
