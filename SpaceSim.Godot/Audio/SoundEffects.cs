using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;

namespace SpaceSim.GodotClient.Audio;

/// <summary>Godot-only presentation layer for Core events and the local ship state.</summary>
public partial class SoundEffects : Node
{
    private AudioStreamPlayer _booster = null!;
    private AudioStreamPlayer _shieldCharge = null!;
    private AudioStreamPlayer _lanceReady = null!;
    private AudioStreamPlayer _lanceShot = null!;
    private AudioStreamPlayer _shieldHit = null!;
    private AudioStreamPlayer _shieldDepleted = null!;
    private AudioStreamPlayer _shipDamage = null!;
    private AudioStreamPlayer _warpJump = null!;
    private bool _lanceWasReady;
    private bool _boosterLoopRequested;
    private bool _shieldChargePlayedForCurrentRecharge;

    public override void _Ready()
    {
        _booster = CreatePlayer("res://Assets/Sfx/Booster.wav", volumeDb: -6f);
        _shieldCharge = CreatePlayer("res://Assets/Sfx/shield_charge.wav", volumeDb: -2f);
        _lanceReady = CreatePlayer("res://Assets/Sfx/Lance_ready.mp3", volumeDb: -4f);
        _lanceShot = CreatePlayer("res://Assets/Sfx/Lance_shot.wav", volumeDb: -5f);
        _shieldHit = CreatePlayer("res://Assets/Sfx/shield_hit.wav", volumeDb: -4f);
        _shieldDepleted = CreatePlayer("res://Assets/Sfx/shield_depleted.wav", volumeDb: -3f);
        _shipDamage = CreatePlayer("res://Assets/Sfx/Ship_Damage.mp3", volumeDb: -3f);
        _warpJump = CreatePlayer("res://Assets/Sfx/Warp_jump.wav", volumeDb: -4f);
        _booster.Finished += () => { if (_boosterLoopRequested) _booster.Play(); };
    }

    public void Update(WorldState world, SimulationSettings settings, ShipCommand command,
        IReadOnlyList<SimulationEvent> events)
    {
        foreach (SimulationEvent item in events)
        {
            switch (item)
            {
                case WeaponFired { Owner: WeaponOwner.Player }:
                    Restart(_lanceShot);
                    break;
                case ShieldHit { TargetOwner: WeaponOwner.Player }:
                    _shieldCharge.Stop();
                    _shieldChargePlayedForCurrentRecharge = false;
                    Restart(_shieldHit);
                    break;
                case ShieldDepleted { TargetOwner: WeaponOwner.Player }:
                    Restart(_shieldDepleted);
                    break;
                case HullDamaged { TargetOwner: WeaponOwner.Player }:
                    Restart(_shipDamage);
                    break;
                case EncounterChanged:
                    Restart(_warpJump);
                    break;
            }
        }

        if (world.GameState != SpaceSim.Core.Combat.GameState.Running)
        {
            StopContinuous();
            return;
        }

        if (world.Lance.IsReady && !_lanceWasReady)
            Restart(_lanceReady);
        _lanceWasReady = world.Lance.IsReady;

        _boosterLoopRequested = command.MainThrust || command.ReverseThrust;
        SyncLoop(_booster, _boosterLoopRequested);
        TriggerShieldFullWarning(world, settings);
    }

    public void ResetForNewSession()
    {
        StopContinuous();
        _lanceWasReady = false;
        _shieldChargePlayedForCurrentRecharge = false;
    }

    public override void _ExitTree()
    {
        foreach (AudioStreamPlayer player in Players())
        {
            player.Stop();
            player.Stream = null;
        }
    }

    private AudioStreamPlayer CreatePlayer(string path, float volumeDb = 0f)
    {
        AudioStream stream = GD.Load<AudioStream>(path);
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = volumeDb };
        AddChild(player);
        return player;
    }

    private static void Restart(AudioStreamPlayer player)
    {
        player.Stop();
        player.Play();
    }

    private static void SyncLoop(AudioStreamPlayer player, bool shouldPlay)
    {
        if (shouldPlay && !player.Playing) player.Play();
        else if (!shouldPlay && player.Playing) player.Stop();
    }

    private void StopContinuous()
    {
        _boosterLoopRequested = false;
        _booster.Stop();
        _shieldCharge.Stop();
    }

    private void TriggerShieldFullWarning(WorldState world, SimulationSettings settings)
    {
        var shield = world.Ship.Shield;
        float rechargePerSecond = settings.Shield.RechargePerSecond * world.Ship.Power.ShieldsPowerFactor *
            world.Ship.Systems.ShieldsCondition;
        bool regenerating = shield.CurrentShield < shield.MaximumShield && !shield.IsRechargeDelayed &&
            rechargePerSecond > 0f;
        if (!regenerating)
        {
            if (shield.CurrentShield >= shield.MaximumShield) _shieldChargePlayedForCurrentRecharge = false;
            return;
        }

        float secondsUntilFull = (shield.MaximumShield - shield.CurrentShield) / rechargePerSecond;
        if (!_shieldChargePlayedForCurrentRecharge && secondsUntilFull <= AudioSettings.ShieldChargeLeadSeconds)
        {
            _shieldChargePlayedForCurrentRecharge = true;
            Restart(_shieldCharge);
        }
    }

    private IEnumerable<AudioStreamPlayer> Players()
    {
        if (_booster is not null) yield return _booster;
        if (_shieldCharge is not null) yield return _shieldCharge;
        if (_lanceReady is not null) yield return _lanceReady;
        if (_lanceShot is not null) yield return _lanceShot;
        if (_shieldHit is not null) yield return _shieldHit;
        if (_shieldDepleted is not null) yield return _shieldDepleted;
        if (_shipDamage is not null) yield return _shipDamage;
        if (_warpJump is not null) yield return _warpJump;
    }
}
