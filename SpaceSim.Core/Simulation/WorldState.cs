using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Combat;

namespace SpaceSim.Core.Simulation;

public sealed class WorldState
{
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public LanceAimState LanceAim { get; } = new();
    public Vector3 LanceDirection => LanceAimSystem.GetDirection(Ship.Forward, LanceAim.YawOffsetDegrees);
    public WarpDriveState WarpDrive { get; } = new();
    public HyperspacePhase HyperspacePhase { get; internal set; }
    public bool IsPlayerInRealSpace => HyperspacePhase == HyperspacePhase.RealSpace;
    public bool IsPlayerInHyperspace => !IsPlayerInRealSpace;
    public int? HyperspaceOriginEncounterId { get; internal set; }
    public IReadOnlyList<EncounterState> Encounters { get; }
    public EncounterState CurrentEncounter { get; internal set; }
    internal List<TargetState> MutableTargets => CurrentEncounter.MutableTargets;
    public IReadOnlyList<TargetState> Targets => CurrentEncounter.Targets;
    public int HitCount { get; internal set; }
    public GameState GameState { get; internal set; } = GameState.Running;
    public IEnumerable<EnemyShipState> CurrentEnemies => CurrentEncounter.Enemies.Where(enemy => !enemy.IsDestroyed);
    /// <summary>Bridge accessibility option; disabling it makes all active local contacts visible without Sensorium confirmation.</summary>
    public bool RequireSensoriumConfirmationForBridgeContacts { get; set; } = true;
    /// <summary>Sensorium-confirmed enemies available to bridge UI and controls.</summary>
    public IEnumerable<EnemyShipState> VisibleEnemies => CurrentEnemies
        .Where(enemy => !RequireSensoriumConfirmationForBridgeContacts ||
                        CurrentEncounter.IsBridgeContactVisible(enemy.EnemyId));
    /// <summary>Convenience access to the first active enemy; use CurrentEnemies for encounter logic.</summary>
    public EnemyShipState? CurrentEnemy => CurrentEnemies.FirstOrDefault();
    public long Tick { get; internal set; }
    public double TimeSeconds => (double)Tick / SimulationSettings.TickRate;

    internal WorldState(ShipState ship, EncounterState[] encounters)
    {
        Ship = ship;
        Encounters = Array.AsReadOnly(encounters);
        CurrentEncounter = encounters[0];
    }
}
