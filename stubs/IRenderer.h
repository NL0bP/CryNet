// Minimal stub for IRenderer.h — CryPhysics only touches IRenderer in debug
// visualization paths (rigidentity.cpp, utils.cpp). On a headless game server
// we never instantiate a renderer; every call goes through `gEnv->pRenderer`
// which stays null, so a forward-declared interface is enough.
#pragma once

#include <cstdint>

struct IRenderAuxGeom;

// Texture formats referenced by utils.cpp debug paths.
enum ETEX_Format { eTF_L8 = 0 };
enum ETextureFilter { FILTER_NONE = 0 };

// Render state flags used in utils.cpp debug draw — values irrelevant since
// the code path is dead on a server (pRenderer is null).
#define GS_NODEPTHTEST 0

struct IRenderer
{
    virtual ~IRenderer() = default;
    virtual IRenderAuxGeom* GetIRenderAuxGeom(void* jobID = nullptr) { return nullptr; }
    virtual int  GetWidth()  { return 0; }
    virtual int  GetHeight() { return 0; }
    virtual void SetState(int state) {}
    virtual void EF_Set2DMode(bool enable, int w, int h) {}
    virtual int  DownLoadToVideoMemory(unsigned char*, int, int, ETEX_Format, ETEX_Format, int, bool, ETextureFilter, int) { return 0; }
    virtual void UpdateTextureInVideoMemory(int, unsigned char*, int, int, int, int, ETEX_Format) {}
    virtual void RemoveTexture(int) {}
    virtual void DrawImage(float, float, float, float, int, float, float, float, float, float, float, float, float, bool) {}
};
