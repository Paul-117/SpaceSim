using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.AI;

/// <summary>
/// Reusable Kestrel-compatible pilot for symmetric, deterministic 1VS1 simulations.
/// It produces exactly the same normal <see cref="ShipCommand"/> values as an enemy controller.
/// </summary>
public sealed class DuelAiPilot
{
    private readonly EnemyAiController _controller;

    public EnemyAiState CurrentState => _controller.CurrentState;
    public EnemyAiContext LastContext => _controller.LastContext;
    public ShipCommand LastCommand => _controller.LastCommand;
    public EnemyAiModel Model => _controller.Model;

    public DuelAiPilot(EnemyAiSettings settings, EnemyDifficulty difficulty, float nominalReverseAcceleration,
        EnemyAiModel model = EnemyAiModel.Kestrel, int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnemyDifficultyProfile profile = EnemyDifficultyProfiles.Create(difficulty, settings);
        _controller = new EnemyAiController(profile.Ai, difficulty, nominalReverseAcceleration, model, seed);
        _controller.Alert();
    }

    /// <summary>Creates the next movement and firing intent for the controlled ship.</summary>
    public ShipCommand Tick(ShipState controlledShip, LanceState controlledLance,
        ShipState targetShip, LanceState targetLance, float lanceRange, GameState gameState) =>
        _controller.TickDuelPilot(controlledShip, controlledLance, targetShip, targetLance, lanceRange, gameState);
}