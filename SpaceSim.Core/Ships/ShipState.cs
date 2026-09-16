using System.Numerics;

namespace SpaceSim.Core.Ships;

/// <summary>3D data model. Only the core can mutate authoritative state.</summary>
public sealed class ShipState
{
    public Vector3 Position { get; internal set; }
    public Vector3 Velocity { get; internal set; }
    public Quaternion Rotation { get; internal set; } = Quaternion.Identity;
    public Vector3 AngularVelocity { get; internal set; }
    public float MassKg { get; }
    public float YawMomentOfInertia { get; }
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);

    internal ShipState(float massKg, float yawMomentOfInertia)
    {
        MassKg = massKg;
        YawMomentOfInertia = yawMomentOfInertia;
    }
}

public readonly record struct ShipInitialState(
    Vector3 Position = default,
    Vector3 Velocity = default,
    float YawRadians = 0f,
    float YawRateRadiansPerSecond = 0f);
