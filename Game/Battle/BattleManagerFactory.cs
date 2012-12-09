﻿using Game.Battle.Reporting;
using Game.Battle.RewardStrategies;
using Game.Comm.Channel;
using Game.Data;
using Game.Data.Stronghold;
using Ninject;
using Persistance;

namespace Game.Battle
{
    class BattleManagerFactory : IBattleManagerFactory
    {
        private readonly IKernel kernel;

        public BattleManagerFactory(IKernel kernel)
        {
            this.kernel = kernel;
        }

        public IBattleManager CreateBattleManager(uint battleId, BattleLocation battleLocation, BattleOwner battleOwner, ICity city)
        {
            var bm = new BattleManager(battleId,
                                       battleLocation,
                                       battleOwner,
                                       kernel.Get<IRewardStrategyFactory>().CreateCityRewardStrategy(city),
                                       kernel.Get<IDbManager>(),
                                       kernel.Get<IBattleReport>(),
                                       kernel.Get<ICombatListFactory>(),
                                       kernel.Get<BattleFormulas>());

            new BattleChannel(bm);

            bm.BattleReport.Battle = bm;
            return bm;
        }

        public IBattleManager CreateBattleManager(BattleLocation location, BattleOwner owner, ICity city)
        {
            var battleId = (uint)BattleReport.BattleIdGenerator.GetNext();
            return CreateBattleManager(battleId, location, owner, city);
        }

        public IBattleManager CreateStrongholdMainBattleManager(uint battleId, BattleLocation battleLocation, BattleOwner battleOwner, IStronghold stronghold)
        {
            var bm = new BattleManager(battleId,
                                       battleLocation,
                                       battleOwner,
                                       kernel.Get<IRewardStrategyFactory>().CreateStrongholdRewardStrategy(stronghold),
                                       kernel.Get<IDbManager>(),
                                       kernel.Get<IBattleReport>(),
                                       kernel.Get<ICombatListFactory>(),
                                       kernel.Get<BattleFormulas>());

            new BattleChannel(bm);

            bm.BattleReport.Battle = bm;
            return bm;
        }

        public IBattleManager CreateStrongholdMainBattleManager(BattleLocation battleLocation, BattleOwner battleOwner, IStronghold stronghold)
        {
            var battleId = (uint)BattleReport.BattleIdGenerator.GetNext();
            return CreateStrongholdMainBattleManager(battleId, battleLocation, battleOwner, stronghold);
        }

        public IBattleManager CreateStrongholdGateBattleManager(BattleLocation battleLocation, BattleOwner battleOwner, IStronghold stronghold)
        {
            var battleId = (uint)BattleReport.BattleIdGenerator.GetNext();
            return CreateStrongholdGateBattleManager(battleId, battleLocation, battleOwner, stronghold);
        }

        public IBattleManager CreateStrongholdGateBattleManager(uint battleId, BattleLocation battleLocation, BattleOwner battleOwner, IStronghold stronghold)
        {
            var bm = new BattleManagerPrivate(battleId,
                                       battleLocation,
                                       battleOwner,
                                       kernel.Get<IRewardStrategyFactory>().CreateStrongholdRewardStrategy(stronghold),
                                       kernel.Get<IDbManager>(),
                                       new BattleReport(new NullBattleReportWriter()),
                                       kernel.Get<ICombatListFactory>(),
                                       kernel.Get<BattleFormulas>());

            new BattleChannel(bm);

            bm.BattleReport.Battle = bm;
            return bm;
        }
    }
}
