namespace AuraVault.App.Aura;

/// <summary>Process-wide animation tuning, updated from settings. 0 speed == reduced motion.</summary>
public static class MotionScale
{
    public static double Speed { get; private set; } = 1.0;

    public static bool ReducedMotion { get; private set; }

    public static void Update(double animationSpeed, bool reducedMotion)
    {
        ReducedMotion = reducedMotion;
        Speed = reducedMotion ? 0.0 : System.Math.Clamp(animationSpeed, 0.05, 2.0);
    }

    /// <summary>Scales a base duration (ms) by the current speed; returns 0 under reduced motion.</summary>
    public static double Duration(double baseMs) => ReducedMotion ? 0 : baseMs / (Speed <= 0 ? 1 : Speed);
}
