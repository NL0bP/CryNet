// Port of CryPhysics qhull.cpp - Quick Hull convex hull computation
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Algorithms;

/// <summary>
/// Convex hull computation using Quick Hull algorithm.
/// Port of qhull() from CryEngine's qhull.cpp.
/// </summary>
public static class ConvexHull
{
    /// <summary>
    /// Compute the convex hull of a set of 3D points.
    /// Returns triangle indices defining the hull surface.
    /// </summary>
    public static int[] ComputeHull(PhysVector3[] points)
    {
        int nPts = points.Length;
        if (nPts < 4)
            return Array.Empty<int>();

        // Find initial tetrahedron using extreme points
        int i0 = 0, i1 = 0, i2 = 0, i3 = 0;
        FindInitialTetrahedron(points, nPts, ref i0, ref i1, ref i2, ref i3);

        if (i0 == i1) return Array.Empty<int>();

        var faces = new List<(int A, int B, int C)>();
        faces.Add((i0, i1, i2));
        faces.Add((i0, i2, i3));
        faces.Add((i0, i3, i1));
        faces.Add((i1, i3, i2));

        // Ensure all faces point outward
        var center = (points[i0] + points[i1] + points[i2] + points[i3]) * 0.25f;
        for (int i = 0; i < faces.Count; i++)
        {
            var (a, b, c) = faces[i];
            var n = (points[b] - points[a]) ^ (points[c] - points[a]);
            if (n.Dot(points[a] - center) < 0)
                faces[i] = (a, c, b);
        }

        // Assign points to faces
        var usedPt = new bool[nPts];
        usedPt[i0] = usedPt[i1] = usedPt[i2] = usedPt[i3] = true;

        // Iteratively add furthest points
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var (a, b, c) = faces[fi];
                var normal = ((points[b] - points[a]) ^ (points[c] - points[a])).Normalized();
                float d = normal.Dot(points[a]);

                // Find furthest point above this face
                float maxDist = 1e-6f;
                int furthest = -1;
                for (int pi = 0; pi < nPts; pi++)
                {
                    if (usedPt[pi]) continue;
                    float dist = normal.Dot(points[pi]) - d;
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        furthest = pi;
                    }
                }

                if (furthest >= 0)
                {
                    usedPt[furthest] = true;
                    changed = true;

                    // Remove visible faces and create new ones
                    var horizon = new List<(int, int)>();
                    var visible = new List<int>();

                    for (int fj = faces.Count - 1; fj >= 0; fj--)
                    {
                        var (fa, fb, fc) = faces[fj];
                        var fn = ((points[fb] - points[fa]) ^ (points[fc] - points[fa])).Normalized();
                        if (fn.Dot(points[furthest] - points[fa]) > 0)
                            visible.Add(fj);
                    }

                    // Find horizon edges
                    foreach (int vi in visible)
                    {
                        var (va, vb, vc) = faces[vi];
                        int[][] edges = { new[] { va, vb }, new[] { vb, vc }, new[] { vc, va } };
                        foreach (var edge in edges)
                        {
                            bool shared = false;
                            foreach (int vj in visible)
                            {
                                if (vj == vi) continue;
                                var (oa, ob, oc) = faces[vj];
                                if (SharesEdge(edge[0], edge[1], oa, ob, oc))
                                {
                                    shared = true;
                                    break;
                                }
                            }
                            if (!shared)
                                horizon.Add((edge[0], edge[1]));
                        }
                    }

                    // Remove visible faces (reverse order)
                    visible.Sort();
                    for (int vi = visible.Count - 1; vi >= 0; vi--)
                        faces.RemoveAt(visible[vi]);

                    // Create new faces from horizon to furthest point
                    foreach (var (e0, e1) in horizon)
                        faces.Add((e0, e1, furthest));

                    break; // Restart face iteration
                }
            }
        }

        // Convert to index array
        var tris = new int[faces.Count * 3];
        for (int i = 0; i < faces.Count; i++)
        {
            tris[i * 3] = faces[i].A;
            tris[i * 3 + 1] = faces[i].B;
            tris[i * 3 + 2] = faces[i].C;
        }
        return tris;
    }

    private static bool SharesEdge(int e0, int e1, int a, int b, int c)
    {
        return (e0 == b && e1 == a) || (e0 == c && e1 == b) || (e0 == a && e1 == c);
    }

    private static void FindInitialTetrahedron(PhysVector3[] pts, int n,
        ref int i0, ref int i1, ref int i2, ref int i3)
    {
        // Find extreme points along each axis
        i0 = 0; i1 = 0;
        float maxDist = 0;
        for (int i = 1; i < n; i++)
        {
            float d = (pts[i] - pts[0]).LengthSq();
            if (d > maxDist) { maxDist = d; i1 = i; }
        }

        // Find point furthest from line i0-i1
        var lineDir = (pts[i1] - pts[i0]).Normalized();
        maxDist = 0;
        i2 = 0;
        for (int i = 0; i < n; i++)
        {
            if (i == i0 || i == i1) continue;
            var v = pts[i] - pts[i0];
            float proj = v.Dot(lineDir);
            float dist = (v - lineDir * proj).LengthSq();
            if (dist > maxDist) { maxDist = dist; i2 = i; }
        }

        // Find point furthest from plane i0-i1-i2
        var planeN = ((pts[i1] - pts[i0]) ^ (pts[i2] - pts[i0])).Normalized();
        maxDist = 0;
        i3 = 0;
        for (int i = 0; i < n; i++)
        {
            if (i == i0 || i == i1 || i == i2) continue;
            float dist = MathF.Abs(planeN.Dot(pts[i] - pts[i0]));
            if (dist > maxDist) { maxDist = dist; i3 = i; }
        }
    }
}
