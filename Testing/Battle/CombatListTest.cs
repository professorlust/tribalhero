﻿using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Game.Battle;
using Game.Battle.CombatGroups;
using Game.Battle.CombatObjects;
using Game.Data.Stats;
using Game.Map;
using NSubstitute;
using Persistance;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoNSubstitute;
using Xunit;

namespace Testing.Battle
{
    public class CombatListTest
    {
        #region GetBestTargets

        /// <summary>
        ///     Given combat list is empty
        ///     When GetBestTargets is called
        ///     Then the result should be CombatList.BestTargetResult.NoneInRange
        ///     And out combatList should be empty
        /// </summary>
        [Fact]
        public void TestNoObjects()
        {
            var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

            var combatObject = Substitute.For<ICombatObject>();

            CombatList list = fixture.CreateAnonymous<CombatList>();

            List<CombatList.Target> result;
            CombatList.BestTargetResult targetResult = list.GetBestTargets(1, combatObject, out result, 1);

            result.Should().BeEmpty();
            targetResult.Should().Be(CombatList.BestTargetResult.NoneInRange);
        }

        /// <summary>
        ///     Given combat list is not empty
        ///     And object is in range
        ///     When GetBestTargets is called
        ///     Then the result should be CombatList.BestTargetResult.Ok
        ///     And out combatList should have the defender
        /// </summary>
        [Fact]
        public void TestInRange()
        {
            var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

            var battleFormulas = Substitute.For<BattleFormulas>();                 
            fixture.Register(() => battleFormulas);                        
            battleFormulas.GetDmgModifier(null, null).ReturnsForAnyArgs(1);

            var attackerStats = Substitute.For<BattleStats>();
            var attacker = Substitute.For<ICombatObject>();
            var defenderStats = Substitute.For<BattleStats>();
            var defender = Substitute.For<ICombatObject>();

            attackerStats.Stl.Returns((byte)1);

            attacker.InRange(defender).Returns(true);
            attacker.Location().Returns(new Position(0, 0));
            attacker.Visibility.Returns((byte)1);
            attacker.IsDead.Returns(false);
            attacker.CanSee(Arg.Any<ICombatObject>(), Arg.Any<uint>()).Returns(true);

            defenderStats.Stl.Returns((byte)2);

            defender.InRange(attacker).Returns(true);
            defender.Location().Returns(new Position(0, 0));
            defender.IsDead.Returns(false);
            defender.Stats.Returns(defenderStats);

            var combatGroup = Substitute.For<ICombatGroup>();
            combatGroup.GetEnumerator().Returns(x => new List<ICombatObject> {defender}.GetEnumerator());

            var listSut = fixture.CreateAnonymous<CombatList>();
            listSut.Add(combatGroup, false);

            List<CombatList.Target> result;
            CombatList.BestTargetResult targetResult = listSut.GetBestTargets(1, attacker, out result, 1);

            result.Should().HaveCount(1);
            result[0].CombatObject.Should().Be(defender);
            targetResult.Should().Be(CombatList.BestTargetResult.Ok);
        }

        /// <summary>
        ///     When a group is removed
        ///     Then the group should be deleted
        /// </summary>
        [Fact]
        public void TestGroupIsClearedWhenRemoved()
        {
            var combatGroup = Substitute.For<ICombatGroup>();

            var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());            

            IDbManager dbManager = fixture.Freeze<IDbManager>();

            var listSut = fixture.CreateAnonymous<CombatList>();
            
            listSut.Add(combatGroup, false);
            listSut.Remove(combatGroup);

            dbManager.Received(1).Delete(combatGroup);
        }

        [Fact]
        public void TestShuffleWhenEnoughOptimalTargets()
        {
            var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

            var attacker = Substitute.For<ICombatObject>();            
            var defender1 = Substitute.For<ICombatObject>();
            var defender2 = Substitute.For<ICombatObject>();
            var defender3 = Substitute.For<ICombatObject>();

            attacker.InRange(null).ReturnsForAnyArgs(true);
            attacker.Location().Returns(new Position(0, 0));              
            attacker.CanSee(null, 0).ReturnsForAnyArgs(true);

            defender1.InRange(null).ReturnsForAnyArgs(true);
            defender1.Location().Returns(new Position(0, 0));
            defender1.Stats.Stl.Returns((byte)1);

            defender2.InRange(null).ReturnsForAnyArgs(true);
            defender2.Location().Returns(new Position(0, 0));
            defender2.Stats.Stl.Returns((byte)1);

            defender3.InRange(null).ReturnsForAnyArgs(true);
            defender3.Location().Returns(new Position(0, 0));
            defender3.Stats.Stl.Returns((byte)1);

            var combatGroup = Substitute.For<ICombatGroup>();
            combatGroup.GetEnumerator().Returns(x => new List<ICombatObject> {defender1, defender2, defender3}.GetEnumerator());

            var battleFormulas = Substitute.For<BattleFormulas>();                 
            fixture.Register(() => battleFormulas);                        
            battleFormulas.GetDmgModifier(null, null).ReturnsForAnyArgs(1, 3, 3);

            var listSut = fixture.CreateAnonymous<CombatList>();
            listSut.Add(combatGroup, false);

            List<CombatList.Target> result;
            CombatList.BestTargetResult targetResult = listSut.GetBestTargets(1, attacker, out result, 2);

            result.Should().HaveCount(2);
            result.Select(x => x.CombatObject).Should().Contain(new[] { defender2, defender3 });
            targetResult.Should().Be(CombatList.BestTargetResult.Ok);
        }

        [Fact]
        public void TestShuffleWhenNotEnoughOptimalTargets()
        {
            var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

            var attacker = Substitute.For<ICombatObject>();            
            var defender1 = Substitute.For<ICombatObject>();
            var defender2 = Substitute.For<ICombatObject>();
            var defender3 = Substitute.For<ICombatObject>();

            attacker.InRange(null).ReturnsForAnyArgs(true);
            attacker.Location().Returns(new Position(0, 0));              
            attacker.CanSee(null, 0).ReturnsForAnyArgs(true);

            defender1.InRange(null).ReturnsForAnyArgs(true);
            defender1.Location().Returns(new Position(0, 0));
            defender1.Stats.Stl.Returns((byte)1);

            defender2.InRange(null).ReturnsForAnyArgs(true);
            defender2.Location().Returns(new Position(0, 0));
            defender2.Stats.Stl.Returns((byte)1);

            defender3.InRange(null).ReturnsForAnyArgs(true);
            defender3.Location().Returns(new Position(0, 0));
            defender3.Stats.Stl.Returns((byte)1);

            var combatGroup = Substitute.For<ICombatGroup>();
            combatGroup.GetEnumerator().Returns(x => new List<ICombatObject> {defender1, defender2, defender3}.GetEnumerator());

            var battleFormulas = Substitute.For<BattleFormulas>();                 
            fixture.Register(() => battleFormulas);                        
            battleFormulas.GetDmgModifier(null, null).ReturnsForAnyArgs(3, 1, 2);

            var listSut = fixture.CreateAnonymous<CombatList>();
            listSut.Add(combatGroup, false);

            List<CombatList.Target> result;
            CombatList.BestTargetResult targetResult = listSut.GetBestTargets(1, attacker, out result, 2);

            result.Should().HaveCount(2);
            result.Select(x => x.CombatObject).Should().Contain(new[] { defender1, defender3 });
            targetResult.Should().Be(CombatList.BestTargetResult.Ok);
        }

        #endregion
    }
}