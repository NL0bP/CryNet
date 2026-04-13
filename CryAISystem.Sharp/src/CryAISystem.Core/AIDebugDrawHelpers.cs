// Literal port of dev/Code/CryEngine/CryAISystem/AIDebugDrawHelpers.{h,cpp} (547 lines).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Description : Helper functions to draw some interesting debug shapes.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using CryAISystem.CryCommon;

namespace CryAISystem;

public static class AIDebugDrawHelpers
{
    private static float g_drawOffset = 0.1f;

    //====================================================================
    // DebugDrawCircleOutline — AIDebugDrawHelpers.cpp lines 31-41
    //====================================================================
    public static void DebugDrawCircleOutline(IRenderer pRend, Vec3 pos, float rad, ColorB col)
    {
        Vec3[] points = new Vec3[20];

        for (int i = 0; i < 20; i++)
        {
            float a = ((float)i / 20.0f) * gf_PI2;
            points[i] = pos + new Vec3(cosf(a) * rad, sinf(a) * rad, 0);
        }
        // pRend->GetIRenderAuxGeom()->DrawPolyline(points, 20, true, col);
    }

    //====================================================================
    // DebugDrawCapsuleOutline — AIDebugDrawHelpers.cpp lines 46-68
    //====================================================================
    public static void DebugDrawCapsuleOutline(IRenderer pRend, Vec3 pos0, Vec3 pos1, float rad, ColorB col)
    {
        Vec3[] points = new Vec3[20];
        Vec3 axisy = pos1 - pos0;
        axisy.Normalize();
        Vec3 axisx = new Vec3(axisy.y, -axisy.x, 0);
        axisx.Normalize();

        for (int i = 0; i < 10; i++)
        {
            float a = ((float)i / 9.0f) * gf_PI;
            points[i] = pos1 + axisx * cosf(a) * rad + axisy * sinf(a) * rad;
        }

        for (int i = 0; i < 10; i++)
        {
            float a = gf_PI + ((float)i / 9.0f) * gf_PI;
            points[i + 10] = pos0 + axisx * cosf(a) * rad + axisy * sinf(a) * rad;
        }

        // pRend->GetIRenderAuxGeom()->DrawPolyline(points, 20, true, col);
    }

    //====================================================================
    // DebugDrawWireSphere — AIDebugDrawHelpers.cpp lines 73-92
    //====================================================================
    public static void DebugDrawWireSphere(IRenderer pRend, Vec3 pos, float rad, ColorB col)
    {
        const int npts = 32;
        Vec3[] xpoints = new Vec3[npts];
        Vec3[] ypoints = new Vec3[npts];
        Vec3[] zpoints = new Vec3[npts];

        for (int i = 0; i < npts; i++)
        {
            float a = ((float)i / (float)npts) * gf_PI2;
            float rx = cosf(a) * rad;
            float ry = sinf(a) * rad;
            xpoints[i] = pos + new Vec3(rx, ry, 0);
            ypoints[i] = pos + new Vec3(0, rx, ry);
            zpoints[i] = pos + new Vec3(ry, 0, rx);
        }
        // pRend->GetIRenderAuxGeom()->DrawPolyline(xpoints, npts, true, col);
        // pRend->GetIRenderAuxGeom()->DrawPolyline(ypoints, npts, true, col);
        // pRend->GetIRenderAuxGeom()->DrawPolyline(zpoints, npts, true, col);
    }

    //====================================================================
    // DebugDrawWireFOVCone — AIDebugDrawHelpers.cpp lines 97-136
    //====================================================================
    // Helper to convert Vec3 <-> PhysVector3 for Matrix33 operations
    private static CryPhysics.Math.PhysVector3 ToPhys(Vec3 v) => new CryPhysics.Math.PhysVector3(v.x, v.y, v.z);
    private static Vec3 FromPhys(CryPhysics.Math.PhysVector3 v) => new Vec3(v.X, v.Y, v.Z);

    public static void DebugDrawWireFOVCone(IRenderer pRend, Vec3 pos, Vec3 dir, float rad, float fov, ColorB col)
    {
        const int npts = 32;
        const int npts2 = 16;
        Vec3[] points = new Vec3[npts];
        Vec3[] pointsx = new Vec3[npts2];
        Vec3[] pointsy = new Vec3[npts2];

        Matrix33 baseM = new Matrix33();
        baseM.SetRotationVDir(ToPhys(dir));

        float coneRadius = sinf(fov) * rad;
        float coneHeight = cosf(fov) * rad;

        for (int i = 0; i < npts; i++)
        {
            float a = ((float)i / (float)npts) * gf_PI2;
            float rx = cosf(a) * coneRadius;
            float ry = sinf(a) * coneRadius;
            points[i] = pos + FromPhys(baseM.TransformVector(new CryPhysics.Math.PhysVector3(rx, coneHeight, ry)));
        }

        for (int i = 0; i < npts2; i++)
        {
            float a = -fov + ((float)i / (float)(npts2 - 1)) * (fov * 2);
            float rx = sinf(a) * rad;
            float ry = cosf(a) * rad;
            pointsx[i] = pos + FromPhys(baseM.TransformVector(new CryPhysics.Math.PhysVector3(rx, ry, 0)));
            pointsy[i] = pos + FromPhys(baseM.TransformVector(new CryPhysics.Math.PhysVector3(0, ry, rx)));
        }

        // pRend->GetIRenderAuxGeom()->DrawPolyline(points, npts, true, col);
        // pRend->GetIRenderAuxGeom()->DrawPolyline(pointsx, npts2, false, col);
        // pRend->GetIRenderAuxGeom()->DrawPolyline(pointsy, npts2, false, col);
        // pRend->GetIRenderAuxGeom()->DrawLine(points[0], col, pos, col);
        // pRend->GetIRenderAuxGeom()->DrawLine(points[npts/4], col, pos, col);
        // pRend->GetIRenderAuxGeom()->DrawLine(points[npts/2], col, pos, col);
        // pRend->GetIRenderAuxGeom()->DrawLine(points[npts/2+npts/4], col, pos, col);
    }

    //====================================================================
    // DebugDrawArrow — AIDebugDrawHelpers.cpp lines 141-195
    //====================================================================
    public static void DebugDrawArrow(IRenderer pRend, Vec3 pos, Vec3 length, float width, ColorB col)
    {
        Vec3[] points = new Vec3[7];
        Vec3[] tris = new Vec3[5 * 3];

        float len = length.GetLength();
        if (len < 0.0001f)
            return;

        float headLen = width * 2.0f;
        float headWidth = width * 2.0f;

        if (headLen > len * 0.8f)
            headLen = len * 0.8f;

        Vec3 dir = length / len;
        Vec3 norm = new Vec3(length.y, -length.x, 0);
        norm.NormalizeSafe();

        Vec3 end = pos + length;
        Vec3 start = pos;

        int n = 0;
        points[n++] = end;
        points[n++] = end - dir * headLen - norm * headWidth / 2;
        points[n++] = end - dir * headLen - norm * width / 2;
        points[n++] = end - dir * headLen + norm * width / 2;
        points[n++] = end - dir * headLen + norm * headWidth / 2;
        points[n++] = start - norm * width / 2;
        points[n++] = start + norm * width / 2;

        n = 0;
        tris[n++] = points[0];
        tris[n++] = points[1];
        tris[n++] = points[2];

        tris[n++] = points[0];
        tris[n++] = points[2];
        tris[n++] = points[3];

        tris[n++] = points[0];
        tris[n++] = points[3];
        tris[n++] = points[4];

        tris[n++] = points[2];
        tris[n++] = points[5];
        tris[n++] = points[6];

        tris[n++] = points[2];
        tris[n++] = points[6];
        tris[n++] = points[3];

        // pRend->GetIRenderAuxGeom()->DrawTriangles(tris, n, col);
    }

    //====================================================================
    // DebugDrawRangeCircle — AIDebugDrawHelpers.cpp lines 200-233
    //====================================================================
    public static void DebugDrawRangeCircle(IRenderer pRend, Vec3 pos, float rad, float width,
        ColorB colFill, ColorB colOutline, bool drawOutline)
    {
        const int npts = 24;

        Vec3[] points = new Vec3[npts];
        Vec3[] pointsOutline = new Vec3[npts];
        Vec3[] tris = new Vec3[npts * 2 * 3];

        if (width > rad) width = rad;

        for (int i = 0; i < npts; i++)
        {
            float a = ((float)i / (float)npts) * gf_PI2;
            points[i] = new Vec3(cosf(a), sinf(a), 0);
            pointsOutline[i] = pos + points[i] * rad;
        }

        int n = 0;
        for (int i = 0; i < npts; i++)
        {
            tris[n++] = pos + points[i] * (rad - width);
            tris[n++] = pos + points[i] * rad;
            tris[n++] = pos + points[(i + 1) % npts] * rad;

            tris[n++] = pos + points[i] * (rad - width);
            tris[n++] = pos + points[(i + 1) % npts] * rad;
            tris[n++] = pos + points[(i + 1) % npts] * (rad - width);
        }

        // pRend->GetIRenderAuxGeom()->DrawTriangles(tris, npts * 2 * 3, colFill);
        // if(drawOutline) pRend->GetIRenderAuxGeom()->DrawPolyline(pointsOutline, npts, true, colOutline);
    }

    //====================================================================
    // DebugDrawRangeArc — AIDebugDrawHelpers.cpp lines 238-278
    //====================================================================
    public static void DebugDrawRangeArc(IRenderer pRend, Vec3 pos, Vec3 dir, float angle, float rad, float width,
        ColorB colFill, ColorB colOutline, bool drawOutline)
    {
        const int npts = 12;

        Vec3[] points = new Vec3[npts];
        Vec3[] pointsOutline = new Vec3[npts];
        Vec3[] tris = new Vec3[(npts - 1) * 2 * 3];

        Vec3 forw = new Vec3(dir.x, dir.y, 0.0f);
        forw.NormalizeSafe();
        Vec3 right = new Vec3(forw.y, -forw.x, 0);

        if (width > rad) width = rad;

        for (int i = 0; i < npts; i++)
        {
            float a = ((float)i / (float)(npts - 1) - 0.5f) * angle;
            points[i] = forw * cosf(a) + right * sinf(a);
            pointsOutline[i] = pos + points[i] * rad;
        }

        int n = 0;
        for (int i = 0; i < npts - 1; i++)
        {
            tris[n++] = pos + points[i] * (rad - width);
            tris[n++] = pos + points[i + 1] * rad;
            tris[n++] = pos + points[i] * rad;

            tris[n++] = pos + points[i] * (rad - width);
            tris[n++] = pos + points[i + 1] * (rad - width);
            tris[n++] = pos + points[i + 1] * rad;
        }

        // pRend->GetIRenderAuxGeom()->DrawTriangles(tris, n, colFill);
        // if(drawOutline)
        // {
        //     pRend->GetIRenderAuxGeom()->DrawPolyline(pointsOutline, npts, false, colOutline);
        //     pRend->GetIRenderAuxGeom()->DrawLine(pos + forw * (rad - width/4), colOutline, pos + forw * (rad + width/4), colOutline);
        // }
    }

    //====================================================================
    // DebugDrawRangeBox — AIDebugDrawHelpers.cpp lines 284-342
    //====================================================================
    public static void DebugDrawRangeBox(IRenderer pRend, Vec3 pos, Vec3 dir, float sizex, float sizey, float width,
        ColorB colFill, ColorB colOutline, bool drawOutline)
    {
        float minX = sizex - width;
        float maxX = sizex;
        float minY = sizey - width;
        float maxY = sizey;

        if (maxX < 0.001f || maxY < 0.001f || minX > maxX || minY > maxY)
            return;

        Vec3[] points = new Vec3[8];
        Vec3[] tris = new Vec3[8 * 3];
        Vec3 norm = new Vec3(dir.y, -dir.x, dir.z);

        points[0] = pos + norm * -minX + dir * -minY;
        points[1] = pos + norm * minX + dir * -minY;
        points[2] = pos + norm * minX + dir * minY;
        points[3] = pos + norm * -minX + dir * minY;

        points[4] = pos + norm * -maxX + dir * -maxY;
        points[5] = pos + norm * maxX + dir * -maxY;
        points[6] = pos + norm * maxX + dir * maxY;
        points[7] = pos + norm * -maxX + dir * maxY;

        int n = 0;

        tris[n++] = points[0];
        tris[n++] = points[5];
        tris[n++] = points[1];
        tris[n++] = points[0];
        tris[n++] = points[4];
        tris[n++] = points[5];

        tris[n++] = points[1];
        tris[n++] = points[6];
        tris[n++] = points[2];
        tris[n++] = points[1];
        tris[n++] = points[5];
        tris[n++] = points[6];

        tris[n++] = points[2];
        tris[n++] = points[7];
        tris[n++] = points[3];
        tris[n++] = points[2];
        tris[n++] = points[6];
        tris[n++] = points[7];

        tris[n++] = points[3];
        tris[n++] = points[4];
        tris[n++] = points[0];
        tris[n++] = points[3];
        tris[n++] = points[7];
        tris[n++] = points[4];

        // pRend->GetIRenderAuxGeom()->DrawTriangles(tris, 8 * 3, colFill);
        // if(drawOutline) pRend->GetIRenderAuxGeom()->DrawPolyline(&points[4], 4, true, colOutline);
    }

    //====================================================================
    // DebugDrawRangePolygon — AIDebugDrawHelpers.cpp lines 347-435
    //====================================================================
    public static void DebugDrawRangePolygon(IRenderer pRend, IList<Vec3> polygon, float width,
        ColorB colFill, ColorB colOutline, bool drawOutline)
    {
        List<Vec3> verts = new List<Vec3>();
        List<int> tris = new List<int>();
        List<Vec3> outline = new List<Vec3>();

        if (polygon.Count < 3) return;

        Vec3 prevDir = polygon[0] - polygon[polygon.Count - 1];
        prevDir.NormalizeSafe();
        Vec3 prevNorm = new Vec3(-prevDir.y, prevDir.x, 0.0f);
        prevNorm.NormalizeSafe();
        Vec3 prevPos = polygon[polygon.Count - 1];

        verts.Clear();
        outline.Clear();

        for (int li = 0; li < polygon.Count; ++li)
        {
            int linext = (li + 1) % polygon.Count;

            Vec3 curPos = polygon[li];
            Vec3 nextPos = polygon[linext];
            Vec3 dirVec = nextPos - curPos;
            Vec3 norm = new Vec3(-dirVec.y, dirVec.x, 0.0f);
            norm.NormalizeSafe();

            Vec3 mid = (prevNorm + norm) * 0.5f;
            float dmr2 = sqr(mid.x) + sqr(mid.y);
            if (dmr2 > 0.00001f)
                mid = mid * (1.0f / dmr2);

            float cross = prevDir.x * dirVec.y - dirVec.x * prevDir.y;

            outline.Add(curPos);

            if (cross < 0.0f)
            {
                if (dmr2 * sqr(2.5f) < 1.0f)
                {
                    // bevel
                    verts.Add(curPos);
                    verts.Add(curPos + prevNorm * width);
                    verts.Add(curPos);
                    verts.Add(curPos + norm * width);
                }
                else
                {
                    verts.Add(curPos);
                    verts.Add(curPos + mid * width);
                }
            }
            else
            {
                verts.Add(curPos);
                verts.Add(curPos + mid * width);
            }

            prevDir = dirVec;
            prevNorm = norm;
            prevPos = curPos;
        }

        tris.Clear();
        int vertCount = verts.Count / 2;
        for (int i = 0; i < vertCount; ++i)
        {
            int j = (i + 1) % vertCount;
            tris.Add(i * 2);
            tris.Add(j * 2);
            tris.Add(j * 2 + 1);

            tris.Add(i * 2);
            tris.Add(j * 2 + 1);
            tris.Add(i * 2 + 1);
        }

        // pRend->GetIRenderAuxGeom()->DrawTriangles(&verts[0], verts.size(), &tris[0], tris.size(), colFill);
        // if(drawOutline) pRend->GetIRenderAuxGeom()->DrawPolyline(&outline[0], outline.size(), true, colOutline);
    }

    //====================================================================
    // DebugDrawLabel — AIDebugDrawHelpers.cpp lines 448-454
    //====================================================================
    public static void DebugDrawLabel(IRenderer pRenderer, int col, int row, string szText, float[] pColor)
    {
        float ColumnSize = 11;
        float RowSize = 11;
        float baseY = 10;
        // pRenderer->Draw2dLabel( ColumnSize*(float)col, baseY+RowSize*(float)row, 1.2f, pColor, false, "%s", szText );
    }

    //====================================================================
    // DebugDrawCircles — AIDebugDrawHelpers.cpp lines 459-497
    //====================================================================
    private static int numPts_circles = 32;
    private static List<Vec3> unitCircle_circles;
    private static bool init_circles = false;

    public static void DebugDrawCircles(IRenderer pRenderer, Vec3 pos,
        float minRadius, float maxRadius, int numRings,
        ColorF insideCol, ColorF outsideCol)
    {
        if (!init_circles)
        {
            init_circles = true;
            unitCircle_circles = new List<Vec3>(numPts_circles);
            for (int i = 0; i < numPts_circles; ++i)
            {
                float angle = gf_PI2 * ((float)i) / numPts_circles;
                unitCircle_circles.Add(new Vec3(sinf(angle), cosf(angle), 0.0f));
            }
        }
        for (int iRing = 0; iRing < numRings; ++iRing)
        {
            float ringFrac = 0.0f;
            if (numRings > 1)
                ringFrac += ((float)iRing) / (numRings - 1);

            float radius = (1.0f - ringFrac) * minRadius + ringFrac * maxRadius;
            ColorF colCF = new ColorF(
                (1.0f - ringFrac) * insideCol.r + ringFrac * outsideCol.r,
                (1.0f - ringFrac) * insideCol.g + ringFrac * outsideCol.g,
                (1.0f - ringFrac) * insideCol.b + ringFrac * outsideCol.b,
                (1.0f - ringFrac) * insideCol.a + ringFrac * outsideCol.a
            );

            Vec3 prevPt = pos + unitCircle_circles[numPts_circles - 1] * radius;
            prevPt.z = GetDebugDrawZ(prevPt, true);
            for (int i = 0; i < numPts_circles; ++i)
            {
                Vec3 pt = pos + unitCircle_circles[i] * radius;
                pt.z = GetDebugDrawZ(pt, true) + 0.05f;

                // pRenderer->GetIRenderAuxGeom()->DrawLine(prevPt, col, pt, col);
                prevPt = pt;
            }
        }
    }

    //====================================================================
    // DebugDrawEllipseOutline — AIDebugDrawHelpers.cpp lines 502-519
    //====================================================================
    public static void DebugDrawEllipseOutline(IRenderer pRend, Vec3 pos, float radx, float rady, float orientation, ColorB col)
    {
        Vec3[] points = new Vec3[20];

        float sin_o = sinf(orientation);
        float cos_o = cosf(orientation);
        for (int i = 0; i < 20; i++)
        {
            float angle = ((float)i / 20.0f) * gf_PI2;
            float sin_a = sinf(angle);
            float cos_a = cosf(angle);
            float x = (cos_o * cos_a * radx) - (sin_o * sin_a * rady);
            float y = (cos_o * sin_a * rady) + (sin_o * cos_a * radx);
            points[i] = pos + new Vec3(x, y, 0.0f);
        }

        // pRend->GetIRenderAuxGeom()->DrawPolyline( points, 20, true, col );
    }

    //====================================================================
    // GetDebugDrawZ — AIDebugDrawHelpers.cpp lines 524-539
    //====================================================================
    public static float GetDebugDrawZ(Vec3 pt, bool useTerrain)
    {
        if (useTerrain)
        {
            if (gAIEnv.CVars.DebugDrawOffset <= 0.0f)
                return -gAIEnv.CVars.DebugDrawOffset;
            if (gEnv.p3DEngine != null)
            {
                float terrainZ = gEnv.p3DEngine.GetTerrainElevation(pt.x, pt.y);
                float waterZ = gEnv.p3DEngine.GetWaterLevel(pt);
                return max(terrainZ, waterZ) + gAIEnv.CVars.DebugDrawOffset;
            }
            return gAIEnv.CVars.DebugDrawOffset;
        }
        else
        {
            return pt.z + gAIEnv.CVars.DebugDrawOffset;
        }
    }

    //===================================================================
    // GetDebugCameraPos — AIDebugDrawHelpers.cpp lines 544-547
    //===================================================================
    public static Vec3 GetDebugCameraPos()
    {
        // return gEnv->pSystem->GetViewCamera().GetPosition();
        return new Vec3(0, 0, 0); // ViewCamera not yet ported
    }
}
