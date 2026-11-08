using System;

namespace ArcanumLib.Gui.RadialMenu;

/// <summary>
/// Geometry of a radial menu, shared by its drawing and its hit test: sectors start at the top and run
/// clockwise, the sub-arc ring (<see cref="RadialMenuItem.Arcs" />) lies just outside the wheel, centred on its sector.
/// </summary>
public readonly struct RadialMenuLayout
{
    /// <summary>Space between the wheel's rim and the sub-arc ring.</summary>
    public const float ArcGap = 6f;
    /// <summary>Radial depth of the sub-arc ring.</summary>
    public const float ArcWidth = 62f;
    /// <summary>Slack around the edges the mouse may stray over without losing the hover.</summary>
    public const float Tolerance = 8f;
    /// <summary>Narrowest sub-arc segment, so a crowded wheel still gets segments one can aim at.</summary>
    public const float MinArcAngle = 0.42f;

    /// <summary>Number of sectors.</summary>
    public readonly int Count;
    /// <summary>Outer radius of the wheel.</summary>
    public readonly float Outer;
    /// <summary>Inner radius (the center button).</summary>
    public readonly float Inner;

    /// <summary>Creates the layout of a wheel with <paramref name="count" /> sectors.</summary>
    public RadialMenuLayout(int count, float outer, float inner)
    {
        Count = Math.Max(1, count);
        Outer = outer;
        Inner = inner;
    }

    /// <summary>Angle each sector spans.</summary>
    public float AnglePerItem => 2f * MathF.PI / Count;

    /// <summary>Start angle of sector <paramref name="i" />; the first one is centred at the top.</summary>
    public float SectorStart(int i) => -MathF.PI / 2f - AnglePerItem / 2f + i * AnglePerItem;

    /// <summary>Inner radius of the sub-arc ring.</summary>
    public float ArcInner => Outer + ArcGap;

    /// <summary>Outer radius of the sub-arc ring.</summary>
    public float ArcOuter => ArcInner + ArcWidth;

    /// <summary>Angle one sub-arc segment spans when a sector has <paramref name="arcCount" /> of them.</summary>
    public float ArcAngle(int arcCount) => MathF.Min(MathF.Max(AnglePerItem, MinArcAngle), 2f * MathF.PI / Math.Max(1, arcCount));

    /// <summary>Start angle of sub-arc <paramref name="arc" /> of sector <paramref name="sector" />.</summary>
    public float ArcStart(int sector, int arcCount, int arc)
    {
        float w = ArcAngle(arcCount);
        float mid = SectorStart(sector) + AnglePerItem / 2f;
        return mid - w * arcCount / 2f + arc * w;
    }

    /// <summary>
    /// What lies under a point <paramref name="dx" />, <paramref name="dy" /> from the centre: sector -2 is the center
    /// button, -1 nothing; arc is the sub-arc of the open sector <paramref name="expanded" />, or -1.
    /// </summary>
    public (int Sector, int Arc) Hit(float dx, float dy, int expanded, int arcCount)
    {
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < Inner + 12f) return (-2, -1);
        float angle = MathF.Atan2(dy, dx);
        float full = 2f * MathF.PI;
        if (expanded >= 0 && arcCount > 0 && dist >= Outer + ArcGap / 2f && dist <= ArcOuter + Tolerance)
        {
            float off = Mod(angle - ArcStart(expanded, arcCount, 0), full);
            int k = (int)(off / ArcAngle(arcCount));
            if (k < arcCount) return (expanded, k);
        }
        if (dist > Outer + Tolerance) return (-1, -1);
        int sector = (int)(Mod(angle - SectorStart(0), full) / AnglePerItem) % Count;
        return (sector, -1);
    }

    private static float Mod(float a, float m) => ((a % m) + m) % m;
}
