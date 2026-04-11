// Global type aliases mirroring CryEngine integer typedefs (BaseTypes.h).
// These are not stubs — they map the C++ fundamental typedefs that the
// engine relies on (uint8, uint16, uint32, uint64, int8, int16, int32, int64)
// into their C# equivalents so that line-by-line ports preserve the original
// declarations verbatim.

global using global::CryAISystem.CryCommon;
global using static global::CryAISystem.GlobalFunctions;

global using uint8 = System.Byte;
global using uint16 = System.UInt16;
global using uint32 = System.UInt32;
global using uint64 = System.UInt64;
global using int8 = System.SByte;
global using int16 = System.Int16;
global using int32 = System.Int32;
global using int64 = System.Int64;
global using f32 = System.Single;
global using f64 = System.Double;
global using size_t = System.UInt64;
