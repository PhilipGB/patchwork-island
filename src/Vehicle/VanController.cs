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

        _entry = Interactable.Create(this, new Vector3(0, 0.1f, 0),
            "E: Drive the van", () => EnterRequested?.Invoke(),
            () => !Driven, true, 4.2f, "VanEntry");
        _entry.SetPrompt(() => Driven ? "E: Exit the van" : "E: Drive the delivery van");
    }

    public event Action EnterRequested;

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "VanVisual" };
        AddChild(_visual);

        // Chassis and boxy cargo body.
        Geo.Box(_visual, new Vector3(0, 0.68f, 0), new Vector3(1.9f, 0.6f, 3.5f), Color.FromHtml("E8B92F"));
        Geo.Box(_visual, new Vector3(0, 1.25f, 0.45f), new Vector3(1.86f, 0.95f, 2.25f), Palette.Yellow);
        Geo.Box(_visual, new Vector3(0, 1.35f, -1.15f), new Vector3(1.78f, 1.1f, 1.05f), Palette.Yellow);
        Geo.Box(_visual, new Vector3(0, 2.0f, 0.2f), new Vector3(1.8f, 0.16f, 3.15f), Color.FromHtml("D9A521"));
        Geo.Box(_visual, new Vector3(0, 0.45f, -1.78f), new Vector3(2.0f, 0.25f, 0.24f), Palette.Navy);
        Geo.Box(_visual, new Vector3(0, 0.45f, 1.78f), new Vector3(2.0f, 0.25f, 0.24f), Palette.Navy);
        Geo.Box(_visual, new Vector3(0, 0.76f, -1.82f), new Vector3(1.95f, 0.35f, 0.14f), Palette.Navy);

        // Windscreen and side windows.
        Geo.Box(_visual, new Vector3(0, 1.48f, -1.68f), new Vector3(1.55f, 0.74f, 0.08f), Color.FromHtml("8FD7FF"));
        Geo.Box(_visual, new Vector3(0, 1.48f, -1.63f), new Vector3(0.08f, 0.8f, 0.12f), Palette.Cream);
        Geo.Box(_visual, new Vector3(-0.92f, 1.42f, -1.05f), new Vector3(0.05f, 0.62f, 0.7f), Color.FromHtml("8FD7FF"));
        Geo.Box(_visual, new Vector3(0.92f, 1.42f, -1.05f), new Vector3(0.05f, 0.62f, 0.7f), Color.FromHtml("8FD7FF"));
        // Headlights and rear lamps.
        Geo.Box(_visual, new Vector3(-0.65f, 0.98f, -1.8f), new Vector3(0.38f, 0.24f, 0.08f), Color.FromHtml("FFF3C9"));
        Geo.Box(_visual, new Vector3(0.65f, 0.98f, -1.8f), new Vector3(0.38f, 0.24f, 0.08f), Color.FromHtml("FFF3C9"));
        Geo.Box(_visual, new Vector3(-0.7f, 1.0f, 1.79f), new Vector3(0.3f, 0.22f, 0.06f), Palette.Red);
        Geo.Box(_visual, new Vector3(0.7f, 1.0f, 1.79f), new Vector3(0.3f, 0.22f, 0.06f), Palette.Red);

        // White envelope mark on both flanks.
        AddEnvelopeMark(_visual, -0.936f, 1.3f, 0.25f, -1);
        AddEnvelopeMark(_visual, 0.936f, 1.3f, 0.25f, 1);
        Geo.Label(_visual, new Vector3(0, 1.55f, 1.82f), "PATCHWORK POST", 34, Palette.Navy,
            false, 0.004f, 0, null, "VanRearText");

        // Wheels as axle pivots so they can spin visually.
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float front in new[] { -1f, 1f })
            {
                var pivot = new Node3D
                {
                    Name = "WheelPivot",
                    Position = new Vector3(side * 0.94f, 0.46f, front * 1.05f),
                };
                _visual.AddChild(pivot);
                Geo.Cylinder(pivot, Vector3.Zero, 0.43f, 0.43f, 0.28f, Color.FromHtml("343A46"), 12,
                    new Vector3(0, 0, 90));
                Geo.Cylinder(pivot, new Vector3(side * 0.15f, 0, 0), 0.18f, 0.18f, 0.06f,
                    Color.FromHtml("AEB6C2"), 10, new Vector3(0, 0, 90));
                _wheelPivots.Add(pivot);
            }
        }

        // Occupant silhouette visible while driving.
        _driverFigure = new Node3D { Name = "DriverFigure", Position = new Vector3(-0.42f, 1.5f, -1.0f) };
        _visual.AddChild(_driverFigure);
        Geo.Box(_driverFigure, Vector3.Zero, new Vector3(0.42f, 0.4f, 0.36f), Palette.Skin);
        Geo.Box(_driverFigure, new Vector3(0, 0.26f, 0), new Vector3(0.5f, 0.16f, 0.44f), Palette.Red);
        Geo.Box(_driverFigure, new Vector3(0, 0.18f, -0.26f), new Vector3(0.5f, 0.08f, 0.22f), Palette.Red);
        _driverFigure.Visible = false;

        // A small friendly antenna flag so the van reads from a distance.
        Geo.Cylinder(_visual, new Vector3(0.74f, 2.25f, 0.7f), 0.03f, 0.03f, 0.6f, Palette.Navy, 5);
        Geo.Box(_visual, new Vector3(0.74f, 2.55f, 0.7f), new Vector3(0.42f, 0.26f, 0.04f), Palette.Red);
    }

    private static void AddEnvelopeMark(Node parent, float x, float y, float z, int side)
    {
        Geo.Box(parent, new Vector3(x, y, z), new Vector3(0.04f, 0.55f, 0.9f), Palette.Cream);
        Geo.Box(parent, new Vector3(x + side * 0.02f, y + 0.08f, z), new Vector3(0.02f, 0.02f, 0.72f), Palette.Navy);
        Geo.Box(parent, new Vector3(x + side * 0.02f, y + 0.08f, z), new Vector3(0.02f, 0.32f, 0.02f), Palette.Navy);
        Geo.Box(parent, new Vector3(x + side * 0.02f, y - 0.1f, z - 0.35f), new Vector3(0.02f, 0.02f, 0.35f), Palette.Navy,
            new Vector3(0, 26, 0));
        Geo.Box(parent, new Vector3(x + side * 0.02f, y - 0.1f, z + 0.35f), new Vector3(0.02f, 0.02f, 0.35f), Palette.Navy,
            new Vector3(0, -26, 0));
    }

    public void SetDriven(bool driven)
    {
        Driven = driven;
        if (_driverFigure != null) _driverFigure.Visible = driven;
        if (_entry != null) _entry.Visible = !driven;
        if (!driven) Speed = 0;
    }

    public void ResetToParking()
    {
        GlobalPosition = _parkingPosition;
        Rotation = new Vector3(0, _parkingYaw, 0);
        Speed = 0;
        Velocity = Vector3.Zero;
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

        float throttle = Input.GetAxis("move_back", "move_forward");
        float steerAxis = Input.GetAxis("move_left", "move_right");
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
        else if (Mathf.Abs(throttle) < 0.05f)
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
        _bodyBob = Mathf.Sin(Time.GetTicksMsec() / 130f) * Mathf.Clamp(Mathf.Abs(Speed) / MaxForwardSpeed, 0f, 1f) * 0.015f;
        _visual.Position = new Vector3(0, _bodyBob, 0);
        _visual.Rotation = new Vector3(0, 0, -_steerInput * Mathf.Clamp(Mathf.Abs(Speed) / MaxForwardSpeed, 0f, 1f) * 0.035f);
    }
}
