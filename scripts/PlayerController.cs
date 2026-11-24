using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Godot;

namespace Asteria;

/// <summary>
/// Temporary first-person controller for testing movement and direct block interaction.
/// This is not the final inventory, tool, mining-time, or item-use implementation.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    private const float MoveSpeed = 4.3f;
    private const float JumpSpeed = 5.0f;
    private const float GroundAcceleration = 28.0f;
    private const float AirAcceleration = 8.0f;
    private const float MouseSensitivity = 0.0022f;
    private const float MaximumPitch = 1.55334306f;
    private const double InteractionReach = 5.0;
    private const float PlayerHalfWidth = 0.35f;
    private const float PlayerHalfHeight = 0.9f;

    private Camera3D _camera = null!;
    private OverworldWorld? _world;
    private Action<BlockPos>? _onBlockChanged;
    private float _pitch;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void ConfigureWorld(
        OverworldWorld world,
        Action<BlockPos> onBlockChanged)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(onBlockChanged);

        if (_world is not null)
            throw new InvalidOperationException("Player world interaction is already configured.");

        _world = world;
        _onBlockChanged = onBlockChanged;
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

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                return;
            }

            if (mouseButton.ButtonIndex == MouseButton.Left)
                TryBreakTargetBlock();
            else if (mouseButton.ButtonIndex == MouseButton.Right)
                TryPlaceTargetBlock();

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

    private BlockHitResult? TraceBlock()
    {
        if (_world is null)
            return null;

        Vector3 cameraForward = -_camera.GlobalTransform.Basis.Z;
        Vec3 from = new(
            _camera.GlobalPosition.X,
            _camera.GlobalPosition.Y,
            _camera.GlobalPosition.Z);
        Vec3 direction = new(
            cameraForward.X,
            cameraForward.Y,
            cameraForward.Z);
        Vec3 to = from.Add(direction.Scale(InteractionReach));

        BlockGetter getter = _world;
        BlockHitResult result = getter.Clip(new ClipContext(
            from,
            to,
            ClipContext.Block.Outline,
            ClipContext.Fluid.None));

        return result.GetHitType() == HitResult.Type.BLOCK
            ? result
            : null;
    }

    private void TryBreakTargetBlock()
    {
        if (_world is null || TraceBlock() is not { } hit)
            return;

        BlockPos position = hit.GetBlockPos();
        BlockState state = _world.GetBlockState(position);
        if (state.IsAir || state.GetDestroySpeed(_world, position) < 0.0f)
            return;

        _world.SetBlockState(position, Blocks.AIR.DefaultState);
        _onBlockChanged?.Invoke(position);
    }

    private void TryPlaceTargetBlock()
    {
        if (_world is null || TraceBlock() is not { } hit)
            return;

        BlockPos position = hit.GetBlockPos().Relative(hit.GetDirection());
        if (position.GetY() < _world.MinY ||
            position.GetY() >= _world.MinY + _world.Height ||
            WouldOverlapPlayer(position))
        {
            return;
        }

        BlockState targetState = _world.GetBlockState(position);
        if (!targetState.IsAir && !targetState.CanBeReplaced())
            return;

        _world.SetBlockState(position, Blocks.DIRT.DefaultState);
        _onBlockChanged?.Invoke(position);
    }

    private bool WouldOverlapPlayer(BlockPos position)
    {
        Vector3 center = GlobalPosition;
        float minX = center.X - PlayerHalfWidth;
        float maxX = center.X + PlayerHalfWidth;
        float minY = center.Y - PlayerHalfHeight;
        float maxY = center.Y + PlayerHalfHeight;
        float minZ = center.Z - PlayerHalfWidth;
        float maxZ = center.Z + PlayerHalfWidth;

        return position.GetX() < maxX &&
               position.GetX() + 1 > minX &&
               position.GetY() < maxY &&
               position.GetY() + 1 > minY &&
               position.GetZ() < maxZ &&
               position.GetZ() + 1 > minZ;
    }
}
