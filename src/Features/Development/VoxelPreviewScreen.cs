using Godot;

namespace oracleofages;

/// <summary>
/// Small, optional 3D preview for comparing active room collision with the
/// original renderer. It is diagnostic presentation and never owns gameplay.
/// </summary>
public partial class VoxelPreviewScreen : Control
{
    private const float CellSize = OracleRoomData.MetatileSize;
    private const float LevelHeight = 5.0f;
    private SubViewport? _viewport;
    private MeshInstance3D? _terrain;
    private Camera3D? _camera;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var background = new ColorRect
        {
            Color = new Color(0.025f, 0.045f, 0.07f, 0.96f),
            Position = Vector2.Zero,
            Size = new Vector2(76, 62),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(background);

        var container = new SubViewportContainer
        {
            Position = new Vector2(2, 10),
            Size = new Vector2(72, 44),
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _viewport = new SubViewport
        {
            Size = new Vector2I(144, 88),
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
        };
        container.AddChild(_viewport);
        AddChild(container);

        var world = new Node3D { Name = "VoxelPreviewWorld" };
        _viewport.AddChild(world);
        _terrain = new MeshInstance3D { Name = "CollisionHeightField" };
        world.AddChild(_terrain);

        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.1f,
            Far = 1000.0f,
            Current = true
        };
        world.AddChild(_camera);

        var light = new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-48), Mathf.DegToRad(-32), 0),
            LightEnergy = 1.2f
        };
        world.AddChild(light);

        AddLabel("COLLISION HEIGHT PREVIEW", new Vector2(3, 1), new Vector2(71, 9));
        AddLabel("F6 CLOSE", new Vector2(3, 53), new Vector2(70, 8));
    }

    internal void Toggle(OracleRoomData room)
    {
        Visible = !Visible;
        if (Visible)
            SetRoom(room);
    }

    internal void SetRoom(OracleRoomData room)
    {
        if (_terrain is null || _camera is null)
            return;

        ArrayMesh mesh = VoxelTerrainMeshBuilder.Build(
            room.WidthInTiles,
            room.HeightInTiles,
            BuildCollisionHeightField(room),
            CellSize,
            LevelHeight,
            room.Texture);
        _terrain.Mesh = mesh;
        _terrain.Position = Vector3.Zero;

        Vector3 target = new(room.Width * 0.5f, -LevelHeight, room.Height * 0.5f);
        _camera.Position = target + new Vector3(
            room.Width * 0.9f,
            room.Width + room.Height,
            room.Height * 0.9f);
        _camera.LookAt(target, Vector3.Up);
        _camera.Size = Mathf.Max(190.0f, (room.Width + room.Height) * 0.8f);
        QueueRedraw();
    }

    internal static byte[] BuildCollisionHeightField(OracleRoomData room)
    {
        var heights = new byte[room.WidthInTiles * room.HeightInTiles];
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 center = new(
                x * OracleRoomData.MetatileSize + OracleRoomData.MetatileSize * 0.5f,
                y * OracleRoomData.MetatileSize + OracleRoomData.MetatileSize * 0.5f);
            // Keep a walkable base slab and raise the sampled Link-solid cells.
            // This deliberately exposes the collision approximation as a
            // preview; it is not yet the game's authored height map.
            heights[y * room.WidthInTiles + x] = room.IsSolid(center) ? (byte)2 : (byte)1;
        }
        return heights;
    }

    private void AddLabel(string text, Vector2 position, Vector2 size)
    {
        var label = new Label
        {
            Text = text,
            Position = position,
            Size = size,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true
        };
        label.AddThemeFontSizeOverride("font_size", 7);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.55f));
        AddChild(label);
    }
}
