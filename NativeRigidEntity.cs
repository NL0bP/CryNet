using CryPhysics.Native.Handles;
using CryPhysics.Native.Marshal;

namespace CryPhysics.Native;

public sealed class NativeRigidEntity : IDisposable
{
    private readonly EntityHandle _handle;

    internal NativeRigidEntity(EntityHandle handle)
    {
        _handle = handle;
    }

    public void AddGeometry(NativeGeometry geom, in PartParams part)
    {
        var rc = NativeMethods.EntityAddGeometry(_handle.Raw, geom.Raw, part);
        CryPhysicsException.Throw(rc, "entity_add_geometry");
    }

    public CpVec3 GetPos()
    {
        var rc = NativeMethods.EntityGetPos(_handle.Raw, out var p);
        CryPhysicsException.Throw(rc, "entity_get_pos");
        return p;
    }

    public CpQuat GetOrientation()
    {
        var rc = NativeMethods.EntityGetQuat(_handle.Raw, out var q);
        CryPhysicsException.Throw(rc, "entity_get_quat");
        return q;
    }

    public (CpVec3 linear, CpVec3 angular) GetVelocity()
    {
        var rc = NativeMethods.EntityGetVelocity(_handle.Raw, out var lin, out var ang);
        CryPhysicsException.Throw(rc, "entity_get_velocity");
        return (lin, ang);
    }

    /// <summary>
    /// Ship-relevant dynamics snapshot. Use this over GetVelocity when you also
    /// need mass / submergedFraction — one native call reads them together out
    /// of pe_status_dynamics so the driver path stays cheap.
    /// </summary>
    public CpDynamics GetDynamics()
    {
        var rc = NativeMethods.EntityGetDynamics(_handle.Raw, out var d);
        CryPhysicsException.Throw(rc, "entity_get_dynamics");
        return d;
    }

    public void SetSimulationParams(in CpSimParams p)
    {
        var rc = NativeMethods.EntitySetSimulationParams(_handle.Raw, p);
        CryPhysicsException.Throw(rc, "entity_set_simulation_params");
    }

    public void SetBuoyancyParams(in CpBuoyancyParams p)
    {
        var rc = NativeMethods.EntitySetBuoyancyParams(_handle.Raw, p);
        CryPhysicsException.Throw(rc, "entity_set_buoyancy_params");
    }

    /// <summary>
    /// LY applies <c>flagsNew = flagsOld &amp; flagsAnd | flagsOr</c>. Use
    /// <c>flagsAnd = ~0u</c> (default) to leave existing bits alone.
    /// </summary>
    public void SetFlags(uint flagsOr, uint flagsAnd = uint.MaxValue)
    {
        var rc = NativeMethods.EntitySetFlags(_handle.Raw, flagsOr, flagsAnd);
        CryPhysicsException.Throw(rc, "entity_set_flags");
    }

    public void SetParams(int paramsType, nint blob, nuint len)
    {
        var rc = NativeMethods.EntitySetParams(_handle.Raw, paramsType, blob, len);
        CryPhysicsException.Throw(rc, "entity_set_params");
    }

    public void DoAction(int actionType, nint blob, nuint len)
    {
        var rc = NativeMethods.EntityDoAction(_handle.Raw, actionType, blob, len);
        CryPhysicsException.Throw(rc, "entity_do_action");
    }

    public void GetStatus(int statusType, nint output, nuint len)
    {
        var rc = NativeMethods.EntityGetStatus(_handle.Raw, statusType, output, len);
        CryPhysicsException.Throw(rc, "entity_get_status");
    }

    /// <summary>
    /// Applies an impulse to the entity. Mirrors pe_action_impulse — the
    /// angular impulse, point of application, and part id are optional; leave
    /// them unset (default CpImpulse) when you only want a linear impulse on
    /// the whole body.
    /// </summary>
    public void ApplyImpulse(in CpImpulse imp)
    {
        var rc = NativeMethods.EntityApplyImpulse(_handle.Raw, imp);
        CryPhysicsException.Throw(rc, "entity_apply_impulse");
    }

    /// <summary>Teleport / reorient. Pass null for a component you want unchanged.</summary>
    public unsafe void SetPose(CpVec3? pos, CpQuat? rot)
    {
        var hasPos = pos.HasValue;
        var hasRot = rot.HasValue;
        var p = hasPos ? pos!.Value : default;
        var q = hasRot ? rot!.Value : default;
        var rc = NativeMethods.EntitySetPose(_handle.Raw, hasPos ? &p : null, hasRot ? &q : null);
        CryPhysicsException.Throw(rc, "entity_set_pose");
    }

    /// <summary>Hard velocity override. Pass null for a component you want unchanged.</summary>
    public unsafe void SetVelocity(CpVec3? linVel, CpVec3? angVel)
    {
        var hasLin = linVel.HasValue;
        var hasAng = angVel.HasValue;
        var lv = hasLin ? linVel!.Value : default;
        var av = hasAng ? angVel!.Value : default;
        var rc = NativeMethods.EntitySetVelocity(_handle.Raw, hasLin ? &lv : null, hasAng ? &av : null);
        CryPhysicsException.Throw(rc, "entity_set_velocity");
    }

    internal nint Raw => _handle.Raw;

    public void Dispose()
    {
        _handle.Dispose();
    }
}
