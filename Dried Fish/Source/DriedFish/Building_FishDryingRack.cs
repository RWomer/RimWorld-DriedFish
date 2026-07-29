using Verse;

namespace DriedFish
{
    /// <summary>
    /// Swaps the rack's texture between its empty and loaded states.
    ///
    /// The ThingDef's own graphicData is the EMPTY frame — that's what shows in the
    /// architect menu, as a blueprint, and while the rack is unused. The loaded
    /// texture comes from fullGraphicData on the drying comp.
    ///
    /// Buildings are baked into the map mesh, so changing Graphic isn't enough on
    /// its own; the comp calls NotifyContentsChanged() to dirty the mesh whenever
    /// it crosses the empty/not-empty line.
    /// </summary>
    public class Building_FishDryingRack : Building
    {
        private CompFishDrying dryingCompInt;
        private Graphic fullGraphicInt;

        public CompFishDrying DryingComp
        {
            get
            {
                if (dryingCompInt == null)
                {
                    dryingCompInt = GetComp<CompFishDrying>();
                }
                return dryingCompInt;
            }
        }

        private Graphic FullGraphic
        {
            get
            {
                if (fullGraphicInt == null)
                {
                    CompFishDrying comp = DryingComp;
                    if (comp != null && comp.Props.fullGraphicData != null)
                    {
                        fullGraphicInt = comp.Props.fullGraphicData.GraphicColoredFor(this);
                    }
                }
                return fullGraphicInt;
            }
        }

        public override Graphic Graphic
        {
            get
            {
                CompFishDrying comp = DryingComp;
                if (comp != null && !comp.Empty)
                {
                    Graphic full = FullGraphic;
                    if (full != null)
                    {
                        return full;
                    }
                }
                return base.Graphic;
            }
        }
    }
}
