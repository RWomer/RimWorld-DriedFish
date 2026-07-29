using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DriedFish
{
    public class CompProperties_FishDrying : CompProperties
    {
        // How much nutrition the rack holds. 3.75 matches the nutrition of the
        // 75 raw meat that VFE Classical's meat drying rack consumes per batch.
        public float capacityNutrition = 3.75f;

        // Days to dry a batch at a temperature where temperatureFactorCurve == 1.
        public float daysToDry = 15f;

        public ThingDef product;

        // Output units per nutrition of input. 20 gives 75 dried fish from a
        // 3.75 nutrition batch, i.e. the same conversion ratio as the meat rack.
        public float productPerNutrition = 20f;

        // ThingCategoryDef defNames the rack accepts. Sub-categories are included.
        public List<string> acceptedCategories = new List<string>();

        // Speed multiplier by ambient temperature (C). See the XML for the curve.
        // NOTE: SimpleCurve requires a <points> wrapper in XML. A bare <li> list
        // silently yields a curve with zero points, and Evaluate returns 0.
        public SimpleCurve temperatureFactorCurve;

        // Above this, the batch begins to spoil. It is NOT destroyed instantly:
        // spoilage accumulates, so the player gets a warning and a chance to react.
        public float ruinTemperature = 32f;

        // Spoilage accrued per day at exactly ruinTemperature, plus an extra
        // amount per degree above it. At 1.0 the batch is lost.
        // Defaults: ~1.9 days to lose a batch at 32C, ~14 hours at 40C.
        public float spoilRatePerDay = 0.5f;
        public float spoilRatePerDegreePerDay = 0.15f;

        // Spoilage does not reverse. Fish that has begun to turn has begun to
        // turn; cooling the rack only stops it getting worse. Raise this if you
        // want heatwaves to be forgivable.
        public float spoilRecoveryPerDay = 0f;

        // Enclosed rooms never match sea wind, even when empty and cavernous.
        public float enclosedBaseFactor = 0.9f;

        // Drying nutrition per room cell -> speed multiplier. The moisture coming
        // off the fish has to go somewhere; in a sealed room it just sits there.
        // Only applies indoors.
        public SimpleCurve airflowFactorCurve;

        // Racks on an unroofed cell stop drying while it rains hard enough.
        // Note this is checked per-cell, not per-room: a roof with no walls gives
        // you rain cover while keeping outdoor airflow. That's a drying shed, and
        // it is supposed to work.
        public float rainRatePause = 0.25f;

        // Map wind speed (0 to 1.5) -> speed multiplier. OUTDOORS ONLY. Wind is
        // what actually dries fish on a hjell; indoor racks are sheltered from it
        // entirely, which is the whole reason indoor drying is steady and slow.
        public SimpleCurve windFactorCurve;

        // A roof overhead cuts the wind reaching the rack. Applied to the wind
        // SPEED rather than to the final rate, which means that in dead calm a
        // roofed rack dries exactly as fast as an open one - there's no wind to
        // block. The shed only gives up the peaks.
        public float roofedWindFactor = 0.75f;

        // Texture for the fish hanging on the rack, drawn as an untinted overlay
        // on top of the stuff-coloured frame. The ThingDef's own graphicData
        // supplies the frame.
        public GraphicData fishGraphicData;

        public CompProperties_FishDrying()
        {
            compClass = typeof(CompFishDrying);
        }
    }

    public class CompFishDrying : ThingComp
    {
        private float storedNutrition;
        private float progress;
        private float spoilPercent;
        private bool warnedSpoiling;
        private ThingDef lastFishDef;

        private List<ThingCategoryDef> resolvedCategories;

        private float cachedRoomLoad;
        private int cachedRoomLoadTick = -9999;

        public CompProperties_FishDrying Props => (CompProperties_FishDrying)props;

        public float StoredNutrition => storedNutrition;
        public float Progress => progress;
        public bool Empty => storedNutrition <= 0.0001f;
        public bool Full => storedNutrition >= Props.capacityNutrition - 0.0001f;
        public bool Finished => !Empty && progress >= 1f;
        public float SpaceLeftNutrition => Mathf.Max(0f, Props.capacityNutrition - storedNutrition);
        public float SpoilPercent => spoilPercent;
        public bool Spoiling => spoilPercent > 0f;

        // Settings are read live rather than baked into Props, because RimWorld
        // mod settings are global and can change mid-session.
        public float EffectiveDaysToDry =>
            Mathf.Max(0.01f, Props.daysToDry * DriedFishMod.Settings.dryingTimeMultiplier);

        public float EffectiveProductPerNutrition =>
            Props.productPerNutrition * DriedFishMod.Settings.yieldMultiplier;

        /// <summary>Currently hot enough to be actively spoiling a batch.</summary>
        public bool TooHot => DriedFishMod.Settings.spoilageMode != SpoilageMode.Off
            && parent.MapHeld != null
            && parent.PositionHeld.GetTemperature(parent.MapHeld) > Props.ruinTemperature;

        private List<ThingCategoryDef> Categories
        {
            get
            {
                if (resolvedCategories == null)
                {
                    resolvedCategories = new List<ThingCategoryDef>();
                    foreach (string name in Props.acceptedCategories)
                    {
                        ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(name);
                        if (cat != null)
                        {
                            resolvedCategories.Add(cat);
                        }
                        else
                        {
                            Log.Warning("[Dried Fish] ThingCategoryDef '" + name
                                + "' does not exist. The rack will accept nothing. "
                                + "Check the acceptedCategories list on " + parent.def.defName + ".");
                        }
                    }
                }
                return resolvedCategories;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedNutrition, "storedNutrition", 0f);
            Scribe_Values.Look(ref progress, "progress", 0f);
            Scribe_Values.Look(ref spoilPercent, "spoilPercent", 0f);
            Scribe_Values.Look(ref warnedSpoiling, "warnedSpoiling", false);
            Scribe_Defs.Look(ref lastFishDef, "lastFishDef");
        }

        /// <summary>
        /// Accepts anything filed under one of the configured categories, including
        /// sub-categories. Using IsWithinCategory rather than childThingDefs is
        /// deliberate: it is what lets modded fish in their own sub-categories work.
        /// </summary>
        public bool Accepts(ThingDef def)
        {
            if (def == null || def == Props.product)
            {
                return false;
            }
            if (NutritionPer(def) <= 0f)
            {
                return false;
            }
            foreach (ThingCategoryDef cat in Categories)
            {
                if (def.IsWithinCategory(cat))
                {
                    return true;
                }
            }
            return false;
        }

        public static float NutritionPer(ThingDef def)
        {
            return def.GetStatValueAbstract(StatDefOf.Nutrition);
        }

        /// <summary>
        /// How many items of this stack the rack can still take.
        /// </summary>
        public int SpaceLeftFor(ThingDef def)
        {
            float per = NutritionPer(def);
            if (per <= 0f)
            {
                return 0;
            }
            return Mathf.Max(0, Mathf.CeilToInt(SpaceLeftNutrition / per));
        }

        /// <summary>
        /// Adds fish by nutrition, not by count. Progress is pulled back
        /// proportionally so that topping up a nearly-finished rack cannot be
        /// used to launder a fresh batch through.
        /// </summary>
        public void AddFish(Thing fish)
        {
            if (fish == null || !Accepts(fish.def))
            {
                return;
            }
            int take = Mathf.Min(fish.stackCount, SpaceLeftFor(fish.def));
            if (take <= 0)
            {
                return;
            }
            float added = take * NutritionPer(fish.def);
            bool wasEmpty = Empty;
            progress = Mathf.Lerp(progress, 0f, added / (storedNutrition + added));
            storedNutrition += added;
            lastFishDef = fish.def;
            fish.SplitOff(take).Destroy();
            if (wasEmpty)
            {
                NotifyContentsChanged();
            }
        }

        /// <summary>
        /// Buildings are printed into the map mesh, so a Graphic swap only takes
        /// effect once the mesh is dirtied. Called when crossing empty/not-empty.
        /// </summary>
        public void NotifyContentsChanged()
        {
            if (parent.Spawned && parent.Map != null)
            {
                parent.Map.mapDrawer.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Things);
            }
        }

        /// <summary>
        /// Total drying nutrition per cell in this rack's room. Outdoors returns -1.
        /// Cached, because the inspect pane calls this every frame.
        /// </summary>
        public float RoomLoadPerCell()
        {
            if (Find.TickManager.TicksGame - cachedRoomLoadTick < GenTicks.TickRareInterval)
            {
                return cachedRoomLoad;
            }
            cachedRoomLoadTick = Find.TickManager.TicksGame;

            Map map = parent.MapHeld;
            Room room = parent.GetRoom();
            if (map == null || room == null || room.UsesOutdoorTemperature)
            {
                cachedRoomLoad = -1f;
                return cachedRoomLoad;
            }

            float nutrition = 0f;
            foreach (IntVec3 cell in room.Cells)
            {
                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    CompFishDrying other = things[i].TryGetComp<CompFishDrying>();
                    if (other != null)
                    {
                        nutrition += other.StoredNutrition;
                    }
                }
            }

            cachedRoomLoad = nutrition / Mathf.Max(1, room.CellCount);
            return cachedRoomLoad;
        }

        public float CurrentSpeedFactor(out string reason)
        {
            reason = null;
            Map map = parent.MapHeld;
            if (map == null)
            {
                return 0f;
            }

            DriedFishSettings settings = DriedFishMod.Settings;

            float temp = parent.PositionHeld.GetTemperature(map);
            float factor = settings.Temperature(Props.temperatureFactorCurve != null
                ? Props.temperatureFactorCurve.Evaluate(temp)
                : 1f);

            float load = RoomLoadPerCell();

            if (load < 0f)
            {
                // Outdoors. Exposed to weather unless this particular cell
                // happens to be roofed.
                bool roofed = map.roofGrid.Roofed(parent.PositionHeld);
                if (settings.rainStopsDrying && !roofed
                    && map.weatherManager.RainRate >= Props.rainRatePause)
                {
                    reason = "FDR_Raining".Translate();
                    return 0f;
                }

                // Wind only reaches outdoor racks. Weather sets the range it
                // wanders within, so fog starves a rack and storms feed it.
                // A roof cuts the wind that gets through, so a shed trades the
                // storm peak for never being stopped by rain.
                if (settings.windEnabled)
                {
                    float wind = map.windManager != null
                        ? Mathf.Min(map.windManager.WindSpeed, 1.5f)
                        : 1f;
                    if (roofed)
                    {
                        wind *= Props.roofedWindFactor;
                    }
                    float windFactor = Props.windFactorCurve != null
                        ? Props.windFactorCurve.Evaluate(wind)
                        : 1f;
                    factor *= windFactor;

                    string windPct = wind.ToStringPercent("F0");
                    if (windFactor >= 1.05f)
                    {
                        reason = "FDR_WindBrisk".Translate(windPct);
                    }
                    else if (windFactor <= 0.75f)
                    {
                        reason = "FDR_WindStill".Translate(windPct);
                    }
                    else
                    {
                        reason = "FDR_WindSteady".Translate(windPct);
                    }
                }
            }
            else
            {
                factor *= settings.Crowding(Props.enclosedBaseFactor);
                float airflow = settings.Crowding(Props.airflowFactorCurve != null
                    ? Props.airflowFactorCurve.Evaluate(load)
                    : 1f);
                factor *= airflow;

                if (airflow < 0.85f)
                {
                    reason = "FDR_Stagnant".Translate(airflow.ToStringPercent("F0"));
                }
                else
                {
                    reason = "FDR_Indoors".Translate();
                }
            }

            return Mathf.Max(0f, factor);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (Empty || parent.MapHeld == null)
            {
                return;
            }

            float temp = parent.PositionHeld.GetTemperature(parent.MapHeld);
            float dayFraction = GenTicks.TickRareInterval / (float)GenDate.TicksPerDay;
            SpoilageMode mode = DriedFishMod.Settings.spoilageMode;

            if (mode != SpoilageMode.Off && temp > Props.ruinTemperature)
            {
                float degreesOver = temp - Props.ruinTemperature;
                spoilPercent += (Props.spoilRatePerDay
                    + degreesOver * Props.spoilRatePerDegreePerDay) * dayFraction;

                if (!warnedSpoiling && spoilPercent > 0.03f)
                {
                    warnedSpoiling = true;
                    Messages.Message(
                        "FDR_BatchSpoiling".Translate(parent.LabelShort),
                        parent,
                        MessageTypeDefOf.CautionInput,
                        false);
                }

                if (spoilPercent >= 1f)
                {
                    Messages.Message(
                        "FDR_BatchSpoiled".Translate(parent.LabelShort),
                        parent,
                        MessageTypeDefOf.NegativeEvent,
                        false);
                    Reset();
                    return;
                }
            }
            else if (spoilPercent > 0f)
            {
                // Permanent mode holds the damage; Forgiving walks it back;
                // Off clears it outright so an existing save isn't left stuck.
                float recovery = mode == SpoilageMode.Forgiving
                    ? DriedFishSettings.ForgivingRecoveryPerDay
                    : (mode == SpoilageMode.Off ? 999f : Props.spoilRecoveryPerDay);
                spoilPercent = Mathf.Max(0f, spoilPercent - recovery * dayFraction);
                if (spoilPercent <= 0f)
                {
                    warnedSpoiling = false;
                }
            }

            if (progress < 1f)
            {
                string ignored;
                float factor = CurrentSpeedFactor(out ignored);
                progress += GenTicks.TickRareInterval / (EffectiveDaysToDry * GenDate.TicksPerDay) * factor;
                progress = Mathf.Min(progress, 1f);
            }

            if (progress >= 1f)
            {
                TryProduce();
            }
        }

        private void TryProduce()
        {
            if (Props.product == null)
            {
                return;
            }

            int count = Mathf.RoundToInt(storedNutrition * EffectiveProductPerNutrition);
            Reset();
            if (count <= 0)
            {
                return;
            }

            // No interaction cell: place Near the rack itself, which searches
            // outward for a free tile. Lets racks be packed together without a
            // reserved output square each.
            IntVec3 cell = parent.PositionHeld;

            while (count > 0)
            {
                int stack = Mathf.Min(count, Props.product.stackLimit);
                Thing made = ThingMaker.MakeThing(Props.product);
                made.stackCount = stack;
                GenPlace.TryPlaceThing(made, cell, parent.MapHeld, ThingPlaceMode.Near);
                count -= stack;
            }
        }

        private void Reset()
        {
            bool wasLoaded = !Empty;
            storedNutrition = 0f;
            progress = 0f;
            spoilPercent = 0f;
            warnedSpoiling = false;
            lastFishDef = null;
            if (wasLoaded)
            {
                NotifyContentsChanged();
            }
        }

        public override string CompInspectStringExtra()
        {
            if (parent.MapHeld == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();

            if (Empty)
            {
                sb.AppendLine("FDR_Empty".Translate());
            }
            else
            {
                string fishLabel = lastFishDef != null ? lastFishDef.label : "FDR_Fish".Translate().ToString();
                sb.AppendLine("FDR_Contains".Translate(
                    storedNutrition.ToString("F2"),
                    Props.capacityNutrition.ToString("F2"),
                    fishLabel));
                sb.AppendLine("FDR_Progress".Translate(progress.ToStringPercent("F0")));
                sb.AppendLine("FDR_YieldEstimate".Translate(
                    Mathf.RoundToInt(storedNutrition * EffectiveProductPerNutrition)));
                if (spoilPercent > 0f)
                {
                    sb.AppendLine("FDR_Spoiling".Translate(spoilPercent.ToStringPercent("F0")));
                }
            }

            float temp = parent.PositionHeld.GetTemperature(parent.MapHeld);
            string reason;
            float factor = CurrentSpeedFactor(out reason);
            sb.Append("FDR_Conditions".Translate(temp.ToStringTemperature("F0"), factor.ToStringPercent("F0")));
            if (!reason.NullOrEmpty())
            {
                sb.Append(" (" + reason + ")");
            }

            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (Prefs.DevMode && !Empty)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Finish drying",
                    action = delegate
                    {
                        progress = 1f;
                        TryProduce();
                    }
                };
            }

            if (Prefs.DevMode && parent.MapHeld != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Report acceptable fish",
                    action = delegate
                    {
                        var cats = string.Join(", ", Categories.Select(c => c.defName));
                        Log.Message("[Dried Fish] Categories resolved: [" + cats + "]");

                        int matched = 0;
                        var sample = new List<string>();
                        foreach (Thing t in parent.MapHeld.listerThings.ThingsInGroup(
                                     ThingRequestGroup.HaulableEver))
                        {
                            if (Accepts(t.def))
                            {
                                matched++;
                                if (sample.Count < 8)
                                {
                                    sample.Add(t.def.defName + " x" + t.stackCount
                                        + " (nutrition " + NutritionPer(t.def).ToString("F3")
                                        + (t.IsForbidden(Faction.OfPlayer) ? ", FORBIDDEN" : "")
                                        + ")");
                                }
                            }
                        }
                        Log.Message("[Dried Fish] Accepted stacks on map: " + matched
                            + (sample.Count > 0 ? " -> " + string.Join(" | ", sample) : ""));
                        Log.Message("[Dried Fish] Rack state: full=" + Full
                            + " finished=" + Finished + " spaceLeft=" + SpaceLeftNutrition.ToString("F2")
                            + " | settings: dryDays=" + EffectiveDaysToDry.ToString("F1")
                            + " yieldPerNutrition=" + EffectiveProductPerNutrition.ToString("F1")
                            + " tempImpact=" + DriedFishMod.Settings.temperatureImpact.ToString("F2")
                            + " crowdImpact=" + DriedFishMod.Settings.crowdingImpact.ToString("F2")
                            + " wind=" + DriedFishMod.Settings.windEnabled
                            + " rain=" + DriedFishMod.Settings.rainStopsDrying
                            + " spoilage=" + DriedFishMod.Settings.spoilageMode);
                    }
                };
            }
        }
    }
}
