// Literal port of the queued-RWI surface from dev/Code/CryEngine/CryPhysics/rwi.cpp.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Entities;
using CryPhysics.Events;
using CryPhysics.Math;

namespace CryPhysics.World;

/// <summary>
/// Async ray request entry. Port of `SRwiRequest` from physicalworld.h.
/// Held in `m_rwiQueue` until <see cref="PhysicalWorld.TracePendingRays"/> drains it.
/// </summary>
public class SRwiRequest
{
    public PhysicsForeignData PForeignData;
    public int IForeignData;
    public PhysVector3 Org;
    public PhysVector3 Dir;
    public int ObjTypes;
    public uint Flags;
    public RayHit[]? Hits;
    public int NMaxHits;
    public int[] IdSkipEnts = new int[8]; // matches sizeof(idSkipEnts) in C++
    public int NSkipEnts;
    public int ICaller;
    public RayHit? PHitLast;
    public System.Action<EventPhysRWIResult>? OnEvent;
}

/// <summary>
/// Queued-RWI flag bit. Port of `enum { rwi_queue = 0x40000000 }` from physinterface.h.
/// </summary>
public static class RwiFlags
{
    public const uint RwiQueue = 0x40000000;
    public const uint RwiUpdateLastHit = 0x10000000;
}

public partial class PhysicalWorld
{
    // Queue + lock — port of m_rwiQueue/m_rwiQueueSz/m_rwiQueueAlloc/m_rwiQueueHead/m_rwiQueueTail/m_lockRwiQueue.
    private readonly System.Collections.Generic.Queue<SRwiRequest> _rwiQueue = new();
    private readonly object _lockRwiQueue = new();

    /// <summary>
    /// Queue an asynchronous ray-world-intersection request. Port of the
    /// `IF (rp.flags & rwi_queue, 0)` branch in CPhysicalWorld::RayWorldIntersection
    /// (rwi.cpp:447-486). The actual ray test runs later in <see cref="TracePendingRays"/>;
    /// the result is dispatched as an EventPhysRWIResult.
    /// </summary>
    public int RayWorldIntersectionAsync(in PhysVector3 origin, in PhysVector3 dir, int objTypes,
        uint flags, int maxHits, IPhysicalEntity[]? skipEnts = null,
        PhysicsForeignData foreignData = default, int iForeignData = 0,
        System.Action<EventPhysRWIResult>? onEvent = null)
    {
        if (dir.LengthSq() <= 0) return 0;

        var req = new SRwiRequest
        {
            PForeignData = foreignData,
            IForeignData = iForeignData,
            Org = origin,
            Dir = dir,
            ObjTypes = objTypes,
            Flags = flags & ~RwiFlags.RwiQueue,
            Hits = null, // result buffer allocated when the request is processed
            NMaxHits = maxHits,
            ICaller = Threading.PhysicsThreading.GetICaller(),
            OnEvent = onEvent,
        };

        if (skipEnts != null)
        {
            req.NSkipEnts = System.Math.Min(req.IdSkipEnts.Length, skipEnts.Length);
            for (int i = 0; i < req.NSkipEnts; i++)
                req.IdSkipEnts[i] = skipEnts[i]?.Id ?? -3;
        }

        lock (_lockRwiQueue) _rwiQueue.Enqueue(req);
        return 1;
    }

    /// <summary>
    /// Drain the queued RWI requests. Port of CPhysicalWorld::TracePendingRays
    /// (rwi.cpp:652-723). Each request is executed via the immediate-mode
    /// <see cref="RayWorldIntersection"/> and dispatched as an EventPhysRWIResult.
    /// </summary>
    /// <param name="doTracing">When false, skips the actual intersection (only flushes events).</param>
    public int TracePendingRays(bool doTracing = true)
    {
        int nProcessed = 0;
        var skipBuf = new IPhysicalEntity[8];

        while (true)
        {
            SRwiRequest req;
            lock (_lockRwiQueue)
            {
                if (_rwiQueue.Count == 0) break;
                req = _rwiQueue.Dequeue();
            }
            nProcessed++;

            // Resolve skip-entity IDs back to references — port of
            // `pSkipEnts[i] = GetPhysicalEntityById(curreq.idSkipEnts[i])`.
            int nSkip = 0;
            for (int i = 0; i < req.NSkipEnts; i++)
            {
                if (_entityById.TryGetValue(req.IdSkipEnts[i], out var ent))
                    skipBuf[nSkip++] = ent;
            }
            var skipArr = new IPhysicalEntity[nSkip];
            System.Array.Copy(skipBuf, skipArr, nSkip);

            req.Hits ??= new RayHit[req.NMaxHits];
            int nHits = doTracing
                ? RayWorldIntersection(req.Org, req.Dir, req.ObjTypes, req.Flags, req.Hits, req.NMaxHits, skipArr)
                : 0;

            // Dispatch result event — port of OnEvent(0, &eprr).
            var eprr = new EventPhysRWIResult
            {
                Entity = StaticPhysicalEntity,
                ForeignData = req.PForeignData.AsObject(),
                ForeignDataType = req.IForeignData,
                Hits = req.Hits,
                NHits = nHits,
                NMaxHits = req.NMaxHits,
            };
            req.OnEvent?.Invoke(eprr);
            DispatchEvent(eprr);
        }

        return nProcessed;
    }
}
