using SpaceSim.Core.Ships;

namespace SpaceSim.Stations.Armarium;

/// <summary>
/// Combines bridge and Armarium intent into one ordinary ship command.
/// The browser contributes at reduced authority while the bridge retains full local control.
/// </summary>
public static class ArmariumCommandMixer
{
    public static ShipCommand Merge(ShipCommand bridgeCommand, ArmariumCommand armariumCommand,
        float armariumYawIntensity)
    {
        float bridgeAxis = GetYawAxis(bridgeCommand.YawLeft, bridgeCommand.YawRight) *
                           ClampIntensity(bridgeCommand.YawIntensity);
        float armariumAxis = GetYawAxis(armariumCommand.YawLeft, armariumCommand.YawRight) *
                             ClampIntensity(armariumYawIntensity);
        float combinedAxis = Math.Clamp(bridgeAxis + armariumAxis, -1f, 1f);

        return bridgeCommand with
        {
            YawLeft = combinedAxis > 0f,
            YawRight = combinedAxis < 0f,
            YawIntensity = MathF.Abs(combinedAxis),
            FireLance = bridgeCommand.FireLance || armariumCommand.FireLance
        };
    }

    private static float GetYawAxis(bool yawLeft, bool yawRight) =>
        (yawLeft ? 1f : 0f) - (yawRight ? 1f : 0f);

    private static float ClampIntensity(float intensity) =>
        float.IsFinite(intensity) ? Math.Clamp(intensity, 0f, 1f) : 0f;
}
