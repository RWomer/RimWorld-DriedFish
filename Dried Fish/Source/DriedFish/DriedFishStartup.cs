using Verse;

namespace DriedFish
{
    /// <summary>
    /// Pushes the saved deterioration setting into FDR_DriedFish's statBases once
    /// defs have finished loading. Without this the setting would only take effect
    /// after the player opened and closed the settings window.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DriedFishStartup
    {
        static DriedFishStartup()
        {
            DriedFishMod.ApplyDeteriorationRate();
        }
    }
}
