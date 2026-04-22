using CryPhysics.Native.Marshal;

namespace CryPhysics.Native;

public sealed class CryPhysicsException : Exception
{
    public CpResult Code { get; }

    public CryPhysicsException(CpResult code, string message) : base($"{message} (code={code})")
    {
        Code = code;
    }

    internal static void Throw(int rawCode, string context)
    {
        var code = (CpResult)rawCode;
        if (code == CpResult.Ok) return;
        throw new CryPhysicsException(code, context);
    }
}
