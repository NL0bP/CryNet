// Literal port of dev/Code/CryEngine/CryAISystem/PolygonSetOps/Utils.h
// Original Copyright Crytek GMBH or its affiliates, used under license.

namespace CryAISystem.PolygonSetOps;

// typedef Vec2d Vector2d;
// CryEngine Vec2d is double-precision 2D vector. CryPhysics.Sharp doesn't have a double-precision
// PhysVector2 (only single-precision). Use System.Numerics-style local struct that mirrors C++ Vec2d.
public struct Vector2d : System.IComparable<Vector2d>
{
    public double x, y;

    public int CompareTo(Vector2d rhs)
    {
        if (x < rhs.x) return -1;
        if (x > rhs.x) return 1;
        if (y < rhs.y) return -1;
        if (y > rhs.y) return 1;
        return 0;
    }

    public Vector2d(double xv, double yv) { x = xv; y = yv; }

    public static Vector2d operator -(Vector2d a, Vector2d b) => new Vector2d(a.x - b.x, a.y - b.y);
    public static Vector2d operator +(Vector2d a, Vector2d b) => new Vector2d(a.x + b.x, a.y + b.y);
    public static Vector2d operator *(Vector2d a, double s) => new Vector2d(a.x * s, a.y * s);

    public double Dot(Vector2d rhs) => x * rhs.x + y * rhs.y;
    public double GetLength() => System.Math.Sqrt(x * x + y * y);
    public Vector2d Perp() => new Vector2d(-y, x);

    // needed to use Vector2d in a map
    public static bool operator <(Vector2d op1, Vector2d op2)
    {
        if (op1.x < op2.x)
            return true;
        else if (op1.x > op2.x)
            return false;
        if (op1.y < op2.y)
            return true;
        else
            return false;
    }
    public static bool operator >(Vector2d op1, Vector2d op2)
    {
        if (op1.x > op2.x) return true;
        if (op1.x < op2.x) return false;
        if (op1.y > op2.y) return true;
        return false;
    }
}
