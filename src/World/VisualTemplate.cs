using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// Loads the authored GLB-backed visual templates and prepares them for the
/// runtime actors. Character and vehicle meshes store their palette in
/// COLOR_0, so their shared material must opt into vertex colours. Pickups
/// already carry authored <see cref="StandardMaterial3D"/> overrides and are
/// returned untouched.
/// </summary>
public static class VisualTemplate
{
    public static readonly IReadOnlyDictionary<string, string> NpcPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["milo"] = "res://scenes/world/assets/characters/Milo_Visual.tscn",
            ["ada"] = "res://scenes/world/assets/characters/Ada_Visual.tscn",
            ["pip"] = "res://scenes/world/assets/characters/Pip_Visual.tscn",
            ["fern"] = "res://scenes/world/assets/characters/Fern_Visual.tscn",
            ["ollie"] = "res://scenes/world/assets/characters/Ollie_Visual.tscn",
            ["ivy"] = "res://scenes/world/assets/characters/Ivy_Visual.tscn",
        };

    public const string PlayerBoyPath =
        "res://scenes/world/assets/characters/Player_Boy_Visual.tscn";
    public const string PlayerGirlPath =
        "res://scenes/world/assets/characters/Player_Girl_Visual.tscn";
    public const string VanPath = "res://scenes/world/assets/vehicles/Van_Visual.tscn";

    public static readonly IReadOnlyDictionary<string, string> TrafficPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["town_scooter"] = "res://scenes/world/assets/vehicles/TownCar_Green_Visual.tscn",
            ["delivery_car"] = "res://scenes/world/assets/vehicles/TownCar_Blue_Visual.tscn",
            ["rally_rival"] = "res://scenes/world/assets/vehicles/RallyCar_Visual.tscn",
        };

    public static string PickupPath(string itemType) => itemType switch
    {
        "apple" => "res://scenes/world/assets/pickups/Pickup_Apple.tscn",
        "wood" => "res://scenes/world/assets/pickups/Pickup_Wood.tscn",
        "stone" => "res://scenes/world/assets/pickups/Pickup_Stone.tscn",
        "blue_parcel" or "orange_parcel" =>
            "res://scenes/world/assets/pickups/Pickup_Parcel.tscn",
        _ when itemType.StartsWith("book_", StringComparison.Ordinal) =>
            "res://scenes/world/assets/pickups/Pickup_Book.tscn",
        "tyre" => "res://scenes/world/assets/pickups/Pickup_RepairTyre.tscn",
        "lamp" => "res://scenes/world/assets/pickups/Pickup_RepairLamp.tscn",
        "battery" => "res://scenes/world/assets/pickups/Pickup_RepairBattery.tscn",
        _ => "res://scenes/world/assets/pickups/Pickup_Coin.tscn",
    };

    public static Node3D Instantiate(Node parent, string name, string path)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var packed = ResourceLoader.Load<PackedScene>(path) ??
            throw new InvalidOperationException($"Missing visual template {path}.");
        var template = packed.Instantiate<Node3D>() ??
            throw new InvalidOperationException($"Could not instantiate {path}.");
        template.Name = name;
        template.SetMeta("visual_template_path", path);
        parent.AddChild(template);
        return template;
    }

    public static StandardMaterial3D VertexColourMaterial()
    {
        return new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.95f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
        };
    }

    public static void ApplyVertexColours(Node root, Material material)
    {
        foreach (Node node in Descendants(root))
            if (node is MeshInstance3D mesh)
                mesh.MaterialOverride = material;
    }

    /// <summary>
    /// Splits an unrigged character mesh into animation pivots. The authored
    /// parts are baked in model space, so each mesh is reparented with its
    /// global transform preserved before its pivot rotates.
    /// </summary>
    public static CharacterRig BuildCharacterRig(Node3D template,
        IReadOnlyDictionary<int, Color> clothingTints = null)
    {
        var body = new Node3D { Name = "Body" };
        var head = new Node3D { Name = "Head", Position = new Vector3(0, 1.6f, 0) };
        var leftArm = new Node3D { Name = "LeftArm", Position = new Vector3(-0.3f, 1.36f, 0) };
        var rightArm = new Node3D { Name = "RightArm", Position = new Vector3(0.3f, 1.36f, 0) };
        var leftLeg = new Node3D { Name = "LeftLeg", Position = new Vector3(-0.13f, 0.62f, 0) };
        var rightLeg = new Node3D { Name = "RightLeg", Position = new Vector3(0.13f, 0.62f, 0) };
        template.AddChild(body);
        template.AddChild(head);
        template.AddChild(leftArm);
        template.AddChild(rightArm);
        template.AddChild(leftLeg);
        template.AddChild(rightLeg);

        var vertexMaterial = VertexColourMaterial();
        var tintMaterials = new Dictionary<Color, StandardMaterial3D>();
        var parts = new List<MeshInstance3D>();
        foreach (Node node in Descendants(template))
            if (node is MeshInstance3D mesh)
                parts.Add(mesh);

        foreach (MeshInstance3D mesh in parts)
        {
            Aabb bounds = mesh.GetAabb();
            Vector3 centre = bounds.GetCenter();
            Node3D pivot = Classify(bounds, centre, body, head, leftArm, rightArm, leftLeg, rightLeg);
            mesh.Reparent(pivot, true);
            mesh.MaterialOverride = vertexMaterial;

            int index = PartIndex(mesh.Name);
            if (clothingTints != null && index >= 0 &&
                clothingTints.TryGetValue(index, out Color tint))
            {
                if (!tintMaterials.TryGetValue(tint, out StandardMaterial3D flat))
                {
                    flat = FlatMaterial(tint);
                    tintMaterials[tint] = flat;
                }
                mesh.MaterialOverride = flat;
            }
        }

        return new CharacterRig
        {
            Template = template,
            Body = body,
            Head = head,
            LeftArm = leftArm,
            RightArm = rightArm,
            LeftLeg = leftLeg,
            RightLeg = rightLeg,
        };
    }

    private static Node3D Classify(Aabb bounds, Vector3 centre, Node3D body, Node3D head,
        Node3D leftArm, Node3D rightArm, Node3D leftLeg, Node3D rightLeg)
    {
        float y = bounds.Position.Y;
        float maxY = bounds.End.Y;
        if (y >= 1.3f || (bounds.Size.Z > 0.35f && y > 1.0f))
            return head;
        if (maxY <= 0.7f && bounds.Size.X < 0.3f && Mathf.Abs(centre.X) > 0.04f)
            return centre.X < 0f ? leftLeg : rightLeg;
        if (y >= 0.7f && y < 1.5f && bounds.Size.Z < 0.3f &&
            Mathf.Abs(centre.X) > 0.2f)
        {
            return centre.X < 0f ? leftArm : rightArm;
        }
        return body;
    }

    public static StandardMaterial3D FlatMaterial(Color colour)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = colour,
            Roughness = 0.95f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
    }

    /// <summary>
    /// Vehicles bake their four wheels into model space as well. Reparenting
    /// each wheel to a pivot at its own centre restores rolling animation.
    /// </summary>
    public static List<Node3D> BuildWheelPivots(Node3D template)
    {
        var wheels = new List<Node3D>();
        var parts = new List<MeshInstance3D>();
        foreach (Node node in Descendants(template))
            if (node is MeshInstance3D mesh)
                parts.Add(mesh);

        foreach (MeshInstance3D mesh in parts)
        {
            Aabb bounds = mesh.GetAabb();
            if (bounds.Size.X > 0.3f || bounds.Size.Y < 0.6f || bounds.Size.Y > 1.1f ||
                bounds.Size.Z < 0.6f || bounds.Size.Z > 1.1f)
            {
                continue;
            }

            var pivot = new Node3D
            {
                Name = $"Wheel_{wheels.Count}",
                Position = bounds.GetCenter(),
            };
            template.AddChild(pivot);
            mesh.Reparent(pivot, true);
            wheels.Add(pivot);
        }
        return wheels;
    }

    public static int PartIndex(StringName name)
    {
        string text = name.ToString();
        if (!text.StartsWith("Part_", StringComparison.Ordinal)) return -1;
        return int.TryParse(text.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture,
            out int index)
            ? index
            : -1;
    }

    public static string TemplatePath(Node node) =>
        node?.GetMeta("visual_template_path", "").AsString() ?? "";

    public static IEnumerable<Node> Descendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child))
                yield return descendant;
        }
    }
}

public sealed class CharacterRig
{
    public Node3D Template { get; init; }
    public Node3D Body { get; init; }
    public Node3D Head { get; init; }
    public Node3D LeftArm { get; init; }
    public Node3D RightArm { get; init; }
    public Node3D LeftLeg { get; init; }
    public Node3D RightLeg { get; init; }
}
