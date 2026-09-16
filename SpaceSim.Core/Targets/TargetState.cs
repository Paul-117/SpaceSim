using System.Numerics;

namespace SpaceSim.Core.Targets;

public sealed record TargetState(int Id, Vector3 Position, float RadiusMeters);
