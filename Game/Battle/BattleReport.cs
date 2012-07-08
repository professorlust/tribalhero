#region

using System.Collections.Generic;
using Game.Data;
using Game.Util;
using Persistance;

#endregion

namespace Game.Battle
{
    public class BattleReport : IBattleReport
    {
        private readonly IBattleReportWriter battleReportWriter;
        public static readonly LargeIdGenerator BattleIdGenerator = new LargeIdGenerator(uint.MaxValue);
        public static readonly LargeIdGenerator ReportIdGenerator = new LargeIdGenerator(uint.MaxValue);
        public static readonly LargeIdGenerator BattleTroopIdGenerator = new LargeIdGenerator(uint.MaxValue);

        private IBattleManager battle;

        /// <summary>
        /// The battle manager that this report belongs to
        /// </summary>
        public IBattleManager Battle
        {
            set
            {
                battle = value;
                ReportedObjects = new ReportedObjects(battle.BattleId);
                ReportedTroops = new ReportedTroops(battle.BattleId);
            }
        }

        /// <summary>
        /// Indicates whether we've already begun a snapshot or not.
        /// </summary>
        public bool ReportStarted { get; set; }

        /// <summary>
        /// Not sure what this report flag is actually doing at the moment. I think it may be removed and only ReportStarted may be used.
        /// </summary>
        public bool ReportFlag { get; set; }

        /// <summary>
        /// Id of the current report. This will change as each new snapshot is taken.
        /// </summary>
        public uint ReportId { get; set; }

        /// <summary>
        /// Persistable list of the objects that have been reported for this snapshot.
        /// </summary>
        public ReportedObjects ReportedObjects { get; private set; }

        /// <summary>
        /// Persistable list of troops that have been reported for this snapshot.
        /// </summary>
        public ReportedTroops ReportedTroops { get; private set; }

        public BattleReport(IBattleReportWriter battleReportWriter)
        {
            this.battleReportWriter = battleReportWriter;
        }

        /// <summary>
        /// Starts the battle report if it hasn't yet.
        /// This will snap all of the current troops.
        /// </summary>
        private void WriteBeginReport()
        {
            if (ReportStarted)
                return;

            uint newReportId;
            battleReportWriter.SnapReport(out newReportId, battle.BattleId);

            ReportId = newReportId;
            ReportStarted = true;
        }

        /// <summary>
        /// Starts the battle report. This should only be called once per battle when it starts.
        /// </summary>
        public void CreateBattleReport()
        {
            battleReportWriter.SnapBattle(battle.BattleId, battle.City.Id);
            WriteBeginReport();
            ReportFlag = true;
            CompleteReport(ReportState.Entering);
        }

        /// <summary>
        /// Completes the report. This should only be called once per battle when it ends.
        /// </summary>
        public void CompleteBattle()
        {
            WriteBeginReport();
            ReportFlag = true;
            CompleteReport(ReportState.Exiting);
            battleReportWriter.SnapBattleEnd(battle.BattleId);
        }

        /// <summary>
        /// Writes the specified combat object (if it hasn't yet) to the report
        /// </summary>
        /// <param name="combatObject"></param>
        /// <param name="isAttacker"></param>
        /// <param name="state"></param>
        public void WriteReportObject(CombatObject combatObject, bool isAttacker, ReportState state)
        {
            uint combatTroopId;

            // Start the report incase it hasn't yet
            WriteBeginReport();

            // Check if we've already snapped this troop
            bool troopAlreadySnapped = ReportedTroops.TryGetValue(combatObject.TroopStub, out combatTroopId);

            if (!troopAlreadySnapped)
            {
                // Snap the troop
                combatTroopId = battleReportWriter.SnapTroop(ReportId,
                                             state,
                                             combatObject.City.Id,
                                             combatObject.TroopStub.TroopId,
                                             combatObject.GroupId,
                                             isAttacker,
                                             combatObject.GroupLoot);

                ReportedTroops[combatObject.TroopStub] = combatTroopId;
            }

            // Update the state if it's not Staying (Staying is the default state basically.. anything else overrides it)
            // TODO: This is currently not very efficient since this logic can run several times for the same group
            // We should really add the idea of groups into the battle so it can call this only once per group
            if (state != ReportState.Staying)
            {
                battleReportWriter.SnapTroopState(combatTroopId, combatObject.TroopStub, state);

                // Log any troops that are entering the battle to the view table so they are able to see this report
                // Notice that we don't log the local troop. This is because they can automatically see all of the battles that take place in their cities by using the battles table                    
                if (battle.City != combatObject.City)
                {
                    switch(state)
                    {
                            // When entering, we log the initial report id
                        case ReportState.Entering:
                            if (!troopAlreadySnapped)
                            {
                                battleReportWriter.SnapBattleReportView(combatObject.City.Id,
                                                                        combatObject.TroopStub.TroopId,
                                                                        battle.BattleId,
                                                                        combatObject.GroupId,
                                                                        isAttacker,
                                                                        ReportId);
                            }
                            break;
                            // When exiting, we log the end report id
                        case ReportState.Exiting:
                        case ReportState.Dying:
                        case ReportState.OutOfStamina:
                        case ReportState.Retreating:
                            battleReportWriter.SnapBattleReportViewExit(battle.BattleId, combatObject.GroupId, ReportId);
                            break;
                    }
                }
            }

            // Check if we've already snapped the combat object
            if (!ReportedObjects.Contains(combatObject))
            {
                // Snap the combat objects
                battleReportWriter.SnapCombatObject(combatTroopId, combatObject);
                ReportedObjects.Add(combatObject);
            }
        }

        /// <summary>
        /// Writes a list of combat objects
        /// </summary>
        /// <param name="list"></param>
        /// <param name="isAttacker"></param>
        /// <param name="state"></param>
        public void WriteReportObjects(IEnumerable<CombatObject> list, bool isAttacker, ReportState state)
        {
            WriteBeginReport();

            ReportFlag = true;

            foreach (var co in list)
            {
                WriteReportObject(co, isAttacker, state);
            }
        }

        /// <summary>
        /// Completes the current snapshot if it has been started.
        /// </summary>
        /// <param name="state"></param>
        public void CompleteReport(ReportState state)
        {
            if (!ReportStarted || !ReportFlag)
            {
                return;
            }

            WriteReportObjects(battle.Attackers, true, state);
            WriteReportObjects(battle.Defender, false, state);
            battleReportWriter.SnapEndReport(ReportId, battle.BattleId, battle.Round, battle.Turn);
            ReportedObjects.Clear();
            ReportedTroops.Clear();
            ReportStarted = false;
            ReportFlag = false;
        }

        public void SetLootedResources(uint cityId, byte troopId, uint battleId, Resource lootResource, Resource bonusResource)
        {
            battleReportWriter.SnapLootedResources(cityId, troopId, battleId, lootResource, bonusResource);
        }

        public IEnumerable<DbDependency> DbDependencies
        {
            get
            {
                return new[] {new DbDependency("ReportedObjects", true, true), new DbDependency("ReportedTroops", true, true)};
            }        
        }
    }
}