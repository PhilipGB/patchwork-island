using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Linux end-to-end driver. Reads scene positions but acts only through movement,
/// E, mouse clicks and menus. Never seeds quest counters, teleports or awards rewards.
/// Run with isolated XDG_DATA_HOME and --dev --playthrough; reruns Continue the real save.
/// </summary>
public partial class GameplayWalkthrough : Node
{
    public Main Main { get; set; }
    private PlayerController Player => Main.GetNode<PlayerController>("Player");
    private CameraRig Rig => Main.GetNode<CameraRig>("CameraRig");
    private Hud Hud => Main.GetNode<Hud>("Hud");
    private VanController Van => Main.GetNode<VanController>("Van");
    private DrivingAcademyController Academy => Main.RtAcademy;
    private readonly Dictionary<Vector2I, bool> _walkable = new();
    private readonly HashSet<Vector2I> _blocked = new();
    private string _stage = "boot";
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = Run();
    }
    private static void SendInput(InputEvent input)
    {
        using (input) Input.ParseInputEvent(input);
    }

    private async Task Run()
    {
        try
        {
            await Wait(1);
            if (Main.Probe.CanContinue) await Button("Continue adventure");
            else { await Button("Start adventure"); await Button("Choose_boy"); }
            if (!Main.Probe.Data.DrivingLicenceIssued && !Main.Probe.Data.LegacyVanPermit)
            {
                _stage = "academy";
                Log("earning the driving licence before M05");
                await CompleteAcademy();
            }
            if (Van.Driven)
            {
                Stop();
                while (Mathf.Abs(Van.Speed) > .2f) await Wait(.1);
                await PressKey(Key.E);
            }
            for (int number = 1; number <= 21; number++)
            {
                string id = $"M{number:00}";
                if (Main.Probe.Data.IsCompleted(id)) continue;
                _stage = id;
                Log("starting");
                await Dismiss();
                if (Main.Probe.ActiveMission != id)
                {
                    await PressKey(Key.J);
                    await Button("Start_" + id);
                }
                switch (number)
                {
                    case 1: await Pickups(id); await Use("RedHouseStation"); break;
                    case 2: await Pickups(id); await Use("WorkshopStation"); break;
                    case 3: await Use("BridgeStation"); await Build("support", 3); await Build("plank", 4); break;
                    case 4:
                        await Use("ShopStation"); await Button("Add  +3"); await Button("Add  +3");
                        await Button("Add  +4"); await PressKey(Key.Escape); await Use("TownHallStation"); break;
                    case 5: case 9: case 19: await Race(number == 19); break;
                    case 6: await Pickups(id); await Use("RedHouseStation"); await Use("WorkshopStation"); break;
                    case 7: await Use("PlaygroundStation"); await Build("rope", 4); await Build("seat", 2); break;
                    case 8:
                        await Use("ParkStation");
                        foreach (string item in new[] { "bench", "flowerbed", "lamp" }) { await ButtonPrefix(item); await Build(item, 1); }
                        break;
                    case 10:
                        await Use("HouseStation"); await Use("FurnitureStation");
                        foreach (string price in new[] { "Buy  +8", "Buy  +5", "Buy  +5", "Buy  +3" }) await Button(price);
                        await Button("Place furniture");
                        foreach (string item in new[] { "table", "chair", "chair", "plant" }) { await ButtonPrefix(item); await Build(item, 1); }
                        break;
                    default: await ChapterTwo(number); break;
                }
                await Wait(.4);
                if (!Main.Probe.Data.IsCompleted(id)) throw new Exception("mission did not complete");
                Log("COMPLETED coins=" + Main.Probe.Coins);
                await Snap(id);
                await Dismiss();
                await Snap(id + "-world");
            }
            for (int number = 1; number <= 3; number++) await City(number);
            Log("ALL 24 MISSIONS COMPLETE");
            await Snap("all-complete");
            await PressKey(Key.Escape); await Button("Save now");
            GetTree().Quit();
        }
        catch (WalkthroughRestartException)
        {
            Stop();
            Log("CHECKPOINT saved; restart and Continue required");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            Stop();
            GD.PrintErr($"PLAYTHROUGH FAIL {_stage}: {ex}");
            await Snap("failure-" + _stage);
            GetTree().Quit(1);
        }
    }
    private void Log(string message) => GD.Print($"PLAYTHROUGH {_stage}: {message} pos={Player.GlobalPosition}");
    private async Task Wait(double seconds = .15) =>
        await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);
    private async Task PressKey(Key key)
    {
        SendInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await Wait(.1);
        SendInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await Wait(.15);
    }
    private async Task Click(Vector2 pos)
    {
        Input.WarpMouse(pos);
        await Wait(.1);
        SendInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos });
        SendInput(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = true });
        await Wait(.06);
        SendInput(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = false });
        await Wait(.18);
    }
    private IEnumerable<Button> Buttons() => Hud.FindChildren("*", "Button", true, false).OfType<Button>()
        .Where(b => b.IsVisibleInTree() && !b.Disabled && !b.IsQueuedForDeletion() && !b.GetParent().IsQueuedForDeletion());
    private async Task Button(string name)
    {
        await Wait();
        var b = Buttons().FirstOrDefault(b => b.Name == name || b.Text == name) ??
            throw new Exception("button missing: " + name + " available: " + string.Join(" | ", Buttons().Select(b => $"{b.Name}:{b.Text}")));
        for (Node parent = b.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(b);
        await Wait();
        await Click(b.GetGlobalRect().GetCenter());
    }
    private async Task ButtonPrefix(string name)
    {
        var b = Buttons().FirstOrDefault(b => b.Text.Contains(name, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("build option " + name);
        await Button(b.Text);
    }
    private async Task Dismiss()
    {
        for (int i = 0; i < 4 && Hud.ModalOpen; i++)
        {
            if (Buttons().Any(b => b.Text == "Continue")) await Button("Continue");
            else await PressKey(Key.Escape);
        }
    }
    private void Stop()
    {
        foreach (string action in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(action);
    }
    private async Task Pickups(string mission)
    {
        while (true)
        {
            var pickup = Main.GetChildren().OfType<Pickup>().Where(p => p.MissionId == mission && !p.IsQueuedForDeletion())
                .OrderBy(p => p.GlobalPosition.DistanceSquaredTo(Player.GlobalPosition)).FirstOrDefault();
            if (pickup == null) break;
            await Use(pickup);
            if (mission == "M18" && Main.GetChildren().OfType<Pickup>().Count(p => p.MissionId == mission && !p.IsQueuedForDeletion()) == 2)
                await Checkpoint("M18-parts");
        }
    }
    private async Task Use(string name) => await Use(GetTree().GetNodesInGroup("interactable").OfType<Interactable>().FirstOrDefault(n => n.Name == name) ?? throw new Exception("interaction missing " + name));
    private async Task Use(Interactable target)
    {
        Log("walk to " + target.Name + " " + target.GlobalPosition);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            await Walk(target.GlobalPosition, Math.Max(1.0f, Math.Min(2.8f, target.Range - .5f) - attempt * .5f));
            await Wait(.25);
            bool equivalentCrate = target.Name.ToString().StartsWith("CityCrateStation_") &&
                Player.NearestInteractable?.Name.ToString().StartsWith("CityCrateStation_") == true &&
                Player.NearestInteractable.IsAvailable();
            if (Player.NearestInteractable == target || equivalentCrate)
            {
                await PressKey(Key.E);
                return;
            }
            Log("nearest was " + Player.NearestInteractable?.Name);
        }
        throw new Exception("cannot focus " + target.Name);
    }
    private async Task Walk(Vector3 target, float range = 1.2f)
    {
        _blocked.Clear();
        for (int leg = 0; Horizontal(Player.GlobalPosition, target) > range; leg++)
        {
            if (leg > 200) throw new Exception("walking route exhausted towards " + target);
            // City collision follows nearby detail tiers. Re-plan from actual
            // colliders as the walker approaches, rather than walking a stale route.
            _walkable.Clear();
            var route = Route(Player.GlobalPosition, target, range).Take(8).ToList();
            if (route.Count == 0) break;
            Vector3 previous = Player.GlobalPosition;
            int stuck = 0;
            bool replan = false;
            foreach (var point in route)
            {
                for (int tick = 0; Horizontal(Player.GlobalPosition, point) > .45f; tick++)
                {
                    if (tick > 100) throw new Exception("walk stuck at " + Player.GlobalPosition + " towards " + point);
                    Vector3 to = point - Player.GlobalPosition; to.Y = 0; to = to.Normalized();
                    var f = -Rig.GlobalBasis.Z; f.Y = 0; f = f.Normalized();
                    var r = Rig.GlobalBasis.X; r.Y = 0; r = r.Normalized();
                    Stop();
                    float x = to.Dot(r), z = to.Dot(f);
                    Input.ActionPress(x > 0 ? "move_right" : "move_left", Mathf.Abs(x));
                    Input.ActionPress(z > 0 ? "move_forward" : "move_back", Mathf.Abs(z));
                    await Wait(.08);
                    if (Horizontal(previous, Player.GlobalPosition) < .03f) stuck++; else stuck = 0;
                    previous = Player.GlobalPosition;
                    if (stuck > 12)
                    {
                        _blocked.Add(Grid(point));
                        Stop(); replan = true; break;
                    }
                }
                if (replan) break;
            }
        }
        Stop();
        await Wait(.2);
    }
    private static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    private static Vector2I Grid(Vector3 p) => new(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Z));
    private static Vector3 Position(Vector2I p) => new(p.X, 0, p.Y);
    private List<Vector3> Route(Vector3 start, Vector3 end, float range)
    {
        var from = Grid(start);
        var frontier = new PriorityQueue<Vector2I, float>(); frontier.Enqueue(from, 0);
        var came = new Dictionary<Vector2I, Vector2I>();
        var cost = new Dictionary<Vector2I, float> { { from, 0 } };
        Vector2I? finish = null;
        int checkedCells = 0;
        while (frontier.TryDequeue(out var current, out _) && checkedCells++ < 20000)
        {
            if (Horizontal(Position(current), end) <= range) { finish = current; break; }
            foreach (var step in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                var next = current + step;
                if (next.X < Math.Min(start.X, end.X) - 50 || next.X > Math.Max(start.X, end.X) + 50 ||
                    next.Y < Math.Min(start.Z, end.Z) - 50 || next.Y > Math.Max(start.Z, end.Z) + 50) continue;
                if (!IsWalkable(next)) continue;
                float nextCost = cost[current] + 1;
                if (cost.TryGetValue(next, out float old) && old <= nextCost) continue;
                cost[next] = nextCost; came[next] = current;
                frontier.Enqueue(next, nextCost + Horizontal(Position(next), end));
            }
        }
        if (finish == null) throw new Exception("no walking route " + start + " to " + end);
        var result = new List<Vector3>();
        for (var n = finish.Value; n != from; n = came[n]) result.Add(Position(n));
        result.Reverse(); return result;
    }
    private bool IsWalkable(Vector2I cell)
    {
        if (_walkable.TryGetValue(cell, out bool result)) return result;
        if (_blocked.Contains(cell)) return false;
        Vector3 p = Position(cell);
        if (p.X < -59 || p.X > 508 || Mathf.Abs(p.Z) > 185) return false;
        if (p.X > 40 && p.X < 50 && (!Main.Probe.BridgeBuilt || Mathf.Abs(p.Z) > 3)) return false;
        using var ground = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 3, p - Vector3.Up, 1);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(ground);
        result = hit.Count > 0 && ((Vector3)hit["position"]).Y < .6f;
        if (result)
        {
            using var shape = new SphereShape3D { Radius = .48f };
            using var query = new PhysicsShapeQueryParameters3D { Shape = shape, Transform = new Transform3D(Basis.Identity, p + Vector3.Up * .95f), CollisionMask = 1 | 4 };
            result = GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
        }
        _walkable[cell] = result; return result;
    }
    private World3D GetWorld3D() => Player.GetWorld3D();
    private async Task Build(string piece, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var socket = GetTree().GetNodesInGroup("build_socket").OfType<BuildSocket>()
                .FirstOrDefault(s => (s.PieceType == piece || s.PieceType == "pad") && !s.Placed && s.SocketEnabled && s.IsVisibleInTree()) ?? throw new Exception("socket missing " + piece);
            var camera = GetViewport().GetCamera3D();
            var shape = socket.GetChildren().OfType<CollisionShape3D>().First();
            await Click(camera.UnprojectPosition(shape.GlobalPosition));
            if (!socket.Placed) throw new Exception("socket click did not place " + socket.SocketId);
        }
    }
    private async Task CompleteAcademy()
    {
        var service = Main.RtSession.Academy;
        if (!service.Enrolled)
        {
            int guard = 0;
            while (Main.Probe.Coins < 15 && guard++ < 12)
            {
                for (int cone = 1; cone <= 3; cone++)
                    await Use($"AcademyConeStation_{cone}");
                await Wait(.2f);
            }
            if (Main.Probe.Coins < 15) throw new Exception("cone job did not reach enrolment cost");
            await Use("DrivingSchoolStation");
            await Wait(.3f);
            await Button("AcademyEnrol");
            await Wait(.5f);
        }

        foreach (string id in new[] { "L01", "L02", "L03", "L04", "L05", "L06" })
        {
            if (service.LessonCompleted(id)) continue;
            await StartAcademyStage(id);
            switch (id)
            {
                case "L01":
                    Input.ActionPress("move_forward");
                    await Wait(1.4f);
                    Input.ActionRelease("move_forward");
                    GD.Print($"ACADEMY WALK L01 after accel car={Academy.TrainingCar?.GlobalPosition} " +
                             $"speed={Academy.TrainingCar?.Speed} stage={Academy.CurrentStage}");
                    await DriveTraining(Academy.TestMarker("Lesson01_Stop"));
                    await Wait(1.8f);
                    break;
                case "L02":
                    await DriveTraining(Academy.TestMarker("Lesson02_Gate01"));
                    await DriveTraining(Academy.TestMarker("Lesson02_Gate02"));
                    await DriveTraining(Academy.TestMarker("Lesson02_Gate02") + new Vector3(20, 0, 0));
                    await DriveTraining(Academy.TestMarker("Lesson02_Gate03"));
                    break;
                case "L03":
                    await DriveTraining(Academy.TestMarker("Lesson03_StopApproach"));
                    await Wait(2.4f);
                    GD.Print($"ACADEMY L03 after hold step={Academy.TestStep} " +
                             $"car={Academy.TrainingCar?.GlobalPosition} speed={Academy.TrainingCar?.Speed} " +
                             $"done={Main.RtSession.Academy.LessonCompleted("L03")}");
                    await DriveTraining(Academy.TestMarker("Lesson03_AfterStop"));
                    GD.Print($"ACADEMY L03 after release step={Academy.TestStep} " +
                             $"car={Academy.TrainingCar?.GlobalPosition} " +
                             $"done={Main.RtSession.Academy.LessonCompleted("L03")}");
                    break;
                case "L04":
                    await DriveTraining(Academy.TestMarker("Lesson04_TrafficLight") + new Vector3(0, 0, -8));
                    await WaitForGreen();
                    await Wait(4.2f);
                    await DriveTraining(Academy.TestMarker("Lesson04_Crossing"));
                    await DriveTraining(Academy.TestMarker("Lesson04_GiveWay"));
                    break;
                case "L05":
                    await DriveTraining(Academy.TestMarker("Lesson05_Start"));
                    await ReverseTraining(2.6f);
                    await Wait(.4f);
                    await DriveTraining(Academy.TestMarker("Lesson05_ParkingTarget"));
                    await Wait(2.2f);
                    break;
                case "L06":
                    await DriveTraining(Academy.TestMarker("Lesson06_Checkpoint01"));
                    await DriveTraining(Academy.VenueOrigin + new Vector3(74, 0, -68));
                    await DriveTraining(Academy.TestMarker("Lesson06_Checkpoint02"));
                    await DriveTraining(Academy.VenueOrigin + new Vector3(74, 0, 68));
                    await DriveTraining(Academy.TestMarker("Lesson06_Checkpoint03"));
                    await DriveTraining(Academy.VenueOrigin + new Vector3(-74, 0, 68));
                    await DriveTraining(Academy.TestMarker("Lesson06_Checkpoint04"));
                    break;
            }
            await Wait(.5f);
            if (!service.LessonCompleted(id)) throw new Exception(id + " did not complete");
            if (Main.Probe.ModalOpen) await Button("AcademyBackToSchool");
            await Wait(.5f);
        }

        await StartAcademyStage("TEST01");
        await DriveTraining(Academy.VenueOrigin + new Vector3(0, 0, -68));
        await DriveTraining(Academy.TestMarker("Final_Stop") + new Vector3(0, 0, -6));
        await Wait(2.4f);
        await WaitForGreen();
        await Wait(4.2f);
        await DriveTraining(Academy.TestMarker("Final_Stop") + new Vector3(0, 0, 9));
        await DriveTraining(Academy.TestMarker("Final_Crossing") + new Vector3(9, 0, 0));
        await DriveTraining(Academy.VenueOrigin);
        await DriveTraining(Academy.VenueOrigin + new Vector3(0, 0, 68));
        await DriveTraining(Academy.VenueOrigin + new Vector3(-32, 0, 68));
        await DriveTraining(Academy.TestMarker("Final_Park"));
        await Wait(2.6f);
        GD.Print($"ACADEMY TEST FINAL step={Academy.TestStep} car={Academy.TrainingCar?.GlobalPosition} " +
                 $"speed={Academy.TrainingCar?.Speed} yaw={Academy.TrainingCar?.RotationDegrees.Y} " +
                 $"modal={Main.Probe.ModalOpen} title={Main.Probe.ModalTitle}");
        if (!Main.Probe.Data.DrivingLicenceIssued)
            throw new Exception($"final academy test did not issue the licence " +
                $"(major={AchievementErrors()})");

        if (Main.Probe.ModalOpen) await Button("AcademyBackToSchool");
        await Wait(.5f);
        await Dismiss();
        Log("driving licence earned");
    }

    private async Task StartAcademyStage(string id)
    {
        if (!Main.Probe.ModalOpen)
        {
            await Use("DrivingSchoolStation");
            await Wait(.3f);
        }
        if (!Main.Probe.PressModalButton("AcademyStart_" + id))
            throw new Exception("academy start button missing for " + id);
        await Wait(.6f);
        if (!Academy.Active) throw new Exception("academy stage did not load " + id);
    }

    private string AchievementErrors() =>
        $"major={Academy.MajorErrors} minor={Academy.MinorErrors} stage={Academy.CurrentStage}";

    private async Task WaitForGreen()
    {
        for (int i = 0; i < 160 && Academy.Light != AcademyLight.Green; i++)
            await Wait(.1f);
        if (Academy.Light != AcademyLight.Green)
            throw new Exception("traffic light never turned green");
        await Wait(.2f);
    }

    private async Task DriveTraining(Vector3 point)
    {
        var car = Academy.TrainingCar ?? throw new Exception("learner car missing");
        for (int tick = 0; Horizontal(car.GlobalPosition, point) > 2.6f; tick++)
        {
            if (tick > 900) throw new Exception("learner car stuck " + point + " at " + car.GlobalPosition);
            if (tick > 0 && tick % 120 == 0)
            {
                GD.Print($"ACADEMY DRIVE recover tick={tick} car={car.GlobalPosition} " +
                         $"speed={car.Speed} target={point}");
                Stop();
                Input.ActionPress("move_back");
                await Wait(0.8f);
                Input.ActionRelease("move_back");
                Stop();
            }
            var to = point - car.GlobalPosition;
            float distance = to.Length();
            float desired = Mathf.Atan2(-to.X, -to.Z);
            float angle = Mathf.Wrap(desired - car.Rotation.Y, -Mathf.Pi, Mathf.Pi);
            Stop();
            Input.ActionPress(angle > 0 ? "move_left" : "move_right",
                Mathf.Clamp(Mathf.Abs(angle) * 2.4f, 0, 1));
            if (distance < 9f && car.Speed > 2f)
                Input.ActionPress("move_back", .7f);
            else if (Mathf.Abs(angle) < .5f)
                Input.ActionPress("move_forward", car.Speed > 7.2f ? 0 : .45f);
            else if (car.Speed > .2f) Input.ActionPress("move_back", .55f);
            else if (car.Speed < -.2f) Input.ActionPress("move_forward", .55f);
            await Wait(.06f);
            if (Main.Probe.ModalOpen || Academy.CurrentStage.Length == 0)
            {
                GD.Print($"ACADEMY DRIVE break tick={tick} modal={Main.Probe.ModalOpen} " +
                         $"title={Main.Probe.ModalTitle} stage={Academy.CurrentStage} car={car.GlobalPosition}");
                break;
            }
        }
        Stop();
        for (int i = 0; i < 60 && !Main.Probe.ModalOpen && car.Speed > .2f; i++)
        {
            Input.ActionPress("move_back", .8f);
            await Wait(.08f);
            Input.ActionRelease("move_back");
            await Wait(.04f);
        }
        Stop();
        for (int i = 0; i < 40 && Mathf.Abs(car.Speed) > .2f; i++) await Wait(.1f);
    }

    private async Task ReverseTraining(float seconds)
    {
        var car = Academy.TrainingCar ?? throw new Exception("learner car missing");
        Stop();
        Input.ActionPress("move_back");
        for (float elapsed = 0; elapsed < seconds; elapsed += .1f) await Wait(.1f);
        Input.ActionRelease("move_back");
        for (int i = 0; i < 40 && Mathf.Abs(car.Speed) > .2f; i++) await Wait(.1f);
        Stop();
    }

    private async Task Race(bool grand)
    {
        await Use(Van.FindChild("VanEntry", true, false) as Interactable ?? throw new Exception("van entry"));
        if (Van.GlobalPosition.X < 40)
        {
            // The free-roaming van can be collected beside the completed park.
            // Go around the furniture/buildings rather than aiming through them.
            if (Van.GlobalPosition.X > 0 && Van.GlobalPosition.Z > 10)
            {
                await Drive(new Vector3(22, 0, 14.5f));
                await Drive(new Vector3(22, 0, 28));
                await Drive(new Vector3(38, 0, 28));
                await Drive(new Vector3(38, 0, 0));
            }
            else await Drive(new Vector3(-30, 0, 0));
            await Drive(new Vector3(32, 0, 0));
        }
        await Drive(new Vector3(55, 0, 0));
        var points = RallyCourse.SampleClosed(10);
        for (int loop = 0; loop < 2 && !Main.Probe.Data.IsCompleted(_stage); loop++)
            foreach (var point in points)
            {
                if (Main.Probe.Data.IsCompleted(_stage)) break;
                await Drive(new Vector3(point.X, 0, point.Z));
            }
        Stop();
        await Dismiss();
        for (int i = 0; i < 60 && Mathf.Abs(Van.Speed) > .2f; i++) await Wait(.1);
        await PressKey(Key.E);
    }
    private async Task Drive(Vector3 point)
    {
        for (int tick = 0; Horizontal(Van.GlobalPosition, point) > 2.5f; tick++)
        {
            if (tick > 1200) throw new Exception("drive stuck " + point + " at " + Van.GlobalPosition);
            var to = point - Van.GlobalPosition;
            float desired = Mathf.Atan2(-to.X, -to.Z);
            float angle = Mathf.Wrap(desired - Van.Rotation.Y, -Mathf.Pi, Mathf.Pi);
            Stop();
            Input.ActionPress(angle > 0 ? "move_left" : "move_right", Mathf.Clamp(Mathf.Abs(angle) * 2.4f, 0, 1));
            if (Mathf.Abs(angle) < .5f) Input.ActionPress("move_forward", Van.Speed > 5 ? 0 : .6f);
            else if (Van.Speed > .2f) Input.ActionPress("move_back", .6f);
            else if (Van.Speed < -.2f) Input.ActionPress("move_forward", .6f);
            await Wait(.06);
            if (Main.Probe.Data.IsCompleted(_stage)) break;
        }
        Stop();
    }
    private async Task Talk(string npc, string option)
    {
        await Use("NpcTalk_" + npc);
        await Button(option);
    }
    private async Task ChapterTwo(int number)
    {
        switch (number)
        {
            case 11:
                foreach (var npc in ChapterTwoRules.CardNpcIds)
                    await Talk(npc, "GiveCard_" + npc);
                break;
            case 12:
                await Talk("ada", "BakeryHelp");
                foreach (var order in ChapterTwoRules.BakeryOrders)
                {
                    for (int i = 0; i < order.Bread; i++) await Button("BreadAdd_" + order.CustomerId);
                    for (int i = 0; i < order.Cakes; i++) await Button("CakeAdd_" + order.CustomerId);
                    await Button("Serve_" + order.CustomerId);
                }
                break;
            case 13:
                await Pickups("M13");
                await Talk("ollie", "LibraryHelp");
                foreach (var book in ChapterTwoRules.BookIds)
                { await Button("Book_" + book.ToUpperInvariant()); await Button("Shelf_" + book.ToUpperInvariant()); }
                break;
            case 14:
                for (int i = 0; i < 3; i++)
                {
                    var socket = ChapterTwoRules.SignSockets[i];
                    await Use("SignSocketStation_" + i);
                    await Button("Sign_" + socket.SignId.ToUpperInvariant());
                    await Button("Direction_" + socket.Direction.ToUpperInvariant());
                    await Button("InstallSign");
                    if (i < 2) await PressKey(Key.Escape);
                }
                break;
            case 15:
                for (int i = 0; i < 6; i++)
                {
                    await Use("GardenPadStation_" + i);
                    await Button(i % 2 == 0 ? "GardenRed" : "GardenBlue");
                    if (i < 5) await PressKey(Key.Escape);
                    if (i == 2) await Checkpoint("M15-pattern");
                }
                break;
            case 16:
                await Use("PostCounterStation");
                foreach (var npc in FinaleRules.DeliveryRecipients)
                {
                    if (Main.Probe.Data.GetMissionCounter("M16", FinaleRules.DeliveredKey(npc)) > 0) continue;
                    await Use(Van.FindChild("VanEntry", true, false) as Interactable);
                    var target = Main.FindChild("NpcTalk_" + npc, true, false) as Interactable;
                    await DriveNear(target.GlobalPosition);
                    await PressKey(Key.E);
                    await Talk(npc, "Deliver_" + npc);
                }
                break;
            case 17:
                await Use("ShopStation");
                foreach (var pair in FinaleRules.MarketRequired)
                    for (int i = 0; i < pair.Value; i++) await Button("MarketAdd_" + pair.Key);
                await Button("MarketFinalise");
                break;
            case 18:
                await Pickups("M18"); await Use("RepairStation");
                foreach (string action in new[] { "InspectCar", "Fit_tyre", "Fit_tyre", "Fit_lamp", "Fit_battery" }) await Button(action);
                break;
            case 20:
                foreach (var npc in FinaleRules.FestivalInvitees)
                    if (Main.Probe.Data.GetMissionCounter("M20", FinaleRules.InvitedKey(npc)) == 0) await Talk(npc, "FestivalInvite");
                await Checkpoint("M20-invitations");
                for (int i = 0; i < 2; i++) if (Main.Probe.Data.GetMissionCounter("M20", FinaleRules.TableKey(i)) == 0) await Use("FestivalTableStation_" + i);
                for (int i = 0; i < 3; i++) if (Main.Probe.Data.GetMissionCounter("M20", FinaleRules.LanternKey(i)) == 0) await Use("FestivalLanternStation_" + i);
                break;
            case 21:
                await Pickups("M21"); await Use("RoadworksStation");
                await Button("RoadworksTotal_6");
                foreach (var kind in RoadworksRules.CorrectOrder) await Button("RoadworksOrder_" + kind);
                await Button("RoadworksCheck");
                break;
        }
    }
    private async Task DriveNear(Vector3 target)
    {
        // Use the town's main street before approaching a neighbour; every movement
        // is produced by the same throttle/brake/steering actions as a human driver.
        await Drive(new Vector3(Van.GlobalPosition.X, 0, 0));
        Vector3 parking = new(target.X, 0, 0);
        if (Horizontal(parking, target) > 8.5f)
        {
            parking = RoadNetwork.Roads.Skip(1)
                .Select(road => new Vector3(road.X, 0,
                    Mathf.Clamp(target.Z, road.Z - road.HalfZ + 1.2f, road.Z + road.HalfZ - 1.2f)))
                .Where(point => Horizontal(point, target) < 8.5f)
                .OrderBy(point => Horizontal(point, target)).First();
        }
        await Drive(new Vector3(parking.X, 0, 0));
        await Drive(parking);
        Stop();
        while (Mathf.Abs(Van.Speed) > .2f) await Wait(.1);
    }
    private async Task City(int number)
    {
        string id = "C0" + number; _stage = id;
        if (Main.Probe.Data.CompletedCityMissions.Contains(id)) return;
        if (number == 1 && Player.GlobalPosition.X < 180)
        {
            await Walk(new Vector3(36, 0, 0));
            for (int x = 60; x <= 200; x += 20) await Walk(new Vector3(x, 0, 0));
        }
        if (Main.Probe.CityMissions.ActiveMission?.Id != id)
        {
            await Use("CityJobBoardStation");
            await Button("CityStart_" + id);
        }
        if (number == 1)
        {
            if (!Main.Probe.CityMissions.HasCourierParcels) await Use("CityDepotStation");
            for (int i = 1; i <= 3; i++)
                if (Main.Probe.CityMissions.Progress(id, "stop_" + i) == 0) await Use("CityStopStation_" + i);
        }
        else if (number == 2)
        {
            while (Main.Probe.CityMissions.C02Crates < 6)
            {
                int remaining = Enumerable.Range(0, 6).First(i => Main.Probe.CityMissions.Progress(id, "crate_" + i) == 0);
                await Use("CityCrateStation_" + remaining);
            }
            await Use("CityStallStation"); await Button("MarketAnswer_6");
        }
        else
        {
            var colors = new[] { "red", "amber", "green" };
            for (int i = 1; i <= 3; i++)
                if (Main.Probe.CityMissions.Progress(id, "cabinet_" + i) == 0)
                { await Use("CityCabinetStation_" + i); await Button("Cabinet_" + colors[i - 1]); }
            await Use("CityEngineerStation");
        }
        if (!Main.Probe.Data.CompletedCityMissions.Contains(id))
        { await Use("CityJobBoardStation"); await Button("CityFinish"); }
        if (!Main.Probe.Data.CompletedCityMissions.Contains(id)) throw new Exception("city mission incomplete");
        Log("COMPLETED coins=" + Main.Probe.Coins); await Snap(id); await Dismiss();
    }
    private sealed class WalkthroughRestartException : Exception { }
    private async Task Checkpoint(string name)
    {
        string marker = ProjectSettings.GlobalizePath("user://walkthrough-" + name + ".checkpoint");
        if (File.Exists(marker)) return;
        await PressKey(Key.Escape);
        await Button("Save now");
        if (!Hud.ModalTitleText.Contains("Saved")) throw new Exception("checkpoint save did not succeed");
        File.WriteAllText(marker, name);
        await Snap(name + "-saved");
        throw new WalkthroughRestartException();
    }
    private async Task Snap(string name)
    {
        if (DisplayServer.GetName() == "headless" || OS.GetCmdlineUserArgs().Contains("--no-captures")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string folder = ProjectSettings.GlobalizePath("user://walkthrough"); Directory.CreateDirectory(folder);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, name + ".png"));
    }
}
