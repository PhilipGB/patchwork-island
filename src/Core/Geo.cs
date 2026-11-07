using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Small primitive-geometry toolkit. Every visible model in the game is built
/// from these low-poly, flat-shaded pieces — no external 3D assets required.
/// </summary>
public static class Geo
{
    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();

    public static StandardMaterial3D Mat(Color color, bool emissive = false, float alpha = 1f)
    {
        string key = $"{color.ToHtml(true)}|{emissive}|{alpha:0.00}";
        if (Materials.TryGetValue(key, out var cached))
            return cached;

        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, alpha),
            Roughness = 0.95f,
            Metallic = 0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
        if (alpha < 0.999f)
        {
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }
        if (emissive)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 0.65f;
        }
        Materials[key] = material;
        return material;
    }

    public static StandardMaterial3D GhostMat(Color color)
    {
        string key = $"ghost|{color.ToHtml(true)}";
        if (Materials.TryGetValue(key, out var cached))
            return cached;
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, 0.48f),
            Roughness = 1f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 0.8f,
            NoDepthTest = false,
        };
        Materials[key] = material;
        return material;
    }

    public static MeshInstance3D Box(Node parent, Vector3 pos, Vector3 size, Color color,
        Vector3? rotationDegrees = null, bool emissive = false, string name = "Box")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Mat(color, emissive),
            Position = pos,
        };
        if (rotationDegrees.HasValue)
            mesh.RotationDegrees = rotationDegrees.Value;
        parent.AddChild(mesh);
        return mesh;
    }

    public static StaticBody3D StaticBox(Node parent, string name, Vector3 pos, Vector3 size, Color color,
        Vector3? rotationDegrees = null)
    {
        var body = new StaticBody3D { Name = name, Position = pos };
        if (rotationDegrees.HasValue)
            body.RotationDegrees = rotationDegrees.Value;
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Mat(color),
        });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }

    public static MeshInstance3D Cylinder(Node parent, Vector3 pos, float topRadius, float bottomRadius,
        float height, Color color, int sides = 8, Vector3? rotationDegrees = null, bool emissive = false,
        string name = "Cylinder")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh
            {
                TopRadius = topRadius,
                BottomRadius = bottomRadius,
                Height = height,
                RadialSegments = sides,
                Rings = 1,
            },
            MaterialOverride = Mat(color, emissive),
            Position = pos,
        };
        if (rotationDegrees.HasValue)
            mesh.RotationDegrees = rotationDegrees.Value;
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Cone(Node parent, Vector3 pos, float radius, float height, Color color,
        int sides = 8, Vector3? rotationDegrees = null)
    {
        return Cylinder(parent, pos, 0f, radius, height, color, sides, rotationDegrees, false, "Cone");
    }

    public static MeshInstance3D Sphere(Node parent, Vector3 pos, float radius, Color color,
        int segments = 8, int rings = 4, Vector3? scale = null, string name = "Sphere")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2f,
                RadialSegments = segments,
                Rings = rings,
            },
            MaterialOverride = Mat(color),
            Position = pos,
        };
        if (scale.HasValue)
            mesh.Scale = scale.Value;
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Disc(Node parent, Vector3 pos, float radius, Color color, float alpha = 1f,
        bool emissive = false)
    {
        var mesh = new MeshInstance3D
        {
            Name = "Disc",
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.035f,
                RadialSegments = 20,
                Rings = 1,
            },
            MaterialOverride = Mat(color, emissive, alpha),
            Position = pos,
        };
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Triangle(Node parent, string name, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var normal = (b - a).Cross(c - a).Normalized();
        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
        surface.SetNormal(-normal);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
        var mesh = new MeshInstance3D
        {
            Name = name,
            Mesh = surface.Commit(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Roughness = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        parent.AddChild(mesh);
        return mesh;
    }

    public static Label3D Label(Node parent, Vector3 pos, string text, int fontSize, Color color,
        bool billboard = false, float pixelSize = 0.01f, int outline = 0, Vector3? rotationDegrees = null,
        string name = "Label3D")
    {
        var label = new Label3D
        {
            Name = name,
            Text = text,
            FontSize = fontSize,
            PixelSize = pixelSize,
            Modulate = color,
            OutlineSize = outline,
            OutlineModulate = new Color(1, 1, 1, 0.9f),
            Billboard = billboard
                ? BaseMaterial3D.BillboardModeEnum.Enabled
                : BaseMaterial3D.BillboardModeEnum.Disabled,
            Position = pos,
            NoDepthTest = false,
        };
        if (rotationDegrees.HasValue)
            label.RotationDegrees = rotationDegrees.Value;
        parent.AddChild(label);
        return label;
    }

    /// <summary>A readable cream sign with a coloured frame and optional SVG icon.</summary>
    public static Node3D SignBoard(Node parent, Vector3 position, float yawDegrees, string text,
        Color accent, float width = 5f, float height = 1.15f, string iconName = null, int fontSize = 64)
    {
        var root = new Node3D { Name = $"Sign_{text}", Position = position, RotationDegrees = new Vector3(0, yawDegrees, 0) };
        parent.AddChild(root);

        Box(root, Vector3.Zero, new Vector3(width, height, 0.16f), Palette.Cream);
        Box(root, new Vector3(0, height * 0.5f, 0), new Vector3(width, 0.13f, 0.2f), accent);
        Box(root, new Vector3(0, -height * 0.5f, 0), new Vector3(width, 0.13f, 0.2f), accent);
        Box(root, new Vector3(-width * 0.5f, 0, 0), new Vector3(0.13f, height, 0.2f), accent);
        Box(root, new Vector3(width * 0.5f, 0, 0), new Vector3(0.13f, height, 0.2f), accent);

        float textOffset = 0f;
        if (!string.IsNullOrWhiteSpace(iconName))
        {
            var texture = IconTexture(iconName);
            if (texture != null)
            {
                var sprite = new Sprite3D
                {
                    Texture = texture,
                    PixelSize = 0.023f,
                    Position = new Vector3(-width * 0.5f + 0.72f, 0, 0.11f),
                    Modulate = Colors.White,
                    Shaded = false,
                    DoubleSided = true,
                };
                root.AddChild(sprite);
                textOffset = 0.48f;
            }
        }

        Label(root, new Vector3(textOffset, 0.01f, 0.12f), text, fontSize, Palette.Navy,
            false, 0.0085f, 0, null, "SignText");
        return root;
    }

    public static Texture2D IconTexture(string name)
    {
        string path = $"res://assets/icons/{name}.svg";
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);
        return null;
    }

    public static Sprite3D IconSprite(Node parent, Vector3 pos, string name, float pixelSize = 0.025f,
        bool billboard = true)
    {
        var texture = IconTexture(name);
        if (texture == null)
            return null;
        var sprite = new Sprite3D
        {
            Texture = texture,
            PixelSize = pixelSize,
            Position = pos,
            Modulate = Colors.White,
            Shaded = false,
            Billboard = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = true,
        };
        parent.AddChild(sprite);
        return sprite;
    }

    public static Color Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    public static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
