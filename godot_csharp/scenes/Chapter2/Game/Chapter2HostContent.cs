using Godot;
using DualOntologyWorldEngine.Chapter2.Prototype;

namespace DualOntologyWorldEngine.Chapter2;

public static class Chapter2HostContent
{
    public static readonly Dictionary<Place, string[]> Places = new()
    {
        [Place.Hall] = ["central_hall", "south"], [Place.Lecture] = ["lecture", "from_central"], [Place.Logistics] = ["logistics", "west"],
        [Place.Clinic] = ["clinic", "south"], [Place.Greenhouse] = ["greenhouse", "south"], [Place.ArchiveFront] = ["archive", "south"],
        [Place.Oath] = ["campus_s", "gate_inside"], [Place.DormLife] = ["living_hall", "north_door"], [Place.DormW] = ["zone_jia", "from_hall"],
        [Place.DormS] = ["zone_bing", "from_hall"], [Place.DormL] = ["zone_ding", "from_hall"],
    };

    public static readonly Dictionary<string, Place> MapPlaces = new()
    {
        ["central_hall"] = Place.Hall, ["lecture"] = Place.Lecture, ["logistics"] = Place.Logistics, ["granary"] = Place.Logistics, ["clinic"] = Place.Clinic,
        ["greenhouse"] = Place.Greenhouse, ["archive"] = Place.ArchiveFront, ["campus_s"] = Place.Oath, ["living_hall"] = Place.DormLife,
        ["zone_jia"] = Place.DormW, ["zone_yi"] = Place.DormLife, ["zone_bing"] = Place.DormS, ["zone_ding"] = Place.DormL,
    };

    public static readonly Dictionary<P, string> Sprites = new()
    {
        [P.Det] = "detective", [P.Huo] = "engineer", [P.Shen] = "chef", [P.Su] = "musician", [P.Lucian] = "poet", [P.Gu] = "archaeologist",
        [P.Quer] = "thief", [P.Ward] = "lawyer", [P.Selma] = "naturalist", [P.Ruan] = "chessplayer", [P.Elena] = "dancer", [P.Moran] = "painter",
        [P.Veronica] = "chemist", [P.Bertrand] = "marksman", [P.Margaret] = "architect", [P.Mora] = "doctor",
    };

    public static Godot.Collections.Dictionary Snapshot()
    {
        var places = new Godot.Collections.Dictionary();
        foreach (var (place, target) in Places) places[place.ToString()] = new Godot.Collections.Array<string>(target);
        var mapPlaces = new Godot.Collections.Dictionary();
        foreach (var (map, place) in MapPlaces) mapPlaces[map] = place.ToString();
        var sprites = new Godot.Collections.Dictionary();
        foreach (var (person, sprite) in Sprites) sprites[person.ToString()] = sprite;
        return new Godot.Collections.Dictionary { ["host_places"] = places, ["map_places"] = mapPlaces, ["sprites"] = sprites };
    }

    public static RoomAction[] GlobalActions(Prototype.Game game)
    {
        var actions = new List<RoomAction>();
        if (game.Slot == Slot.Night)
        {
            actions.Add(new("睡下", "睡"));
            actions.Add(new("守夜……", "守夜", "night"));
        }
        else actions.Add(new("歇着（什么也不做，过一个时段）", "歇"));
        return actions.ToArray();
    }

    public static string Animation(string frame) => frame switch
    {
        "行走" => "walk", "操作" or "递物" or "拉拽" => "interact", "交谈" => "talk",
        "坐" => "sit", "蹲查" => "inspect", "看守" => "idle", _ => "idle",
    };
}
