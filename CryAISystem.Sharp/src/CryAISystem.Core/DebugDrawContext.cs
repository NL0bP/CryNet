// Forward declaration shell for DebugDrawContext.h (Phase 1 leaf — full literal port of the
// 40-line header pending). Provides the operator-> indirection that the C++ uses.

using CryAISystem.CryCommon;

namespace CryAISystem;

public class CDebugDrawContext
{
    public CDebugDrawContext Op_Arrow() { return this; }

    public float GetWidth() { return 0; }
    public float GetHeight() { return 0; }
    public void Draw2dLabel(float x, float y, float scale, ColorB color, bool center, string format, params object[] args) { }
    public void TextToScreen(float x, float y, string format, params object[] args) { }

    // Subset of IAIDebugRenderer drawing methods used by AICollision.cpp.
    // The C++ pipes these through the IRenderer / IRenderAuxGeom backend; the literal port
    // is the call shape — actual rendering will land when IRenderAuxGeom is ported.
    public void DrawLine(Vec3 v0, ColorB c0, Vec3 v1, ColorB c1) { }
    public void DrawCone(Vec3 pos, Vec3 dir, float radius, float height, ColorB color) { }
    public void DrawCylinder(Vec3 pos, Vec3 dir, float radius, float height, ColorB color) { }
    public void DrawOBB(OBB obb, Matrix34 transform, bool drawSolid, ColorB color, EBoundingBoxDrawStyle style) { }
    public void DrawTriangle(Vec3 v0, ColorB c0, Vec3 v1, ColorB c1, Vec3 v2, ColorB c2) { }
    public void SetBackFaceCulling(bool b) { }
    public void SetDepthWrite(bool b) { }
    public void DrawAABB(AABB aabb, bool drawSolid, ColorB color, EBoundingBoxDrawStyle style) { }
    public void DrawAABB(AABB aabb, Matrix34 transform, bool drawSolid, ColorB color, EBoundingBoxDrawStyle style) { }
    public void Draw3dLabel(Vec3 pos, float size, string format, params object[] args) { }
    public void DrawPolyline(System.Collections.Generic.IList<Vec3> pts, int count, bool closed, ColorB color, float width) { }
    public void DrawTriangles(Vec3[] pts, int count, ColorB color) { }
    public void DrawSphere(Vec3 pos, float radius, ColorB color) { }
    public void DrawLine(Vec3 v0, ColorB c0, Vec3 v1, ColorB c1, float width) { }
    public void DrawCylinder(Vec3 pos, Vec3 dir, float radius, float height, ColorB color, bool drawShaded) { }
    public float GetDebugDrawZ(Vec3 pos, bool useTerrain) { return pos.z; }
    public void DrawLine(Vec3 v0, ColorF c0, Vec3 v1, ColorF c1) { }
    public void DrawSphere(Vec3 pos, float radius, ColorF color) { }
    public void DrawArrow(Vec3 from, Vec3 dir, float size, ColorB color) { }
    public void DrawCapsuleOutline(Vec3 start, Vec3 end, float radius, ColorB color) { }
    // Added for CollisionAvoidanceSystem.cpp / VisionMap.cpp literal port
    public void DrawCircleOutline(Vec3 pos, float radius, ColorB color) { }
    public void DrawRangeCircle(Vec3 pos, float radius, float width, ColorB innerColor, ColorB outerColor) { }
    public void DrawWireSphere(Vec3 pos, float radius, ColorB color) { }
    public void SetAlphaBlended(bool b) { }
    public void SetDepthTest(bool b) { }
    public bool ProjectToScreen(float x, float y, float z, out float sx, out float sy, out float sz) { sx = 0; sy = 0; sz = 0; return false; }
}

// Col_* color constants — Cry_Color.h
public static class Col
{
    public static readonly ColorB Col_SteelBlue = new ColorB(70, 130, 180);
    public static readonly ColorB Col_Grey = new ColorB(128, 128, 128);
    public static readonly ColorB Col_SlateBlue = new ColorB(106, 90, 205);
    public static readonly ColorB Col_Brown = new ColorB(165, 42, 42);
    public static readonly ColorB Col_Cyan = new ColorB(0, 255, 255);
    public static readonly ColorB Col_DarkOrchid = new ColorB(153, 50, 204);
    public static readonly ColorB Col_White = new ColorB(255, 255, 255);
}

// Shell for ColorB (CryColor.h leaf — full literal port pending).
public struct ColorB
{
    public uint8 r, g, b, a;
    public ColorB(uint8 r_, uint8 g_, uint8 b_) { r = r_; g = g_; b = b_; a = 255; }
    public ColorB(uint8 r_, uint8 g_, uint8 b_, uint8 a_) { r = r_; g = g_; b = b_; a = a_; }
}
