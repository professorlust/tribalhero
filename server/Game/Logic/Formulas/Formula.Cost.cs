#region

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Stats;
using Game.Data.Tribe;
using Game.Setup;

#endregion

namespace Game.Logic.Formulas
{
    public partial class Formula
    {
        /// <summary>
        ///     Returns the cost for building the specified structure
        /// </summary>
        public virtual Resource StructureCost(ICity city, IStructureBaseStats stats)
        {
            if (city.Battle == null)
            {
                return stats.Cost;
            }

            return stats.Cost * Config.battle_cost_penalty;
        }

        /// <summary>
        ///     Returns the cost for training the specified unit
        /// </summary>
        public virtual Resource UnitTrainCost(ICity city, ushort type, byte lvl)
        {
            if (city.Battle == null)
            {
                return UnitFactory.GetCost(type, lvl);
            }

            return UnitFactory.GetCost(type, lvl) * Config.battle_cost_penalty;
        }

        /// <summary>
        ///     Returns the cost for upgrading the specified unit
        /// </summary>
        public virtual Resource UnitUpgradeCost(ICity city, ushort type, byte lvl)
        {
            if (city.Battle == null)
            {
                return UnitFactory.GetUpgradeCost(type, lvl);
            }

            return UnitFactory.GetUpgradeCost(type, lvl) * Config.battle_cost_penalty;
        }

        /// <summary>
        ///     Returns the amount of HP that can be repaired by the specified structure
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public virtual ushort RepairRate(IStructure structure)
        {
            return (ushort)(structure.Stats.Labor * 5);
        }

        /// <summary>
        ///     Returns the cost for repairing the specified amount
        /// </summary>
        public virtual Resource RepairCost(ICity city, ushort repairPower)
        {
            int lumber = city.Technologies.GetEffects(EffectCode.RepairSaving)
                             .Aggregate<Effect, int>(repairPower,
                                                     (current, effect) =>
                                                     current - repairPower * (int)effect.Value[0] / 100);

            return new Resource(0, 0, 0, lumber, 0);
        }

        /// <summary>
        ///     Maximum amount of laborers that can be assigned at once
        /// </summary>
        public virtual ushort LaborMoveMax(IStructure structure)
        {
            return (ushort)Math.Max(5, Math.Ceiling(structure.Stats.Base.MaxLabor / 2.0));
        }

        /// <summary>
        ///     Specifies which resources can be purchased in the market
        /// </summary>
        public virtual IEnumerable<ResourceType> MarketResourceSellable(IStructure structure)
        {
            if (structure.Lvl >= 8)
            {
                return new[] {ResourceType.Wood, ResourceType.Crop, ResourceType.Iron};
            }
            return new[] {ResourceType.Wood, ResourceType.Crop};
        }

        /// <summary>
        ///     Specifies which resources can be sold in the market
        /// </summary>
        public virtual IEnumerable<ResourceType> MarketResourceBuyable(IStructure structure)
        {
            if (structure.Lvl >= 8)
            {
                return new[] {ResourceType.Wood, ResourceType.Crop, ResourceType.Iron};
            }
            if (structure.Lvl >= 3)
            {
                return new[] {ResourceType.Wood, ResourceType.Crop};
            }
            return new ResourceType[] {};
        }

        /// <summary>
        ///     Specifies which resources can be sold in the market
        /// </summary>
        /// <param name="structure"></param>
        /// <returns></returns>
        public virtual int MarketTradeQuantity(IStructure structure)
        {
            int[] quantity = {0, 200, 400, 400, 400, 700, 700, 1100, 1100, 1800, 1800};
            return quantity[structure.Lvl];
        }

        /// <summary>
        ///     Gets amount of resources that are hidden
        /// </summary>
        /// <param name="city"></param>
        /// <param name="checkAlignmentPointBonus"></param>
        /// <returns></returns>
        public virtual Resource HiddenResource(ICity city, bool checkAlignmentPointBonus = false)
        {
            int[] rateCrop = {0, 100, 150, 220, 330, 500, 740, 1100, 1100, 1100, 1100};
            int[] rateWood = {0, 100, 150, 220, 330, 500, 740, 1100, 1100, 1100, 1100};
            int[] rateGold = {0, 0, 0, 0, 0, 100, 150, 220, 330, 500, 740};
            int[] rateIron = {0, 0, 0, 0, 0, 0, 0, 0, 200, 360, 660};

            var resource = new Resource();
            foreach (var structure in city.Where(x => ObjectTypeFactory.IsStructureType("Basement", x)))
            {
                resource.Add(rateCrop[structure.Lvl],
                             rateGold[structure.Lvl],
                             rateIron[structure.Lvl],
                             rateWood[structure.Lvl],
                             0);
            }

            int[] tempRateCrop = { 0, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700 };
            int[] tempRateWood = { 0, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700 };
            int[] tempRateGold = { 0, 0, 0, 0, 0, 500, 600, 700, 800, 900, 1000 };
            int[] tempRateIron = { 0, 0, 0, 0, 0, 0, 0, 0, 500, 750, 1000 };
            foreach (var structure in city.Where(x => ObjectTypeFactory.IsStructureType("TempBasement", x)))
            {
                resource.Add(tempRateCrop[structure.Lvl],
                             tempRateGold[structure.Lvl],
                             tempRateIron[structure.Lvl],
                             tempRateWood[structure.Lvl],
                             0);
            }

            if (checkAlignmentPointBonus && city.AlignmentPoint >= 75m)
            {
                const double pct = .75;
                return new Resource((int)Math.Max(city.Resource.Crop.Limit * pct, resource.Crop),
                                    (int)Math.Max(city.Resource.Gold.Limit * pct, resource.Gold),
                                    (int)Math.Max(city.Resource.Iron.Limit * pct, resource.Iron),
                                    (int)Math.Max(city.Resource.Wood.Limit * pct, resource.Wood),
                                    0);
            }
            return resource;
        }

        /// <summary>
        ///     Returns the amount of resources the user will get back when cancelling an action
        /// </summary>
        /// <param name="beginTime"></param>
        /// <param name="cost"></param>
        /// <returns></returns>
        public virtual Resource GetActionCancelResource(DateTime beginTime, Resource cost)
        {
            if (DateTime.UtcNow.Subtract(beginTime).TotalSeconds <= Config.actions_free_cancel_interval_in_sec)
            {
                return cost;
            }

            return new Resource(cost / 2);
        }

        public virtual Resource GetSendCapacity(IStructure structure)
        {
            //Maximum amount of resources that can be sent from the specified structure        
            int[] sendRate = {0, 200, 200, 400, 400, 600, 600, 800, 1000, 1200, 1200, 1400, 1600, 1800, 1800, 2000};
            return new Resource(sendRate[structure.Lvl],
                                sendRate[structure.Lvl],
                                sendRate[structure.Lvl],
                                sendRate[structure.Lvl],
                                sendRate[structure.Lvl]);
        }

        public virtual Resource GetContributeCapacity(IStructure structure)
        {
            //Maximum amount of resources that can be contribute from the specified structure    
            return GetSendCapacity(structure) * 2;
        }

        public virtual ushort CalculateCityValue(ICity city)
        {
            return (ushort)city.Where(structure => !ObjectTypeFactory.IsStructureType("NoInfluencePoint", structure)).Sum(x => x.Lvl * x.Size);
        }

        public virtual decimal CalculateTotalCityExpense(ICity city, IStructureCsvFactory structureFactory, TechnologyFactory technologyFactory, UnitFactory unitFactory)
        {
            decimal expenses = 0m;

            Func<Resource, decimal> calculateCost = resource => resource.Crop + resource.Wood + resource.Gold * 2 + resource.Iron * 5 + resource.Labor * 100;

            foreach (var structure in city)
            {
                for (var lvl = 0; lvl <= structure.Lvl; lvl++)
                {
                    expenses += calculateCost(structureFactory.GetCost(structure.Type, lvl));
                }

                foreach (var technology in structure.Technologies)
                {
                    for (var lvl = 0; lvl <= technology.Level; lvl++)
                    {
                        expenses += calculateCost(technologyFactory.GetTechnology(technology.Type, (byte)lvl).TechBase.Resources);
                    }
                }
            }

            foreach (var unit in city.Template)
            {
                for (var lvl = 1; lvl <= unit.Value.Lvl; lvl++)
                {
                    expenses += calculateCost(unitFactory.GetUpgradeCost(unit.Value.Type, lvl));
                }
            }

            return expenses;
        }

        public virtual bool IsWeaponExportOverLimit(int weaponExport, int currentGold)
        {
            return weaponExport * 2000 < currentGold;
        }

        public virtual int GetWeaponExportLaborProduce(int weaponExport, int labor, int currentGold)
        {
            var isOverLimit = weaponExport * 2000 < currentGold;
            labor = Math.Min(labor, weaponExport * 60);
            switch(weaponExport)
            {
                case 1:
                case 2:
                    return labor / (isOverLimit? 15: 3);
                case 3:
                case 4:
                    return labor * 2 / (isOverLimit ? 25 : 5);
                case 5:
                    return labor / (isOverLimit ? 10 : 2);
                default:
                    return 0;
            }
        }

        public virtual Resource GetTribeUpgradeCost(byte level)
        {
            return new Resource(crop: 1000, gold: 400, iron: 40, wood: 2000) * Tribe.MEMBERS_PER_LEVEL * level * (1 + ((double)level * level / 20));
        }

        public virtual void GetNewCityCost(int cityCount, out int influencePoints, out int wagons)
        {
            influencePoints = (80 + 50 * cityCount) * cityCount;
            wagons = 50 * cityCount;
        }
    }
}