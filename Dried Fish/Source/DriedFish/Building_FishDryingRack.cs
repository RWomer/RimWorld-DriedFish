using Verse;

namespace DriedFish
{
    /// <summary>
    /// Draws the rack in two layers.
    ///
    /// The frame is the ThingDef's own graphicData, so it takes the stuff colour
    /// and turns wood brown, steel grey, and so on. That is why the frame texture
    /// is authored in neutral greys: stuff colour multiplies against the texture
    /// and can only darken it, so any residual hue would poison every material.
    ///
    /// The fish are a separate overlay printed on top, deliberately fetched via
    /// GraphicData.Graphic rather than GraphicColoredFor(this) so they keep their
    /// own colour. Steel racks should not have steel-coloured fish.
    ///
    /// This also replaces the old empty/full texture swap: "full" simply means the
    /// overlay prints. The comp still calls NotifyContentsChanged() when it crosses
    /// the empty/loaded line, because buildings are printed into the map mesh and
    /// the mesh has to be dirtied for the change to appear.
    /// </summary>
    public class Building_FishDryingRack : Building
    {
        private CompFishDrying dryingCompInt;
        private Graphic fishGraphicInt;

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

        private Graphic FishGraphic
        {
            get
            {
                if (fishGraphicInt == null)
                {
                    CompFishDrying comp = DryingComp;
                    if (comp != null && comp.Props.fishGraphicData != null)
                    {
                        // NOT GraphicColoredFor: the fish must ignore stuff colour.
                        fishGraphicInt = comp.Props.fishGraphicData.Graphic;
                    }
                }
                return fishGraphicInt;
            }
        }

        public override void Print(SectionLayer layer)
        {
            base.Print(layer);

            CompFishDrying comp = DryingComp;
            if (comp == null || comp.Empty)
            {
                return;
            }

            Graphic fish = FishGraphic;
            if (fish != null)
            {
                fish.Print(layer, this, 0f);
            }
        }
    }
}
