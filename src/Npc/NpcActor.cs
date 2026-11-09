using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// One named villager: a small procedural paper-doll-style body with a readable
/// name, portrait marker, idle/walk animation, verified patrol and a generous E
/// interaction. ProcessMode is Pausable and collision layer is 0 so villagers
/// can never push the player, van or traffic.
/// </summary>
public partial class NpcActor : CharacterBody3D
{
    public NpcDefinition Definition { get; private set; }
    public bool Talking { get; private set; }
    public string NpcId => Definition?.Id ?? "";
    public event Action<NpcActor> TalkRequested;

    private Node3D _model;
    private Node3D _leftLeg;
    private Node3D _rightLeg;
    private Node3D _leftArm;
    private Node3D _rightArm;
    private Node3D _head;
    private Interactable _talk;
    private int _patrolIndex;
    private float _wait;
    private float _stuck;
    private float _animTime;
    private float _yaw;
    private float _wave;
    private Vector3 _lastPosition;
    private Label3D _nameLabel;

    public void Initialize(NpcDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Name = $"Npc_{definition.Id}";
        Position = new Vector3(definition.Home.X, 0.05f, definition.Home.Z);
        CollisionLayer = 0;   // never blocks the player or vehicles
        CollisionMask = 1;    // still rests on world geometry
        FloorSnapLength = 0.4f;
        FloorMaxAngle = Mathf.DegToRad(48f);
        SafeMargin = 0.03f;
        UpDirection = Vector3.Up;
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public override void _Ready()
    {
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.55f },
            Position = new Vector3(0, 0.82f, 0),
        });
        BuildModel();
        BuildMarker();
        _talk = Interactable.Create(this, Vector3.Zero,
            $"E: Talk to {Definition.Name} — {Definition.Role}",
            () => TalkRequested?.Invoke(this), null, true, 3.3f, $"NpcTalk_{Definition.Id}");
        _talk.SetPrompt(() => $"E: Talk to {Definition.Name} — {Definition.Role}");
        _patrolIndex = 1 % Mathf.Max(1, Definition.PatrolPoints.Count);
        _lastPosition = GlobalPosition;
    }

    public override string ToString() => $"NpcActor({NpcId})";

    private void BuildModel()
    {
        _model = new Node3D { Name = "NpcModel", Scale = new Vector3(0.94f, 0.94f, 0.94f) };
        AddChild(_model);
        Color main = Color.FromHtml(Definition.MainHex);
        Color hair = Color.FromHtml(Definition.HairHex);
        Color accent = Color.FromHtml(Definition.AccentHex);
        Color secondary = main.Darkened(0.18f);
        Color shoes = Color.FromHtml("4A3B33");

        if (Definition.Dress)
        {
            Geo.Box(_model, new Vector3(0, 1.08f, 0), new Vector3(0.62f, 0.46f, 0.4f), main);
            Geo.Cylinder(_model, new Vector3(0, 0.79f, 0), 0.32f, 0.48f, 0.52f, main, 10,
                null, false, "Dress");
            Geo.Cylinder(_model, new Vector3(0, 0.55f, 0), 0.485f, 0.485f, 0.06f, accent, 10,
                null, false, "Hem");
            Geo.Box(_model, new Vector3(0, 1.26f, 0), new Vector3(0.66f, 0.09f, 0.44f), accent);
        }
        else
        {
            Geo.Box(_model, new Vector3(0, 1.06f, 0), new Vector3(0.7f, 0.55f, 0.44f), main);
            Geo.Box(_model, new Vector3(0, 0.75f, 0), new Vector3(0.62f, 0.3f, 0.42f), secondary);
            Geo.Box(_model, new Vector3(0, 1.27f, 0), new Vector3(0.76f, 0.1f, 0.48f), accent);
        }

        _head = new Node3D { Name = "Head", Position = new Vector3(0, 1.6f, 0) };
        _model.AddChild(_head);
        Geo.Box(_head, Vector3.Zero, new Vector3(0.58f, 0.53f, 0.5f), Palette.Skin);
        Geo.Box(_head, new Vector3(0, 0.2f, 0.02f), new Vector3(0.6f, 0.18f, 0.54f), hair,
            null, false, "HairTop");
        Geo.Box(_head, new Vector3(0, 0.12f, 0.25f), new Vector3(0.52f, 0.3f, 0.12f), hair,
            null, false, "HairBack");
        if (Definition.LongHair)
        {
            Geo.Box(_head, new Vector3(-0.31f, -0.16f, 0.03f), new Vector3(0.1f, 0.66f, 0.42f), hair,
                null, false, "LongHairL");
            Geo.Box(_head, new Vector3(0.31f, -0.16f, 0.03f), new Vector3(0.1f, 0.66f, 0.42f), hair,
                null, false, "LongHairR");
            Geo.Box(_head, new Vector3(0, -0.24f, 0.27f), new Vector3(0.54f, 0.4f, 0.18f), hair,
                null, false, "LongHairB");
        }
        Geo.Box(_head, new Vector3(-0.13f, 0.02f, -0.26f), new Vector3(0.07f, 0.09f, 0.03f), Palette.Navy);
        Geo.Box(_head, new Vector3(0.13f, 0.02f, -0.26f), new Vector3(0.07f, 0.09f, 0.03f), Palette.Navy);
        Geo.Box(_head, new Vector3(0, -0.12f, -0.27f), new Vector3(0.17f, 0.03f, 0.02f), Palette.Navy);
        Geo.Box(_head, new Vector3(-0.21f, -0.03f, -0.26f), new Vector3(0.07f, 0.035f, 0.02f),
            new Color("E99A86"));
        Geo.Box(_head, new Vector3(0.21f, -0.03f, -0.26f), new Vector3(0.07f, 0.035f, 0.02f),
            new Color("E99A86"));

        _leftArm = new Node3D { Name = "LeftArm", Position = new Vector3(-0.42f, 1.26f, 0) };
        _rightArm = new Node3D { Name = "RightArm", Position = new Vector3(0.42f, 1.26f, 0) };
        _model.AddChild(_leftArm);
        _model.AddChild(_rightArm);
        BuildArm(_leftArm, main, accent);
        BuildArm(_rightArm, main, accent);

        _leftLeg = new Node3D { Name = "LeftLeg", Position = new Vector3(-0.17f, 0.64f, 0) };
        _rightLeg = new Node3D { Name = "RightLeg", Position = new Vector3(0.17f, 0.64f, 0) };
        _model.AddChild(_leftLeg);
        _model.AddChild(_rightLeg);
        Color legColor = Definition.Dress ? Palette.Skin : secondary;
        BuildLeg(_leftLeg, legColor, shoes);
        BuildLeg(_rightLeg, legColor, shoes);
        BuildRoleAccessory(main, accent, hair);
    }

    private void BuildRoleAccessory(Color main, Color accent, Color hair)
    {
        switch (Definition.Id)
        {
            case "milo":
                Geo.Box(_model, new Vector3(0, 1.02f, -0.24f), new Vector3(0.58f, 0.55f, 0.08f),
                    accent);
                Geo.Box(_head, new Vector3(0, 0.34f, 0), new Vector3(0.64f, 0.13f, 0.56f),
                    main.Darkened(0.12f));
                Geo.Box(_head, new Vector3(0, 0.3f, -0.3f), new Vector3(0.5f, 0.06f, 0.24f),
                    main.Darkened(0.2f));
                break;
            case "ada":
                Geo.Box(_model, new Vector3(0, 1.02f, -0.24f), new Vector3(0.5f, 0.5f, 0.07f),
                    Palette.Cream);
                Geo.Cylinder(_head, new Vector3(0, 0.38f, 0), 0.27f, 0.31f, 0.2f,
                    Palette.White, 10, null, false, "BakerHat");
                Geo.Cylinder(_head, new Vector3(0, 0.28f, 0), 0.34f, 0.34f, 0.06f,
                    Palette.Cream, 10);
                break;
            case "pip":
                Geo.Box(_head, new Vector3(0, 0.35f, 0), new Vector3(0.64f, 0.14f, 0.56f),
                    accent);
                Geo.Box(_head, new Vector3(0, 0.31f, 0.3f), new Vector3(0.52f, 0.06f, 0.2f),
                    accent.Darkened(0.18f));
                Geo.Box(_rightArm, new Vector3(0, -0.62f, -0.08f), new Vector3(0.08f, 0.34f, 0.08f),
                    Palette.StoneDark, new Vector3(20, 0, 0));
                break;
            case "fern":
                Geo.Cylinder(_head, new Vector3(0.26f, 0.22f, -0.02f), 0.05f, 0.05f, 0.24f,
                    Palette.GrassDark, 5);
                Geo.Sphere(_head, new Vector3(0.26f, 0.36f, -0.02f), 0.11f, Palette.Pink, 6, 3);
                Geo.Box(_leftArm, new Vector3(0, -0.64f, 0), new Vector3(0.2f, 0.22f, 0.2f),
                    Palette.Grass, new Vector3(0, 0, -12));
                break;
            case "ollie":
                Geo.Box(_head, new Vector3(-0.13f, 0.02f, -0.29f), new Vector3(0.16f, 0.12f, 0.03f),
                    Palette.Stone);
                Geo.Box(_head, new Vector3(0.13f, 0.02f, -0.29f), new Vector3(0.16f, 0.12f, 0.03f),
                    Palette.Stone);
                Geo.Box(_rightArm, new Vector3(0, -0.58f, -0.06f), new Vector3(0.28f, 0.22f, 0.06f),
                    Palette.Blue, new Vector3(-12, 0, 0));
                break;
            case "ivy":
                Geo.Box(_model, new Vector3(0, 1.27f, 0), new Vector3(0.72f, 0.12f, 0.46f), accent);
                Geo.Box(_head, new Vector3(0.25f, 0.16f, 0.01f), new Vector3(0.16f, 0.16f, 0.08f),
                    accent);
                Geo.Box(_leftArm, new Vector3(0, -0.6f, -0.04f), new Vector3(0.26f, 0.3f, 0.04f),
                    Palette.Cream);
                break;
        }
    }

    private static void BuildArm(Node3D arm, Color main, Color accent)
    {
        Geo.Box(arm, new Vector3(0, -0.18f, 0), new Vector3(0.18f, 0.34f, 0.19f), main);
        Geo.Box(arm, new Vector3(0, -0.36f, 0), new Vector3(0.19f, 0.05f, 0.2f), accent);
        Geo.Box(arm, new Vector3(0, -0.46f, 0), new Vector3(0.15f, 0.19f, 0.16f), Palette.Skin);
        Geo.Sphere(arm, new Vector3(0, -0.58f, 0), 0.09f, Palette.Skin, 6, 3);
    }

    private static void BuildLeg(Node3D leg, Color color, Color shoes)
    {
        Geo.Box(leg, new Vector3(0, -0.27f, 0), new Vector3(0.2f, 0.52f, 0.23f), color);
        Geo.Box(leg, new Vector3(0, -0.55f, -0.05f), new Vector3(0.24f, 0.12f, 0.35f), shoes);
    }

    private void BuildMarker()
    {
        var portrait = new Sprite3D
        {
            Name = "Portrait",
            Texture = NpcPortrait.Load(Definition.Id),
            PixelSize = 0.0034f,
            Position = new Vector3(0, 2.3f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false,
            DoubleSided = true,
        };
        AddChild(portrait);
        _nameLabel = Geo.Label(this, new Vector3(0, 2.92f, 0), Definition.Name, 68, Palette.Navy,
            true, 0.0058f, 10);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (Definition == null) return;
        if (Talking)
        {
            Velocity = new Vector3(0, IsOnFloor() ? -0.2f : Velocity.Y - 23f * dt, 0);
            MoveAndSlide();
            Animate(dt, 0f);
            return;
        }

        _wave = Mathf.Max(0f, _wave - dt);
        if (_wait > 0f)
        {
            _wait -= dt;
            Velocity = new Vector3(0, IsOnFloor() ? -0.2f : Velocity.Y - 23f * dt, 0);
            MoveAndSlide();
            Animate(dt, 0f);
            return;
        }

        var target = Definition.PatrolPoints[_patrolIndex];
        Vector3 targetPos = new(target.X, GlobalPosition.Y, target.Z);
        Vector3 to = targetPos - GlobalPosition;
        to.Y = 0;
        float distance = to.Length();
        if (distance < 0.28f)
        {
            AdvancePatrol();
            return;
        }

        Vector3 direction = to / Mathf.Max(0.001f, distance);
        float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-dt * 7f));
        if (_model != null) _model.Rotation = new Vector3(0, _yaw, 0);

        float speed = Definition.WalkSpeed;
        float angle = Mathf.Abs(Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi));
        if (angle > 1.1f) speed *= 0.25f;
        Vector3 velocity = direction * speed;
        velocity.Y = IsOnFloor() ? -0.2f : Velocity.Y - 23f * dt;
        Velocity = velocity;
        MoveAndSlide();

        if (_wait <= 0f && GlobalPosition.DistanceTo(_lastPosition) < 0.006f)
        {
            _stuck += dt;
            if (_stuck > 1.8f)
            {
                _stuck = 0f;
                AdvancePatrol();
            }
        }
        else
        {
            _stuck = 0f;
        }
        _lastPosition = GlobalPosition;
        Animate(dt, speed);
    }

    private void AdvancePatrol()
    {
        _patrolIndex = (_patrolIndex + 1) % Mathf.Max(1, Definition.PatrolPoints.Count);
        _wait = 0.7f + (_patrolIndex % 3) * 0.35f;
        Velocity = Vector3.Zero;
    }

    private void Animate(float dt, float speed)
    {
        if (_model == null) return;
        float speedFactor = Mathf.Clamp(speed / Mathf.Max(0.1f, Definition.WalkSpeed), 0f, 1f);
        _animTime += dt * (2.2f + speedFactor * 7f);
        float swing = Mathf.Sin(_animTime) * (0.08f + speedFactor * 0.55f);
        if (speedFactor > 0.05f)
        {
            _leftLeg.Rotation = new Vector3(swing, 0, 0);
            _rightLeg.Rotation = new Vector3(-swing, 0, 0);
            _leftArm.Rotation = new Vector3(-swing * 0.85f, 0, 0.05f);
            _rightArm.Rotation = new Vector3(swing * 0.85f, 0, -0.05f);
            _model.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_animTime * 2f)) * 0.025f, 0);
        }
        else
        {
            float breathe = Mathf.Sin(_animTime * 0.8f) * 0.012f;
            _leftLeg.Rotation = _leftLeg.Rotation.Lerp(Vector3.Zero, 1f - Mathf.Exp(-dt * 10f));
            _rightLeg.Rotation = _rightLeg.Rotation.Lerp(Vector3.Zero, 1f - Mathf.Exp(-dt * 10f));
            _leftArm.Rotation = new Vector3(0, 0, 0.06f + breathe);
            _rightArm.Rotation = new Vector3(0, 0, -0.06f - breathe);
            _model.Position = new Vector3(0, breathe, 0);
        }
        if (_wave > 0f)
        {
            _rightArm.Rotation = new Vector3(0, 0, -1.9f + Mathf.Sin(Time.GetTicksMsec() / 90f) * 0.35f);
        }
        _head.Rotation = new Vector3(0, Mathf.Sin(_animTime * 0.4f) * 0.08f, 0);
    }

    public void SetTalking(bool talking)
    {
        Talking = talking;
        if (talking)
        {
            Velocity = Vector3.Zero;
            _wait = 0.6f;
        }
    }

    public void FaceToward(Vector3 position)
    {
        Vector3 to = position - GlobalPosition;
        to.Y = 0;
        if (to.LengthSquared() < 0.001f) return;
        _yaw = Mathf.Atan2(-to.X, -to.Z);
        if (_model != null) _model.Rotation = new Vector3(0, _yaw, 0);
    }

    public void Wave(float seconds = 2.2f) => _wave = Mathf.Max(_wave, seconds);

    public string NameLabelText => _nameLabel?.Text ?? "";
    public bool IsMoving => !Talking && new Vector2(Velocity.X, Velocity.Z).Length() > 0.1f;
}
