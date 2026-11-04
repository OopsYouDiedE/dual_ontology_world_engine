using DuelView.Runtime;
using System.Diagnostics;
using System.Text.Json;
using System.Numerics;

internal static class RoomLifecycleChecks
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, Action action)
        {
            var timer = Stopwatch.StartNew();
            try { action(); Console.WriteLine($"PASS {name} {timer.Elapsed.TotalMilliseconds:F3} ms"); }
            catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        static void Expect(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
        static RoomLifecycle Active()
        {
            var room = new RoomLifecycle(); room.Update(0, true, true, false, 5);
            room.CompletePreparation(room.Generation); return room;
        }
        Check("cold demand prepares before activation", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, true, false, 5);
            Expect(t.Effect == RoomEffect.Prepare && r.Phase == RoomPhase.Preparing);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.Activate);
        });
        Check("prepared room remains invisible until active demand", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.Phase == RoomPhase.Preparing && r.Prepared);
            Expect(r.Update(1, true, true, false, 5).Effect == RoomEffect.Activate);
        });
        Check("cooldown retains and reactivates same generation", () => {
            var r = Active(); var g = r.Generation;
            Expect(r.Update(1, false, false, false, 5).Effect == RoomEffect.Retain);
            Expect(r.Update(2, true, true, false, 5).Effect == RoomEffect.Activate);
            Expect(r.Generation == g);
        });
        Check("release occurs at exact retention boundary", () => {
            var r = Active(); r.Update(1, false, false, false, 5);
            Expect(r.Update(4.999, false, false, false, 5).Effect == RoomEffect.None);
            var t = r.Update(5, false, false, false, 5);
            Expect(t.Effect == RoomEffect.Release && r.Phase == RoomPhase.Unloading);
            Expect(r.CompleteRelease(t.Generation) && r.Phase == RoomPhase.Unloaded && !r.Prepared);
        });
        Check("late async callback cannot reactivate cancelled preparation", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, true, false, 0);
            var release = r.Update(1, false, false, false, 0);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(release.Generation));
            r.Update(2, true, true, false, 0);
            Expect(!r.CompleteRelease(release.Generation));
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
        });
        Check("in flight protection delays release", () => {
            var r = Active(); r.Update(1, false, false, false, 5);
            Expect(r.Update(100, false, false, true, 5).Effect == RoomEffect.None);
            Expect(r.Update(104.99, false, false, false, 5).Effect == RoomEffect.None);
            Expect(r.Update(105, false, false, false, 5).Effect == RoomEffect.Release);
        });
        Check("10000 doorway reversals do not start a second preparation", () => {
            var r = Active(); var g = r.Generation;
            for (var i = 1; i <= 10000; i++) r.Update(i * .001, true, i % 2 == 0, false, 5);
            Expect(r.Generation == g && r.Prepared);
        });
        Check("monotonic finite clock required", () => {
            var r = Active(); var caught = 0;
            foreach (var time in new[] { -1.0, double.NaN, double.PositiveInfinity })
                try { r.Update(time, false, false, false, 5); } catch (ArgumentOutOfRangeException) { caught++; }
            Expect(caught == 3);
        });
        Check("failed preparation releases before retry", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            var failed = r.FailPreparation(t.Generation);
            Expect(failed.Effect == RoomEffect.Release && r.Phase == RoomPhase.Unloading);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(failed.Generation));
            Expect(r.Update(1, true, true, false, 5).Effect == RoomEffect.Prepare);
        });
        Check("demand arriving during release starts a fresh generation on next slice", () => {
            var r = Active(); r.Update(1, false, false, false, 0);
            var release = r.Update(2, false, false, false, 0);
            Expect(r.Update(3, true, true, false, 0).Effect == RoomEffect.None);
            Expect(r.CompleteRelease(release.Generation));
            var restart = r.Update(4, true, true, false, 0);
            Expect(restart.Effect == RoomEffect.Prepare && restart.Generation > release.Generation);
        });
        Check("cold pinned room prepares without activation", () => {
            var r = new RoomLifecycle();
            Expect(r.Update(0, false, false, true, 5).Effect == RoomEffect.Prepare);
            Expect(r.CompletePreparation(r.Generation).Effect == RoomEffect.None);
        });
        Check("invalid retention cannot mutate state", () => {
            foreach (var retention in new[] { -1.0, double.NaN, double.PositiveInfinity }) {
                var r = new RoomLifecycle(); var caught = false;
                try { r.Update(1, true, true, false, retention); } catch (ArgumentOutOfRangeException) { caught = true; }
                Expect(caught && r.Phase == RoomPhase.Unloaded && r.Generation == 0);
            }
        });
        Check("residency emits preparation activation retention and release separately", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetPrepare("fixture", true);
            var prepare = manager.Step(0).Single();
            Expect(prepare.Effect == RoomEffect.Prepare && !manager.Ready("fixture"));
            Expect(manager.Acknowledge(prepare, true, 0) is null);
            manager.SetVisible("fixture", true, 1);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Activate && manager.Ready("fixture"));
            manager.SetPrepare("fixture", false); manager.SetVisible("fixture", false, 2);
            Expect(manager.Step(2).Single().Effect == RoomEffect.Retain);
            var release = manager.Step(6).Single();
            Expect(release.Effect == RoomEffect.Release && !manager.Ready("fixture"));
            Expect(manager.Acknowledge(release, true, 6) is null);
            Expect(manager.Phase("fixture") == RoomPhase.Unloaded);
        });
        Check("duplicate preparation completion and failure cannot mutate prepared room", () => {
            var r = new RoomLifecycle(); var t = r.Update(0, true, false, false, 5);
            r.CompletePreparation(t.Generation);
            Expect(r.CompletePreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.FailPreparation(t.Generation).Effect == RoomEffect.None);
            Expect(r.Prepared && r.Phase == RoomPhase.Preparing && r.Generation == t.Generation);
        });
        Check("residency returns activation after asynchronous preparation", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single();
            var activation = manager.Acknowledge(prepare, true, 0);
            Expect(activation?.Effect == RoomEffect.Activate && manager.Ready("fixture"));
        });
        Check("failed release remains pending and returns same request", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 0,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single(); manager.Acknowledge(prepare, true, 0);
            manager.SetVisible("fixture", false, 1); manager.Step(1);
            var release = manager.Step(2).Single();
            Expect(manager.Acknowledge(release, false, 2) == release);
            Expect(manager.Phase("fixture") == RoomPhase.Unloading);
            Expect(manager.Acknowledge(release, true, 3) is null && manager.Phase("fixture") == RoomPhase.Unloaded);
        });
        Check("failed preparation retains retry delay after release", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetPrepare("fixture", true);
            var prepare = manager.Step(0).Single();
            var release = manager.CancelPreparation(prepare, 0);
            manager.Acknowledge(release, true, 0);
            Expect(manager.Step(0.5).Count == 0 && manager.Phase("fixture") == RoomPhase.Unloaded);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Prepare);
        });
        Check("completion uses current visibility before next slice", () => {
            var policy = JsonSerializer.SerializeToElement(new { inactive_retention_seconds = 5,
                room_requests_per_slice = 1, inactive_simulation = "core", retry_delay_seconds = 1 });
            var manager = new RoomResidency(policy); manager.Register("fixture", 10, 1);
            manager.SetVisible("fixture", true, 0);
            var prepare = manager.Step(0).Single();
            manager.SetVisible("fixture", false, 0);
            Expect(manager.Acknowledge(prepare, true, 0) is null && !manager.Ready("fixture"));
            manager.SetVisible("fixture", true, 1);
            Expect(manager.Step(1).Single().Effect == RoomEffect.Activate && manager.Ready("fixture"));
        });
        Check("room bounds include plane contact and reject outside", () => {
            Plane[] planes = [new(Vector3.UnitX, 0)];
            Expect(RoomVisibility.IntersectsBounds(new(-1, -1, -1), new(0, 1, 1), planes));
            Expect(RoomVisibility.IntersectsBounds(new(-1, -1, -1), new(1, 1, 1), planes));
            Expect(!RoomVisibility.IntersectsBounds(new(-2, -1, -1), new(-1, 1, 1), planes));
            var caught = false;
            try { RoomVisibility.IntersectsBounds(Vector3.One, Vector3.Zero, planes); } catch (ArgumentException) { caught = true; }
            Expect(caught);
        });
        Check("aperture clipping preserves intersection and rejects outside", () => {
            Vector3[] aperture = [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)];
            Plane[] planes = [new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)];
            var result = RoomVisibility.ClipAperture(aperture, planes);
            Expect(result.Count == 4 && result.All(p => p.X >= 0 && p.Y >= 0));
            Expect(result.Any(p => p == Vector3.Zero) && result.Any(p => p == new Vector3(1, 1, 0)));
            Expect(RoomVisibility.ClipAperture(aperture, [new Plane(Vector3.UnitX, -2)]).Count == 0);
        });
        Check("external retention setting updates existing cooldown without resetting elapsed time", () => {
            var settings=RuntimeDebugSettings.Parse("schema \"dv-script-v1\"; data debug.coregodot { value = { room_inactive_retention_seconds = 60; }; }");
            var policy=JsonSerializer.SerializeToElement(new {inactive_retention_seconds=5,room_requests_per_slice=1,inactive_simulation="core",retry_delay_seconds=1});
            var manager=new RoomResidency(policy);manager.Register("fixture",10,1);
            manager.SetVisible("fixture",true,0);var prepare=manager.Step(0).Single();manager.Acknowledge(prepare,true,0);
            manager.SetVisible("fixture",false,1);manager.Step(1);
            manager.SetRetentionSeconds(settings.RoomInactiveRetentionSeconds);
            Expect(manager.Step(5).Count==0 && manager.Phase("fixture")==RoomPhase.CoolingDown);
            Expect(manager.Step(59.999).Count==0);
            Expect(manager.Step(60).Single().Effect==RoomEffect.Release);
            var rejected=false;try { manager.SetRetentionSeconds(double.NaN); } catch(ArgumentOutOfRangeException) {rejected=true;}
            Expect(rejected && manager.RetentionSeconds==60);
        });
        Check("connected sightlines prepare behind closed leaves before first opening", () => {
            RoomSightLink[] links=[new("corridor","foyer",true,true),new("foyer","outside",true,false),
                new("outside","backdrop",true,true),new("backdrop","foyer",true,true),new("foyer","hidden",false,true)];
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,false).SetEquals(["corridor","foyer"]));
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,true).SetEquals(["corridor","foyer","outside","backdrop"]));
            links[1]=links[1] with {Open=true};
            Expect(RoomVisibility.ReachableRooms(["corridor"],links,false).SetEquals(["corridor","foyer","outside","backdrop"]));
            Expect(RoomVisibility.ReachableRooms([],links,true).Count==0);
        });
        Check("room visibility rejects invalid planes", () => {
            Vector3[] aperture = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
            foreach (Plane[] planes in new Plane[][] { [], [new Plane(Vector3.Zero, 0)],
                [new Plane(new Vector3(float.NaN, 0, 0), 0)], [new Plane(Vector3.UnitX, float.PositiveInfinity)] }) {
                var boundsCaught = false; var clipCaught = false;
                try { RoomVisibility.IntersectsBounds(Vector3.Zero, Vector3.One, planes); } catch (ArgumentException) { boundsCaught = true; }
                try { RoomVisibility.ClipAperture(aperture, planes); } catch (ArgumentException) { clipCaught = true; }
                Expect(boundsCaught && clipCaught);
            }
        });
        return failures == 0 ? 0 : 1;
    }
}
