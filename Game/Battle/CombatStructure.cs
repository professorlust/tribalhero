#region

using System;
using System.Data;
using Game.Data;
using Game.Data.Stats;
using Game.Data.Troop;
using Game.Logic.Actions;
using Game.Logic.Formulas;
using Game.Map;
using Persistance;

#endregion

namespace Game.Battle
{
    public class CombatStructure : CombatObject
    {
        public const string DB_TABLE = "combat_structures";
        private readonly byte lvl;
        private readonly BattleStats stats;
        private readonly Formula formula;
        private readonly BattleFormulas battleFormula;
        private readonly ushort type;
        private decimal hp; //need to keep a copy track of the hp for reporting

        public CombatStructure(IBattleManager owner, IStructure structure, BattleStats stats, Formula formula, BattleFormulas battleFormula)
        {
            battleManager = owner;
            this.stats = stats;
            this.formula = formula;
            this.battleFormula = battleFormula;
            Structure = structure;
            type = structure.Type;
            lvl = structure.Lvl;
            hp = structure.Stats.Hp;
        }

        public CombatStructure(IBattleManager owner, IStructure structure, BattleStats stats, decimal hp, ushort type, byte lvl, Formula formula, BattleFormulas battleFormula)
        {
            battleManager = owner;
            Structure = structure;
            this.formula = formula;
            this.battleFormula = battleFormula;
            this.stats = stats;
            this.hp = hp;
            this.type = type;
            this.lvl = lvl;
        }

        public IStructure Structure { get; private set; }

        public override BaseBattleStats BaseStats
        {
            get
            {
                return Structure.Stats.Base.Battle;
            }
        }

        public override BattleStats Stats
        {
            get
            {
                return stats;
            }
        }

        public override uint Visibility
        {
            get
            {
                return Stats.Rng;
            }
        }

        public override uint PlayerId
        {
            get
            {
                return Structure.City.Owner.PlayerId;
            }
        }

        public override ICity City
        {
            get
            {
                return Structure.City;
            }
        }

        public override ITroopStub TroopStub
        {
            get
            {
                return City.DefaultTroop;
            }
        }

        public override BattleClass ClassType
        {
            get
            {
                return BattleClass.Structure;
            }
        }

        public override bool IsDead
        {
            get
            {
                return hp == 0;
            }
        }

        public override ushort Count
        {
            get
            {
                return (ushort)(hp > 0 ? 1 : 0);
            }
        }

        public override byte Lvl
        {
            get
            {
                return lvl;
            }
        }

        public override ushort Type
        {
            get
            {
                return type;
            }
        }

        public override Resource GroupLoot
        {
            get
            {
                return new Resource();
            }
        }

        public override Resource Loot
        {
            get
            {
                return new Resource();
            }
        }

        public override decimal Hp
        {
            get
            {
                return hp;
            }
        }

        public override int Upkeep
        {
            get
            {
                return battleFormula.GetUnitsPerStructure(Structure) / 5;
            }
        }

        public override short Stamina
        {
            get
            {
                return -1;
            }
        }

        public override string DbTable
        {
            get
            {
                return DB_TABLE;
            }
        }

        public override DbColumn[] DbPrimaryKey
        {
            get
            {
                return new[] {new DbColumn("id", Id, DbType.UInt32), new DbColumn("city_id", battleManager.City.Id, DbType.UInt32)};
            }
        }

        public override DbDependency[] DbDependencies
        {
            get
            {
                return new DbDependency[] {};
            }
        }

        public override DbColumn[] DbColumns
        {
            get
            {
                return new[]
                       {
                               new DbColumn("last_round", LastRound, DbType.UInt32), new DbColumn("rounds_participated", RoundsParticipated, DbType.UInt32),
                               new DbColumn("damage_dealt", DmgDealt, DbType.Decimal), new DbColumn("damage_received", DmgRecv, DbType.Decimal),
                               new DbColumn("group_id", GroupId, DbType.UInt32), new DbColumn("structure_city_id", Structure.City.Id, DbType.UInt32),
                               new DbColumn("structure_id", Structure.ObjectId, DbType.UInt32), new DbColumn("hp", hp, DbType.Decimal),
                               new DbColumn("type", type, DbType.UInt16), new DbColumn("level", lvl, DbType.Byte), new DbColumn("max_hp", stats.MaxHp, DbType.Decimal),
                               new DbColumn("attack", stats.Atk, DbType.Decimal), new DbColumn("splash", stats.Splash, DbType.Byte),
                               new DbColumn("range", stats.Rng, DbType.Byte), new DbColumn("stealth", stats.Stl, DbType.Byte), new DbColumn("speed", stats.Spd, DbType.Byte),
                               new DbColumn("hits_dealt", HitDealt, DbType.UInt16), new DbColumn("hits_dealt_by_unit", HitDealtByUnit, DbType.UInt32),
                               new DbColumn("hits_received", HitRecv, DbType.UInt16),
                       };
            }
        }

        public override bool InRange(CombatObject obj)
        {
            if (obj is AttackCombatUnit)
            {
                ITroopObject troop = (obj as AttackCombatUnit).TroopStub.TroopObject;
                return RadiusLocator.Current.IsOverlapping(new Location(troop.X, troop.Y),
                                                           troop.Stats.AttackRadius,
                                                           new Location(Structure.X, Structure.Y),
                                                           Structure.Stats.Base.Radius);
            }

            throw new Exception(string.Format("Why is a structure trying to kill a unit of type {0}?", obj.GetType().FullName));
        }


        public override void Location(out uint x, out uint y) {
            x = Structure.X;
            y = Structure.Y;
        }

        public override void CalculateDamage(decimal dmg, out decimal actualDmg)
        {
            actualDmg = Math.Min(Hp, dmg);
        }

        public override void TakeDamage(decimal dmg, out Resource returning, out int attackPoints)
        {
            attackPoints = 0;

            hp = (dmg > hp) ? 0 : hp - dmg;
            
            Structure.BeginUpdate();
            Structure.Stats.Hp = hp;
            Structure.EndUpdate();

            if (hp == 0)
                attackPoints = formula.GetStructureKilledAttackPoint(type, lvl);

            returning = null;
        }

        public override void CleanUp()
        {
            base.CleanUp();

            // Remove structure if our combat object died
            if (hp <= 0)
            {
                ICity city = Structure.City;

                World.Current.LockRegion(Structure.X, Structure.Y);
                if (Structure.Lvl > 1)
                {
                    Structure.BeginUpdate();
                    Structure.State = GameObjectState.NormalState();
                    Structure.EndUpdate();

                    Structure.City.Worker.DoPassive(Structure.City, new StructureDowngradePassiveAction(Structure.City.Id, Structure.ObjectId), false);
                }
                else
                {
                    Structure.BeginUpdate();
                    World.Current.Remove(Structure);
                    city.ScheduleRemove(Structure, true);
                    Structure.EndUpdate();
                }
                World.Current.UnlockRegion(Structure.X, Structure.Y);
            }           
        }

        public override void ExitBattle()
        {
            Structure.BeginUpdate();
            Structure.State = GameObjectState.NormalState();
            Structure.EndUpdate();
        }

        public override void ReceiveReward(int reward, Resource resource)
        {
        }

        public override int CompareTo(object other)
        {
            if (other is IStructure)
                return other == Structure ? 0 : 1;

            return -1;
        }
    }
}