namespace SpaceSim.Core.AI;

public enum EnemyAiState
{
    Acquire,
    Approach,
    Attack,
    Evade,
    Reposition,
    Destroyed
}

public enum EvadeDirection
{
    Left = 1,
    Right = -1
}

