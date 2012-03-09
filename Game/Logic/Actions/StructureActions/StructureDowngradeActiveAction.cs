#region

using System;
using System.Collections.Generic;
using Game.Data;
using Game.Logic.Formulas;
using Game.Logic.Procedures;
using Game.Map;
using Game.Setup;
using Game.Util;
using Game.Util.Locking;
using Ninject;

#endregion

namespace Game.Logic.Actions
{
    public class StructureDowngradeActiveAction : ScheduledActiveAction
    {
        private readonly uint cityId;
        private readonly uint structureId;

        public StructureDowngradeActiveAction(uint cityId, uint structureId)
        {
            this.cityId = cityId;
            this.structureId = structureId;
        }

        public StructureDowngradeActiveAction(uint id,
                                            DateTime beginTime,
                                            DateTime nextTime,
                                            DateTime endTime,
                                            int workerType,
                                            byte workerIndex,
                                            ushort actionCount,
                                            Dictionary<string, string> properties)
                : base(id, beginTime, nextTime, endTime, workerType, workerIndex, actionCount)
        {
            cityId = uint.Parse(properties["city_id"]);
            structureId = uint.Parse(properties["structure_id"]);
        }

        public override ConcurrencyType ActionConcurrency
        {
            get
            {
                return ConcurrencyType.Normal;
            }
        }

        public override ActionType Type
        {
            get
            {
                return ActionType.StructureDowngradeActive;
            }
        }

        public override Error Execute()
        {
            ICity city;
            IStructure structure;

            if (!World.Current.TryGetObjects(cityId, structureId, out city, out structure))
                return Error.ObjectNotFound;

            if (Ioc.Kernel.Get<ObjectTypeFactory>().IsStructureType("MainBuilding", structure))
                return Error.StructureUndowngradable;

            if (Ioc.Kernel.Get<ObjectTypeFactory>().IsStructureType("Unattackable", structure))
                return Error.StructureUndowngradable;

            if (Ioc.Kernel.Get<ObjectTypeFactory>().IsStructureType("Undestroyable", structure) && structure.Lvl <= 1)
                return Error.StructureUndestroyable;

            endTime =
                    DateTime.UtcNow.AddSeconds(
                                               CalculateTime(Formula.Current.BuildTime(Ioc.Kernel.Get<StructureFactory>().GetTime(structure.Type, (byte)(structure.Lvl + 1)),
                                                                               city,
                                                                               structure.Technologies)));
            BeginTime = DateTime.UtcNow;

            if (WorkerObject.WorkerId != structureId)
                city.Worker.References.Add(structure, this);

            return Error.Ok;
        }

        public override void Callback(object custom)
        {
            ICity city;
            IStructure structure;

            // Block structure
            using (Concurrency.Current.Lock(cityId, structureId, out city, out structure))
            {
                if (!IsValid())
                    return;

                if (structure == null)
                {
                    StateChange(ActionState.Completed);
                    return;
                }

                if (WorkerObject.WorkerId != structureId)
                {
                    city.Worker.References.Remove(structure, this);
                }

                if (structure.IsBlocked)
                {
                    StateChange(ActionState.Failed);
                    return;
                }

                if (Ioc.Kernel.Get<ObjectTypeFactory>().IsStructureType("Undestroyable", structure))
                {
                    StateChange(ActionState.Failed);
                    return;
                }

                if (structure.State.Type == ObjectState.Battle)
                {
                    StateChange(ActionState.Failed);
                    return;
                }

                structure.BeginUpdate();
                structure.IsBlocked = true;
                structure.EndUpdate();
            }

            structure.City.Worker.Remove(structure, new GameAction[] {this});

            using (Concurrency.Current.Lock(cityId, structureId, out city, out structure))
            {
                city.BeginUpdate();
                structure.BeginUpdate();

                // Unblock structure since we're done with it in this action and ObjectRemoveAction will take it from here
                structure.IsBlocked = false;

                // Destroy structure
                World.Current.Remove(structure);
                city.ScheduleRemove(structure, false);

                structure.EndUpdate();
                city.EndUpdate();

                StateChange(ActionState.Completed);
            }
        }

        public override Error Validate(string[] parms)
        {
            return Error.Ok;
        }

        private void InterruptCatchAll(bool wasKilled)
        {
            ICity city;
            using (Concurrency.Current.Lock(cityId, out city))
            {
                if (!IsValid())
                    return;

                IStructure structure;
                if (WorkerObject.WorkerId != structureId && city.TryGetStructure(structureId, out structure))
                    city.Worker.References.Remove(structure, this);

                StateChange(ActionState.Failed);
            }
        }

        public override void UserCancelled()
        {
            InterruptCatchAll(false);
        }

        public override void WorkerRemoved(bool wasKilled)
        {
            InterruptCatchAll(wasKilled);
        }

        #region IPersistable

        public override string Properties
        {
            get
            {
                return XmlSerializer.Serialize(new[] {new XmlKvPair("city_id", cityId), new XmlKvPair("structure_id", structureId)});
            }
        }

        #endregion
    }
}