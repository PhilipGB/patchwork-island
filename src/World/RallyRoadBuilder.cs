using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Builds the rally circuit as one continuous closed road surface with a joined
/// shoulder, painted dashes and a single collision mesh. No overlapping road
/// rectangles are used, so joins cannot Z-fight or leave cracks.
/// </summary>
public static class RallyRoadBuilder
{
    public const float RoadTopY = 0.065f;
    public const float ShoulderTopY = 0.048f;
    public const float MarkingY = 0.079f;

    public sealed class Result
    {
        public Node3D Root { get; init; }
        public Node3D StartLine { get; init; }
        public StaticBody3D Collision { get; init; }
        public int SampleCount { get; init; }
        public float Length { get; init; }
    }

    public static Result Build(Node3D parent)
    {
        var root = new Node3D { Name = "RallyRoad" };
        parent.AddChild(root);

        var samples = RallyCourse.SampleClosed(28);
        var faces = new List<Vector3>();
        BuildStrip(root, samples, RallyCourse.HalfRoad, RoadTopY, Color.FromHtml("8A8491"),
            "RallyRoadSurface", colorVariant: false);
        BuildShoulders(root, samples, faces);
        BuildRoadFaces(samples, RallyCourse.HalfRoad, RoadTopY, faces);
        BuildEdgeLines(root, samples);
        BuildDashes(root, samples);

        var collision = new StaticBody3D
        {
            Name = "RallyRoadCollision",
            CollisionLayer = 1,
            CollisionMask = 0,
        };
        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces.ToArray());
        collision.AddChild(new CollisionShape3D { Shape = shape });
        root.AddChild(collision);

        var startLine = BuildStartLine(root);
        BuildConnector(root, faces, collision, shape);
        return new Result
        {
            Root = root,
            StartLine = startLine,
            Collision = collision,
            SampleCount = samples.Count,
            Length = RallyCourse.Length(),
        };
    }

    private static Vector2[] LeftVectors(IReadOnlyList<RallyCourse.Point> samples)
    {
        int count = samples.Count;
        var left = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var previous = samples[(i - 1 + count) % count];
            var next = samples[(i + 1) % count];
            var tangent = Normalized(next.X - previous.X, next.Z - previous.Z);
            left[i] = new Vector2(-tangent.Y, tangent.X);
        }
        return left;
    }

    private static void BuildStrip(Node3D parent, IReadOnlyList<RallyCourse.Point> samples,
        float halfWidth, float y, Color color, string name, bool colorVariant)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int count = samples.Count;
        var lefts = LeftVectors(samples);
        for (int i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var la = lefts[i];
            var lb = lefts[(i + 1) % count];
            Vector3 aL = new(a.X + la.X * halfWidth, y, a.Z + la.Y * halfWidth);
            Vector3 aR = new(a.X - la.X * halfWidth, y, a.Z - la.Y * halfWidth);
            Vector3 bL = new(b.X + lb.X * halfWidth, y, b.Z + lb.Y * halfWidth);
            Vector3 bR = new(b.X - lb.X * halfWidth, y, b.Z - lb.Y * halfWidth);
            AddQuad(surface, aL, aR, bL, bR, color);
        }
        CommitSurface(parent, surface, name);
    }

    private static void BuildShoulders(Node3D parent, IReadOnlyList<RallyCourse.Point> samples,
        List<Vector3> faces)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Color shoulder = Color.FromHtml("D8C49B");
        int count = samples.Count;
        var lefts = LeftVectors(samples);
        for (int i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var la = lefts[i];
            var lb = lefts[(i + 1) % count];
            Vector3 aOutL = new(a.X + la.X * RallyCourse.HalfClearance, ShoulderTopY, a.Z + la.Y * RallyCourse.HalfClearance);
            Vector3 aInL = new(a.X + la.X * RallyCourse.HalfRoad, ShoulderTopY, a.Z + la.Y * RallyCourse.HalfRoad);
            Vector3 bOutL = new(b.X + lb.X * RallyCourse.HalfClearance, ShoulderTopY, b.Z + lb.Y * RallyCourse.HalfClearance);
            Vector3 bInL = new(b.X + lb.X * RallyCourse.HalfRoad, ShoulderTopY, b.Z + lb.Y * RallyCourse.HalfRoad);
            Vector3 aOutR = new(a.X - la.X * RallyCourse.HalfClearance, ShoulderTopY, a.Z - la.Y * RallyCourse.HalfClearance);
            Vector3 aInR = new(a.X - la.X * RallyCourse.HalfRoad, ShoulderTopY, a.Z - la.Y * RallyCourse.HalfRoad);
            Vector3 bOutR = new(b.X - lb.X * RallyCourse.HalfClearance, ShoulderTopY, b.Z - lb.Y * RallyCourse.HalfClearance);
            Vector3 bInR = new(b.X - lb.X * RallyCourse.HalfRoad, ShoulderTopY, b.Z - lb.Y * RallyCourse.HalfRoad);
            AddQuad(surface, aOutL, aInL, bOutL, bInL, shoulder);
            AddQuad(surface, aInR, aOutR, bInR, bOutR, shoulder);
            AddQuadFaces(faces, aOutL, aInL, bOutL, bInL);
            AddQuadFaces(faces, aInR, aOutR, bInR, bOutR);
        }
        CommitSurface(parent, surface, "RallyShoulders");
    }

    private static void BuildRoadFaces(IReadOnlyList<RallyCourse.Point> samples, float halfWidth,
        float y, List<Vector3> faces)
    {
        int count = samples.Count;
        var lefts = LeftVectors(samples);
        for (int i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var la = lefts[i];
            var lb = lefts[(i + 1) % count];
            Vector3 aL = new(a.X + la.X * halfWidth, y, a.Z + la.Y * halfWidth);
            Vector3 aR = new(a.X - la.X * halfWidth, y, a.Z - la.Y * halfWidth);
            Vector3 bL = new(b.X + lb.X * halfWidth, y, b.Z + lb.Y * halfWidth);
            Vector3 bR = new(b.X - lb.X * halfWidth, y, b.Z - lb.Y * halfWidth);
            AddQuadFaces(faces, aL, aR, bL, bR);
        }
    }

    private static void BuildEdgeLines(Node3D parent, IReadOnlyList<RallyCourse.Point> samples)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Color line = Color.FromHtml("F1E9D2");
        int count = samples.Count;
        var lefts = LeftVectors(samples);
        float inner = RallyCourse.HalfRoad - 0.24f;
        float outer = RallyCourse.HalfRoad - 0.08f;
        for (int i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var la = lefts[i];
            var lb = lefts[(i + 1) % count];
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 aIn = new(a.X + side * la.X * inner, MarkingY, a.Z + side * la.Y * inner);
                Vector3 aOut = new(a.X + side * la.X * outer, MarkingY, a.Z + side * la.Y * outer);
                Vector3 bIn = new(b.X + side * lb.X * inner, MarkingY, b.Z + side * lb.Y * inner);
                Vector3 bOut = new(b.X + side * lb.X * outer, MarkingY, b.Z + side * lb.Y * outer);
                AddQuad(surface, aIn, aOut, bIn, bOut, line);
            }
        }
        CommitSurface(parent, surface, "RallyEdgeLines");
    }

    private static void BuildDashes(Node3D parent, IReadOnlyList<RallyCourse.Point> samples)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Color line = Color.FromHtml("FFF3C9");
        int count = samples.Count;
        float total = 0f;
        var segmentLengths = new float[count];
        for (int i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            segmentLengths[i] = Mathf.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
            total += segmentLengths[i];
        }

        float distance = 0f;
        int index = 0;
        float segmentStart = 0f;
        while (distance < total)
        {
            while (index < count - 1 && distance > segmentStart + segmentLengths[index])
            {
                segmentStart += segmentLengths[index];
                index++;
            }
            var a = samples[index];
            var b = samples[(index + 1) % count];
            float segmentLength = Mathf.Max(0.001f, segmentLengths[index]);
            float t = Mathf.Clamp((distance - segmentStart) / segmentLength, 0f, 1f);
            Vector3 start = new(Mathf.Lerp(a.X, b.X, t), MarkingY, Mathf.Lerp(a.Z, b.Z, t));
            float tangentX = b.X - a.X;
            float tangentZ = b.Z - a.Z;
            float tangentLength = Mathf.Max(0.001f, Mathf.Sqrt(tangentX * tangentX + tangentZ * tangentZ));
            tangentX /= tangentLength;
            tangentZ /= tangentLength;
            float dashLength = 2.6f;
            float halfWidth = 0.11f;
            Vector3 end = start + new Vector3(tangentX, 0, tangentZ) * dashLength;
            Vector3 side = new Vector3(-tangentZ, 0, tangentX) * halfWidth;
            AddQuad(surface, start - side, start + side, end - side, end + side, line);
            distance += 7.2f;
        }
        CommitSurface(parent, surface, "RallyDashes");
    }

    private static Node3D BuildStartLine(Node3D parent)
    {
        var root = new Node3D
        {
            Name = "RallyStartLine",
            Position = new Vector3(RallyCourse.Sample(0f).X, MarkingY, RallyCourse.Sample(0f).Z),
            RotationDegrees = new Vector3(0, RallyCourse.YawDegrees(0f), 0),
        };
        parent.AddChild(root);
        const int tiles = 10;
        for (int i = 0; i < tiles; i++)
        {
            float x = -3.6f + i * 0.8f;
            Geo.Box(root, new Vector3(x, 0.005f, 0f), new Vector3(0.8f, 0.012f, 0.7f),
                i % 2 == 0 ? Palette.Cream : Palette.Navy);
        }
        return root;
    }

    private static void BuildConnector(Node3D root, List<Vector3> faces,
        StaticBody3D collision, ConcavePolygonShape3D shape)
    {
        var connectorSamples = new List<RallyCourse.Point>
        {
            new(49.2f, 0f),
            new(50.6f, -0.6f),
            new(52.0f, -1.8f),
            new(53.4f, -3.0f),
            new(54.6f, -4.3f),
            new(56f, -6f),
        };
        var node = new Node3D { Name = "RallyConnector" };
        root.AddChild(node);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var connectorLefts = LeftVectors(connectorSamples);
        for (int i = 0; i < connectorSamples.Count - 1; i++)
        {
            var a = connectorSamples[i];
            var b = connectorSamples[i + 1];
            var la = connectorLefts[i];
            var lb = connectorLefts[i + 1];
            const float half = 3.2f;
            Vector3 aL = new(a.X + la.X * half, RoadTopY, a.Z + la.Y * half);
            Vector3 aR = new(a.X - la.X * half, RoadTopY, a.Z - la.Y * half);
            Vector3 bL = new(b.X + lb.X * half, RoadTopY, b.Z + lb.Y * half);
            Vector3 bR = new(b.X - lb.X * half, RoadTopY, b.Z - lb.Y * half);
            AddQuad(surface, aL, aR, bL, bR, Color.FromHtml("8A8491"));
            AddQuadFaces(faces, aL, aR, bL, bR);
        }
        CommitSurface(node, surface, "RallyConnectorSurface");
        shape.SetFaces(faces.ToArray());
    }

    private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Color color)
    {
        surface.SetColor(color);
        surface.SetNormal(Vector3.Up);
        surface.SetUV(new Vector2(0, 0));
        surface.AddVertex(a);
        surface.SetUV(new Vector2(1, 0));
        surface.AddVertex(b);
        surface.SetUV(new Vector2(0, 1));
        surface.AddVertex(c);
        surface.SetUV(new Vector2(1, 1));
        surface.AddVertex(d);
        surface.SetUV(new Vector2(1, 0));
        surface.AddVertex(b);
        surface.SetUV(new Vector2(1, 1));
        surface.AddVertex(c);
    }

    private static void AddQuadFaces(List<Vector3> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        faces.Add(a);
        faces.Add(b);
        faces.Add(c);
        faces.Add(a);
        faces.Add(c);
        faces.Add(d);
    }

    private static void CommitSurface(Node3D parent, SurfaceTool surface, string name)
    {
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.97f,
            Metallic = 0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = surface.Commit(),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);
    }

    private static Vector2 Normalized(float dx, float dz)
    {
        float length = Mathf.Sqrt(dx * dx + dz * dz);
        if (length < 0.00001f) return new Vector2(0, 1);
        return new Vector2(dx / length, dz / length);
    }
}
