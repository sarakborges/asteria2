using Asteria.Content.Blocks;
using Asteria.Core;
using Godot;

namespace Asteria.Gameplay;

/// <summary>
/// Local first-person controller for the initial voxel gameplay slice.
/// Movement and collision use Godot's CharacterBody3D physics.
/// </summary>
public sealed partial class FirstPersonPlayer : CharacterBody3D
{
    private const float MoveSpeed = 5.5f;
    private const float JumpSpeed = 6.5f;
    private const float MouseSensitivity = 0.0025f;
    private const float EyeHeight = 1.62f;
    private const float PlayerHeight = 1.8f;

    private VoxelWorld? _world;
    private Camera3D? _camera;
    private RayCast3D? _interactionRay;
    private Label? _statusLabel;
    private float _pitch;
    private bool _jumpRequested;
    private BlockState _selectedBlock = Blocks.GRASS_BLOCK.DefaultState;
    private string _selectedBlockName = "Grama";

    public void Configure(VoxelWorld world) =>
        _world = world ?? throw new ArgumentNullException(nameof(world));

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1;
        FloorSnapLength = 0.2f;
        SafeMargin = 0.001f;

        var bodyShape = new CollisionShape3D
        {
            Name = "BodyCollider",
            Position = new Vector3(0, PlayerHeight / 2.0f, 0),
            Shape = new CapsuleShape3D
            {
                Radius = 0.35f,
                Height = PlayerHeight
            }
        };
        AddChild(bodyShape);

        _camera = new Camera3D
        {
            Name = "Camera",
            Position = new Vector3(0, EyeHeight, 0),
            Current = true
        };
        AddChild(_camera);

        _interactionRay = new RayCast3D
        {
            Name = "BlockRay",
            TargetPosition = new Vector3(0, 0, -6),
            CollisionMask = 1,
            Enabled = true
        };
        _camera.AddChild(_interactionRay);

        CreateHud();
        UpdateHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 movement = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A)) movement.X -= 1.0f;
        if (Input.IsKeyPressed(Key.D)) movement.X += 1.0f;
        if (Input.IsKeyPressed(Key.W)) movement.Y -= 1.0f;
        if (Input.IsKeyPressed(Key.S)) movement.Y += 1.0f;

        if (movement.LengthSquared() > 1.0f)
            movement = movement.Normalized();

        Vector3 direction = GlobalTransform.Basis * new Vector3(movement.X, 0, movement.Y);
        direction.Y = 0;
        if (direction.LengthSquared() > 0)
            direction = direction.Normalized();

        Vector3 velocity = Velocity;
        velocity.X = direction.X * MoveSpeed;
        velocity.Z = direction.Z * MoveSpeed;

        if (IsOnFloor())
        {
            if (velocity.Y < 0)
                velocity.Y = 0;

            if (_jumpRequested)
                velocity.Y = JumpSpeed;
        }
        else
        {
            velocity += GetGravity() * (float)delta;
        }

        _jumpRequested = false;
        Velocity = velocity;
        MoveAndSlide();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseMotion motion &&
            Input.MouseMode == Input.MouseModeEnum.Captured &&
            _camera is not null)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity,
                -1.5533f,
                1.5533f);
            _camera.Rotation = new Vector3(_pitch, 0, 0);
            return;
        }

        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
        {
            switch (key.Keycode)
            {
                case Key.Escape:
                    Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                        ? Input.MouseModeEnum.Visible
                        : Input.MouseModeEnum.Captured;
                    break;
                case Key.Space:
                    _jumpRequested = true;
                    break;
                case Key.Key1:
                    SelectBlock(Blocks.GRASS_BLOCK.DefaultState, "Grama");
                    break;
                case Key.Key2:
                    SelectBlock(Blocks.DIRT.DefaultState, "Terra");
                    break;
                case Key.Key3:
                    SelectBlock(Blocks.STONE.DefaultState, "Pedra");
                    break;
            }
            return;
        }

        if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        if (Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
                Input.MouseMode = Input.MouseModeEnum.Captured;
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left)
            BreakTargetedBlock();
        else if (mouseButton.ButtonIndex == MouseButton.Right)
            PlaceSelectedBlock();
    }

    private void SelectBlock(BlockState block, string displayName)
    {
        _selectedBlock = block;
        _selectedBlockName = displayName;
        UpdateHud();
    }

    private void BreakTargetedBlock()
    {
        if (_world is null || _interactionRay is null)
            return;

        _interactionRay.ForceRaycastUpdate();
        if (!_interactionRay.IsColliding())
            return;

        Vector3 point = _interactionRay.GetCollisionPoint();
        Vector3 normal = _interactionRay.GetCollisionNormal();
        var position = new BlockPos(
            Mathf.FloorToInt(point.X - normal.X * 0.01f),
            Mathf.FloorToInt(point.Y - normal.Y * 0.01f),
            Mathf.FloorToInt(point.Z - normal.Z * 0.01f));

        _world.SetBlockState(position, Blocks.AIR.DefaultState);
    }

    private void PlaceSelectedBlock()
    {
        if (_world is null || _interactionRay is null)
            return;

        _interactionRay.ForceRaycastUpdate();
        if (!_interactionRay.IsColliding())
            return;

        Vector3 point = _interactionRay.GetCollisionPoint();
        Vector3 normal = _interactionRay.GetCollisionNormal();
        var position = new BlockPos(
            Mathf.FloorToInt(point.X + normal.X * 0.01f),
            Mathf.FloorToInt(point.Y + normal.Y * 0.01f),
            Mathf.FloorToInt(point.Z + normal.Z * 0.01f));

        if (!_world.GetBlockState(position.GetX(), position.GetY(), position.GetZ()).IsAir)
            return;
        if (WouldIntersectPlayer(position))
            return;

        _world.SetBlockState(position, _selectedBlock);
    }

    private bool WouldIntersectPlayer(BlockPos position)
    {
        float blockCenterX = position.GetX() + 0.5f;
        float blockCenterZ = position.GetZ() + 0.5f;
        float playerBottom = GlobalPosition.Y;
        float playerTop = playerBottom + PlayerHeight;

        return position.GetY() < playerTop &&
               position.GetY() + 1 > playerBottom &&
               Mathf.Abs(blockCenterX - GlobalPosition.X) < 0.55f &&
               Mathf.Abs(blockCenterZ - GlobalPosition.Z) < 0.55f;
    }

    private void CreateHud()
    {
        var layer = new CanvasLayer
        {
            Name = "Hud"
        };
        AddChild(layer);

        var crosshairContainer = new CenterContainer
        {
            Name = "Crosshair",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        crosshairContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        crosshairContainer.AddChild(new Label
        {
            Text = "+"
        });
        layer.AddChild(crosshairContainer);

        _statusLabel = new Label
        {
            Name = "Instructions",
            Position = new Vector2(16, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(_statusLabel);
    }

    private void UpdateHud()
    {
        if (_statusLabel is null)
            return;

        _statusLabel.Text =
            $"Bloco: {_selectedBlockName} (1/2/3)\n" +
            "WASD mover | ESPAÇO pular | Mouse olhar\n" +
            "Botão esquerdo quebrar | Direito colocar | ESC liberar cursor";
    }
}
