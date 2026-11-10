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
        Color hair = Color.FromHtml("6E462B");
        Color darkHair = hair.Darkened(0.14f);

        _model = new Node3D { Name = "Model", Rotation = new Vector3(0, _yaw, 0) };
        _model.Scale = new Vector3(0.92f, 0.92f, 0.92f);
        AddChild(_model);

        if (_wearsDress)
            BuildDressBody(main, secondary, accent);
        else
            BuildShirtBody(main, secondary, accent);

        // Head and friendly face.
        _head = new Node3D { Name = "Head", Position = new Vector3(0, 1.61f, 0) };
        _model.AddChild(_head);

        if (_longHair)
        {
            // Long hair silhouette and side locks, behind the face.
            Geo.Box(_head, new Vector3(0, -0.18f, 0.3f), new Vector3(0.6f, 0.84f, 0.18f), hair,
                null, false, "LongHairBack");
            Geo.Box(_head, new Vector3(-0.32f, -0.16f, 0.02f), new Vector3(0.09f, 0.56f, 0.4f), darkHair,
                null, false, "LongHairLeft");
            Geo.Box(_head, new Vector3(0.32f, -0.16f, 0.02f), new Vector3(0.09f, 0.56f, 0.4f), darkHair,
                null, false, "LongHairRight");
            Geo.Box(_head, new Vector3(0, 0.16f, 0.04f), new Vector3(0.6f, 0.12f, 0.44f), hair,
                null, false, "LongHairTop");
        }
        else
        {
            // Short, tidy hair with a small front fringe.
            Geo.Box(_head, new Vector3(0, 0.17f, 0.03f), new Vector3(0.58f, 0.14f, 0.44f), hair,
                null, false, "ShortHairTop");
            Geo.Box(_head, new Vector3(0, 0.1f, 0.24f), new Vector3(0.5f, 0.2f, 0.12f), darkHair,
                null, false, "ShortHairBack");
            Geo.Box(_head, new Vector3(0, 0.24f, -0.25f), new Vector3(0.46f, 0.1f, 0.07f), hair,
                null, false, "ShortHairFringe");
        }

        Geo.Box(_head, Vector3.Zero, new Vector3(0.6f, 0.55f, 0.52f), Palette.Skin);
        Geo.Box(_head, new Vector3(0, -0.24f, 0.05f), new Vector3(0.2f, 0.14f, 0.2f), Palette.Skin);
        Geo.Box(_head, new Vector3(-0.14f, 0.05f, -0.27f), new Vector3(0.07f, 0.1f, 0.035f), Palette.Navy);
        Geo.Box(_head, new Vector3(0.14f, 0.05f, -0.27f), new Vector3(0.07f, 0.1f, 0.035f), Palette.Navy);
        Geo.Box(_head, new Vector3(0, -0.1f, -0.28f), new Vector3(0.18f, 0.035f, 0.025f), Palette.Navy);
        Geo.Box(_head, new Vector3(-0.24f, -0.05f, -0.27f), new Vector3(0.08f, 0.04f, 0.02f), new Color("E99A86"));
        Geo.Box(_head, new Vector3(0.24f, -0.05f, -0.27f), new Vector3(0.08f, 0.04f, 0.02f), new Color("E99A86"));

        if (_longHair)
        {
            // A cheerful bow, matching the sketch's flower/bow accessory.
            var bow = new Node3D { Name = "HairBow", Position = new Vector3(0.28f, 0.26f, -0.02f) };
            _head.AddChild(bow);
            Geo.Box(bow, new Vector3(-0.09f, 0, 0), new Vector3(0.16f, 0.12f, 0.07f), accent,
                new Vector3(0, 0, 22));
            Geo.Box(bow, new Vector3(0.09f, 0, 0), new Vector3(0.16f, 0.12f, 0.07f), accent,
                new Vector3(0, 0, -22));
            Geo.Box(bow, new Vector3(0, 0, 0.035f), new Vector3(0.08f, 0.08f, 0.06f), accent.Darkened(0.18f));
        }

        // Cap (recoloured by the M01 reward).
        _capTop = Geo.Cylinder(_head, new Vector3(0, 0.29f, 0), 0.32f, 0.32f, 0.16f, Palette.Blue, 12);
        _capBrim = Geo.Box(_head, new Vector3(0, 0.235f, -0.31f), new Vector3(0.5f, 0.06f, 0.3f), Palette.Blue);
        Geo.Box(_head, new Vector3(0, 0.39f, 0.2f), new Vector3(0.14f, 0.06f, 0.14f), Palette.Cream);

        // Arms with pivots at the shoulders.
        _leftArm = new Node3D { Position = new Vector3(-0.44f, 1.28f, 0) };
        _model.AddChild(_leftArm);
        BuildArm(_leftArm, main, accent);
        _rightArm = new Node3D { Position = new Vector3(0.44f, 1.28f, 0) };
        _model.AddChild(_rightArm);
        BuildArm(_rightArm, main, accent);

        // Legs and simple shoes.
        _leftLeg = new Node3D { Position = new Vector3(-0.18f, 0.64f, 0) };
        _model.AddChild(_leftLeg);
        BuildLeg(_leftLeg, secondary, shoes, _wearsDress);
        _rightLeg = new Node3D { Position = new Vector3(0.18f, 0.64f, 0) };
        _model.AddChild(_rightLeg);
        BuildLeg(_rightLeg, secondary, shoes, _wearsDress);

        ApplyCapColor();
    }

    private void BuildShirtBody(Color main, Color secondary, Color accent)
    {
        // Shirt, shorts and a small sunny pocket.
        Geo.Box(_model, new Vector3(0, 1.06f, 0), new Vector3(0.72f, 0.58f, 0.46f), main);
        Geo.Box(_model, new Vector3(0, 1.3f, 0), new Vector3(0.78f, 0.12f, 0.5f), accent);
        Geo.Box(_model, new Vector3(0, 0.74f, 0), new Vector3(0.64f, 0.32f, 0.44f), secondary);
        Geo.Box(_model, new Vector3(0, 1.04f, 0.25f), new Vector3(0.3f, 0.22f, 0.06f), accent);
        Geo.Box(_model, new Vector3(0, 0.51f, 0), new Vector3(0.66f, 0.08f, 0.46f), accent);
    }

    private void BuildDressBody(Color main, Color secondary, Color accent)
    {
        // Bodice and a low-poly flared skirt: unmistakably a dress while
        // keeping the same friendly proportions as the boy character.
        Geo.Box(_model, new Vector3(0, 1.09f, 0), new Vector3(0.64f, 0.5f, 0.42f), main);
        Geo.Box(_model, new Vector3(0, 1.27f, 0), new Vector3(0.68f, 0.1f, 0.46f), accent);
        Geo.Box(_model, new Vector3(0, 1.02f, 0.23f), new Vector3(0.14f, 0.12f, 0.06f), accent);
        Geo.Cylinder(_model, new Vector3(0, 0.79f, 0), 0.34f, 0.52f, 0.56f, main, 10,
            null, false, "DressSkirt");
        Geo.Cylinder(_model, new Vector3(0, 0.535f, 0), 0.525f, 0.525f, 0.07f, accent, 10,
            null, false, "DressHem");
        // A little waist ribbon ties the dress to the selected outfit.
        Geo.Box(_model, new Vector3(0, 0.98f, 0), new Vector3(0.68f, 0.08f, 0.46f), secondary);
    }

    private static void BuildArm(Node3D arm, Color main, Color accent)
    {
        Geo.Box(arm, new Vector3(0, -0.18f, 0), new Vector3(0.19f, 0.36f, 0.2f), main);
        Geo.Box(arm, new Vector3(0, -0.37f, 0), new Vector3(0.2f, 0.05f, 0.21f), accent);
        Geo.Box(arm, new Vector3(0, -0.46f, 0), new Vector3(0.16f, 0.2f, 0.17f), Palette.Skin);
        Geo.Sphere(arm, new Vector3(0, -0.58f, 0), 0.1f, Palette.Skin, 6, 3);
    }

    private static void BuildLeg(Node3D leg, Color secondary, Color shoes, bool dress)
    {
        Color legColor = dress ? Palette.Skin : secondary;
        Geo.Box(leg, new Vector3(0, -0.28f, 0), new Vector3(0.21f, 0.54f, 0.24f), legColor);
        Geo.Box(leg, new Vector3(0, -0.56f, -0.06f), new Vector3(0.25f, 0.13f, 0.37f), shoes);
        Geo.Box(leg, new Vector3(0, -0.61f, -0.06f), new Vector3(0.26f, 0.04f, 0.39f), shoes.Darkened(0.2f));
    }

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
