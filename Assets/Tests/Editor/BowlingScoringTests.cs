using System;
using System.Linq;
using MotionControllers.Bowling;
using NUnit.Framework;
namespace MotionControllers.Tests
{
    public class BowlingScoringTests
    {
        private static BowlingMatch Game(int count = 1) => new BowlingMatch(Enumerable.Range(1,count).Select(i=>new BowlingPlayer("id"+i,"Player "+i)));
        private static void Roll(BowlingMatch game, params int[] pins) { foreach (int p in pins) game.RecordRoll(p); }
        [Test] public void OpenFramesAndGutterGame() {
            var g=Game(); for(int i=0;i<10;i++) Roll(g,3,4);
            Assert.That(g.Complete,Is.True); Assert.That(g.Players[0].Total,Is.EqualTo(70));
            g=Game();for(int i=0;i<20;i++)g.RecordRoll(0);Assert.That(g.Players[0].Total,Is.Zero);
        }
        [Test] public void SpareWaitsForNextRollAndStrikeWaitsForTwo() {
            var g=Game();Roll(g,5,5);Assert.That(g.Players[0].Frames[0].Cumulative,Is.Null);
            Roll(g,3);Assert.That(g.Players[0].Frames[0].Cumulative,Is.EqualTo(13));
            g=Game();Roll(g,10,3);Assert.That(g.Players[0].Frames[0].Cumulative,Is.Null);
            Roll(g,4);Assert.That(g.Players[0].Frames[0].Cumulative,Is.EqualTo(17));Assert.That(g.Players[0].Total,Is.EqualTo(24));
        }
        [Test] public void ConsecutiveStrikesAndTurkeyResolveInOrder() {
            var g=Game();Roll(g,10,10,3,4);Assert.That(g.Players[0].Frames[0].Score,Is.EqualTo(23));Assert.That(g.Players[0].Total,Is.EqualTo(47));
            g=Game();Roll(g,10,10,10);Assert.That(g.Players[0].Total,Is.EqualTo(30));
            Assert.That(g.Players[0].Frames[1].Score,Is.Null);Roll(g,3,4);Assert.That(g.Players[0].Total,Is.EqualTo(77));
        }
        [Test] public void PerfectGameAndAllSpares() {
            var g=Game();for(int i=0;i<12;i++)g.RecordRoll(10);Assert.That(g.Complete,Is.True);Assert.That(g.Players[0].Total,Is.EqualTo(300));
            g=Game();for(int i=0;i<21;i++)g.RecordRoll(5);Assert.That(g.Complete,Is.True);Assert.That(g.Players[0].Total,Is.EqualTo(150));
        }
        [TestCase(new[]{10,10,10},30)] [TestCase(new[]{10,7,3},20)] [TestCase(new[]{10,0,10},20)]
        [TestCase(new[]{7,3,10},20)] [TestCase(new[]{0,10,10},20)] [TestCase(new[]{7,2},9)] [TestCase(new[]{10,10,7},27)]
        public void TenthBonusAndOpenFrames(int[] tenth,int score) {
            var g=Game();for(int i=0;i<18;i++)g.RecordRoll(0);
            for(int i=0;i<tenth.Length;i++){Assert.That(g.Complete,Is.False);g.RecordRoll(tenth[i]);}
            Assert.That(g.Complete,Is.True);Assert.That(g.Players[0].Total,Is.EqualTo(score));Assert.Throws<InvalidOperationException>(()=>g.RecordRoll(0));
        }
        [Test] public void TenthRackAndNotationFollowBonusBallRules() {
            var g=Game();for(int i=0;i<18;i++)g.RecordRoll(0);
            Assert.That(g.RecordRoll(10).ResetRack,Is.True);Assert.That(g.CurrentFrame.RemainingPins,Is.EqualTo(10));
            Assert.That(g.RecordRoll(7).ResetRack,Is.False);Assert.That(g.CurrentFrame.RemainingPins,Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>g.RecordRoll(4));g.RecordRoll(3);
            Assert.That(string.Join("",Enumerable.Range(0,3).Select(g.Players[0].Frames[9].Notation)),Is.EqualTo("X7/"));
            g=Game();Roll(g,0,10);Assert.That(g.Players[0].Frames[0].Notation(1),Is.EqualTo("/"));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EveryPlayerCompletesWholeFramesInOrderWithIndependentScores(int count) {
            var g=Game(count);
            for(int frame=0;frame<10;frame++)for(int player=0;player<count;player++){
                Assert.That(g.FrameIndex,Is.EqualTo(frame));Assert.That(g.PlayerIndex,Is.EqualTo(player));
                var first=g.RecordRoll(player);Assert.That(first.ResetRack,Is.False);Assert.That(g.PlayerIndex,Is.EqualTo(player));
                Assert.That(g.RecordRoll(1).ResetRack,Is.True);
            }
            Assert.That(g.Complete,Is.True);for(int i=0;i<count;i++)Assert.That(g.Players[i].Total,Is.EqualTo(10*(i+1)));
        }
        [Test] public void MultiplayerTenthStrikeWaitsForBothBonusesAndTiesShareRank() {
            var g=Game(2);for(int i=0;i<36;i++)g.RecordRoll(0);
            Roll(g,10,10);Assert.That(g.PlayerIndex,Is.Zero);Assert.That(g.Complete,Is.False);
            g.RecordRoll(10);Assert.That(g.PlayerIndex,Is.EqualTo(1));Roll(g,10,10,10);
            var result=BowlingScorePresentation.Results(g);Assert.That(result.Summary,Does.StartWith("Tie:"));
            Assert.That(result.Detail,Does.Contain("1.  Player 1").And.Contain("1.  Player 2"));
        }
        [Test] public void InvalidRollAndRosterCannotMutateTheGame() {
            var g=Game();g.RecordRoll(8);Assert.Throws<ArgumentOutOfRangeException>(()=>g.RecordRoll(3));
            Assert.That(g.CurrentFrame.Rolls.Count,Is.EqualTo(1));Assert.Throws<ArgumentOutOfRangeException>(()=>g.RecordRoll(-1));
            Assert.Throws<ArgumentException>(()=>Game(0));Assert.Throws<ArgumentException>(()=>Game(5));
        }
    }
}
