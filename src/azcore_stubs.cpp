// Minimal no-op implementations of the AzCore symbols CryPhysics references.
//
// CryPhysics uses AzCore at link time for: assertions, profiler macros, EBus
// module bootstrap, allocator bootstrap, and UUID stringification in a handful
// of debug/type-info code paths. On a headless DLL with no engine around it,
// none of these are load-bearing for physics simulation correctness — we just
// need the link to succeed. All implementations are no-ops or trivial fallbacks.
//
// If any of these ever need real behavior (e.g. crash-on-assert in a parity
// harness), promote that specific symbol to a proper implementation.

#include <AzCore/PlatformDef.h>
#include <AzCore/base.h>
#include <AzCore/Debug/Trace.h>
#include <AzCore/Debug/Profiler.h>
#include <AzCore/EBus/Environment.h>
#include <AzCore/Module/Environment.h>
#include <AzCore/Math/Uuid.h>
#include <AzCore/Console/IConsole.h>
#include <AzCore/Memory/AllocatorManager.h>
#include <AzCore/Memory/AllocatorBase.h>
#include <AzCore/std/allocator.h>
#include <AzCore/std/hash.h>

#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace AZ
{
    namespace Debug
    {
        // Global tracer instance referenced by every CryPhysics AZ_Assert expansion.
        Trace g_tracer;

        void Trace::Assert(const char* /*fileName*/, int /*line*/, const char* /*funcName*/, const char* /*format*/, ...) {}
        void Trace::Error(const char* /*fileName*/, int /*line*/, const char* /*funcName*/, const char* /*window*/, const char* /*format*/, ...) {}

        // Profiler globals — match definitions in AzCore's Profiler.cpp so the linker finds them.
        u64  Profiler::s_id = 0;

        ProfilerRegister* ProfilerRegister::TimerCreateAndStart(const char* /*systemName*/, const char* /*name*/, ProfilerSection* /*section*/, const char* /*fileName*/, int /*lineNum*/)
        {
            return nullptr;
        }
        void ProfilerRegister::TimerStart(ProfilerSection* /*section*/) {}
        void ProfilerRegister::TimerStop() {}

        void ProfileModuleInit() {}
    } // namespace Debug

    namespace Internal
    {
        // ContextBase constructor referenced from EBus templates.
        ContextBase::ContextBase(EBusEnvironment* /*env*/)
            : m_ebusEnvironmentTLSIndex(-1)
            , m_ebusEnvironmentGetter(nullptr)
        {
        }

        // EBusEnvironmentAllocator — CryPhysics never instantiates an EBus environment,
        // but static-init code paths still reference these. Trivial malloc/free fallback.
        EBusEnvironmentAllocator::EBusEnvironmentAllocator() {}
        EBusEnvironmentAllocator::EBusEnvironmentAllocator(const EBusEnvironmentAllocator&) {}

        void* EBusEnvironmentAllocator::allocate(std::size_t byteSize, std::size_t /*alignment*/, int /*flags*/)
        {
            return std::malloc(byteSize);
        }
        void EBusEnvironmentAllocator::deallocate(void* ptr, std::size_t /*byteSize*/, std::size_t /*alignment*/)
        {
            std::free(ptr);
        }

        EnvironmentVariableResult GetVariable(u32 /*guid*/)
        {
            return EnvironmentVariableResult{};
        }
        u32 EnvironmentVariableNameToId(const char* name)
        {
            // FNV-1a 32-bit — deterministic but never actually looked up (table is empty).
            u32 h = 0x811C9DC5u;
            if (name) { while (*name) { h ^= static_cast<unsigned char>(*name++); h *= 0x01000193u; } }
            return h;
        }
    } // namespace Internal

    namespace Environment
    {
        bool IsReady() { return false; }
        Internal::EnvironmentInterface* GetInstance() { return nullptr; }
        void* GetModuleId() { return nullptr; }
        void  Attach(Internal::EnvironmentInterface* /*sourceEnvironment*/, bool /*useAsGetFallback*/) {}
        void  Detach() {}
    }

    bool EBusEnvironment::InsertContext(int /*slotId*/, Internal::ContextBase* /*context*/, bool /*takeOwnership*/) { return false; }
    Internal::ContextBase* EBusEnvironment::FindContext(int /*slotId*/) { return nullptr; }

    // AllocatorManager ctor/dtor are private in the header — we provide out-of-line
    // empty bodies here. Definitions are legal outside the class even when declared
    // private; only *calls* from non-friends are forbidden, and the static Instance()
    // below calls them from inside the class's own function scope.
    AllocatorManager::MemoryBreak::MemoryBreak() {}
    AllocatorManager::AllocatorManager() {}
    AllocatorManager::~AllocatorManager() {}

    AllocatorManager& AllocatorManager::Instance()
    {
        static AllocatorManager s_instance;
        return s_instance;
    }

    bool AllocatorBase::IsReady() const { return false; }

    // Uuid stub — CryPhysics only uses Uuid for type-info strings in dead (edit-only) paths.
    Uuid Uuid::CreateString(const char* /*string*/, std::size_t /*stringLength*/)
    {
        return Uuid{};
    }
    int Uuid::ToString(char* output, int outputSize, bool /*isBrackets*/, bool /*isDashes*/) const
    {
        if (output && outputSize > 0) { output[0] = '\0'; }
        return 1;
    }

    // ConsoleFunctorBase static list heads — CryPhysics doesn't register CVars; both stay null/false.
    ConsoleFunctorBase*     ConsoleFunctorBase::s_deferredHead = nullptr;
    bool                    ConsoleFunctorBase::s_deferredHeadInvoked = false;
} // namespace AZ

namespace AZStd
{
    // AZStd::allocator — the unspecialized allocator used in a handful of AzCore containers.
    // Replace with malloc/free. Alignment is honored via _aligned_malloc / std::aligned_alloc.
    void* allocator::allocate(std::size_t byteSize, std::size_t alignment, int /*flags*/)
    {
#if defined(_MSC_VER)
        return alignment ? _aligned_malloc(byteSize, alignment ? alignment : sizeof(void*) * 2) : std::malloc(byteSize);
#else
        if (alignment == 0) alignment = sizeof(void*) * 2;
        // aligned_alloc requires size to be a multiple of alignment.
        std::size_t padded = (byteSize + alignment - 1) & ~(alignment - 1);
        return std::aligned_alloc(alignment, padded ? padded : alignment);
#endif
    }
    void allocator::deallocate(void* ptr, std::size_t /*byteSize*/, std::size_t /*alignment*/)
    {
#if defined(_MSC_VER)
        _aligned_free(ptr);
#else
        std::free(ptr);
#endif
    }

    // Simple prime-based bucket growth — matches AzCore's unordered-map implementation shape.
    std::size_t hash_next_bucket_size(std::size_t current)
    {
        static const std::size_t kPrimes[] = {
            17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853, 87719,
            175447, 350899, 701819, 1403641, 2807303, 5614657, 11229331, 22458671
        };
        for (std::size_t p : kPrimes) { if (p > current) return p; }
        return current * 2 + 1;
    }
} // namespace AZStd
