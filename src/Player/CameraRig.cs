using System;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Smooth third-person camera with optional right-drag orbit and wheel zoom.
/// It follows position automatically; mouse input is never required to play.
/// The camera pulls in when world geometry would occlude the player.
/// </summary>
public partial class CameraRig : Camera3D
{
    public Node3D Target { get; private set; }

    private float _yaw;
    internal float OrbitYaw => _yaw;
    private float _pitch = 27f;
    private float _distance = 7.4f;
    private float _targetDistance = 7.4f;
    private float _lookHeight = 1.35f;
    private bool _autoYaw;
    private float _dragSuppress;
    private bool _indoor;
    private Vector3 _smoothedPivot;
    private PhysicsRayQueryParameters3D _collisionQuery;

    public override void _Ready()
    {
        _collisionQuery = new PhysicsRayQueryParameters3D { CollisionMask = 1u | 16u };
        Current = true;
        Fov = 66;
        Near = 0.15f;
        Far = 300f;
    }

    public void SetTarget(Node3D target, bool autoYaw, float distance, float pitch, float lookHeight, bool snap = true)
    {
        Target = target;
        var exclude = new Godot.Collections.Array<Rid>();
        if (target is CollisionObject3D body) exclude.Add(body.GetRid());
        _collisionQuery.Exclude = exclude;
        _collisionQuery.CollisionMask = 1u | 16u;
        _autoYaw = autoYaw;
        _targetDistance = distance;
        _distance = distance;
        _pitch = pitch;
        _lookHeight = lookHeight;
        if (snap && target != null)
        {
            _smoothedPivot = target.GlobalPosition + Vector3.Up * lookHeight;
            if (autoYaw)
                _yaw = target.Rotation.Y;
            ApplyCamera(0f, true);
        }
    }

    public void SetIndoor(bool indoor) => _indoor = indoor;

    /// <summary>Developer / screenshot helper. Gameplay never needs this.</summary>
    public void SetView(float yawDegrees, float pitch, float distance)
    {
        _yaw = Mathf.DegToRad(yawDegrees);
        _pitch = pitch;
        _distance = distance;
        _targetDistance = distance;
        Snap();
    }

    public void Snap()
    {
        if (Target == null) return;
        _smoothedPivot = Target.GlobalPosition + Vector3.Up * _lookHeight;
        ApplyCamera(0f, true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - 0.7f, 3.6f, 12f);
                GetViewport().SetInputAsHandled();
            }
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance + 0.7f, 3.6f, 12f);
                GetViewport().SetInputAsHandled();
            }
            else if (button.ButtonIndex == MouseButton.Right)
            {
                _dragSuppress = button.Pressed ? 0.9f : 0f;
            }
        }
        else if (@event is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            _yaw -= motion.Relative.X * 0.0055f;
            _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * 0.0045f, 11f, 48f);
            _dragSuppress = 2.2f;
        }
    }

    public override void _Process(double delta)
    {
        if (Target == null) return;
        float dt = (float)delta;
        _dragSuppress = Mathf.Max(0f, _dragSuppress - dt);

        if (_autoYaw && _dragSuppress <= 0f && Target != null)
        {
            _yaw = Mathf.LerpAngle(_yaw, Target.Rotation.Y, 1f - Mathf.Exp(-dt * 2.6f));
        }

        float desiredDistance = _indoor ? Mathf.Min(_targetDistance, 4.2f) : _targetDistance;
        _distance = Mathf.Lerp(_distance, desiredDistance, 1f - Mathf.Exp(-dt * 7f));
        ApplyCamera(dt, false);
    }

    public override void _ExitTree()
    {
        _collisionQuery?.Dispose();
        _collisionQuery = null;
    }

    private void ApplyCamera(float dt, bool instant)
    {
        if (Target == null) return;
        Vector3 pivot = Target.GlobalPosition + Vector3.Up * _lookHeight;
        _smoothedPivot = instant ? pivot : _smoothedPivot.Lerp(pivot, 1f - Mathf.Exp(-dt * 10f));

        float pitch = _indoor ? Mathf.Min(_pitch, 22f) : _pitch;
        float horiz = Mathf.Cos(Mathf.DegToRad(pitch)) * _distance;
        float vert = Mathf.Sin(Mathf.DegToRad(pitch)) * _distance;
        Vector3 offset = new Vector3(Mathf.Sin(_yaw) * horiz, vert, Mathf.Cos(_yaw) * horiz);
        Vector3 desired = _smoothedPivot + offset;

        // Camera collision: pull toward the pivot if a wall is in the way.
        _collisionQuery.From = _smoothedPivot;
        _collisionQuery.To = desired;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(_collisionQuery);
        if (hit.Count > 0)
        {
            Vector3 pos = (Vector3)hit["position"];
            Vector3 dir = (pos - _smoothedPivot).Normalized();
            float dist = _smoothedPivot.DistanceTo(pos) - 0.25f;
            if (dist < 2.0f) dist = 2.0f;
            desired = _smoothedPivot + dir * dist;
        }

        GlobalPosition = instant ? desired : GlobalPosition.Lerp(desired, 1f - Mathf.Exp(-dt * 11f));
        if (GlobalPosition.DistanceTo(_smoothedPivot) > 0.15f)
            LookAt(_smoothedPivot + Vector3.Up * 0.15f, Vector3.Up);
    }
}
