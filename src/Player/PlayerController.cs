using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Third-person character with forgiving jump, camera-relative movement and
/// generous proximity interaction (no aiming or crosshair required).
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    public const float WalkSpeed = 6.1f;
    public const float JumpSpeed = 7.5f;
    public const float Gravity = 23f;

    public CameraRig Rig;
    public bool ControlEnabled = true;
    public event Action<Interactable> TargetChanged;
    public Interactable NearestInteractable { get; private set; }

    private Node3D _model;
    private Node3D _leftLeg, _rightLeg, _leftArm, _rightArm, _head;
    private MeshInstance3D _capTop, _capBrim;
    private Color _capColor = new(-1, -1, -1);
    private string _preset = CharacterCustomisation.DefaultPreset;
    private string _outfitId = CharacterCustomisation.DefaultOutfit;
    private bool _longHair;
    private bool _wearsDress;
    private int _appearanceRevision;
    private float _yaw;

    public string AppearancePreset => _preset;
    public string AppearanceOutfit => _outfitId;
    public bool HasLongHair => _longHair;
    public bool WearsDress => _wearsDress;
    public int AppearanceRevision => _appearanceRevision;

    /// <summary>Current model facing, saved so a reload faces the same way.</summary>
    public float FacingYaw => _yaw;
    private float _animTime;
    private float _scanTimer;
    private float _jumpBuffer;
    private float _coyote;
    private bool _wasOnFloor = true;
    private float _stepDistance;
    private int _stepSide;
    private bool _audioOnFloor = true;

    public override void _Ready()
    {
        Name = "Player";
        CollisionLayer = 2;
        CollisionMask = 1 | 4; // world + van
        FloorSnapLength = 0.45f;
        FloorMaxAngle = Mathf.DegToRad(48f);
        SafeMargin = 0.03f;
        UpDirection = Vector3.Up;

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.5f },
            Position = new Vector3(0, 0.82f, 0),
        });

        BuildModel();
        SetCapColor(Palette.Blue);
    }

    private void BuildModel()
    {
        if (_model != null && GodotObject.IsInstanceValid(_model))
        {
            RemoveChild(_model);
            _model.QueueFree();
        }

        _longHair = _preset == CharacterCustomisation.Girl;
        _wearsDress = _longHair;
        _appearanceRevision++;

        var style = CharacterCustomisation.StyleFor(_preset, _outfitId);
        Color main = Color.FromHtml(style.MainHex);
        Color secondary = Color.FromHtml(style.SecondaryHex);
        Color accent = Color.FromHtml(style.AccentHex);
        Color shoes = Color.FromHtml(style.ShoeHex);

        string templatePath = _wearsDress
            ? VisualTemplate.PlayerGirlPath
            : VisualTemplate.PlayerBoyPath;
        _model = new Node3D
        {
            Name = "Model",
            Rotation = new Vector3(0, _yaw, 0),
            Scale = new Vector3(0.92f, 0.92f, 0.92f),
        };
        AddChild(_model);
        Node3D template = VisualTemplate.Instantiate(_model, "Visual", templatePath);
        VisualTemplatePath = templatePath;

        var tints = new Dictionary<int, Color>
        {
            [0] = main, [1] = main, [2] = main, [3] = main,
        };
        if (_wearsDress)
        {
            tints[4] = main;
            tints[5] = secondary;
            tints[6] = secondary;
            tints[7] = shoes;
            tints[8] = shoes;
            tints[15] = accent;
            tints[16] = accent;
            tints[17] = accent;
        }
        else
        {
            tints[4] = secondary;
            tints[5] = secondary;
            tints[6] = shoes;
            tints[7] = shoes;
            tints[12] = accent;
        }

        CharacterRig rig = VisualTemplate.BuildCharacterRig(template, tints);
        _head = rig.Head;
        _leftArm = rig.LeftArm;
        _rightArm = rig.RightArm;
        _leftLeg = rig.LeftLeg;
        _rightLeg = rig.RightLeg;

        if (_wearsDress)
        {
            _capTop = null;
            _capBrim = null;
        }
        else
        {
            // The authored boy model has no cap, so keep the M01 reward as a
            // small overlay that still reads from the default camera.
            _capTop = Geo.Cylinder(_head, new Vector3(0, 0.39f, 0),
                0.32f, 0.32f, 0.16f, Palette.Blue, 12);
            _capBrim = Geo.Box(_head, new Vector3(0, 0.335f, -0.3f),
                new Vector3(0.5f, 0.06f, 0.3f), Palette.Blue);
            Geo.Box(_head, new Vector3(0, 0.48f, 0.2f),
                new Vector3(0.14f, 0.06f, 0.14f), Palette.Cream);
        }

        BuildHair();
        ApplyCapColor();
    }

    private void BuildHair()
    {
        // The authored head is a plain block from behind. Add a short hair
        // shell so the back and sides read as hair from the chase camera.
        Color hair = Color.FromHtml(_wearsDress ? "6B4226" : "3E2A1C");
        Geo.Box(_head, new Vector3(0, 0.05f, 0.295f),
            new Vector3(0.6f, 0.62f, 0.1f), hair, null, false, "HairBack");
        Geo.Box(_head, new Vector3(-0.29f, 0.04f, 0.03f),
            new Vector3(0.1f, 0.56f, 0.48f), hair, null, false, "HairLeft");
        Geo.Box(_head, new Vector3(0.29f, 0.04f, 0.03f),
            new Vector3(0.1f, 0.56f, 0.48f), hair, null, false, "HairRight");
        Geo.Box(_head, new Vector3(0, 0.33f, -0.01f),
            new Vector3(0.62f, 0.12f, 0.54f), hair, null, false, "HairFringe");
        if (_wearsDress)
            Geo.Box(_head, new Vector3(0, -0.30f, 0.31f),
                new Vector3(0.52f, 0.30f, 0.12f), hair, null, false, "HairBob");
    }

    public string VisualTemplatePath { get; private set; } = "";

    public void SetAppearance(string preset, string outfitId)
    {
        string normalisedPreset = CharacterCustomisation.IsValidPreset(preset)
            ? preset
            : CharacterCustomisation.DefaultPreset;
        string normalisedOutfit = CharacterCustomisation.Find(outfitId) != null
            ? outfitId
            : CharacterCustomisation.DefaultOutfit;

        if (_model != null && normalisedPreset == _preset && normalisedOutfit == _outfitId)
            return;

        _preset = normalisedPreset;
        _outfitId = normalisedOutfit;
        BuildModel();
    }

    public void SetCapColor(Color color)
    {
        _capColor = color;
        ApplyCapColor();
    }

    private void ApplyCapColor()
    {
        if (_capTop == null || _capBrim == null) return;
        Color color = _capColor.R < 0 ? Palette.Blue : _capColor;
        _capTop.MaterialOverride = Geo.Mat(color);
        _capBrim.MaterialOverride = Geo.Mat(color.Darkened(0.08f));
    }

    public void SetControlEnabled(bool enabled)
    {
        ControlEnabled = enabled;
        if (!enabled)
        {
            _jumpBuffer = 0;
            SetNearest(null);
        }
    }

    public void Teleport(Vector3 position, float? yaw = null)
    {
        GlobalPosition = position;
        Velocity = Vector3.Zero;
        if (yaw.HasValue)
        {
            _yaw = yaw.Value;
            if (_model != null)
                _model.Rotation = new Vector3(0, _yaw, 0);
        }
        if (Rig != null)
        {
            Rig.SetTarget(this, false, 8.2f, 18f, 1.55f, true);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        bool onFloor = IsOnFloor();
        if (onFloor) _coyote = 0.13f;
        else _coyote = Mathf.Max(0f, _coyote - dt);
        _jumpBuffer = Mathf.Max(0f, _jumpBuffer - dt);

        Vector2 input = Vector2.Zero;
        if (ControlEnabled)
        {
            input = new Vector2(
                Input.GetAxis("move_left", "move_right"),
                Input.GetAxis("move_back", "move_forward"));
            if (input.Length() > 1f) input = input.Normalized();
            if (Input.IsActionJustPressed("jump"))
                _jumpBuffer = 0.16f;
        }

        Vector3 camForward = -Rig.GlobalTransform.Basis.Z;
        camForward.Y = 0;
        if (camForward.LengthSquared() < 0.01f) camForward = Vector3.Forward;
        camForward = camForward.Normalized();
        Vector3 camRight = Rig.GlobalTransform.Basis.X;
        camRight.Y = 0;
        camRight = camRight.Normalized();

        Vector3 desired = (camRight * input.X + camForward * input.Y) * WalkSpeed;
        Vector3 horizontal = new Vector3(Velocity.X, 0, Velocity.Z);
        float accel = onFloor ? 38f : 14f;
        horizontal = horizontal.MoveToward(desired, accel * dt);

        float vertical = Velocity.Y;
        if (onFloor && vertical < 0) vertical = -0.4f;
        else vertical -= Gravity * dt;
        if (vertical < -32f) vertical = -32f;

        if (_jumpBuffer > 0f && _coyote > 0f && ControlEnabled)
        {
            vertical = JumpSpeed;
            _jumpBuffer = 0f;
            _coyote = 0f;
            AudioManager.PlayWorld("jump", -7f);
        }

        Velocity = new Vector3(horizontal.X, vertical, horizontal.Z);
        MoveAndSlide();

        float planarSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        TickFootsteps(dt, planarSpeed);
        if (planarSpeed > 0.2f)
        {
            float targetYaw = Mathf.Atan2(-Velocity.X, -Velocity.Z);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-dt * 13f));
        }
        if (_model != null)
            _model.Rotation = new Vector3(0, _yaw, 0);

        AnimateModel(dt, planarSpeed, IsOnFloor());

        if (ControlEnabled)
        {
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.1f;
                ScanInteractables();
            }
            if (NearestInteractable != null && Input.IsActionJustPressed("interact"))
            {
                var target = NearestInteractable;
                target.Interact();
                SetNearest(null);
                _scanTimer = 0.05f;
            }
        }

        _wasOnFloor = IsOnFloor();
    }

    private void TickFootsteps(float dt, float planarSpeed)
    {
        bool onFloor = IsOnFloor();
        if (onFloor && !_audioOnFloor)
            AudioManager.PlayWorld("land", -6f);
        _audioOnFloor = onFloor;
        if (!onFloor || planarSpeed < 1.2f)
            return;

        _stepDistance += planarSpeed * dt;
        if (_stepDistance < 2.0f)
            return;
        _stepDistance = 0f;
        bool road = SurfaceRules.IsRoad(GlobalPosition);
        string cue = (road ? "footstep_road_" : "footstep_grass_") + (_stepSide == 0 ? "01" : "02");
        _stepSide ^= 1;
        AudioManager.PlayWorld(cue, -13f, (float)GD.RandRange(0.96f, 1.04f));
    }

    private void AnimateModel(float dt, float speed, bool onFloor)
    {
        if (_model == null) return;
        float speedFactor = Mathf.Clamp(speed / WalkSpeed, 0f, 1f);
        _animTime += dt * (2.5f + speedFactor * 9f);
        float swing = Mathf.Sin(_animTime) * (0.12f + speedFactor * 0.65f);
        if (!onFloor)
        {
            _leftLeg.Rotation = new Vector3(0.35f, 0, 0);
            _rightLeg.Rotation = new Vector3(-0.2f, 0, 0);
            _leftArm.Rotation = new Vector3(-0.8f, 0, 0.18f);
            _rightArm.Rotation = new Vector3(-0.8f, 0, -0.18f);
            _model.Position = new Vector3(0, 0, 0);
        }
        else if (speedFactor > 0.05f)
        {
            _leftLeg.Rotation = new Vector3(swing, 0, 0);
            _rightLeg.Rotation = new Vector3(-swing, 0, 0);
            _leftArm.Rotation = new Vector3(-swing * 0.9f, 0, 0.06f);
            _rightArm.Rotation = new Vector3(swing * 0.9f, 0, -0.06f);
            _model.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_animTime * 2f)) * 0.035f, 0);
        }
        else
        {
            float breathe = Mathf.Sin(_animTime * 0.7f) * 0.018f;
            _leftLeg.Rotation = _leftLeg.Rotation.Lerp(Vector3.Zero, 1f - Mathf.Exp(-dt * 12f));
            _rightLeg.Rotation = _rightLeg.Rotation.Lerp(Vector3.Zero, 1f - Mathf.Exp(-dt * 12f));
            _leftArm.Rotation = new Vector3(0, 0, 0.06f + breathe);
            _rightArm.Rotation = new Vector3(0, 0, -0.06f - breathe);
            _model.Position = new Vector3(0, breathe, 0);
        }
        _head.Rotation = new Vector3(0, 0, Mathf.Sin(_animTime * 0.35f) * 0.03f);
    }

    private void ScanInteractables()
    {
        Interactable best = null;
        float bestDistance = float.MaxValue;
        var nodes = GetTree().GetNodesInGroup("interactable");
        foreach (var node in nodes)
        {
            if (node is not Interactable interactable || !GodotObject.IsInstanceValid(interactable))
                continue;
            if (!interactable.IsAvailable()) continue;
            float distance = GlobalPosition.DistanceTo(interactable.FocusPosition);
            if (distance <= interactable.Range && distance < bestDistance)
            {
                best = interactable;
                bestDistance = distance;
            }
        }
        SetNearest(best);
    }

    private void SetNearest(Interactable target)
    {
        if (NearestInteractable == target) return;
        if (NearestInteractable != null && GodotObject.IsInstanceValid(NearestInteractable))
            NearestInteractable.TargetHighlight(false);
        NearestInteractable = target;
        if (target != null)
            target.TargetHighlight(true);
        TargetChanged?.Invoke(target);
    }

    public void ForceRefreshInteraction()
    {
        SetNearest(null);
        _scanTimer = 0f;
    }
}
