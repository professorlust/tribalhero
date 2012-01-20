#region

using Game.Data;
using Game.Data.Stats;
using Game.Data.Troop;
using Game.Logic.Formulas;
using Game.Map;

#endregion

namespace Game.Logic.Procedures
{
    public partial class Procedure
    {
        public virtual bool TroopStubCreate(ICity city, ITroopStub stub, TroopState initialState = TroopState.Idle) {
            if (!RemoveFromNormal(city.DefaultTroop, stub))
                return false;

            stub.State = initialState;
            city.Troops.Add(stub);
            
            return true;
        }

        public virtual void TroopStubDelete(ICity city, ITroopStub stub)
        {
            AddToNormal(stub, city.DefaultTroop);
            city.Troops.Remove(stub.TroopId);
        }

        public virtual void TroopObjectCreate(ICity city, ITroopStub stub)
        {
            var troop = new TroopObject(stub) { X = city.X, Y = city.Y };
            city.Add(troop);

            troop.BeginUpdate();
            troop.Stats = new TroopStats(Formula.Current.GetTroopRadius(stub, null), Formula.Current.GetTroopSpeed(stub));
            World.Current.Add(troop);
            troop.EndUpdate();            
        }

        public virtual bool TroopObjectCreateFromCity(ICity city, TroopStub stub, uint x, uint y)
        {
            if (stub.TotalCount == 0 || !RemoveFromNormal(city.DefaultTroop, stub))
                return false;

            var troop = new TroopObject(stub) {X = x, Y = y + 1};

            city.Troops.Add(stub);
            city.Add(troop);

            troop.BeginUpdate();
            troop.Stats = new TroopStats(Formula.Current.GetTroopRadius(stub, null), Formula.Current.GetTroopSpeed(stub));
            World.Current.Add(troop);
            troop.EndUpdate();

            return true;
        }

        private bool RemoveFromNormal(ITroopStub source, ITroopStub unitsToRemove)
        {
            if (!source.HasFormation(FormationType.Normal))
                return false;

            var totalUnits = unitsToRemove.ToUnitList();

            // Make sure there are enough units
            foreach (var unit in totalUnits)
            {
                ushort count;
                if (!source[FormationType.Normal].TryGetValue(unit.Type, out count) || count < unit.Count)
                    return false;
            }

            // Remove them, shouldnt fail since we've already checked
            source.BeginUpdate();
            foreach (var formation in unitsToRemove)
            {
                foreach (var unit in formation)
                {
                    source[FormationType.Normal].Remove(unit.Key, unit.Value);
                }
            }
            source.EndUpdate();

            return true;
        }
    }
}