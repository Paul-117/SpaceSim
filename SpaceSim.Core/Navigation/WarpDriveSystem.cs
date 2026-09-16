using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Power;

namespace SpaceSim.Core.Navigation;

internal static class WarpDriveSystem
{
    public static void Step(WorldState world, NavigationCommand command, SimulationSettings settings,
        List<SimulationEvent> events)
    {
        var drive = world.WarpDrive;
        drive.ChargedSeconds = Math.Min(settings.WarpChargeSeconds,
            drive.ChargedSeconds + 1.0 / SimulationSettings.TickRate);
        if (drive.ChargedSeconds + 1e-10 >= settings.WarpChargeSeconds)
            drive.ChargedSeconds = settings.WarpChargeSeconds;
        drive.IsReady = drive.ChargedSeconds >= settings.WarpChargeSeconds;
        drive.ChargeFraction = (float)(drive.ChargedSeconds / settings.WarpChargeSeconds);
        drive.RemainingSeconds = settings.WarpChargeSeconds - drive.ChargedSeconds;

        if (!drive.IsReady || command.JumpToEncounterId is not { } destinationId ||
            destinationId == world.CurrentEncounter.Id) return;
        var destination = world.Encounters.FirstOrDefault(e => e.Id == destinationId);
        if (destination is null) return;

        int originId = world.CurrentEncounter.Id;
        world.CurrentEncounter = destination;
        // Each encounter has its own local origin; a jump arrives at rest.
        world.Ship.Position = Vector3.Zero;
        world.Ship.Velocity = Vector3.Zero;
        world.Ship.Rotation = Quaternion.Identity;
        world.Ship.AngularVelocity = Vector3.Zero;
        PowerDistributionSystem.ApplyProfile(world.Ship.Power, settings.Power.DefaultProfile);
        drive.ChargedSeconds = 0;
        drive.ChargeFraction = 0;
        drive.RemainingSeconds = settings.WarpChargeSeconds;
        drive.IsReady = false;
        events.Add(new EncounterChanged(originId, destinationId));
    }
}
