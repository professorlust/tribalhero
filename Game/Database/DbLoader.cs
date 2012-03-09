#region

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Game.Battle;
using Game.Data;
using Game.Data.Stats;
using Game.Data.Tribe;
using Game.Data.Troop;
using Game.Logic;
using Game.Logic.Actions;
using Game.Logic.Actions.ResourceActions;
using Game.Logic.Formulas;
using Game.Map;
using Game.Module;
using Game.Setup;
using Game.Util;
using Ninject;
using Persistance;

#endregion

namespace Game.Database
{
    public class DbLoader
    {
        private readonly IDbManager dbManager;

        private readonly DbLoaderActionFactory actionFactory;

        public DbLoader(IDbManager dbManager, DbLoaderActionFactory actionFactory)
        {
            this.dbManager = dbManager;
            this.actionFactory = actionFactory;
        }

        public bool LoadFromDatabase()
        {
            SystemVariablesUpdater.Pause();
            Scheduler.Current.Pause();
            Global.FireEvents = false;

            Global.Logger.Info("Loading database...");

            DateTime now = DateTime.UtcNow;

            using (Persistance.DbTransaction transaction = dbManager.GetThreadTransaction())
            {
                try
                {
                    // Set all players to offline
                    dbManager.Query("UPDATE `players` SET `online` = @online", new[] { new DbColumn("online", false, DbType.Boolean) });

                    // Load sys vars
                    LoadSystemVariables(dbManager);

                    // Calculate how long server was down
                    TimeSpan downTime = now.Subtract((DateTime)Global.SystemVariables["System.time"].Value);
                    if (downTime.TotalMilliseconds < 0) downTime = new TimeSpan(0);
                    
                    Global.Logger.Info(string.Format("Server was down for {0}", downTime));

                    LoadReportIds(dbManager);
                    LoadMarket(dbManager);
                    LoadPlayers(dbManager);
                    LoadCities(dbManager, downTime);
                    LoadTribes(dbManager);
                    LoadTribesmen(dbManager);
                    LoadUnitTemplates(dbManager);
                    LoadStructures(dbManager);
                    LoadStructureProperties(dbManager);
                    LoadTechnologies(dbManager);
                    LoadForests(dbManager, downTime);
                    LoadTroopStubs(dbManager);
                    LoadTroopStubTemplates(dbManager);
                    LoadTroops(dbManager);
                    LoadBattleManagers(dbManager);
                    LoadActions(dbManager, downTime);
                    LoadActionReferences(dbManager);
                    LoadActionNotifications(dbManager);
                    LoadAssignments(dbManager,downTime);

                    World.Current.AfterDbLoaded();

                    //Ok data all loaded. We can get the system going now.
                    Global.SystemVariables["System.time"].Value = now;
                    dbManager.Save(Global.SystemVariables["System.time"]);
                }
                catch(Exception e)
                {
                    Global.Logger.Error("Database loader error", e);
                    transaction.Rollback();
                    return false;
                }
            }

            Global.Logger.Info("Database loading finished");

            SystemVariablesUpdater.Resume();
            Global.FireEvents = true;
            Scheduler.Current.Resume();
            return true;
        }

        private uint GetMaxId(IDbManager dbManager, string table)
        {
            using (var reader = dbManager.ReaderQuery(string.Format("SELECT max(`id`) FROM `{0}`", table)))
            {
                reader.Read();
                if (DBNull.Value.Equals(reader[0]))
                    return 0;

                return (uint)reader[0];
            }
        }

        private void LoadReportIds(IDbManager dbManager)
        {
            BattleReport.BattleIdGenerator.Set(GetMaxId(dbManager, SqlBattleReportWriter.BATTLE_DB));
            BattleReport.ReportIdGenerator.Set(GetMaxId(dbManager, SqlBattleReportWriter.BATTLE_REPORTS_DB));
            BattleReport.BattleTroopIdGenerator.Set(GetMaxId(dbManager, SqlBattleReportWriter.BATTLE_REPORT_TROOPS_DB));
        }

        private void LoadTribes(IDbManager dbManager) 
        {
            #region Tribes

            Global.Logger.Info("Loading tribes...");
            using (var reader = dbManager.Select(Tribe.DB_TABLE))
            {
                while (reader.Read())
                {
                    var resource = new Resource((int)reader["crop"], (int)reader["gold"], (int)reader["iron"], (int)reader["wood"], 0);
                    var tribe = new Tribe(World.Current.Players[(uint)reader["player_id"]],
                                          (string)reader["name"],
                                          (string)reader["desc"],
                                          (byte)reader["level"],
                                          (int)reader["attack_point"],
                                          (int)reader["defense_point"],
                                          resource) {DbPersisted = true};
                    Global.Tribes.Add(tribe.Id, tribe);
                }
            }
            #endregion
        }

        private void LoadTribesmen(IDbManager dbManager) {
            #region Tribes

            Global.Logger.Info("Loading tribesmen...");
            using (var reader = dbManager.Select(Tribesman.DB_TABLE)) {
                while (reader.Read()) {
                    ITribe tribe = Global.Tribes[(uint)reader["tribe_id"]];
                    var contribution = new Resource((int)reader["crop"], (int)reader["gold"], (int)reader["iron"], (int)reader["wood"], 0);
                    var tribesman = new Tribesman(tribe, World.Current.Players[(uint)reader["player_id"]], DateTime.SpecifyKind((DateTime)reader["join_date"], DateTimeKind.Utc), contribution, (byte)reader["rank"])
                                    {DbPersisted = true};
                    tribe.AddTribesman(tribesman,false);
                }
            }
            #endregion
        }

        private void LoadAssignments(IDbManager dbManager, TimeSpan downTime)
        {
            #region Assignments

            IAssignmentFactory assignmentFactory = Ioc.Kernel.Get<IAssignmentFactory>();

            Global.Logger.Info("Loading assignements...");
            using (var reader = dbManager.Select(Assignment.DB_TABLE))
            {
                while (reader.Read())
                {
                    ITribe tribe = Global.Tribes[(uint)reader["tribe_id"]];
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    Assignment assignment = assignmentFactory.CreateAssignmentFromDb((int)reader["id"],
                                                           tribe,
                                                           (uint)reader["x"],
                                                           (uint)reader["y"],
                                                           city,
                                                           (AttackMode)Enum.Parse(typeof(AttackMode), (string)reader["mode"]),
                                                           DateTime.SpecifyKind((DateTime)reader["attack_time"], DateTimeKind.Utc).Add(downTime),
                                                           (uint)reader["dispatch_count"],
                                                           (string)reader["description"]);

                    using (DbDataReader listReader = dbManager.SelectList(assignment))
                    {
                        while (listReader.Read())
                        {
                            if (!World.Current.TryGetObjects((uint)listReader["city_id"], out city))
                                throw new Exception("City not found");

                            ITroopStub assignmentStub;
                            if (!city.Troops.TryGetStub((byte)listReader["stub_id"], out assignmentStub))
                                throw new Exception("Stub not found");

                            assignment.DbLoaderAdd(assignmentStub, (byte)listReader["dispatched"] == 1);
                        }
                    }

                    assignment.DbPersisted = true;
                    
                    // Add assignment to tribe
                    tribe.DbLoaderAddAssignment(assignment);

                    // Reschedule and save assignment
                    assignment.Reschedule();
                }
            }

            #endregion
        }

        private void LoadSystemVariables(IDbManager dbManager)
        {
            #region System variables

            Global.Logger.Info("Loading system variables...");
            using (var reader = dbManager.Select(SystemVariable.DB_TABLE))
            {
                while (reader.Read())
                {
                    var systemVariable = new SystemVariable((string)reader["name"],
                                                            DataTypeSerializer.Deserialize((string)reader["value"], (byte)reader["datatype"]))
                                         {DbPersisted = true};
                    Global.SystemVariables.Add(systemVariable.Key, systemVariable);
                }
            }

            // Set system variable defaults
            if (!Global.SystemVariables.ContainsKey("System.time"))
                Global.SystemVariables.Add("System.time", new SystemVariable("System.time", DateTime.UtcNow));

            if (!Global.SystemVariables.ContainsKey("Map.start_index"))
                Global.SystemVariables.Add("Map.start_index", new SystemVariable("Map.start_index", 0));

            #endregion
        }

        private void LoadMarket(IDbManager dbManager)
        {
            #region Market

            Global.Logger.Info("Loading market...");
            using (var reader = dbManager.Select(Market.DB_TABLE))
            {
                while (reader.Read())
                {
                    var type = (ResourceType)((byte)reader["resource_type"]);
                    var market = new Market(type, (int)reader["price"]);
                    market.DbLoad((int)reader["outgoing"], (int)reader["incoming"]);
                    market.DbPersisted = true;
                    switch(type)
                    {
                        case ResourceType.Crop:
                            Market.Crop = market;
                            break;
                        case ResourceType.Wood:
                            Market.Wood = market;
                            break;
                        case ResourceType.Iron:
                            Market.Iron = market;
                            break;
                        default:
                            continue;
                    }
                }
            }

            #endregion
        }

        private void LoadPlayers(IDbManager dbManager)
        {
            #region Players

            Global.Logger.Info("Loading players...");
            using (var reader = dbManager.Select(Player.DB_TABLE))
            {
                while (reader.Read())
                {
                    var player = new Player((uint)reader["id"],
                                            DateTime.SpecifyKind((DateTime)reader["created"], DateTimeKind.Utc),
                                            DateTime.SpecifyKind((DateTime)reader["last_login"], DateTimeKind.Utc),
                                            (string)reader["name"],
                                            (string)reader["description"],
                                            false) { DbPersisted = true, TribeRequest = (uint)reader["invitation_tribe_id"] };
                    World.Current.Players.Add(player.PlayerId, player);
                }
            }

            #endregion
        }

        private void LoadCities(IDbManager dbManager, TimeSpan downTime)
        {
            #region Cities

            Global.Logger.Info("Loading cities...");
            using (var reader = dbManager.Select(City.DB_TABLE))
            {
                while (reader.Read())
                {

                    DateTime cropRealizeTime = DateTime.SpecifyKind((DateTime)reader["crop_realize_time"], DateTimeKind.Utc).Add(downTime);
                    DateTime woodRealizeTime = DateTime.SpecifyKind((DateTime)reader["wood_realize_time"], DateTimeKind.Utc).Add(downTime);
                    DateTime ironRealizeTime = DateTime.SpecifyKind((DateTime)reader["iron_realize_time"], DateTimeKind.Utc).Add(downTime);
                    DateTime laborRealizeTime = DateTime.SpecifyKind((DateTime)reader["labor_realize_time"], DateTimeKind.Utc).Add(downTime);
                    DateTime goldRealizeTime = DateTime.SpecifyKind((DateTime)reader["gold_realize_time"], DateTimeKind.Utc).Add(downTime);

                    var resource = new LazyResource((int)reader["crop"],
                                                    cropRealizeTime,
                                                    (int)reader["crop_production_rate"],
                                                    (int)reader["crop_upkeep"],
                                                    (int)reader["gold"],
                                                    goldRealizeTime,
                                                    (int)reader["gold_production_rate"],
                                                    (int)reader["iron"],
                                                    ironRealizeTime,
                                                    (int)reader["iron_production_rate"],
                                                    (int)reader["wood"],
                                                    woodRealizeTime,
                                                    (int)reader["wood_production_rate"],
                                                    (int)reader["labor"],
                                                    laborRealizeTime,
                                                    (int)reader["labor_production_rate"]);
                    var city = new City(World.Current.Players[(uint)reader["player_id"]], (string)reader["name"], resource, (byte)reader["radius"], null)
                               {
                                       DbPersisted = true,
                                       LootStolen = (uint)reader["loot_stolen"],
                                       AttackPoint = (int)reader["attack_point"],
                                       DefensePoint = (int)reader["defense_point"],
                                       HideNewUnits = (bool)reader["hide_new_units"],
                                       Value = (ushort)reader["value"],
                                       Deleted = (City.DeletedState)reader["deleted"]
                               };

                    World.Current.DbLoaderAdd((uint)reader["id"], city);

                    switch (city.Deleted)
                    {
                        case City.DeletedState.Deleting:
                            city.Owner.Add(city);
                            CityRemover cr = new CityRemover(city.Id);
                            cr.Start(true);
                            break;
                        case City.DeletedState.NotDeleted:
                            city.Owner.Add(city);
                            break;
                    }
                }
            }

            #endregion
        }

        private void LoadUnitTemplates(IDbManager dbManager)
        {
            #region Unit Template

            Global.Logger.Info("Loading unit template...");
            using (var reader = dbManager.Select(UnitTemplate.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");
                    
                    city.Template.DbPersisted = true;

                    using (DbDataReader listReader = dbManager.SelectList(city.Template))
                    {
                        while (listReader.Read())
                            city.Template.DbLoaderAdd((ushort)listReader["type"],
                                                      Ioc.Kernel.Get<UnitFactory>().GetUnitStats((ushort)listReader["type"], (byte)listReader["level"]));
                    }
                }
            }

            #endregion
        }

        private void LoadForests(IDbManager dbManager, TimeSpan downTime)
        {
            Global.Logger.Info("Loading forests...");
            using (var reader = dbManager.Select(Forest.DB_TABLE))
            {
                while (reader.Read())
                {
                    var forest = new Forest((byte)reader["level"], (int)reader["capacity"], (float)reader["rate"])
                                 {
                                         DbPersisted = true,
                                         X = (uint)reader["x"],
                                         Y = (uint)reader["y"],
                                         Labor = (ushort)reader["labor"],
                                         ObjectId = (uint)reader["id"],
                                         State = {Type = (ObjectState)((byte)reader["state"])},
                                         Wood =
                                                 new AggressiveLazyValue((int)reader["lumber"],
                                                                         DateTime.SpecifyKind((DateTime)reader["last_realize_time"], DateTimeKind.Utc).Add(downTime),
                                                                         0,
                                                                         (int)reader["upkeep"]) {Limit = (int)reader["capacity"]},
                                         DepleteTime = DateTime.SpecifyKind((DateTime)reader["deplete_time"], DateTimeKind.Utc).Add(downTime),
                                         InWorld = (bool)reader["in_world"]
                                 };

                    foreach (var variable in XmlSerializer.DeserializeList((string)reader["state_parameters"]))
                        forest.State.Parameters.Add(variable);

                    // Add lumberjacks
                    foreach (var vars in XmlSerializer.DeserializeComplexList((string)reader["structures"]))
                    {
                        ICity city;
                        if (!World.Current.TryGetObjects((uint)vars[0], out city))
                            throw new Exception("City not found");

                        IStructure structure;
                        if (!city.TryGetStructure((uint)vars[1], out structure))
                            throw new Exception("Structure not found");

                        forest.AddLumberjack(structure);
                    }

                    if (forest.InWorld)
                    {
                        // Create deplete time
                        forest.DepleteAction = new ForestDepleteAction(forest, forest.DepleteTime);
                        Scheduler.Current.Put(forest.DepleteAction);
                        World.Current.DbLoaderAdd(forest);
                        World.Current.Forests.DbLoaderAdd(forest);                        
                    }

                    // Resave to include new time
                    dbManager.Save(forest);
                }
            }
        }

        private void LoadStructures(IDbManager dbManager)
        {
            #region Structures

            Global.Logger.Info("Loading structures...");
            using (var reader = dbManager.Select(Structure.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");
                    IStructure structure = Ioc.Kernel.Get<StructureFactory>().GetNewStructure((ushort)reader["type"], (byte)reader["level"]);
                    structure.InWorld = (bool)reader["in_world"];
                    structure.Technologies.Parent = city.Technologies;
                    structure.X = (uint)reader["x"];
                    structure.Y = (uint)reader["y"];
                    structure.Stats.Hp = (decimal)reader["hp"];
                    structure.ObjectId = (uint)reader["id"];
                    structure.Stats.Labor = (ushort)reader["labor"];
                    structure.DbPersisted = true;
                    structure.State.Type = (ObjectState)((byte)reader["state"]);
                    structure.IsBlocked = (bool)reader["is_blocked"];

                    foreach (var variable in XmlSerializer.DeserializeList((string)reader["state_parameters"]))
                        structure.State.Parameters.Add(variable);

                    city.Add(structure.ObjectId, structure, false);

                    if (structure.InWorld)
                        World.Current.DbLoaderAdd(structure);
                }
            }
            
            #endregion
        }

        private void LoadStructureProperties(IDbManager dbManager)
        {
            #region Structure Properties

            Global.Logger.Info("Loading structure properties...");
            using (var reader = dbManager.Select(StructureProperties.DB_TABLE))
            {
                ICity city = null;
                while (reader.Read())
                {
                    // Simple optimization                        
                    if (city == null || city.Id != (uint)reader["city_id"])
                    {
                        if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                            throw new Exception("City not found");
                    }

                    var structure = (IStructure)city[(uint)reader["structure_id"]];

                    structure.Properties.DbPersisted = true;

                    using (DbDataReader listReader = dbManager.SelectList(structure.Properties))
                    {
                        while (listReader.Read())
                            structure.Properties.Add(listReader["name"],
                                                     DataTypeSerializer.Deserialize((string)listReader["value"], (byte)listReader["datatype"]));
                    }
                }
            }

            #endregion
        }

        private void LoadTechnologies(IDbManager dbManager)
        {
            #region Technologies

            Global.Logger.Info("Loading technologies...");
            using (var reader = dbManager.Select(TechnologyManager.DB_TABLE))
            {
                while (reader.Read())
                {
                    var ownerLocation = (EffectLocation)((byte)reader["owner_location"]);

                    ITechnologyManager manager;

                    switch(ownerLocation)
                    {
                        case EffectLocation.Object:
                        {
                            ICity city;
                            if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                                throw new Exception("City not found");

                            var structure = (IStructure)city[(uint)reader["owner_id"]];
                            manager = structure.Technologies;
                        }
                            break;
                        case EffectLocation.City:
                        {
                            ICity city;
                            if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                                throw new Exception("City not found");
                            manager = city.Technologies;
                        }
                            break;
                        default:
                            throw new Exception("Unknown effect location?");
                    }

                    manager.DbPersisted = true;

                    using (DbDataReader listReader = dbManager.SelectList(manager))
                    {
                        while (listReader.Read())
                            manager.Add(Ioc.Kernel.Get<TechnologyFactory>().GetTechnology((uint)listReader["type"], (byte)listReader["level"]), false);
                    }
                }
            }

            #endregion
        }

        private void LoadTroopStubs(IDbManager dbManager)
        {
            #region Troop Stubs


            List<dynamic> stationedTroops = new List<dynamic>();

            Global.Logger.Info("Loading troop stubs...");
            using (var reader = dbManager.Select(TroopStub.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    var stub = new TroopStub
                               {
                                       TroopManager = city.Troops,
                                       TroopId = (byte)reader["id"],
                                       State = (TroopState)Enum.Parse(typeof(TroopState), reader["state"].ToString(), true),
                                       DbPersisted = true
                               };

                    var formationMask = (ushort)reader["formations"];
                    var formations = (FormationType[])Enum.GetValues(typeof(FormationType));
                    foreach (var type in formations)
                    {
                        if ((formationMask & (ushort)Math.Pow(2, (ushort)type)) != 0)
                            stub.AddFormation(type);
                    }

                    using (DbDataReader listReader = dbManager.SelectList(stub))
                    {
                        while (listReader.Read())
                            stub.AddUnit((FormationType)((byte)listReader["formation_type"]), (ushort)listReader["type"], (ushort)listReader["count"]);
                    }

                    city.Troops.DbLoaderAdd((byte)reader["id"], stub);

                    var stationedCityId = (uint)reader["stationed_city_id"];
                    if (stationedCityId != 0)
                        stationedTroops.Add(new {stub, stationedCityId}); 
                }
            }

            foreach (var stubInfo in stationedTroops)
            {
                ICity stationedCity;
                if (!World.Current.TryGetObjects(stubInfo.stationedCityId, out stationedCity))
                    throw new Exception("City not found");
                stationedCity.Troops.AddStationed(stubInfo.stub);
            }

            #endregion
        }

        private void LoadTroopStubTemplates(IDbManager dbManager)
        {
            #region Troop Stub's Templates

            Global.Logger.Info("Loading troop stub templates...");
            using (var reader = dbManager.Select(TroopTemplate.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");
                    ITroopStub stub = city.Troops[(byte)reader["troop_stub_id"]];
                    stub.Template.DbPersisted = true;

                    using (DbDataReader listReader = dbManager.SelectList(stub.Template))
                    {
                        while (listReader.Read())
                        {
                            //First we load the BaseBattleStats and pass it into the BattleStats
                            //The BattleStats constructor will copy the basic values then we have to manually apply the values from the db
                            var battleStats = new BattleStats(Ioc.Kernel.Get<UnitFactory>().GetBattleStats((ushort)listReader["type"], (byte)listReader["level"]))
                                              {
                                                      MaxHp = (decimal)listReader["max_hp"],
                                                      Atk = (decimal)listReader["attack"],
                                                      Splash = (byte)listReader["splash"],
                                                      Rng = (byte)listReader["range"],
                                                      Stl = (byte)listReader["stealth"],
                                                      Spd = (byte)listReader["speed"],
                                                      Carry = (ushort)listReader["carry"]
                                              };

                            stub.Template.DbLoaderAdd(battleStats);
                        }
                    }
                }
            }

            #endregion
        }

        private void LoadTroops(IDbManager dbManager)
        {
            #region Troops

            Global.Logger.Info("Loading troops...");
            using (var reader = dbManager.Select(TroopObject.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");
                    ITroopStub stub = (byte)reader["troop_stub_id"] != 0 ? city.Troops[(byte)reader["troop_stub_id"]] : null;
                    var obj = new TroopObject(stub)
                              {
                                      X = (uint)reader["x"],
                                      Y = (uint)reader["y"],
                                      TargetX = (uint)reader["target_x"],
                                      TargetY = (uint)reader["target_y"],
                                      ObjectId = (uint)reader["id"],
                                      DbPersisted = true,
                                      State = {Type = (ObjectState)((byte)reader["state"])},
                                      Stats =
                                              new TroopStats((int)reader["attack_point"],
                                                             (byte)reader["attack_radius"],
                                                             (byte)reader["speed"],
                                                             (short)reader["stamina"],
                                                             new Resource((int)reader["crop"], (int)reader["gold"], (int)reader["iron"], (int)reader["wood"], 0)),
                                      IsBlocked = (bool)reader["is_blocked"],
                                      InWorld = (bool)reader["in_world"],
                              };

                    foreach (var variable in XmlSerializer.DeserializeList((string)reader["state_parameters"]))
                        obj.State.Parameters.Add(variable);

                    city.Add(obj.ObjectId, obj, false);

                    if (obj.InWorld)
                        World.Current.DbLoaderAdd(obj);
                }
            }

            #endregion
        }

        private void LoadBattleManagers(IDbManager dbManager)
        {
            #region Battle Managers

            Global.Logger.Info("Loading battles...");
            using (var reader = dbManager.Select(BattleManager.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    var bm = Ioc.Kernel.Get<BattleManager.Factory>()(city);
                    city.Battle = bm;
                    bm.DbPersisted = true;
                    bm.BattleId = (uint)reader["battle_id"];
                    bm.BattleStarted = (bool)reader["battle_started"];
                    bm.Round = (uint)reader["round"];
                    bm.Turn = (uint)reader["round"];

                    bm.BattleReport.ReportFlag = (bool)reader["report_flag"];
                    bm.BattleReport.ReportStarted = (bool)reader["report_started"];
                    bm.BattleReport.ReportId = (uint)reader["report_id"];

                    using (DbDataReader listReader = dbManager.SelectList(CombatStructure.DB_TABLE, new DbColumn("city_id", city.Id, DbType.UInt32)))
                    {
                        while (listReader.Read())
                        {
                            ICity structureCity;
                            if (!World.Current.TryGetObjects((uint)listReader["structure_city_id"], out structureCity))
                                throw new Exception("City not found");

                            var structure = (IStructure)structureCity[(uint)listReader["structure_id"]];

                            //First we load the BaseBattleStats and pass it into the BattleStats
                            //The BattleStats constructor will copy the basic values then we have to manually apply the values from the db
                            var battleStats = new BattleStats(structure.Stats.Base.Battle)
                                              {
                                                      MaxHp = (decimal)listReader["max_hp"],
                                                      Atk = (decimal)listReader["attack"],
                                                      Splash = (byte)listReader["splash"],
                                                      Rng = (byte)listReader["range"],
                                                      Stl = (byte)listReader["stealth"],
                                                      Spd = (byte)listReader["speed"],                                                      
                                              };

                            var combatStructure = new CombatStructure(bm,
                                                                      structure,
                                                                      battleStats,
                                                                      (decimal)listReader["hp"],
                                                                      (ushort)listReader["type"],
                                                                      (byte)listReader["level"],
                                                                      Formula.Current,
                                                                      BattleFormulas.Current)                                                                      
                                                  {
                                                          GroupId = (uint)listReader["group_id"],
                                                          DmgDealt = (decimal)listReader["damage_dealt"],
                                                          DmgRecv = (decimal)listReader["damage_received"],
                                                          LastRound = (uint)listReader["last_round"],
                                                          RoundsParticipated = (int)listReader["rounds_participated"],
                                                          DbPersisted = true
                                                  };

                            bm.DbLoaderAddToLocal(combatStructure, (uint)listReader["id"]);
                        }
                    }

                    //this will load both defense/attack units (they are saved to same table)
                    using (DbDataReader listReader = dbManager.SelectList(DefenseCombatUnit.DB_TABLE, new DbColumn("city_id", city.Id, DbType.UInt32)))
                    {
                        while (listReader.Read())
                        {
                            ICity troopStubCity;
                            if (!World.Current.TryGetObjects((uint)listReader["troop_stub_city_id"], out troopStubCity))
                                throw new Exception("City not found");
                            ITroopStub troopStub = troopStubCity.Troops[(byte)listReader["troop_stub_id"]];

                            CombatObject combatObj;
                            if ((bool)listReader["is_local"])
                            {
                                combatObj = new DefenseCombatUnit(bm,
                                                                  troopStub,
                                                                  (FormationType)((byte)listReader["formation_type"]),
                                                                  (ushort)listReader["type"],
                                                                  (byte)listReader["level"],
                                                                  (ushort)listReader["count"],
                                                                  (decimal)listReader["left_over_hp"]);
                            }
                            else
                            {
                                combatObj = new AttackCombatUnit(bm,
                                                                 troopStub,
                                                                 (FormationType)((byte)listReader["formation_type"]),
                                                                 (ushort)listReader["type"],
                                                                 (byte)listReader["level"],
                                                                 (ushort)listReader["count"],
                                                                 (decimal)listReader["left_over_hp"],
                                                                 new Resource((int)listReader["loot_crop"],
                                                                              (int)listReader["loot_gold"],
                                                                              (int)listReader["loot_iron"],
                                                                              (int)listReader["loot_wood"],
                                                                              (int)listReader["loot_labor"]));
                            }

                            combatObj.MinDmgDealt = (ushort)listReader["damage_min_dealt"];
                            combatObj.MaxDmgDealt = (ushort)listReader["damage_max_dealt"];
                            combatObj.MinDmgRecv = (ushort)listReader["damage_min_received"];
                            combatObj.MinDmgDealt = (ushort)listReader["damage_max_received"];
                            combatObj.HitDealt = (ushort)listReader["hits_dealt"];
                            combatObj.HitDealtByUnit = (uint)listReader["hits_dealt_by_unit"];
                            combatObj.HitRecv = (ushort)listReader["hits_received"];
                            combatObj.GroupId = (uint)listReader["group_id"];
                            combatObj.DmgDealt = (decimal)listReader["damage_dealt"];
                            combatObj.DmgRecv = (decimal)listReader["damage_received"];
                            combatObj.LastRound = (uint)listReader["last_round"];
                            combatObj.RoundsParticipated = (int)listReader["rounds_participated"];
                            combatObj.DbPersisted = true;

                            bm.DbLoaderAddToCombatList(combatObj, (uint)listReader["id"], (bool)listReader["is_local"]);
                        }
                    }

                    bm.ReportedTroops.DbPersisted = true;
                    using (DbDataReader listReader = dbManager.SelectList(bm.ReportedTroops))
                    {
                        while (listReader.Read())
                        {
                            ICity troopStubCity;
                            if (!World.Current.TryGetObjects((uint)listReader["troop_stub_city_id"], out troopStubCity))
                                throw new Exception("City not found");
                            ITroopStub troopStub = troopStubCity.Troops[(byte)listReader["troop_stub_id"]];

                            if (troopStub == null)
                                continue;

                            bm.ReportedTroops[troopStub] = (uint)listReader["combat_troop_id"];
                        }
                    }

                    bm.ReportedObjects.DbPersisted = true;
                    using (DbDataReader listReader = dbManager.SelectList(bm.ReportedObjects))
                    {
                        while (listReader.Read())
                        {
                            CombatObject co = bm.GetCombatObject((uint)listReader["combat_object_id"]);

                            if (co == null)
                                continue;

                            bm.ReportedObjects.Add(co);
                        }
                    }

                    bm.RefreshBattleOrder();
                }
            }

            #endregion
        }

        private void LoadActions(IDbManager dbManager, TimeSpan downTime)
        {
            #region Active Actions

            Global.Logger.Info("Loading active actions...");

            using (var reader = dbManager.Select(ActiveAction.DB_TABLE))
            {
                while (reader.Read())
                {
                    var actionType = (ActionType)((int)reader["type"]);
                    Type type = Type.GetType("Game.Logic.Actions." + actionType.ToString().Replace("_", "") + "Action", true, true);                    

                    DateTime beginTime = DateTime.SpecifyKind((DateTime)reader["begin_time"], DateTimeKind.Utc).Add(downTime);

                    DateTime nextTime = DateTime.SpecifyKind((DateTime)reader["next_time"], DateTimeKind.Utc);
                    if (nextTime != DateTime.MinValue)
                        nextTime = nextTime.Add(downTime);

                    DateTime endTime = DateTime.SpecifyKind((DateTime)reader["end_time"], DateTimeKind.Utc).Add(downTime);

                    Dictionary<string, string> properties = XmlSerializer.Deserialize((string)reader["properties"]);

                    var action =
                            (ScheduledActiveAction)
                            actionFactory.CreateScheduledActiveAction(type,
                                                                      (uint)reader["id"],
                                                                      beginTime,
                                                                      nextTime,
                                                                      endTime,
                                                                      (int)reader["worker_type"],
                                                                      (byte)reader["worker_index"],
                                                                      (ushort)reader["count"],
                                                                      properties);
                    action.DbPersisted = true;

                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    var workerId = (uint)reader["object_id"];
                    if (workerId == 0)
                        action.WorkerObject = city;
                    else
                        action.WorkerObject = city[(uint)reader["object_id"]];

                    city.Worker.DbLoaderDoActive(action);

                    dbManager.Save(action);
                }
            }

            #endregion

            #region Passive Actions

            Global.Logger.Info("Loading passive actions...");

            //this will hold chain actions that we encounter for the next phase
            var chainActions = new Dictionary<uint, List<PassiveAction>>();            

            using (var reader = dbManager.Select(PassiveAction.DB_TABLE))
            {
                while (reader.Read())
                {
                    var actionType = (ActionType)((int)reader["type"]);
                    Type type = Type.GetType("Game.Logic.Actions." + actionType.ToString().Replace("_", "") + "Action", true, true);

                    Dictionary<string, string> properties = XmlSerializer.Deserialize((string)reader["properties"]);

                    PassiveAction action;

                    if ((bool)reader["is_scheduled"])
                    {
                        DateTime beginTime = DateTime.SpecifyKind((DateTime)reader["begin_time"], DateTimeKind.Utc);
                        beginTime = beginTime.Add(downTime);

                        DateTime nextTime = DateTime.SpecifyKind((DateTime)reader["next_time"], DateTimeKind.Utc);
                        if (nextTime != DateTime.MinValue)
                            nextTime = nextTime.Add(downTime);

                        DateTime endTime = DateTime.SpecifyKind((DateTime)reader["end_time"], DateTimeKind.Utc);
                        endTime = endTime.Add(downTime);

                        string nlsDescription = DBNull.Value.Equals(reader["nls_description"]) ? string.Empty : (string)reader["nls_description"];

                        action = actionFactory.CreateScheduledPassiveAction(type, (uint)reader["id"], beginTime, nextTime, endTime, (bool)reader["is_visible"], nlsDescription, properties);
                    }
                    else
                    {
                        action = actionFactory.CreatePassiveAction(type, (uint)reader["id"], (bool)reader["is_visible"], properties);
                    }

                    action.DbPersisted = true;

                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    var workerId = (uint)reader["object_id"];
                    if (workerId == 0)
                        action.WorkerObject = city;
                    else
                        action.WorkerObject = city[workerId];

                    if ((bool)reader["is_chain"] == false)
                        city.Worker.DbLoaderDoPassive(action);
                    else
                    {
                        List<PassiveAction> chainList;
                        if (!chainActions.TryGetValue(city.Id, out chainList))
                        {
                            chainList = new List<PassiveAction>();
                            chainActions[city.Id] = chainList;
                        }

                        action.IsChain = true;

                        city.Worker.DbLoaderDoPassive(action);

                        chainList.Add(action);
                    }

                    // Resave city to update times
                    dbManager.Save(action);
                }
            }

            #endregion

            #region Chain Actions

            Global.Logger.Info("Loading chain actions...");

            using (var reader = dbManager.Select(ChainAction.DB_TABLE))
            {
                while (reader.Read())
                {
                    var actionType = (ActionType)((int)reader["type"]);
                    Type type = Type.GetType("Game.Logic.Actions." + actionType + "Action", true, true);

                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    var currentActionId = DBNull.Value.Equals(reader["current_action_id"]) ? 0 : (uint)reader["current_action_id"];

                    List<PassiveAction> chainList;
                    PassiveAction currentAction = null;
                    //current action might be null if it has already completed and we are in the call chain part of the cycle
                    if (chainActions.TryGetValue(city.Id, out chainList))
                        currentAction = chainList.Find(lookupAction => lookupAction.ActionId == currentActionId);

                    Dictionary<string, string> properties = XmlSerializer.Deserialize((string)reader["properties"]);
                    var action = actionFactory.CreateChainAction(type,
                                                                 (uint)reader["id"],
                                                                 (string)reader["chain_callback"],
                                                                 currentAction,
                                                                 (ActionState)((byte)reader["chain_state"]),
                                                                 (bool)reader["is_visible"],
                                                                 properties);
                    
                    action.DbPersisted = true;

                    var workerId = (uint)reader["object_id"];
                    if (workerId == 0)
                        action.WorkerObject = city;
                    else
                        action.WorkerObject = city[(uint)reader["object_id"]];

                    city.Worker.DbLoaderDoPassive(action);

                    dbManager.Save(action);
                }
            }

            #endregion
        }

        private void LoadActionReferences(IDbManager dbManager)
        {
            #region Action References

            Global.Logger.Info("Loading action references...");
            using (var reader = dbManager.Select(ReferenceStub.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    GameAction action;
                    if ((bool)reader["is_active"])
                        action = city.Worker.ActiveActions[(uint)reader["action_id"]];
                    else
                        action = city.Worker.PassiveActions[(uint)reader["action_id"]];

                    ICanDo obj;
                    var workerId = (uint)reader["object_id"];
                    if (workerId == 0)
                        obj = city;
                    else
                        obj = city[(uint)reader["object_id"]];

                    var referenceStub = new ReferenceStub((ushort)reader["id"], obj, action) {DbPersisted = true};

                    city.Worker.References.DbLoaderAdd(referenceStub);
                }
            }

            #endregion
        }

        private void LoadActionNotifications(IDbManager dbManager)
        {
            #region Action Notifications

            Global.Logger.Info("Loading action notifications...");
            using (var reader = dbManager.Select(NotificationManager.Notification.DB_TABLE))
            {
                while (reader.Read())
                {
                    ICity city;
                    if (!World.Current.TryGetObjects((uint)reader["city_id"], out city))
                        throw new Exception("City not found");

                    IGameObject obj = city[(uint)reader["object_id"]];
                    PassiveAction action = city.Worker.PassiveActions[(uint)reader["action_id"]];

                    var notification = new NotificationManager.Notification(obj, action);

                    using (DbDataReader listReader = dbManager.SelectList(notification))
                    {
                        while (listReader.Read())
                        {
                            ICity subscriptionCity;
                            if (!World.Current.TryGetObjects((uint)listReader["subscription_city_id"], out subscriptionCity))
                                throw new Exception("City not found");
                            notification.Subscriptions.Add(subscriptionCity);
                        }
                    }

                    city.Worker.Notifications.DbLoaderAdd(false, notification);

                    notification.DbPersisted = true;
                }
            }

            #endregion
        }
    }
}