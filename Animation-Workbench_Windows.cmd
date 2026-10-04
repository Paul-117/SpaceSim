@echo off
setlocal
set "ROOT=%~dp0"
"%ROOT%Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path "%ROOT%SpaceSim.Godot" --scene res://Scenes/AnimationWorkbench.tscn
