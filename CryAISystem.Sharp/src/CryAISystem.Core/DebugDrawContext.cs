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
}

// Shell for ColorB (CryColor.h leaf — full literal port pending).
public struct ColorB
{
    public uint8 r, g, b, a;
    public ColorB(uint8 r_, uint8 g_, uint8 b_) { r = r_; g = g_; b = b_; a = 255; }
    public ColorB(uint8 r_, uint8 g_, uint8 b_, uint8 a_) { r = r_; g = g_; b = b_; a = a_; }
}
