using Godot;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.UI;

namespace SpaceSim.GodotClient.Testing;

/// <summary>Exercises the three-stage hyperspace UI: exit, destination selection and manual re-entry point.</summary>
internal sealed class WarpSmokeScenario(WorldState world, FlightHud hud, StarMap map)
{
    private bool _mouseEntered;

    public ShipCommand BeforeTick()
    {
        map.Refresh();
        switch (world.Tick)
        {
            case 600:
                Click(hud.WarpButton);
                break;
            case 601:
                Check(world.HyperspacePhase == HyperspacePhase.SelectingDestination && map.Visible,
                    "Ready Warp Drive must enter hyperspace and open the star map.");
                Click(map.DestinationButton(2));
                Click(map.JumpButton);
                break;
            case 602:
                Check(world.HyperspacePhase == HyperspacePhase.PlanningEntry && !map.Visible,
                    "Destination Jump must open free entry planning without spawning the ship.");
                ClickMap(hud.GetViewport().GetVisibleRect().Size / 2f + new Vector2(80, -35));
                break;
            case 603:
                Click(hud.WarpButton);
                break;
            case 604:
                Check(world.HyperspacePhase == HyperspacePhase.RealSpace && world.CurrentEncounter.Id == 2,
                    "Confirming the red entry point must re-enter the selected encounter.");
                Check(world.Ship.Velocity.LengthSquared() == 0f && !world.WarpDrive.IsReady,
                    "Re-entry must spawn the player at rest and consume the warp charge.");
                break;
        }
        return default;
    }

    private void ClickMap(Vector2 position)
    {
        var viewport = hud.GetViewport();
        EnterMouse(viewport);
        viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        viewport.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }

    private void Click(Button button)
    {
        Vector2 position = button.GetGlobalRect().GetCenter();
        var viewport = button.GetViewport();
        EnterMouse(viewport);
        viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        viewport.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        viewport.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }

    private void EnterMouse(Viewport viewport)
    {
        if (_mouseEntered || DisplayServer.GetName() != "headless") return;
        viewport.NotifyMouseEntered();
        _mouseEntered = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
