using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DriedFish
{
    public enum SpoilageMode
    {
        Off,
        Forgiving,
        Permanent
    }

    public class DriedFishSettings : ModSettings
    {
        // Multipliers rather than raw values, so they stay meaningful if the
        // underlying def numbers are ever rebalanced.
        public float dryingTimeMultiplier = 1f;
        public float yieldMultiplier = 1f;

        // Each environmental factor gets its own control. 0 means that factor is
        // ignored entirely, 1 is as designed and tested, above 1 exaggerates it.
        public float temperatureImpact = 1f;
        public float crowdingImpact = 1f;

        // Wind and rain are near enough to binary that a slider would be false
        // precision. The wind toggle simply enables the wind simulation; when on,
        // wind behaves at full strength, roof interaction included.
        public bool windEnabled = true;
        public bool rainStopsDrying = true;

        public SpoilageMode spoilageMode = SpoilageMode.Permanent;

        // HP per day lost by dried fish left uncovered. Applied to the ThingDef's
        // statBases at startup and whenever settings are saved.
        public float deteriorationRate = 1f;

        public const float ForgivingRecoveryPerDay = 0.15f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dryingTimeMultiplier, "dryingTimeMultiplier", 1f);
            Scribe_Values.Look(ref yieldMultiplier, "yieldMultiplier", 1f);
            Scribe_Values.Look(ref temperatureImpact, "temperatureImpact", 1f);
            Scribe_Values.Look(ref crowdingImpact, "crowdingImpact", 1f);
            Scribe_Values.Look(ref windEnabled, "windEnabled", true);
            Scribe_Values.Look(ref rainStopsDrying, "rainStopsDrying", true);
            Scribe_Values.Look(ref spoilageMode, "spoilageMode", SpoilageMode.Permanent);
            Scribe_Values.Look(ref deteriorationRate, "deteriorationRate", 1f);
        }

        public void ResetToDefaults()
        {
            dryingTimeMultiplier = 1f;
            yieldMultiplier = 1f;
            temperatureImpact = 1f;
            crowdingImpact = 1f;
            windEnabled = true;
            rainStopsDrying = true;
            spoilageMode = SpoilageMode.Permanent;
            deteriorationRate = 1f;
        }

        /// <summary>
        /// Pulls a multiplier toward 1.0 (i.e. toward "no effect") by the given
        /// impact setting.
        /// </summary>
        private static float Scale(float raw, float impact)
        {
            if (impact == 1f)
            {
                return raw;
            }
            return Mathf.Lerp(1f, raw, impact);
        }

        public float Temperature(float raw)
        {
            return Scale(raw, temperatureImpact);
        }

        public float Crowding(float raw)
        {
            return Scale(raw, crowdingImpact);
        }
    }

    public class DriedFishMod : Mod
    {
        private static DriedFishSettings settingsInt;
        private static CompProperties_FishDrying cachedProps;

        /// <summary>
        /// Never null. Falls back to defaults if something queries settings before
        /// the Mod constructor has run.
        /// </summary>
        public static DriedFishSettings Settings
        {
            get
            {
                if (settingsInt == null)
                {
                    settingsInt = new DriedFishSettings();
                }
                return settingsInt;
            }
        }

        /// <summary>
        /// The live rack comp properties, so the worked examples in the settings
        /// page stay honest if the curves in XML are ever retuned. Null-safe:
        /// examples are simply omitted if the def can't be found.
        /// </summary>
        private static CompProperties_FishDrying RackProps
        {
            get
            {
                if (cachedProps == null)
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("FDR_FishDryingRack");
                    if (def != null)
                    {
                        cachedProps = def.GetCompProperties<CompProperties_FishDrying>();
                    }
                }
                return cachedProps;
            }
        }

        public DriedFishMod(ModContentPack content) : base(content)
        {
            settingsInt = GetSettings<DriedFishSettings>();
        }

        /// <summary>
        /// Deterioration is a stat read off the ThingDef, not something the comp
        /// controls, so the setting has to be written into the def's statBases.
        /// Called once after defs load and again whenever settings are saved.
        /// </summary>
        public static void ApplyDeteriorationRate()
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("FDR_DriedFish");
            if (def == null)
            {
                return;
            }
            if (def.statBases == null)
            {
                def.statBases = new List<StatModifier>();
            }

            StatModifier existing = def.statBases.Find(s => s.stat == StatDefOf.DeteriorationRate);
            if (existing != null)
            {
                existing.value = Settings.deteriorationRate;
            }
            else
            {
                def.statBases.Add(new StatModifier
                {
                    stat = StatDefOf.DeteriorationRate,
                    value = Settings.deteriorationRate
                });
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyDeteriorationRate();
        }

        public override string SettingsCategory()
        {
            return "FDR_SettingsCategory".Translate();
        }

        // ---- small UI helpers -------------------------------------------------

        private static void Note(Listing_Standard list, string text)
        {
            GameFont old = Text.Font;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            Text.Font = GameFont.Tiny;
            list.Label(text);
            Text.Font = old;
            GUI.color = Color.white;
        }

        private static float SnappedSlider(Listing_Standard list, float value, float min, float max)
        {
            return Mathf.Round(list.Slider(value, min, max) * 20f) / 20f;
        }

        /// <summary>Snaps to quarters, for values measured in HP rather than percent.</summary>
        private static float QuarterSlider(Listing_Standard list, float value, float min, float max)
        {
            return Mathf.Round(list.Slider(value, min, max) * 4f) / 4f;
        }

        // ---- worked examples, computed from the real defs ---------------------

        private static string DryingTimeExample(DriedFishSettings s)
        {
            CompProperties_FishDrying p = RackProps;
            if (p == null)
            {
                return null;
            }
            float days = p.daysToDry * s.dryingTimeMultiplier;
            return "FDR_ExDryingTime".Translate(days.ToString("0.#"));
        }

        private static string YieldExample(DriedFishSettings s)
        {
            CompProperties_FishDrying p = RackProps;
            if (p == null)
            {
                return null;
            }
            int count = Mathf.RoundToInt(p.capacityNutrition * p.productPerNutrition * s.yieldMultiplier);
            return "FDR_ExYield".Translate(p.capacityNutrition.ToString("0.##"), count);
        }

        private static string TemperatureExample(DriedFishSettings s)
        {
            CompProperties_FishDrying p = RackProps;
            if (p == null || p.temperatureFactorCurve == null)
            {
                return null;
            }
            float warm = s.Temperature(p.temperatureFactorCurve.Evaluate(20f));
            float cold = s.Temperature(p.temperatureFactorCurve.Evaluate(-20f));
            return "FDR_ExTemperature".Translate(
                warm.ToStringPercent("F0"), cold.ToStringPercent("F0"));
        }

        private static string CrowdingExample(DriedFishSettings s)
        {
            CompProperties_FishDrying p = RackProps;
            if (p == null || p.airflowFactorCurve == null)
            {
                return null;
            }
            float roomy = s.Crowding(p.enclosedBaseFactor)
                * s.Crowding(p.airflowFactorCurve.Evaluate(4f * p.capacityNutrition / 121f));
            float packed = s.Crowding(p.enclosedBaseFactor)
                * s.Crowding(p.airflowFactorCurve.Evaluate(4f * p.capacityNutrition / 30f));
            return "FDR_ExCrowding".Translate(
                roomy.ToStringPercent("F0"), packed.ToStringPercent("F0"));
        }

        private static string DeteriorationExample(DriedFishSettings s)
        {
            if (s.deteriorationRate <= 0f)
            {
                return "FDR_ExDeteriorationNever".Translate();
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("FDR_DriedFish");
            if (def == null)
            {
                return null;
            }
            float maxHp = def.BaseMaxHitPoints;
            float days = maxHp / s.deteriorationRate;
            return "FDR_ExDeterioration".Translate(days.ToString("0"));
        }

        // ---- the page ---------------------------------------------------------

        private static Vector2 scrollPos = Vector2.zero;

        // Grown as needed. Content is taller than any settings window, so the page
        // scrolls rather than being split into columns that silently clip.
        private static float lastContentHeight = 1400f;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            DriedFishSettings s = Settings;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 24f, lastContentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(new Rect(0f, 0f, viewRect.width, 99999f));

            // ----- throughput -----
            Text.Font = GameFont.Medium;
            list.Label("FDR_SectionThroughput".Translate());
            Text.Font = GameFont.Small;
            Note(list, "FDR_SectionThroughputDesc".Translate());
            list.Gap(6f);

            list.Label("FDR_SetDryingTime".Translate(s.dryingTimeMultiplier.ToStringPercent("F0")),
                -1f, "FDR_SetDryingTimeDesc".Translate());
            s.dryingTimeMultiplier = SnappedSlider(list, s.dryingTimeMultiplier, 0.25f, 3f);
            Note(list, "FDR_SetDryingTimeDesc".Translate());
            string dt = DryingTimeExample(s);
            if (dt != null)
            {
                Note(list, dt);
            }
            list.Gap(10f);

            list.Label("FDR_SetYield".Translate(s.yieldMultiplier.ToStringPercent("F0")),
                -1f, "FDR_SetYieldDesc".Translate());
            s.yieldMultiplier = SnappedSlider(list, s.yieldMultiplier, 0.5f, 2f);
            Note(list, "FDR_SetYieldDesc".Translate());
            string yd = YieldExample(s);
            if (yd != null)
            {
                Note(list, yd);
            }
            list.Gap(10f);

            list.Label("FDR_SetDeterioration".Translate(s.deteriorationRate.ToString("0.##")),
                -1f, "FDR_SetDeteriorationDesc".Translate());
            s.deteriorationRate = QuarterSlider(list, s.deteriorationRate, 0f, 5f);
            Note(list, "FDR_SetDeteriorationDesc".Translate());
            string de = DeteriorationExample(s);
            if (de != null)
            {
                Note(list, de);
            }
            list.Gap(10f);

            // ----- conditions -----
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("FDR_SectionEnvironment".Translate());
            Text.Font = GameFont.Small;
            Note(list, "FDR_SectionEnvironmentDesc".Translate());
            list.Gap(6f);

            list.Label("FDR_SetTemperature".Translate(s.temperatureImpact.ToStringPercent("F0")),
                -1f, "FDR_SetTemperatureDesc".Translate());
            s.temperatureImpact = SnappedSlider(list, s.temperatureImpact, 0f, 2f);
            Note(list, "FDR_SetTemperatureDesc".Translate());
            string te = TemperatureExample(s);
            if (te != null)
            {
                Note(list, te);
            }
            list.Gap(10f);

            list.Label("FDR_SetCrowding".Translate(s.crowdingImpact.ToStringPercent("F0")),
                -1f, "FDR_SetCrowdingDesc".Translate());
            s.crowdingImpact = SnappedSlider(list, s.crowdingImpact, 0f, 2f);
            Note(list, "FDR_SetCrowdingDesc".Translate());
            string ce = CrowdingExample(s);
            if (ce != null)
            {
                Note(list, ce);
            }
            list.Gap(10f);

            list.CheckboxLabeled("FDR_SetWind".Translate(), ref s.windEnabled,
                "FDR_SetWindDesc".Translate());
            Note(list, "FDR_SetWindDesc".Translate());
            list.Gap(6f);

            list.CheckboxLabeled("FDR_SetRain".Translate(), ref s.rainStopsDrying,
                "FDR_SetRainDesc".Translate());
            Note(list, "FDR_SetRainDesc".Translate());
            list.Gap(10f);

            // ----- spoilage -----
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("FDR_SetSpoilage".Translate());
            Text.Font = GameFont.Small;
            Note(list, "FDR_SetSpoilageDesc".Translate());
            list.Gap(4f);
            if (list.RadioButton("FDR_SpoilOff".Translate(), s.spoilageMode == SpoilageMode.Off, 8f,
                    "FDR_SpoilOffDesc".Translate()))
            {
                s.spoilageMode = SpoilageMode.Off;
            }
            Note(list, "FDR_SpoilOffDesc".Translate());
            if (list.RadioButton("FDR_SpoilForgiving".Translate(), s.spoilageMode == SpoilageMode.Forgiving, 8f,
                    "FDR_SpoilForgivingDesc".Translate()))
            {
                s.spoilageMode = SpoilageMode.Forgiving;
            }
            Note(list, "FDR_SpoilForgivingDesc".Translate());
            if (list.RadioButton("FDR_SpoilPermanent".Translate(), s.spoilageMode == SpoilageMode.Permanent, 8f,
                    "FDR_SpoilPermanentDesc".Translate()))
            {
                s.spoilageMode = SpoilageMode.Permanent;
            }
            Note(list, "FDR_SpoilPermanentDesc".Translate());

            list.GapLine();
            if (list.ButtonText("FDR_SetReset".Translate()))
            {
                s.ResetToDefaults();
            }
            list.Gap(12f);

            // Remember how tall the content actually was, so the scrollbar is
            // correct next frame however long the translated strings turn out.
            lastContentHeight = list.CurHeight;

            list.End();
            Widgets.EndScrollView();

            base.DoSettingsWindowContents(inRect);
        }
    }
}
