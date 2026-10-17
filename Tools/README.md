# SpaceSim Tools

These launchers collect the development tools in one place.

- `SpaceSim_Windows.cmd` - builds and starts the normal SpaceSim simulation.
- `1VS1-Duell_Windows.cmd` - starts a direct 1 vs 1 Kestrel duel and writes an AI log to `SpaceSim.Godot/Logs/Kestrel`.
- `UI-Editor_Windows.cmd` - starts the Bridge layout editor in the browser.
- `Shop-UI-Editor_Windows.cmd` - starts the Shop layout editor in the browser.
- `Shop-UI-Editor_Linux.sh` - starts the Shop layout editor on Linux.
- `Animations-Werkbank_Windows.cmd` - opens the Godot animation workbench.
- `Duell-Log-Viewer_Windows.cmd` - opens the browser viewer for saved duel logs.
- `Gefechts-Simulation_Windows.cmd` - starts the local combat laboratory at `http://127.0.0.1:41871/` and opens it in the default browser.

The combat laboratory stores its own runs in `SpaceSim.Godot/Logs/Gefechts Simulationen`. It supports separate Nomad and enemy booster, shield and lance values, then runs five battles by default.

Armarium, Voltarium and Sensorium are stations of the running simulation. They open through the game's station server and are not separate simulations.

## Shop UI workflow

Author the shop screen in `UI/Shop/`. Every PNG copied there appears in the editor's **Element hinzufuegen** dialog immediately. Export the completed layout as `shop-layout.json`, then copy that file and its used PNG files to `SpaceSim.Godot/Assets/Shop/` for the Godot implementation.
