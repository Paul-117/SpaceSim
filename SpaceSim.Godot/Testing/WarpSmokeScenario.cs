using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.UI;
using NVector3 = System.Numerics.Vector3;

namespace SpaceSim.GodotClient.Testing;

/// <summary>Exercises real mouse input through the map while the flight scene keeps ticking.</summary>
internal sealed class WarpSmokeScenario(WorldState world, FlightHud hud, StarMap map)
{
    private NVector3 _driftStart;
    private int[] _survivingIds = Array.Empty<int>();
    private bool _mouseEntered;

    public ShipCommand BeforeTick()
    {
        map.Refresh();
        switch (world.Tick)
        {
            case 1:
                Click(hud.WarpButton);
                Check(map.Visible, "Warp Drive must open the map while uncharged.");
                Check(map.JumpButton.Disabled, "Jump requires an explicit destination.");
                break;
            case 2:
                Click(map.DestinationButton(2));
                Check(map.SelectedEncounterId == 2, "Mouse selection must select encounter 2.");
                Check(map.JumpButton.Disabled, "Early Jump must be disabled.");
                Click(map.JumpButton);
                break;
            case 4:
                Check(world.CurrentEncounter.Id == 1, "Disabled Jump must do nothing.");
                Click(map.CloseButton);
                Check(!map.Visible, "Close must return to flight.");
                Click(hud.WarpButton);
                Check(map.Visible, "Map can always reopen.");
                Check(map.SelectedEncounterId is null, "Reopening requires selecting a destination again.");
                Click(map.DestinationButton(2));
                break;
            case 570:
                Check(world.Targets.Count == 9 && world.HitCount == 1, "Hit through the live map must not respawn.");
                _survivingIds = world.Targets.Select(t => t.Id).ToArray();
                _driftStart = world.Ship.Position;
                break;
            case 600:
                Check(map.Visible && world.WarpDrive.IsReady, "Map must stay open while warp charges.");
                Check(NVector3.Distance(_driftStart, world.Ship.Position) > 1, "Flight must continue behind the map.");
                Check(world.Ship.AngularVelocity.Y > 0 && world.Lance.ChargeFraction > 0f,
                    "Rotation and lance charging must keep running.");
                Check(!map.JumpButton.Disabled, "Jump must become enabled after ten seconds.");
                Click(map.JumpButton);
                break;
            case 601:
                Check(world.CurrentEncounter.Id == 2 && world.Targets.Count == 0, "Jump must enter encounter 2.");
                Check(!map.Visible, "Successful Jump must close the map.");
                Check(world.Ship.Position == NVector3.Zero && world.Ship.Velocity == NVector3.Zero &&
                    world.Ship.AngularVelocity == NVector3.Zero, "Arrival must stop the ship at the origin.");
                Check(!world.WarpDrive.IsReady, "Jump must drain the drive.");
                Click(hud.WarpButton);
                Click(map.DestinationButton(1));
                Check(map.Visible && map.JumpButton.Disabled, "Map must reopen during recharge.");
                Click(map.JumpButton);
                break;
            case 1201:
                Check(world.CurrentEncounter.Id == 2, "Early return must not have been queued.");
                Check(!map.JumpButton.Disabled, "Return Jump must become available after recharging.");
                Click(map.JumpButton);
                break;
            case 1202:
                Check(world.CurrentEncounter.Id == 1, "Return Jump must enter encounter 1.");
                Check(world.Targets.Select(t => t.Id).SequenceEqual(_survivingIds), "Progress must survive the round trip.");
                Check(world.HitCount == 1 && world.CurrentEncounter.HitCount == 1, "Hit counts must survive travel.");
                Check(!map.Visible && !world.WarpDrive.IsReady, "Return must close map and consume charge.");
                break;
        }
        return new ShipCommand(MainThrust: world.Tick is >= 181 and < 210,
            YawLeft: world.Tick is >= 571 and < 581, FireLance: world.Tick == 540);
    }

    private void Click(Button button)
    {
        Vector2 position = button.GetGlobalRect().GetCenter();
        var viewport = button.GetViewport();
        // Headless windows have no OS cursor; deliver local mouse events to the real GUI.
        if (!_mouseEntered && DisplayServer.GetName() == "headless") viewport.NotifyMouseEntered();
        _mouseEntered = true;
        viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true
        }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false
        }, true);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
