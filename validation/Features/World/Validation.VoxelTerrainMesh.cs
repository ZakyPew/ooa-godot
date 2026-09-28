using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVoxelTerrainMesh()
    {
        using ArrayMesh isolated = VoxelTerrainMeshBuilder.Build(
            1, 1, [1], cellSize: 16.0f, levelHeight: 4.0f);
        Vector3[] isolatedVertices =
            (Vector3[])isolated.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
        Vector3[] isolatedNormals =
            (Vector3[])isolated.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal];
        Vector2[] isolatedUvs =
            (Vector2[])isolated.SurfaceGetArrays(0)[(int)Mesh.ArrayType.TexUV];
        FailIf(isolatedVertices.Length != 30 || isolatedNormals.Length != 30 ||
            isolatedUvs.Length != 30,
            "A one-cell voxel column must contain one top and four exposed side quads.");
        FailIf(isolatedVertices.Min(vertex => vertex.Y) != -4.0f ||
            isolatedVertices.Max(vertex => vertex.Y) != 0.0f ||
            isolatedVertices.Max(vertex => vertex.X) != 16.0f ||
            isolatedVertices.Max(vertex => vertex.Z) != 16.0f,
            "Voxel columns must use the requested cell size and level height.");
        FailIf(isolatedNormals.Any(normal => Mathf.Abs(normal.Length() - 1.0f) > 0.001f),
            "Voxel mesh normals must be normalized.");
        ValidateWinding(isolatedVertices, isolatedNormals);
        FailIf(isolatedUvs.Any(uv => uv.X is < 0.0f or > 1.0f || uv.Y is < 0.0f or > 1.0f),
            "Voxel texture coordinates must remain within the normalized room texture.");

        using ArrayMesh joined = VoxelTerrainMeshBuilder.Build(
            2, 1, [1, 1], cellSize: 16.0f, levelHeight: 4.0f);
        Vector3[] joinedVertices =
            (Vector3[])joined.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
        FailIf(joinedVertices.Length != 48,
            "Adjacent equal-height columns must omit their hidden shared wall.");
        ValidateWinding(joinedVertices,
            (Vector3[])joined.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal]);

        using ArrayMesh stepped = VoxelTerrainMeshBuilder.Build(
            2, 1, [2, 1], cellSize: 16.0f, levelHeight: 4.0f);
        Vector3[] steppedVertices =
            (Vector3[])stepped.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
        FailIf(steppedVertices.Length != 54 ||
            steppedVertices.Min(vertex => vertex.Y) != -8.0f,
            "Stepped columns must keep the exposed height difference and both top faces.");
        ValidateWinding(steppedVertices,
            (Vector3[])stepped.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Normal]);

        using ArrayMesh empty = VoxelTerrainMeshBuilder.Build(
            2, 1, [0, 0], cellSize: 16.0f, levelHeight: 4.0f);
        FailIf(empty.GetSurfaceCount() != 0,
            "A zero-height field must not produce visible geometry.");

        OracleRoomData room = _rooms.GetRoom(0, 0x54);
        byte[] collisionHeights = VoxelPreviewScreen.BuildCollisionHeightField(room);
        FailIf(collisionHeights.Length != room.WidthInTiles * room.HeightInTiles,
            "The collision preview must use playable room dimensions, not padded layout stride.");
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 center = new(
                x * OracleRoomData.MetatileSize + OracleRoomData.MetatileSize * 0.5f,
                y * OracleRoomData.MetatileSize + OracleRoomData.MetatileSize * 0.5f);
            byte expected = room.IsSolid(center) ? (byte)2 : (byte)1;
            FailIf(collisionHeights[y * room.WidthInTiles + x] != expected,
                "Each preview column must reflect Link-solid collision at its metatile center.");
        }
    }

    private static void ValidateWinding(Vector3[] vertices, Vector3[] normals)
    {
        for (int index = 0; index < vertices.Length; index += 3)
        {
            Vector3 winding = (vertices[index + 1] - vertices[index])
                .Cross(vertices[index + 2] - vertices[index]).Normalized();
            FailIf(winding.Dot(normals[index]) < 0.999f,
                $"Voxel triangle {index / 3} winding must point along its stored normal.");
        }
    }
}
