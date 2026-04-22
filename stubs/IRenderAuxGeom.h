// Minimal stub for IRenderAuxGeom.h — CryPhysics calls this via `gEnv->pRenderer->GetIRenderAuxGeom()`
// in debug visualization paths. On a server we never enter those code paths.
#pragma once

#include <cstdint>

struct ColorB
{
    std::uint8_t r = 0, g = 0, b = 0, a = 255;
    ColorB() = default;
    ColorB(std::uint8_t R, std::uint8_t G, std::uint8_t B, std::uint8_t A = 255) : r(R), g(G), b(B), a(A) {}
};

struct SAuxGeomRenderFlags
{
    std::uint32_t m_renderFlags = 0;
    SAuxGeomRenderFlags() = default;
    SAuxGeomRenderFlags(std::uint32_t f) : m_renderFlags(f) {}
};

struct IRenderAuxGeom
{
    virtual ~IRenderAuxGeom() = default;
    virtual void DrawLine(const void*, const ColorB&, const void*, const ColorB&, float = 1.f) {}
    virtual void DrawTriangle(const void*, const ColorB&, const void*, const ColorB&, const void*, const ColorB&) {}
    virtual void DrawSphere(const void*, float, const ColorB&, bool = true) {}
    virtual void DrawAABB(const void*, bool, const ColorB&, int = 0) {}
    virtual SAuxGeomRenderFlags GetRenderFlags() { return {}; }
    virtual void                SetRenderFlags(const SAuxGeomRenderFlags&) {}
    virtual void                Flush() {}
};
