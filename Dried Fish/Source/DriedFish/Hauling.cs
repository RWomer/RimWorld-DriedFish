using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DriedFish
{
    [DefOf]
    public static class DriedFishDefOf
    {
        public static JobDef FDR_FillFishDryingRack;

        static DriedFishDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DriedFishDefOf));
        }
    }

    public class WorkGiver_FillFishDryingRack : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompFishDrying comp = t.TryGetComp<CompFishDrying>();
            if (comp == null || comp.Full || comp.Finished)
            {
                return false;
            }
            // Don't let pawns keep loading a rack that's currently spoiling its
            // contents -- that's just walking fish into a fire.
            if (comp.TooHot)
            {
                return false;
            }
            if (t.IsBurning() || t.IsForbidden(pawn))
            {
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            return FindFish(pawn, comp) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompFishDrying comp = t.TryGetComp<CompFishDrying>();
            if (comp == null)
            {
                return null;
            }

            Thing fish = FindFish(pawn, comp);
            if (fish == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(DriedFishDefOf.FDR_FillFishDryingRack, t, fish);
            job.count = Math.Min(fish.stackCount, comp.SpaceLeftFor(fish.def));
            return job;
        }

        private Thing FindFish(Pawn pawn, CompFishDrying comp)
        {
            Predicate<Thing> validator = delegate (Thing x)
            {
                if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
                {
                    return false;
                }
                if (!comp.Accepts(x.def))
                {
                    return false;
                }
                CompRottable rot = x.TryGetComp<CompRottable>();
                return rot == null || rot.Stage == RotStage.Fresh;
            };

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                validator);
        }
    }

    public class JobDriver_FillFishDryingRack : JobDriver
    {
        private const TargetIndex RackInd = TargetIndex.A;
        private const TargetIndex FishInd = TargetIndex.B;
        private const int DepositTicks = 200;

        private CompFishDrying Comp => job.GetTarget(RackInd).Thing.TryGetComp<CompFishDrying>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(RackInd), job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(job.GetTarget(FishInd), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(RackInd);
            this.FailOnBurningImmobile(RackInd);
            this.FailOn(() => Comp == null || Comp.Full || Comp.Finished || Comp.TooHot);

            yield return Toils_Goto.GotoThing(FishInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(FishInd)
                .FailOnSomeonePhysicallyInteracting(FishInd);

            yield return Toils_Haul.StartCarryThing(FishInd, false, true);

            yield return Toils_Goto.GotoThing(RackInd, PathEndMode.Touch);

            Toil deposit = Toils_General.Wait(DepositTicks)
                .FailOnDestroyedNullOrForbidden(FishInd)
                .FailOnDestroyedNullOrForbidden(RackInd)
                .WithProgressBarToilDelay(RackInd);
            yield return deposit;

            yield return new Toil
            {
                initAction = delegate
                {
                    CompFishDrying comp = Comp;
                    if (comp != null && pawn.carryTracker.CarriedThing != null)
                    {
                        comp.AddFish(pawn.carryTracker.CarriedThing);
                    }
                    if (pawn.carryTracker.CarriedThing != null
                        && pawn.carryTracker.CarriedThing.Destroyed == false
                        && pawn.carryTracker.CarriedThing.stackCount <= 0)
                    {
                        pawn.carryTracker.CarriedThing.Destroy();
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}
