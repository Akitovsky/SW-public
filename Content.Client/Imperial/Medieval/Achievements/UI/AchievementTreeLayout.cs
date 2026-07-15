using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

public sealed class AchievementTreeLayout : LayoutContainer
{
    private const float HalfCenter = 0.5f;
    private const float EdgeWidth = 1f;
    private const float FeatherPx = 1f;

    private static readonly Color UnlockedEdge = Color.FromHex("#5c8a3ecc");
    private static readonly Color UnlockedCenter = Color.FromHex("#2e5020cc");
    private static readonly Color LockedEdge = Color.FromHex("#5a4428aa");
    private static readonly Color LockedCenter = Color.FromHex("#2a1e0eaa");

    public IReadOnlyDictionary<(string From, string To), List<Vector2>> EdgeCurves { get; set; } =
        new Dictionary<(string From, string To), List<Vector2>>();

    public Vector2 PanOffset { get; set; }
    public float Zoom { get; set; } = 1f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (EdgeCurves.Count == 0)
            return;

        var nodesById = new Dictionary<string, AchievementTreeNode>();
        foreach (var child in Children)
        {
            if (child is AchievementTreeNode n)
                nodesById[n.Proto.ID] = n;
        }

        foreach (var ((fromId, _), basePoints) in EdgeCurves)
        {
            if (basePoints.Count < 2)
                continue;

            var (edge, center) = nodesById.TryGetValue(fromId, out var parent) && parent.Unlocked
                ? (UnlockedEdge, UnlockedCenter)
                : (LockedEdge, LockedCenter);

            DrawCurve(handle, basePoints, edge, center);
        }
    }

    private void DrawCurve(DrawingHandleScreen handle, List<Vector2> basePoints, Color edge, Color center)
    {
        Vector2 ToScreen(Vector2 p) => (p * Zoom + PanOffset) * UIScale;

        var screenPoints = new Vector2[basePoints.Count];
        for (var i = 0; i < basePoints.Count; i++)
            screenPoints[i] = ToScreen(basePoints[i]);

        for (var i = 0; i < screenPoints.Length - 1; i++)
        {
            DrawThickSegment(handle, screenPoints[i], screenPoints[i + 1], (HalfCenter + EdgeWidth) * UIScale, edge);
            DrawThickSegment(handle, screenPoints[i], screenPoints[i + 1], HalfCenter * UIScale, center);
        }

        for (var i = 1; i < screenPoints.Length - 1; i++)
        {
            handle.DrawCircle(screenPoints[i], (HalfCenter + EdgeWidth) * UIScale, edge);
            handle.DrawCircle(screenPoints[i], HalfCenter * UIScale, center);
        }
    }

    private static void DrawThickSegment(DrawingHandleScreen handle, Vector2 a, Vector2 b, float halfWidth, Color color)
    {
        var dir = b - a;
        var len = dir.Length();

        if (len < 0.001f)
            return;

        dir /= len;
        var perp = new Vector2(-dir.Y, dir.X);

        var inner = MathF.Max(halfWidth - FeatherPx, 0f);
        var outer = halfWidth + FeatherPx;
        var clear = new Color(color.R, color.G, color.B, 0f);

        Vector2 Off(Vector2 p, float w) => p + perp * w;

        AddQuad(handle, Off(a, -inner), Off(b, -inner), Off(b, inner), Off(a, inner), color, color, color, color);
        AddQuad(handle, Off(a, inner), Off(b, inner), Off(b, outer), Off(a, outer), color, color, clear, clear);
        AddQuad(handle, Off(a, -outer), Off(b, -outer), Off(b, -inner), Off(a, -inner), clear, clear, color, color);
    }

    private static void AddQuad(DrawingHandleScreen handle, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color c0, Color c1, Color c2, Color c3)
    {
        var verts = new DrawVertexUV2DColor[6]
        {
            new(p0, c0), new(p1, c1), new(p2, c2),
            new(p0, c0), new(p2, c2), new(p3, c3),
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, verts);
    }
}
