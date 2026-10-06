using System.Numerics;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;

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
    public ReactorState Reactor { get; }
    public PowerState Power { get; }
    public ShieldState Shield { get; }
    public HullState Hull { get; }
    public SubsystemState Systems { get; }
    /// <summary>Individual propulsion, lance and shield recharge values for this ship.</summary>
    public ShipTuning Tuning { get; }
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);

    internal ShipState(float massKg, float yawMomentOfInertia, ReactorState reactor, PowerState power, ShieldState shield,
        HullState hull, SubsystemState systems, ShipTuning tuning)
    {
        MassKg = massKg;
        YawMomentOfInertia = yawMomentOfInertia;
        Reactor = reactor;
        Power = power;
        Shield = shield;
        Hull = hull;
        Systems = systems;
        Tuning = tuning;
    }
}

public readonly record struct ShipInitialState(
    Vector3 Position = default,
    Vector3 Velocity = default,
    float YawRadians = 0f,
    float YawRateRadiansPerSecond = 0f);
