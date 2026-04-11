// Port of CryPhysics multi-threaded stepping infrastructure
// Original: Copyright Crytek GMBH, used under license
// Ports the worker thread model from physicalworld.cpp (PhysicsWorkerThread, m_nWorkerThreads)

namespace CryPhysics.Threading;

/// <summary>
/// Represents a unit of work for physics stepping.
/// Port of the per-entity work items dispatched in TimeStep from physicalworld.cpp.
/// </summary>
public sealed class PhysicsWorkItem
{
    /// <summary>Action to execute on a worker thread.</summary>
    public Action? Work { get; set; }
}

/// <summary>
/// Thread pool dedicated to physics simulation.
/// Port of the CryPhysics worker thread model (PhysicsWorkerThread, m_nWorkerThreads).
///
/// Provides barrier-synchronized multi-phase stepping:
///   Phase 1: StartStep all entities (parallel)
///   Phase 2: CheckForNewContacts (parallel with thread-safe grid)
///   Phase 3: Contact solving (sequential)
///   Phase 4: Update positions (parallel)
/// </summary>
public sealed class PhysicsThreadPool : IDisposable
{
    private readonly Thread[] _workers;
    private readonly BlockingWorkQueue _queue;
    private readonly Barrier _barrier;
    private volatile bool _shutdown;
    private readonly int _threadCount;

    /// <summary>Number of worker threads.</summary>
    public int ThreadCount => _threadCount;

    /// <summary>
    /// Create a physics thread pool.
    /// Port of the thread spawning in CPhysicalWorld::TimeStep when m_vars.numThreads changes.
    /// </summary>
    /// <param name="threadCount">Number of worker threads. Clamped to [1, MaxPhysThreads-1].</param>
    public PhysicsThreadPool(int threadCount)
    {
        _threadCount = System.Math.Clamp(threadCount, 1, PhysicsThreading.MaxPhysThreads - 1);
        _queue = new BlockingWorkQueue();
        // Barrier includes all workers + the caller thread
        _barrier = new Barrier(_threadCount + 1);
        _workers = new Thread[_threadCount];

        for (int i = 0; i < _threadCount; i++)
        {
            int workerIdx = i + 1; // index 0 is reserved for the main thread
            _workers[i] = new Thread(() => WorkerLoop(workerIdx))
            {
                Name = $"PhysicsWorkerThread_{workerIdx}",
                IsBackground = true
            };
            _workers[i].Start();
        }
    }

    /// <summary>
    /// Enqueue a batch of work items and wait for all to complete.
    /// This is the main dispatch mechanism for parallel entity stepping.
    /// </summary>
    public void ExecuteBatch(IReadOnlyList<Action> workItems)
    {
        if (workItems.Count == 0) return;

        int remaining = workItems.Count;
        using var done = new ManualResetEventSlim(false);

        for (int i = 0; i < workItems.Count; i++)
        {
            var work = workItems[i];
            _queue.Enqueue(() =>
            {
                work();
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            });
        }

        // Main thread helps process work too
        while (!done.IsSet)
        {
            if (_queue.TryDequeue(out var item))
                item();
            else
                done.Wait(1);
        }
    }

    /// <summary>
    /// Execute a parallel-for over a range, distributing work across the pool.
    /// Port of the entity-loop parallelization in CPhysicalWorld::TimeStep.
    /// </summary>
    public void ParallelFor(int fromInclusive, int toExclusive, Action<int> body)
    {
        int count = toExclusive - fromInclusive;
        if (count <= 0) return;

        if (count == 1)
        {
            body(fromInclusive);
            return;
        }

        int remaining = count;
        using var done = new ManualResetEventSlim(false);

        for (int i = fromInclusive; i < toExclusive; i++)
        {
            int idx = i;
            _queue.Enqueue(() =>
            {
                body(idx);
                if (Interlocked.Decrement(ref remaining) == 0)
                    done.Set();
            });
        }

        // Main thread helps drain the queue
        while (!done.IsSet)
        {
            if (_queue.TryDequeue(out var item))
                item();
            else
                done.Wait(1);
        }
    }

    /// <summary>
    /// Signal a barrier synchronization point.
    /// All worker threads and the caller block until everyone arrives.
    /// Port of the phase barriers in CPhysicalWorld::TimeStep.
    /// </summary>
    public void BarrierSync()
    {
        _barrier.SignalAndWait();
    }

    private void WorkerLoop(int workerIdx)
    {
        PhysicsThreading.MarkAsPhysThread(workerIdx);

        while (!_shutdown)
        {
            if (_queue.TryDequeue(out var work, timeoutMs: 10))
            {
                work();
            }
        }
    }

    public void Dispose()
    {
        _shutdown = true;
        for (int i = 0; i < _workers.Length; i++)
        {
            _workers[i].Join(timeout: TimeSpan.FromSeconds(2));
        }
        _barrier.Dispose();
        _queue.Dispose();
    }
}

/// <summary>
/// Simple thread-safe work queue for the physics thread pool.
/// </summary>
internal sealed class BlockingWorkQueue : IDisposable
{
    private readonly Queue<Action> _queue = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(Action item)
    {
        lock (_lock)
        {
            _queue.Enqueue(item);
        }
        _signal.Release();
    }

    public bool TryDequeue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Action? item, int timeoutMs = 0)
    {
        if (_signal.Wait(timeoutMs))
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    item = _queue.Dequeue();
                    return true;
                }
            }
        }
        item = null;
        return false;
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}

/// <summary>
/// Represents a group of interacting entities (an island) that must be stepped together.
/// Port of the island concept from CPhysicalWorld::TimeStep where connected rigid bodies
/// are grouped for coherent contact solving.
/// </summary>
public sealed class EntityIsland
{
    private readonly List<Entities.IPhysicalEntity> _entities = new();

    /// <summary>Entities in this island.</summary>
    public IReadOnlyList<Entities.IPhysicalEntity> Entities => _entities;

    /// <summary>Add an entity to this island.</summary>
    public void Add(Entities.IPhysicalEntity entity) => _entities.Add(entity);

    /// <summary>Number of entities in the island.</summary>
    public int Count => _entities.Count;

    /// <summary>Clear the island for reuse.</summary>
    public void Clear() => _entities.Clear();
}
