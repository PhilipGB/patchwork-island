using System;
using System.Collections.Generic;

namespace PatchworkIsland;

/// <summary>
/// Closed, smooth rally centreline shared by the road mesh, checkpoint gates,
/// traffic safety checks and pure tests. Catmull-Rom sampling keeps the track
/// continuous and avoids the overlapping coplanar road rectangles used by the
/// original four-corner meadow loop.
/// </summary>
public static class RallyCourse
{
    public const float RoadWidth = 8f;
    public const float ShoulderWidth = 1.5f;
    public const float HalfRoad = RoadWidth * 0.5f;
    public const float HalfClearance = HalfRoad + ShoulderWidth;
    public const float MinimumBoundaryClearance = 1.0f;

    public readonly struct Point
    {
        public readonly float X;
        public readonly float Z;

        public Point(float x, float z)
        {
            X = x;
            Z = z;
        }
    }

    public sealed class Gate
    {
        public int Number { get; init; }
        public float X { get; init; }
        public float Z { get; init; }
        public float YawDegrees { get; init; }
        public float Fraction { get; init; }
    }

    // East-island layout. All centres keep the paved width plus shoulder inside
    // the island hedge (x 49..94.5, z -54..54).
    public static readonly Point[] ControlPoints =
    {
        new(56f, -6f),
        new(56f, -23f),
        new(61f, -37f),
        new(75f, -45f),
        new(86f, -37f),
        new(87f, -20f),
        new(86f, 6f),
        new(82f, 27f),
        new(73f, 40f),
        new(62f, 43f),
        new(56.5f, 34f),
        new(56f, 18f),
        new(56f, 2f),
    };

    public static readonly float[] FourGateFractions = { 0.10f, 0.30f, 0.52f, 0.74f };
    public static readonly float[] SixGateFractions = { 0.06f, 0.20f, 0.34f, 0.50f, 0.66f, 0.82f };

    public static IReadOnlyList<Gate> FourGates => BuildGates(FourGateFractions);
    public static IReadOnlyList<Gate> SixGates => BuildGates(SixGateFractions);

    public static Point Sample(float fraction)
    {
        fraction -= MathF.Floor(fraction);
        int count = ControlPoints.Length;
        float scaled = fraction * count;
        int index = (int)MathF.Floor(scaled) % count;
        float t = scaled - MathF.Floor(scaled);
        Point p0 = ControlPoints[(index - 1 + count) % count];
        Point p1 = ControlPoints[index];
        Point p2 = ControlPoints[(index + 1) % count];
        Point p3 = ControlPoints[(index + 2) % count];
        return CatmullRom(p0, p1, p2, p3, t);
    }

    public static Point Tangent(float fraction)
    {
        const float epsilon = 0.0005f;
        Point before = Sample(fraction - epsilon);
        Point after = Sample(fraction + epsilon);
        float dx = after.X - before.X;
        float dz = after.Z - before.Z;
        float length = MathF.Sqrt(dx * dx + dz * dz);
        if (length < 1e-5f) return new Point(0f, 1f);
        return new Point(dx / length, dz / length);
    }

    public static float YawDegrees(float fraction)
    {
        Point tangent = Tangent(fraction);
        return MathF.Atan2(tangent.X, tangent.Z) * 180f / MathF.PI;
    }

    public static Gate StartGate() => new()
    {
        Number = 0,
        X = Sample(0f).X,
        Z = Sample(0f).Z,
        YawDegrees = YawDegrees(0f),
        Fraction = 0f,
    };

    public static IReadOnlyList<Gate> BuildGates(IReadOnlyList<float> fractions)
    {
        var gates = new List<Gate>();
        for (int i = 0; i < fractions.Count; i++)
        {
            Point point = Sample(fractions[i]);
            gates.Add(new Gate
            {
                Number = i + 1,
                X = point.X,
                Z = point.Z,
                YawDegrees = YawDegrees(fractions[i]),
                Fraction = fractions[i],
            });
        }
        return gates;
    }

    /// <summary>Approximate closed-loop length in metres, sampled at 24 steps per span.</summary>
    public static float Length()
    {
        var samples = SampleClosed(24);
        float length = 0f;
        for (int i = 1; i < samples.Count; i++)
            length += Distance(samples[i - 1], samples[i]);
        length += Distance(samples[^1], samples[0]);
        return length;
    }

    public static List<Point> SampleClosed(int stepsPerSpan)
    {
        int perSpan = Math.Max(2, stepsPerSpan);
        int count = ControlPoints.Length;
        var samples = new List<Point>(count * perSpan);
        for (int index = 0; index < count; index++)
        {
            Point p0 = ControlPoints[(index - 1 + count) % count];
            Point p1 = ControlPoints[index];
            Point p2 = ControlPoints[(index + 1) % count];
            Point p3 = ControlPoints[(index + 2) % count];
            for (int step = 0; step < perSpan; step++)
                samples.Add(CatmullRom(p0, p1, p2, p3, step / (float)perSpan));
        }
        return samples;
    }

    /// <summary>Distance from an XZ position to the paved centreline.</summary>
    public static float NearestDistance(float x, float z)
    {
        var samples = SampleClosed(20);
        float best = float.MaxValue;
        for (int i = 0; i < samples.Count; i++)
        {
            Point a = samples[i];
            Point b = samples[(i + 1) % samples.Count];
            best = MathF.Min(best, DistanceToSegment(x, z, a, b));
        }
        return best;
    }

    /// <summary>Ensures the whole paved corridor plus shoulder sits inside the island.</summary>
    public static bool IsInsideIsland(float x, float z) =>
        x - HalfClearance >= 49.0f + MinimumBoundaryClearance &&
        x + HalfClearance <= 94.5f - MinimumBoundaryClearance &&
        z - HalfClearance >= -54.0f + MinimumBoundaryClearance &&
        z + HalfClearance <= 54.0f - MinimumBoundaryClearance;

    private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float x = 0.5f * (2f * p1.X + (-p0.X + p2.X) * t +
                          (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2 +
                          (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3);
        float z = 0.5f * (2f * p1.Z + (-p0.Z + p2.Z) * t +
                          (2f * p0.Z - 5f * p1.Z + 4f * p2.Z - p3.Z) * t2 +
                          (-p0.Z + 3f * p1.Z - 3f * p2.Z + p3.Z) * t3);
        return new Point(x, z);
    }

    private static float DistanceToSegment(float px, float pz, Point a, Point b)
    {
        float dx = b.X - a.X;
        float dz = b.Z - a.Z;
        float lengthSq = dx * dx + dz * dz;
        if (lengthSq < 1e-8f) return MathF.Sqrt((px - a.X) * (px - a.X) + (pz - a.Z) * (pz - a.Z));
        float t = ((px - a.X) * dx + (pz - a.Z) * dz) / lengthSq;
        t = Math.Clamp(t, 0f, 1f);
        float cx = a.X + dx * t;
        float cz = a.Z + dz * t;
        return MathF.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
    }

    private static float Distance(Point a, Point b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
}
