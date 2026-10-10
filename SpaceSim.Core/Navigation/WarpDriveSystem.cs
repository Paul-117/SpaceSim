using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Power;
using SpaceSim.Core.Combat;

namespace SpaceSim.Core.Navigation;

internal static class WarpDriveSystem
{
    public static void Step(WorldState world, NavigationCommand command, SimulationSettings settings,
        List<SimulationEvent> events, Random navigationRandom)
    {
        if (command.QuickStartEncounterId is int quickStartEncounterId)
        {
            EncounterState? destination = world.Encounters.FirstOrDefault(encounter => encounter.Id == quickStartEncounterId);
            EnemyShipState? enemy = destination?.Enemies.FirstOrDefault(candidate => !candidate.IsDestroyed);
            if (destination is not null && enemy is not null && float.IsFinite(command.QuickStartDistanceMeters) &&
                command.QuickStartDistanceMeters > settings.ShipCollisionDistanceMeters)
            {
                int quickStartOriginId = world.CurrentEncounter.Id;
                world.CurrentEncounter = destination;
                EnterRealSpace(world, enemy.Ship.Position + Vector3.UnitZ * command.QuickStartDistanceMeters);
                events.Add(new EncounterChanged(quickStartOriginId, destination.Id));
            }
            return;
        }

        var drive = world.WarpDrive;
        drive.ChargedSeconds = Math.Min(settings.WarpChargeSeconds,
            drive.ChargedSeconds + 1.0 / SimulationSettings.TickRate);
        if (drive.ChargedSeconds + 1e-10 >= settings.WarpChargeSeconds)
            drive.ChargedSeconds = settings.WarpChargeSeconds;
        drive.IsReady = drive.ChargedSeconds >= settings.WarpChargeSeconds;
        drive.ChargeFraction = (float)(drive.ChargedSeconds / settings.WarpChargeSeconds);
        drive.RemainingSeconds = settings.WarpChargeSeconds - drive.ChargedSeconds;

        if (world.HyperspacePhase == HyperspacePhase.RealSpace && command.EnterHyperspace && drive.IsReady)
        {
            world.HyperspaceOriginEncounterId = world.CurrentEncounter.Id;
            world.HyperspacePhase = HyperspacePhase.SelectingDestination;
            Drain(drive, settings);
            events.Add(new EnteredHyperspace(world.CurrentEncounter.Id));
            return;
        }

        // At the initial game start there is no origin encounter, so Encounter 1 is a valid first destination too.
        bool maySelectCurrentEncounter = world.HyperspaceOriginEncounterId is null;
        if (world.HyperspacePhase == HyperspacePhase.SelectingDestination && command.JumpToEncounterId is { } destinationId &&
            (destinationId != world.CurrentEncounter.Id || maySelectCurrentEncounter))
        {
            var destination = world.Encounters.FirstOrDefault(e => e.Id == destinationId);
            if (destination is null) return;
            world.CurrentEncounter = destination;
            destination.CaptureLastKnownEnemyPosition(navigationRandom, 300f);
            world.HyperspacePhase = HyperspacePhase.PlanningEntry;
            return;
        }

        if (world.HyperspacePhase != HyperspacePhase.PlanningEntry || command.EntryPosition is not { } entry) return;
        int originId = world.HyperspaceOriginEncounterId ?? world.CurrentEncounter.Id;
        EnterRealSpace(world, entry);
        // Re-entry is the completed warp. The next charge cycle begins on the following simulation tick.
        Drain(drive, settings);
        events.Add(new SystemsRepaired(WeaponOwner.Player, null, world.Ship.Position));
        events.Add(new EncounterChanged(originId, world.CurrentEncounter.Id));
    }

    private static void EnterRealSpace(WorldState world, Vector3 entry)
    {
        world.Ship.Position = new Vector3(entry.X, 0f, entry.Z);
        world.Ship.Velocity = Vector3.Zero;
        world.Ship.Rotation = Quaternion.Identity;
        world.Ship.AngularVelocity = Vector3.Zero;
        world.Ship.Systems.Repair();
        ShieldSystem.RestoreFull(world.Ship.Shield);
        world.HyperspaceOriginEncounterId = null;
        world.HyperspacePhase = HyperspacePhase.RealSpace;
    }

    private static void Drain(WarpDriveState drive, SimulationSettings settings)
    {
        drive.ChargedSeconds = 0;
        drive.ChargeFraction = 0;
        drive.RemainingSeconds = settings.WarpChargeSeconds;
        drive.IsReady = false;
    }
}
