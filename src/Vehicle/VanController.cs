using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Simple arcade delivery van. A CharacterBody3D with stable floor tracking and
/// no rigid-body instability: accelerate/brake/reverse, steer, reset.
/// </summary>
public partial class VanController : CharacterBody3D
{
    public const float MaxForwardSpeed = 11.5f;
    public const float MaxReverseSpeed = 4.5f;

    public bool Driven { get; private set; }
    public bool InputEnabled { get; private set; } = true;
    public float Speed { get; private set; }
    public event Action Crashed;

    private Node3D _visual;
    private Node3D _driverFigure;
    private readonly List<Node3D> _wheelPivots = new();
    private Interactable _entry;
    private Vector3 _parkingPosition;
    private float _parkingYaw;
    private float _wheelAngle;
    private float _steerInput;
    private float _bodyBob;
    private bool _resetting;
    private AudioStreamPlayer3D _engineIdle;
    private AudioStreamPlayer3D _engineDrive;
    private bool _autopilot;
    private IReadOnlyList<RallyCourse.Point> _autopilotPath;
    private int _autopilotIndex;

    public bool Autopilot => _autopilot;

    public bool IsParkedNear(Vector3 position) =>
        GlobalPosition.DistanceSquaredTo(position) <= 100f && Mathf.Abs(Speed) < 0.8f;

    public void StartRallyAutopilot()
    {
        _autopilotPath = RallyCourse.SampleClosed(10);
        _autopilotIndex = 1 % System.Math.Max(1, _autopilotPath.Count);
        _autopilot = true;
    }

    /// <summary>Test-only route autopilot used for the city traversal regression.</summary>
    public void StartRouteAutopilot(IReadOnlyList<Vector3> points)
    {
        if (points == null || points.Count < 2) return;
        var path = new List<RallyCourse.Point>(points.Count);
        foreach (var point in points) path.Add(new RallyCourse.Point(point.X, point.Z));
        _autopilotPath = path;
        _autopilotIndex = 1 % path.Count;
        _autopilot = true;
    }

    public void StopAutopilot() => _autopilot = false;

    public void Initialize(Vector3 parkingPosition, float parkingYaw, Hud hud)
    {
        Name = "Van";
        _parkingPosition = parkingPosition;
        _parkingYaw = parkingYaw;
        CollisionLayer = 4;        // layer 3
        CollisionMask = 1;         // world only; the player owns player-vs-van
        FloorSnapLength = 0.7f;
        FloorMaxAngle = Mathf.DegToRad(42f);
        SafeMargin = 0.04f;
        UpDirection = Vector3.Up;
        Position = parkingPosition;

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.9f, 1.9f, 3.6f) },
            Position = new Vector3(0, 1.0f, 0),
        });

        BuildVisual();
        SetDriven(false);

        _engineIdle = AudioManager.CreateVehicleLoop(this, "engine_idle_loop", 6f, 34f, -20f);
        _engineDrive = AudioManager.CreateVehicleLoop(this, "engine_drive_loop", 6f, 34f, -40f);
        if (_engineIdle != null) _engineIdle.Position = new Vector3(0, 0.55f, 0.9f);
        if (_engineDrive != null) _engineDrive.Position = new Vector3(0, 0.55f, 0.9f);

        _entry = Interactable.Create(this, new Vector3(0, 0.1f, 0),
            "E: Drive the van", () => EnterRequested?.Invoke(),
            () => !Driven, true, 4.2f, "VanEntry");
        _entry.SetPrompt(() => Driven ? "E: Exit the van" : "E: Drive the delivery van");
    }

    public event Action EnterRequested;

    public bool HasEngineAudio => _engineIdle != null && _engineDrive != null;
    public bool EngineIdlePlaying => _engineIdle?.Playing == true;
    public bool EngineDrivePlaying => _engineDrive?.Playing == true;

    public void StopEngineAudio()
    {
        _engineIdle?.Stop();
        _engineDrive?.Stop();
    }

    public string VisualTemplatePath { get; private set; } = "";

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "VanVisual" };
        AddChild(_visual);
        Node3D template = VisualTemplate.Instantiate(_visual, "Visual", VisualTemplate.VanPath);
        VisualTemplatePath = VisualTemplate.VanPath;
        VisualTemplate.ApplyVertexColours(template, VisualTemplate.VertexColourMaterial());
        _wheelPivots.Clear();
        _wheelPivots.AddRange(VisualTemplate.BuildWheelPivots(template));

        // The authored van has no face normals, so its large flat panels
        // self-shadow into a moire pattern. Let the body receive shadows but
        // not cast them, and ground it with one soft contact shadow that
        // covers the whole footprint.
        VisualTemplate.DisableSelfShadow(template);
        var groundShadow = Geo.Disc(this, new Vector3(0, 0.02f, 0), 1.0f,
            new Color(0.06f, 0.10f, 0.16f), 0.18f);
        groundShadow.Name = "GroundShadow";
        groundShadow.Scale = new Vector3(1.18f, 1f, 1.78f);
        groundShadow.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // Postal identity sits flush on the rear cargo door and both sides.
        AddPostalMark(_visual, -1.105f, 1.3f, 0.25f, -1);
        AddPostalMark(_visual, 1.105f, 1.3f, 0.25f, 1);
        Geo.Label(_visual, new Vector3(0, 1.05f, 1.83f), "PATCHWORK POST", 42, Palette.Navy,
            false, 0.0055f, 6, null, "VanRearText");

        // The authored van has a closed cabin, so the driver figure is kept as
        // an optional hidden node rather than rebuilt from boxes.
        _driverFigure = new Node3D { Name = "DriverFigure", Position = new Vector3(-0.42f, 1.5f, -1.0f) };
        _visual.AddChild(_driverFigure);
        _driverFigure.Visible = false;

        // A small friendly antenna flag so the van reads from a distance. It
        // must not throw a detached blob shadow beside the van.
        var antenna = Geo.Cylinder(_visual, new Vector3(0.78f, 2.12f, 0.68f), 0.03f, 0.03f, 0.6f,
            Palette.Navy, 5);
        antenna.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var antennaFlag = Geo.Box(_visual, new Vector3(0.78f, 2.42f, 0.68f),
            new Vector3(0.42f, 0.26f, 0.04f), Palette.Red);
        antennaFlag.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static void AddPostalMark(Node parent, float x, float y, float z, int side)
    {
        // A cream side panel with a navy border, envelope flap, stamp and the
        // word POST, so the mark reads as postal identity rather than a window.
        var panel = Geo.Box(parent, new Vector3(x, y, z),
            new Vector3(0.04f, 0.52f, 0.86f), Palette.Cream);
        panel.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        float face = x + side * 0.03f;
        Color navy = Palette.Navy;
        var top = Geo.Box(parent, new Vector3(face, y + 0.24f, z),
            new Vector3(0.02f, 0.04f, 0.86f), navy);
        var bottom = Geo.Box(parent, new Vector3(face, y - 0.24f, z),
            new Vector3(0.02f, 0.04f, 0.86f), navy);
        var front = Geo.Box(parent, new Vector3(face, y, z - 0.41f),
            new Vector3(0.02f, 0.52f, 0.04f), navy);
        var back = Geo.Box(parent, new Vector3(face, y, z + 0.41f),
            new Vector3(0.02f, 0.52f, 0.04f), navy);
        top.CastShadow = bottom.CastShadow = front.CastShadow = back.CastShadow =
            GeometryInstance3D.ShadowCastingSetting.Off;

        var leftFlap = Geo.Box(parent, new Vector3(face + side * 0.01f, y + 0.10f, z - 0.20f),
            new Vector3(0.02f, 0.035f, 0.45f), navy, new Vector3(26.6f, 0, 0));
        var rightFlap = Geo.Box(parent, new Vector3(face + side * 0.01f, y + 0.10f, z + 0.20f),
            new Vector3(0.02f, 0.035f, 0.45f), navy, new Vector3(-26.6f, 0, 0));
        leftFlap.CastShadow = rightFlap.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        var stamp = Geo.Box(parent, new Vector3(face + side * 0.01f, y + 0.15f, z + 0.28f),
            new Vector3(0.02f, 0.12f, 0.12f), Palette.Red);
        stamp.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        Geo.Label(parent, new Vector3(face + side * 0.025f, y - 0.16f, z), "POST", 24, navy,
            false, 0.004f, 3, new Vector3(0, side * 90f, 0), "VanPostText");
    }

    public void SetDriven(bool driven)
    {
        Driven = driven;
        if (_driverFigure != null) _driverFigure.Visible = driven;
        if (_entry != null) _entry.Visible = !driven;
        if (!driven)
        {
            Speed = 0;
            _engineIdle?.Stop();
            _engineDrive?.Stop();
        }
        else
        {
            if (_engineIdle != null && !_engineIdle.Playing) _engineIdle.Play();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        InputEnabled = enabled;
        if (!enabled) _steerInput = 0f;
    }

    public void ResetToParking()
    {
        GlobalPosition = _parkingPosition;
        Rotation = new Vector3(0, _parkingYaw, 0);
        Speed = 0;
        Velocity = Vector3.Zero;
    }

    /// <summary>Non-shipping physics test seam used by the city runtime selftest.</summary>
    internal void TestLaunch(float speed)
    {
        Speed = speed;
        Velocity = new Vector3(0, Velocity.Y, 0);
    }

    public List<Vector3> GetExitCandidates()
    {
        return new List<Vector3>
        {
            GlobalPosition + GlobalTransform.Basis.X * -2.6f,
            GlobalPosition + GlobalTransform.Basis.X * 2.6f,
            GlobalPosition - GlobalTransform.Basis.Z * 3.2f,
            GlobalPosition + GlobalTransform.Basis.Z * 3.6f,
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!Driven)
        {
            // Settle on the ground while parked.
            Vector3 parkedVelocity = Velocity;
            if (!IsOnFloor()) parkedVelocity.Y -= 23f * dt;
            else parkedVelocity.Y = -0.2f;
            parkedVelocity.X = 0;
            parkedVelocity.Z = 0;
            Velocity = parkedVelocity;
            MoveAndSlide();
            return;
        }

        float throttle = InputEnabled ? Input.GetAxis("move_back", "move_forward") : 0f;
        float steerAxis = InputEnabled ? Input.GetAxis("move_left", "move_right") : 0f;
        if (_autopilot)
            ComputeAutopilot(out throttle, out steerAxis);
        _steerInput = Mathf.MoveToward(_steerInput, steerAxis, dt * 4.2f);

        if (throttle > 0.05f)
        {
            if (Speed < -0.2f)
                Speed += 11f * dt; // brake the reverse motion first
            else
                Speed += 6.2f * throttle * dt;
        }
        else if (throttle < -0.05f)
        {
            if (Speed > 0.2f)
                Speed -= 11f * dt; // brake first
            else
                Speed += 4.6f * throttle * dt; // then reverse slowly
        }
        else
        {
            Speed = Mathf.MoveToward(Speed, 0f, 3.4f * dt);
        }

        Speed = Mathf.Clamp(Speed, -MaxReverseSpeed, MaxForwardSpeed);
        if (Mathf.Abs(Speed) < 0.04f) Speed = 0f;

        if (Mathf.Abs(Speed) > 0.3f)
        {
            float turnRate = 1.18f * Mathf.Clamp(Mathf.Abs(Speed) / 5f, 0.18f, 1f);
            Rotation = new Vector3(0, Rotation.Y - _steerInput * turnRate * dt * Mathf.Sign(Speed), 0);
        }
        else if (Mathf.Abs(throttle) < 0.05f && !_autopilot)
        {
            // Friendly toy-like pivot: children can line the van up before driving.
            Rotation = new Vector3(0, Rotation.Y - steerAxis * 1.25f * dt, 0);
        }

        Vector3 forward = -GlobalTransform.Basis.Z;
        Vector3 vertical = new Vector3(0, IsOnFloor() ? -0.3f : Velocity.Y - 23f * dt, 0);
        Velocity = forward * Speed + vertical;
        MoveAndSlide();

        if (IsOnWall() && Mathf.Abs(Speed) > 1f)
            Speed = Mathf.MoveToward(Speed, 0f, 18f * dt);

        if (GlobalPosition.Y < -1.5f && !_resetting)
        {
            _resetting = true;
            ResetToParking();
            Crashed?.Invoke();
            _resetting = false;
        }

        _wheelAngle -= Speed * dt / 0.43f;
        foreach (var pivot in _wheelPivots)
            pivot.Rotation = new Vector3(_wheelAngle, 0, 0);
        UpdateEngineAudio();
        _bodyBob = Mathf.Sin(Time.GetTicksMsec() / 130f) * Mathf.Clamp(Mathf.Abs(Speed) / MaxForwardSpeed, 0f, 1f) * 0.015f;
        _visual.Position = new Vector3(0, _bodyBob, 0);
        _visual.Rotation = new Vector3(0, 0, -_steerInput * Mathf.Clamp(Mathf.Abs(Speed) / MaxForwardSpeed, 0f, 1f) * 0.035f);
    }

    private void ComputeAutopilot(out float throttle, out float steerAxis)
    {
        throttle = 0f;
        steerAxis = 0f;
        if (_autopilotPath == null || _autopilotPath.Count < 2) return;
        var targetPoint = _autopilotPath[_autopilotIndex];
        Vector3 target = new(targetPoint.X, GlobalPosition.Y, targetPoint.Z);
        Vector3 to = target - GlobalPosition;
        to.Y = 0;
        if (to.Length() < 3.6f)
        {
            _autopilotIndex = (_autopilotIndex + 1) % _autopilotPath.Count;
            targetPoint = _autopilotPath[_autopilotIndex];
            target = new Vector3(targetPoint.X, GlobalPosition.Y, targetPoint.Z);
            to = target - GlobalPosition;
            to.Y = 0;
        }
        if (to.LengthSquared() < 0.001f) return;
        float desiredYaw = Mathf.Atan2(-to.X, -to.Z);
        float angle = Mathf.Wrap(desiredYaw - Rotation.Y, -Mathf.Pi, Mathf.Pi);
        steerAxis = Mathf.Clamp(-angle * 2.4f, -1f, 1f);
        float absAngle = Mathf.Abs(angle);
        if (Speed > 7.4f) throttle = -0.15f;
        else if (absAngle > 1.15f) throttle = Speed < 1.2f ? 0.35f : 0.05f;
        else if (absAngle > 0.55f) throttle = 0.42f;
        else throttle = 0.9f;
        // Keep the autopilot test out of the river if it is ever pushed off.
        if (GlobalPosition.X > 40.5f && GlobalPosition.X < 49.5f && GlobalPosition.Y < 0.2f)
            throttle = 0f;
    }

    private void UpdateEngineAudio()
    {
        float ratio = Mathf.Clamp(Mathf.Abs(Speed) / MaxForwardSpeed, 0f, 1f);
        if (_engineIdle != null)
        {
            if (!_engineIdle.Playing) _engineIdle.Play();
            _engineIdle.VolumeDb = Mathf.Lerp(-24f, -17f, ratio);
            _engineIdle.PitchScale = 0.92f + ratio * 0.18f;
        }
        if (_engineDrive != null)
        {
            if (ratio > 0.035f)
            {
                if (!_engineDrive.Playing) _engineDrive.Play();
                _engineDrive.VolumeDb = Mathf.Lerp(-30f, -11f, ratio);
                _engineDrive.PitchScale = 0.78f + ratio * 0.85f;
            }
            else
            {
                _engineDrive.VolumeDb = -40f;
            }
        }
    }
}
