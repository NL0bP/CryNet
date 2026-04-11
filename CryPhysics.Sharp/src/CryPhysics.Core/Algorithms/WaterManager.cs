// Port of CryPhysics waterman.h/cpp - water surface simulation
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;

namespace CryPhysics.Algorithms;

/// <summary>
/// Water tile data. Port of SWaterTile from CryEngine.
/// </summary>
public class WaterTile
{
    public float[] Heights;
    public PhysVector3[] Velocities;
    public PhysVector2[] MomentumVec;
    public float[] Mass;
    public byte[] Normals;
    public int NCells;
    public bool Active;

    public WaterTile(int nCells)
    {
        NCells = nCells;
        int n = nCells * nCells;
        Heights = new float[n];
        Velocities = new PhysVector3[n];
        MomentumVec = new PhysVector2[n];
        Mass = new float[n];
        Normals = new byte[n];
    }

    public void Activate(bool active = true)
    {
        if (Active && !active) Zero();
        Active = active;
    }

    public void Zero()
    {
        int n = NCells * NCells;
        Array.Clear(Heights, 0, n);
        Array.Clear(Velocities, 0, n);
        Array.Clear(Normals, 0, n);
    }

    public bool CellUsed(int i) => Normals[i] < 255;
}

/// <summary>
/// Water surface manager. Port of CWaterMan from CryEngine.
/// Simulates interactive water surfaces with wave propagation.
/// </summary>
public class WaterManager
{
    // World reference
    public IDisposable? World;

    // State
    public bool Active;
    public float TimeSurplus;
    public float Dt;

    // Grid configuration
    public int NTiles;
    public int NCells;
    public float TileSize;
    public float RTileSize;     // 1/tileSize
    public float CellSize;
    public float RCellSize;     // 1/cellSize

    // Wave parameters
    public float WaveSpeed = 4f;
    public float DampingCenter = 0.3f;
    public float DampingRim = 0.6f;
    public float MinHSpread = 0.01f;
    public float MinVel = 0.01f;
    public float KResistance = 0.5f;
    public float Depth = 10f;
    public float HLimit = 1f;
    public float DOffs;

    // Spatial data
    public PhysVector3 Origin;
    public PhysVector3 WaterOrigin;
    public PhysVector3 PosViewer;
    public int Ix, Iy;
    public PhysMatrix33 R = PhysMatrix33.Identity;

    // Tile storage
    public WaterTile?[] Tiles = Array.Empty<WaterTile?>();
    public WaterTile?[] TilesTmp = Array.Empty<WaterTile?>();

    public WaterManager(int nTiles = 3, int nCells = 16, float tileSize = 4f)
    {
        NTiles = nTiles;
        NCells = nCells;
        TileSize = tileSize;
        RTileSize = 1f / tileSize;
        CellSize = tileSize / nCells;
        RCellSize = nCells / tileSize;

        int totalTiles = (nTiles * 2 + 1) * (nTiles * 2 + 1);
        Tiles = new WaterTile?[totalTiles];
        TilesTmp = new WaterTile?[totalTiles];
    }

    /// <summary>Notify of entity interaction with water.</summary>
    public void OnWaterInteraction(PhysVector3 pos, PhysVector3 vel, float radius)
    {
        // Create disturbance at position
        var localPos = R * (pos - Origin);
        int tx = (int)(localPos.X * RTileSize) + NTiles;
        int ty = (int)(localPos.Y * RTileSize) + NTiles;
        int tileIdx = GetTileIndex(tx, ty);

        if (tileIdx >= 0 && tileIdx < Tiles.Length)
        {
            var tile = Tiles[tileIdx] ??= new WaterTile(NCells);
            tile.Activate();

            float cx = (localPos.X - (tx - NTiles) * TileSize) * RCellSize;
            float cy = (localPos.Y - (ty - NTiles) * TileSize) * RCellSize;
            int icx = (int)cx, icy = (int)cy;

            if (icx >= 0 && icx < NCells && icy >= 0 && icy < NCells)
            {
                int idx = icy * NCells + icx;
                tile.Heights[idx] += vel.Z * 0.1f;
                tile.Velocities[idx] = tile.Velocities[idx] + vel * 0.05f;
            }
        }
    }

    /// <summary>Step the water simulation forward.</summary>
    public void TimeStep(float dt)
    {
        if (dt <= 0) return;
        Dt = dt;

        float c2 = WaveSpeed * WaveSpeed * dt * dt;
        float damping = 1f - DampingCenter * dt;

        int totalTiles = (NTiles * 2 + 1) * (NTiles * 2 + 1);
        for (int ti = 0; ti < totalTiles; ti++)
        {
            var tile = Tiles[ti];
            if (tile == null || !tile.Active) continue;

            // Wave equation: h_new = 2*h - h_old + c^2*(laplacian)
            for (int y = 1; y < NCells - 1; y++)
            {
                for (int x = 1; x < NCells - 1; x++)
                {
                    int i = y * NCells + x;
                    float laplacian =
                        tile.Heights[i - 1] + tile.Heights[i + 1] +
                        tile.Heights[i - NCells] + tile.Heights[i + NCells] -
                        4f * tile.Heights[i];

                    float vel = tile.Velocities[i].Z + c2 * laplacian;
                    vel *= damping;
                    tile.Heights[i] += vel * dt;
                    tile.Velocities[i] = new PhysVector3(0, 0, vel);

                    // Clamp height
                    tile.Heights[i] = System.Math.Clamp(tile.Heights[i], -HLimit, HLimit);
                }
            }

            // Check if tile is still active
            bool stillActive = false;
            for (int i = 0; i < NCells * NCells; i++)
            {
                if (MathF.Abs(tile.Heights[i]) > MinHSpread ||
                    MathF.Abs(tile.Velocities[i].Z) > MinVel)
                {
                    stillActive = true;
                    break;
                }
            }
            if (!stillActive) tile.Activate(false);
        }
    }

    /// <summary>Get water height at a point.</summary>
    public float GetHeight(PhysVector2 pt)
    {
        var localPos = new PhysVector3(pt.X, pt.Y, 0);
        localPos = R * (localPos - new PhysVector3(Origin.X, Origin.Y, 0));

        int tx = (int)(localPos.X * RTileSize) + NTiles;
        int ty = (int)(localPos.Y * RTileSize) + NTiles;
        int tileIdx = GetTileIndex(tx, ty);

        if (tileIdx < 0 || tileIdx >= Tiles.Length || Tiles[tileIdx] == null || !Tiles[tileIdx]!.Active)
            return 0;

        var tile = Tiles[tileIdx]!;
        float cx = (localPos.X - (tx - NTiles) * TileSize) * RCellSize;
        float cy = (localPos.Y - (ty - NTiles) * TileSize) * RCellSize;
        int icx = System.Math.Clamp((int)cx, 0, NCells - 1);
        int icy = System.Math.Clamp((int)cy, 0, NCells - 1);

        return tile.Heights[icy * NCells + icx] + DOffs;
    }

    /// <summary>Reset all water tiles.</summary>
    public void Reset()
    {
        foreach (var tile in Tiles)
            tile?.Activate(false);
        DOffs = 0;
    }

    /// <summary>Check if any tile is active.</summary>
    public bool IsActive()
    {
        foreach (var tile in Tiles)
            if (tile != null && tile.Active)
                return true;
        return false;
    }

    private int GetTileIndex(int tx, int ty)
    {
        int w = NTiles * 2 + 1;
        if (tx < 0 || tx >= w || ty < 0 || ty >= w) return -1;
        return ty * w + tx;
    }
}
