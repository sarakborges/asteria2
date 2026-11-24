using Godot;

namespace Asteria;

/// <summary>
/// Temporary first-person controller for testing the generated solid-terrain region.
/// It is not the final player, input, or movement implementation.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    private const float MoveSpeed = 4.3f;
    private const float JumpSpeed = 5.0f;
    private const float GroundAcceleration = 28.0f;
    private const float AirAcceleration = 8.0f;
    private const float MouseSensitivity = 0.0022f;
    private const float MaximumPitch = 1.55334306f;

    private Camera3D _camera = null!;
    private float _pitch;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key &&
            key.Pressed &&
            !key.Echo &&
            (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            return;
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return;
        }

        if (@event is not InputEventMouseMotion mouseMotion ||
            Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            return;
        }

        RotateY(-mouseMotion.Relative.X * MouseSensitivity);
        _pitch = Mathf.Clamp(
            _pitch - mouseMotion.Relative.Y * MouseSensitivity,
            -MaximumPitch,
            MaximumPitch);
        _camera.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaSeconds = (float)delta;
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
            velocity += GetGravity() * deltaSeconds;
        else if (velocity.Y < 0.0f)
            velocity.Y = 0.0f;

        if (Input.IsActionJustPressed("jump") && IsOnFloor())
            velocity.Y = JumpSpeed;

        Vector2 input = Input.GetVector(
            "move_left",
            "move_right",
            "move_forward",
            "move_back");
        Vector3 direction = (
            Transform.Basis * new Vector3(input.X, 0.0f, input.Y)).Normalized();

        float acceleration = IsOnFloor()
            ? GroundAcceleration
            : AirAcceleration;
        velocity.X = Mathf.MoveToward(
            velocity.X,
            direction.X * MoveSpeed,
            acceleration * deltaSeconds);
        velocity.Z = Mathf.MoveToward(
            velocity.Z,
            direction.Z * MoveSpeed,
            acceleration * deltaSeconds);

        Velocity = velocity;
        MoveAndSlide();
    }
}
