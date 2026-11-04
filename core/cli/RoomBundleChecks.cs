using DuelView.Dv;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

internal static class RoomBundleChecks
{
    public static int Run()
    {
        var failed=0;
        void Check(string name,Action run) {
            var clock=Stopwatch.StartNew();
            try { run(); Console.WriteLine($"PASS {name} {clock.Elapsed.TotalMilliseconds:F3} ms"); }
            catch(Exception error) { failed++;Console.WriteLine($"FAIL {name}: {error.Message}"); }
        }
        static void Expect(bool value) { if(!value)throw new InvalidDataException("Assertion failed."); }
        static string PathOf(string resource)=>Path.GetFullPath(resource.Replace("res://",""));
        static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PathOf(path)))).ToLowerInvariant();
        var scope=DvScriptReader.Parse(File.ReadAllText("assets/data/stage1-zone-b.dv")).GetProperty("data").GetProperty("stage1.zone_b").GetProperty("value");
        var source=DvScriptReader.Parse(File.ReadAllText(PathOf(scope.GetProperty("source").GetString()!))).GetProperty("data").GetProperty("space.configurable_building").GetProperty("value");
        var manifest=JsonDocument.Parse(File.ReadAllText(".runtime/generated/stage1/bundles/manifest.json")).RootElement;
        Check("compiled room metadata matches current DV publication",()=>{
            Expect(Hash(scope.GetProperty("source").GetString()!)==manifest.GetProperty("source_sha256").GetString());
            Expect(Hash("res://assets/data/stage1-zone-b.dv")==manifest.GetProperty("scope_sha256").GetString());
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var id in scope.GetProperty("rooms").EnumerateArray().Concat(scope.GetProperty("boundary_rooms").EnumerateArray())) {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id.GetString());
                var expected=authored.GetProperty("bbox").EnumerateArray().Select(x=>x.GetDouble());
                var actual=manifest.GetProperty("rooms").GetProperty(id.GetString()!).EnumerateArray().Select(x=>x.GetDouble()).ToArray();
                Expect(actual.Length==4 && expected.Zip(actual).All(pair=>Math.Abs(pair.First-pair.Second)<1e-8));
            }
        });
        Check("every room owns resource bundles and all bundle checksums match",()=>{
            var rooms=manifest.GetProperty("rooms").EnumerateObject().Select(p=>p.Name).ToHashSet();
            var owned=new HashSet<string>();var paths=new HashSet<string>();
            foreach(var b in manifest.GetProperty("bundles").EnumerateArray()) {
                var path=b.GetProperty("path").GetString()!;
                Expect(paths.Add(path) && Hash(path)==b.GetProperty("sha256").GetString());
                foreach(var owner in b.GetProperty("rooms").EnumerateArray()) {Expect(rooms.Contains(owner.GetString()!));owned.Add(owner.GetString()!);}
            }
            Expect(rooms.SetEquals(owned));
        });
        Check("boundary view regions derive dimensions from authored rooms",()=>{
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var region in scope.GetProperty("boundary_regions").EnumerateArray()) {
                var authored=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==region.GetProperty("room_ref").GetString()).GetProperty("bbox");
                var actual=manifest.GetProperty("rooms").GetProperty(region.GetProperty("id").GetString()!);
                var lateral=manifest.GetProperty("rooms").GetProperty(region.GetProperty("lateral_ref").GetString()!);
                var margin=region.GetProperty("lateral_margin_m").GetDouble();
                Expect(Math.Abs(actual[0].GetDouble()-Math.Max(authored[0].GetDouble(),lateral[0].GetDouble()-margin))<1e-8);
                Expect(Math.Abs(actual[2].GetDouble()-Math.Min(authored[2].GetDouble(),lateral[2].GetDouble()+margin))<1e-8);
                Expect(actual[1].GetDouble()==authored[1].GetDouble());
                var expectedEnd=region.TryGetProperty("cap_at_room_end",out var cap)&&cap.GetBoolean()?authored[3].GetDouble():manifest.GetProperty("rooms").GetProperty(region.GetProperty("connect_to").GetString()!)[1].GetDouble();
                Expect(actual[3].GetDouble()==expectedEnd);
            }
        });
        Check("room portals connect both authored sides including gate exterior",()=>{
            foreach(var portal in manifest.GetProperty("openings").EnumerateArray()) {
                var links=portal.GetProperty("room_links").EnumerateArray().Select(x=>x.GetString()!).ToArray();
                Expect(links.Length==2 && links.Distinct().Count()==links.Length);
                Expect(links.All(id=>manifest.GetProperty("rooms").TryGetProperty(id,out _)));
                Expect(portal.GetProperty("top").GetDouble()>portal.GetProperty("bottom").GetDouble());
            }
        });
        Check("self-closing boundary leaf replaces all obsolete rotor collision",()=>{
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(o=>o.GetProperty("id").GetString()==scope.GetProperty("gate").GetString());
            var leaf=gate.GetProperty("door");
            Expect(leaf.GetProperty("self_close_seconds").GetDouble()>0);
            Expect(leaf.GetProperty("initial_angle").GetDouble()==0);
            Expect(leaf.GetProperty("angle_min").GetDouble()<=leaf.GetProperty("open_angle").GetDouble());
            var replaced=leaf.GetProperty("replaces_colliders").EnumerateArray().Select(x=>x.GetString()).ToHashSet();
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            var mechanism=gate.GetProperty("source_mechanism").GetProperty("point").EnumerateArray().Select(x=>x.GetDouble()).ToArray();
            foreach(var collider in floor.GetProperty("colliders").EnumerateArray()) {
                if(collider.GetProperty("kind").GetString()!="gate_rotor" || !collider.TryGetProperty("rect",out var rectangle))continue;
                var r=rectangle.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
                if(Math.Abs((r[0]+r[2])/2-mechanism[0])<1 && Math.Abs((r[1]+r[3])/2-mechanism[1])<1)
                    Expect(replaced.Contains(collider.GetProperty("id").GetString()));
            }
        });
        Check("circular blast hatch dimensions align bore plate hinge and floor gap",()=>{
            var gate=manifest.GetProperty("openings").EnumerateArray().Single(o=>o.GetProperty("id").GetString()==scope.GetProperty("gate").GetString());
            var frame=gate.GetProperty("frame");var leaf=gate.GetProperty("door");
            Expect(frame.GetProperty("profile").GetString()=="circular_blast" && leaf.GetProperty("profile").GetString()=="circular_blast");
            var radius=frame.GetProperty("clear_diameter_m").GetDouble()/2;
            var width=leaf.GetProperty("width").GetDouble();
            Expect(width==leaf.GetProperty("height").GetDouble() && width/2>radius && width<gate.GetProperty("structural_width").GetDouble());
            var floor=gate.GetProperty("bottom").GetDouble();
            var hingeZ=leaf.GetProperty("hinge_design")[2].GetDouble();
            Expect(Math.Abs(hingeZ-floor-gate.GetProperty("source_mechanism").GetProperty("under_gap").GetDouble())<1e-8);
            Expect(Math.Abs(hingeZ+width/2-floor-frame.GetProperty("center_height_m").GetDouble())<1e-8);
            Expect(frame.GetProperty("center_height_m").GetDouble()-radius>=0);
            Expect(leaf.GetProperty("dog_count").GetDouble()>=3 && leaf.GetProperty("wheel_radius_m").GetDouble()>0);
        });
        Check("rectangular hinges clear their frame face and floor",()=>{
            var floor=source.GetProperty("floors").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==scope.GetProperty("floor").GetString());
            foreach(var opening in manifest.GetProperty("openings").EnumerateArray()) {
                var leaf=opening.GetProperty("door");
                if(leaf.TryGetProperty("profile",out var profile)&&profile.GetString()=="circular_blast")continue;
                var rect=opening.GetProperty("rect");var hinge=leaf.GetProperty("hinge_design");
                var axis=rect[2].GetDouble()-rect[0].GetDouble()<rect[3].GetDouble()-rect[1].GetDouble()?0:1;
                var center=(rect[axis].GetDouble()+rect[axis+2].GetDouble())/2;
                var frame=opening.GetProperty("frame");
                var offset=frame.GetProperty("depth").GetDouble()/2+leaf.GetProperty("thickness").GetDouble()/2+leaf.GetProperty("hinge_face_clearance_m").GetDouble();
                Expect(Math.Abs(Math.Abs(hinge[axis].GetDouble()-center)-offset)<1e-8);
                var room=floor.GetProperty("rooms").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==leaf.GetProperty("opens_into").GetString()).GetProperty("bbox");
                Expect((hinge[axis].GetDouble()-center)*((room[axis].GetDouble()+room[axis+2].GetDouble())/2-center)>0);
                var gap=leaf.GetProperty("floor_gap_m").GetDouble();
                Expect(Math.Abs(hinge[2].GetDouble()-opening.GetProperty("bottom").GetDouble()-gap)<1e-8);
                Expect(Math.Abs(leaf.GetProperty("height").GetDouble()+2*gap-frame.GetProperty("net_height").GetDouble())<1e-8);
            }
        });
        return failed==0?0:1;
    }
}
