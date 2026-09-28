using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Builds a textured voxel height field for optional world presentation.
/// Heights are presentation data only; gameplay coordinates and collision stay
/// owned by OracleRoomData and the existing room systems.
/// </summary>
internal static class VoxelTerrainMeshBuilder
{
    internal static ArrayMesh Build(
        int width,
        int height,
        IReadOnlyList<byte> heights,
        float cellSize,
        float levelHeight,
        Texture2D? texture = null)
    {
        ArgumentNullException.ThrowIfNull(heights);
        if (width <= 0 || height <= 0 || heights.Count != checked(width * height))
            throw new ArgumentOutOfRangeException(nameof(heights),
                "The height field must contain exactly width * height cells.");
        if (!float.IsFinite(cellSize) || cellSize <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (!float.IsFinite(levelHeight) || levelHeight <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(levelHeight));

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        byte At(int x, int z) => x < 0 || x >= width || z < 0 || z >= height
            ? (byte)0
            : heights[z * width + x];

        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            int cellIndex = z * width + x;
            int level = heights[cellIndex];
            if (level == 0)
                continue;

            float x0 = x * cellSize;
            float x1 = x0 + cellSize;
            float z0 = z * cellSize;
            float z1 = z0 + cellSize;
            float top = -level * levelHeight;
            float bottom = 0.0f;
            float u0 = (float)x / width;
            float u1 = (float)(x + 1) / width;
            float v0 = (float)z / height;
            float v1 = (float)(z + 1) / height;

            // Godot uses Y-up. The original room's image is mapped to the top
            // of each column in row-major room order.
            AddQuad(
                new(x0, top, z0), new(x0, top, z1),
                new(x1, top, z1), new(x1, top, z0), Vector3.Up,
                new(u0, v0), new(u0, v1), new(u1, v1), new(u1, v0));

            AddSideIfExposed(At(x, z - 1),
                new(x1, top, z0), new(x0, top, z0),
                new(x0, bottom, z0), new(x1, bottom, z0),
                Vector3.Forward, u1, u0, v0, v1);
            AddSideIfExposed(At(x + 1, z),
                new(x1, top, z1), new(x1, top, z0),
                new(x1, bottom, z0), new(x1, bottom, z1),
                Vector3.Right, u1, u0, v0, v1);
            AddSideIfExposed(At(x, z + 1),
                new(x0, top, z1), new(x1, top, z1),
                new(x1, bottom, z1), new(x0, bottom, z1),
                Vector3.Back, u0, u1, v0, v1);
            AddSideIfExposed(At(x - 1, z),
                new(x0, top, z0), new(x0, top, z1),
                new(x0, bottom, z1), new(x0, bottom, z0),
                Vector3.Left, u0, u1, v0, v1);

            void AddSideIfExposed(
                byte neighborLevel,
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 d,
                Vector3 normal,
                float sideU0,
                float sideU1,
                float sideV0,
                float sideV1)
            {
                if (neighborLevel >= level)
                    return;
                float neighborTop = -neighborLevel * levelHeight;
                // End the exposed wall at the neighboring column's top. This
                // also removes the hidden part of stepped internal faces.
                c = new Vector3(c.X, neighborTop, c.Z);
                d = new Vector3(d.X, neighborTop, d.Z);
                AddQuad(a, b, c, d, normal,
                    new(sideU0, sideV0), new(sideU1, sideV0),
                    new(sideU1, sideV1), new(sideU0, sideV1));
            }
        }

        if (vertices.Count == 0)
            return new ArrayMesh();

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (texture is not null)
        {
            mesh.SurfaceSetMaterial(0, new StandardMaterial3D
            {
                AlbedoTexture = texture,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
            });
        }
        return mesh;

        void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            Vector2 uvA,
            Vector2 uvB,
            Vector2 uvC,
            Vector2 uvD)
        {
            AddTriangle(a, b, c, normal, uvA, uvB, uvC);
            AddTriangle(a, c, d, normal, uvA, uvC, uvD);
        }

        void AddTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 normal,
            Vector2 uvA,
            Vector2 uvB,
            Vector2 uvC)
        {
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(uvA);
            uvs.Add(uvB);
            uvs.Add(uvC);
        }
    }
}
