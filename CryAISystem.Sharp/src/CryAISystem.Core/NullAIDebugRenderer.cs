// Literal port of dev/Code/CryEngine/CryAISystem/NullAIDebugRenderer.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Helper functions to draw some interesting debug shapes.

namespace CryAISystem;

public struct CNullAIDebugRenderer : IAIDebugRenderer
{
    public float GetCameraFOV() { return 0; }

    public Vec3 GetCameraPos() { return new Vec3(0, 0, 0); }
    public float GetDebugDrawZ(Vec3 vPoint, bool bUseTerrainOrWater) { return 0; }

    // Assume 800x600 screen resolution to prevent possible division by zero
    public int GetWidth() { return 800; }
    public int GetHeight() { return 600; }

    public void DrawAABB(AABB aabb, bool bSolid, ColorB color, EBoundingBoxDrawStyle bbDrawStyle) { }
    public void DrawAABB(AABB aabb, Matrix34 matWorld, bool bSolid, ColorB color, EBoundingBoxDrawStyle bbDrawStyle) { }
    public void DrawArrow(Vec3 vPos, Vec3 vLength, float fWidth, ColorB color) { }
    public void DrawCapsuleOutline(Vec3 vPos0, Vec3 vPos1, float fRadius, ColorB color) { }
    public void DrawCircleOutline(Vec3 vPos, float fRadius, ColorB color) { }
    public void DrawCircles(Vec3 vPos,
                                                     float fMinRadius, float fMaxRadius, int numRings,
                                                     ColorF vInsideColor, ColorF vOutsideColor) { }
    public void DrawCone(Vec3 vPos, Vec3 vDir, float fRadius, float fHeight, ColorB color, bool fDrawShaded = true) { }
    public void DrawCylinder(Vec3 vPos, Vec3 vDir, float fRadius, float fHeight, ColorB color, bool bDrawShaded = true) { }
    public void DrawEllipseOutline(Vec3 vPos, float fRadiusX, float fRadiusY, float fOrientation, ColorB color) { }
    public void Draw2dLabel(int nCol, int nRow, string szText, ColorB color) { }
    public void Draw2dLabel(float fX, float fY, float fFontSize, ColorB color, bool bCenter, string text, params object[] args) { }
    public void Draw3dLabel(Vec3 vPos, float fFontSize, string text, params object[] args) { }
    public void Draw3dLabelEx(Vec3 vPos, float fFontSize, ColorB color, bool bFixedSize, bool bCenter, bool bDepthTest, bool bFramed, string text, params object[] args) { }
    public void Draw2dImage(float fX, float fY, float fWidth, float fHeight, int nTextureID, float fS0 = 0, float fT0 = 0, float fS1 = 1, float fT1 = 1, float fAngle = 0, float fR = 1, float fG = 1, float fB = 1, float fA = 1, float fZ = 1) { }
    public void DrawLine(Vec3 v0, ColorB colorV0, Vec3 v1, ColorB colorV1, float fThickness = 1.0f) { }
    public void DrawOBB(OBB obb, Vec3 vPos, bool bSolid, ColorB color, EBoundingBoxDrawStyle bbDrawStyle) { }
    public void DrawOBB(OBB obb, Matrix34 matWorld, bool bSolid, ColorB color, EBoundingBoxDrawStyle bbDrawStyle) { }
    public void DrawPolyline(Vec3[] va, uint32 nPoints, bool bClosed, ColorB color, float fThickness = 1.0f) { }
    public void DrawPolyline(Vec3[] va, uint32 nPoints, bool bClosed, ColorB[] colorArray, float fThickness = 1.0f) { }
    public void DrawRangeArc(Vec3 vPos, Vec3 vDir, float fAngle, float fRadius, float fWidth,
                                                        ColorB colorFill, ColorB colorOutline, bool bDrawOutline) { }
    public void DrawRangeBox(Vec3 vPos, Vec3 vDir, float fSizeX, float fSizeY, float fWidth,
                                                        ColorB colorFill, ColorB colorOutline, bool bDrawOutline) { }
    public void DrawRangeCircle(Vec3 vPos, float fRadius, float fWidth,
                                                                 ColorB colorFill, ColorB colorOutline, bool bDrawOutline) { }
    public void DrawRangePolygon(Vec3[] polygon, int nVertices, float fWidth,
                                                                    ColorB colorFill, ColorB colorOutline, bool bDrawOutline) { }
    public void DrawSphere(Vec3 vPos, float fRadius, ColorB color, bool bDrawShaded = true) { }
    public void DrawTriangle(Vec3 v0, ColorB colorV0, Vec3 v1, ColorB colorV1, Vec3 v2, ColorB colorV2) { }
    public void DrawTriangles(Vec3[] va, uint numPoints, ColorB color) { }
    public void DrawWireFOVCone(Vec3 vPos, Vec3 vDir, float fRadius, float fFOV, ColorB color) { }
    public void DrawWireSphere(Vec3 vPos, float fRadius, ColorB color) { }

    public ITexture LoadTexture(string sNameOfTexture, uint32 nFlags) { return null; }

    // [9/16/2010 evgeny] ProjectToScreen is not guaranteed to work if used outside Renderer
    public bool ProjectToScreen(float fInX, float fInY, float fInZ, out float fOutX, out float fOutY, out float fOutZ) { fOutX = 0; fOutY = 0; fOutZ = 0; return false; }

    public void TextToScreen(float fX, float fY, string format, params object[] args) { }
    public void TextToScreenColor(int nX, int nY, float fRed, float fGreen, float fBlue, float fAlpha, string format, params object[] args) { }

    public void Init2DMode() { }
    public void Init3DMode() { }

    public void SetAlphaBlended(bool bOn) { }
    public void SetBackFaceCulling(bool bOn) { }
    public void SetDepthTest(bool bOn) { }
    public void SetDepthWrite(bool bOn) { }
    public void SetDrawInFront(bool bOn) { }

    public void SetMaterialColor(float fRed, float fGreen, float fBlue, float fAlpha) { }

    public uint PopState() { return 0; }
    public uint PushState() { return 0; }
}

// Forward decls / shells for IAIDebugRenderer.h types ported on demand.
public interface IAIDebugRenderer
{
    float GetCameraFOV();
    Vec3 GetCameraPos();
    float GetDebugDrawZ(Vec3 vPoint, bool bUseTerrainOrWater);
    int GetWidth();
    int GetHeight();
    void DrawSphere(Vec3 vPos, float fRadius, ColorB color, bool bDrawShaded = true);
    void DrawLine(Vec3 v0, ColorB colorV0, Vec3 v1, ColorB colorV1, float fThickness = 1.0f);
    void Draw2dLabel(float fX, float fY, float fFontSize, ColorB color, bool bCenter, string text, params object[] args);
    void TextToScreen(float fX, float fY, string format, params object[] args);
}

public struct ColorF { public float r, g, b, a; public ColorF(float r, float g, float b, float a = 1.0f) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator ColorB(ColorF c) { return new ColorB((uint8)(c.r*255), (uint8)(c.g*255), (uint8)(c.b*255), (uint8)(c.a*255)); } }
public class ITexture { }
// OBB / Matrix34 / EBoundingBoxDrawStyle live in CryCommon/Cry_Geo.cs (literal port).
