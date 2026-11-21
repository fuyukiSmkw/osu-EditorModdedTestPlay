#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Rulesets.EditorModdedTestPlay.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Tests
{
    [TestFixture]
    public class StableManiaScoreV2RegressionTest
    {
        [TestCase(1d, ScoreRank.X)]
        [TestCase(0.9999d, ScoreRank.S)]
        [TestCase(0.9501d, ScoreRank.S)]
        [TestCase(0.95d, ScoreRank.A)]
        [TestCase(0.9001d, ScoreRank.A)]
        [TestCase(0.9d, ScoreRank.B)]
        [TestCase(0.8001d, ScoreRank.B)]
        [TestCase(0.8d, ScoreRank.C)]
        [TestCase(0.7001d, ScoreRank.C)]
        [TestCase(0.7d, ScoreRank.D)]
        public void TestManiaRankCutoffs(double accuracy, ScoreRank expected)
        {
            using var processor = new StableManiaScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenManiaRuleset());
            Assert.That(processor.RankFromScore(accuracy, new Dictionary<HitResult, int>()), Is.EqualTo(expected));
        }

        [TestCase(5f, 111d, false)]
        [TestCase(5f, 112d, true)]
        [TestCase(5f, 112.4d, true)]
        [TestCase(5f, 136d, true)]
        [TestCase(5f, -111d, false)]
        [TestCase(5f, -112d, false)]
        [TestCase(9.3f, 98d, false)]
        [TestCase(9.3f, 99d, true)]
        public void TestStableLateWindowIsExclusive(float overallDifficulty, double timeOffset, bool expected)
        {
            var windows = new ManiaHitWindows();
            windows.SetDifficulty(overallDifficulty);

            Assert.That(StableManiaJudgement.HasPassedLateWindow(windows, timeOffset), Is.EqualTo(expected));
        }

        [TestCase(5f, -135d, false)]
        [TestCase(5f, -136d, false)]
        [TestCase(5f, -136.6d, true)]
        [TestCase(5f, -137d, true)]
        public void TestEarlyHoldBreakWindow(float overallDifficulty, double timeOffset, bool expected)
        {
            var windows = new ManiaHitWindows();
            windows.SetDifficulty(overallDifficulty);

            Assert.That(StableManiaJudgement.IsEarlyHoldBreak(windows, timeOffset), Is.EqualTo(expected));
        }

        [Test]
        public void TestHiddenManiaUsesStableDrawableRuleset()
        {
            var ruleset = new HiddenScoreV2Rulesets.HiddenManiaRuleset();
            var beatmap = new ManiaBeatmap(new StageDefinition(4));
            beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;
            beatmap.HitObjects.Add(new Note { StartTime = 1000, Column = 0 });

            using var drawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap);

            Assert.That(drawableRuleset, Is.TypeOf<StableDrawableManiaRuleset>());
        }
    }
}
