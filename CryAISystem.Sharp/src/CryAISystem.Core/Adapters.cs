// Literal port of dev/Code/CryEngine/CryAISystem/Adapters.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Implements adapters for AI objects from external interfaces to internal
//               This is purely a translation layer without concrete instances
//               They can have no state and must remain abstract

namespace CryAISystem;

public static class Adapters
{
    public static CWeakRef<CAIObject> GetWeakRefSafe(IAIObject pObj)
    {
        return (pObj != null ? WeakRefHelpers.GetWeakRef((CAIObject)pObj) : new CWeakRef<CAIObject>(type_nil_ref.NILREF));
    }
}

public abstract class CPipeUserAdapter : IPipeUser
{
    // virtual ~CPipeUserAdapter() {}

    public abstract bool SelectPipe(int id, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, bool resetAlways = false, GoalParams node = null);
    public abstract IGoalPipe InsertSubPipe(int id, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, GoalParams node = null);

    public abstract string GetName();
    public abstract void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData data);
    public abstract uint8 GetFactionID();
    public abstract CPuppet CastToCPuppet();
    public abstract ushort GetAIType();

    private bool SelectPipe(int id, string name, IAIObject pArgument = null, int goalPipeId = 0, bool resetAlways = false, GoalParams node = null)
    { return SelectPipe(id, name, Adapters.GetWeakRefSafe(pArgument), goalPipeId, resetAlways, node); }

    private IGoalPipe InsertSubPipe(int id, string name, IAIObject pArgument = null, int goalPipeId = 0, GoalParams node = null)
    { return InsertSubPipe(id, name, Adapters.GetWeakRefSafe(pArgument), goalPipeId, node); }
}

public abstract class CAIGroupAdapter : IAIGroup
{
    public abstract CWeakRef<CAIObject> GetAttentionTarget(bool bHostileOnly = false, bool bLiveOnly = false, CWeakRef<CAIObject> refSkipTarget = null);

    private IAIObject GetAttentionTargetIface(bool bHostileOnly = false, bool bLiveOnly = false)
    {
        CWeakRef<CAIObject> refTarget = GetAttentionTarget(bHostileOnly, bLiveOnly, new CWeakRef<CAIObject>(type_nil_ref.NILREF));
        return refTarget.GetIAIObject();
    }
}

// Forward decls for IPipeUser, IAIGroup, IGoalPipe, GoalParams (CryCommon ports pending)
public interface IPipeUser : IAIObject { }
public interface IAIGroup { }
public interface IGoalPipe { void ParseParams(GoalParams param); }
// GoalParams — partial port of IGoalPipe.h GoalParams (XML-tree-of-key-value pairs).
public class GoalParams
{
    private string m_name = "";
    private object m_value;
    private System.Collections.Generic.List<GoalParams> m_children = new System.Collections.Generic.List<GoalParams>();
    public void SetName(string name) { m_name = name; }
    public void SetValue(object v) { m_value = v; }
    public void SetValue(Vec3 v) { m_value = v; }
    public void SetValue(uint v) { m_value = v; }
    public void AddChild(GoalParams c) { m_children.Add(c); }
}
