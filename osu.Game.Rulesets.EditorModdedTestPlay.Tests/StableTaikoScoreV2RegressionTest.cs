#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Beatmaps;
using osu.Game.Rulesets.Taiko.Mods;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.EditorModdedTestPlay.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Tests
{
    [TestFixture]
    public class StableTaikoScoreV2RegressionTest
    {
        [TearDown]
        public void TearDown()
        {
            StableTaikoBeatmaps.SelectedMods = null;
        }

        [TestCase(1010, null, 10)]
        [TestCase(1000, 1100, 8)]
        [TestCase(1000, 2000, 9)]
        public void TestDrumRollTickCountMatchesStable(double duration, double? nextObjectStart, int expectedTicks)
        {
            var drumRoll = new DrumRoll
            {
                StartTime = 0,
                Duration = duration,
            };

            var objects = new List<TaikoHitObject> { drumRoll };

            if (nextObjectStart != null)
            {
                objects.Add(new Hit
                {
                    StartTime = nextObjectStart.Value,
                });
            }

            TaikoBeatmap beatmap = createBeatmap(objects);
            applyStableTaikoBeatmapProcessor(beatmap);

            var stableDrumRoll = (DrumRoll)beatmap.HitObjects[0];
            var ticks = stableDrumRoll.NestedHitObjects.OfType<DrumRollTick>().ToList();

            Assert.That(ticks.Count, Is.EqualTo(expectedTicks));
            Assert.That(ticks.All(t => t.StartTime == (int)t.StartTime), Is.True);
        }

        [TestCase(1000, 5, false, false, 9, 8)]
        [TestCase(1000, 5, true, false, 7, 6)]
        [TestCase(1000, 5, false, true, 13, 12)]
        [TestCase(1500, 5, false, false, 12, 11)]
        public void TestSwellRequiredHitsMatchesStable(double duration, double overallDifficulty, bool doubleTime, bool halfTime, int expectedRequiredHits, int expectedRotationRequirement)
        {
            Mod[] mods = doubleTime
                ? new Mod[] { new TaikoModDoubleTime() }
                : halfTime
                    ? new Mod[] { new TaikoModHalfTime() }
                    : Array.Empty<Mod>();

            StableTaikoBeatmaps.SelectedMods = mods;

            var swell = new Swell
            {
                StartTime = 0,
                Duration = duration,
            };

            TaikoBeatmap beatmap = createBeatmap(new[] { swell }, overallDifficulty);
            applyStableTaikoBeatmapProcessor(beatmap);

            var stableSwell = (StableTaikoSwell)beatmap.HitObjects[0];

            Assert.That(stableSwell.StableRotationRequirement, Is.EqualTo(expectedRotationRequirement));
            Assert.That(stableSwell.RequiredHits, Is.EqualTo(expectedRequiredHits));
            Assert.That(stableSwell.NestedHitObjects.OfType<StableSwellTick>().Count(), Is.EqualTo(expectedRequiredHits));
        }

        [Test]
        public void TestSwellCompletionGives300()
        {
            (StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell) = createSwellProcessor(8, 9);

            applySwellTicks(processor, swell, 9);
            processor.ApplyResult(createResult(swell, HitResult.LargeBonus));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(1));
            Assert.That(score.Statistics[HitResult.Ok], Is.Zero);
            Assert.That(score.Statistics[HitResult.Miss], Is.Zero);
            Assert.That(score.Rank, Is.EqualTo(ScoreRank.X));
        }

        [Test]
        public void TestSwellCompletionGives100WhenShortByOne()
        {
            (StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell) = createSwellProcessor(8, 9);

            applySwellTicks(processor, swell, 8);
            processor.ApplyResult(createResult(swell, HitResult.IgnoreMiss));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Great], Is.Zero);
            Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(1));
            Assert.That(score.Statistics[HitResult.Miss], Is.Zero);
        }

        [Test]
        public void TestSwellCompletionGivesMissWhenBelowHalf()
        {
            (StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell) = createSwellProcessor(8, 9);

            applySwellTicks(processor, swell, 4);
            processor.ApplyResult(createResult(swell, HitResult.IgnoreMiss));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Great], Is.Zero);
            Assert.That(score.Statistics[HitResult.Ok], Is.Zero);
            Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(1));
        }

        [TestCase(10, 0, 0, ScoreRank.X)]
        [TestCase(9, 1, 0, ScoreRank.A)]
        [TestCase(9, 0, 1, ScoreRank.B)]
        [TestCase(8, 2, 0, ScoreRank.B)]
        [TestCase(7, 3, 0, ScoreRank.C)]
        public void TestRankMatchesStable(int count300, int count100, int countMiss, ScoreRank expected)
        {
            var hits = Enumerable.Range(0, count300 + count100 + countMiss)
                                 .Select(i => new Hit { StartTime = i * 1000 })
                                 .ToArray();

            TaikoBeatmap beatmap = createBeatmap(hits);
            applyStableTaikoBeatmapProcessor(beatmap);

            var processor = new StableTaikoScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenTaikoRuleset());
            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < count300; i++)
                processor.ApplyResult(createResult(hits[i], HitResult.Great));

            for (int i = 0; i < count100; i++)
                processor.ApplyResult(createResult(hits[count300 + i], HitResult.Ok));

            for (int i = 0; i < countMiss; i++)
                processor.ApplyResult(createResult(hits[count300 + count100 + i], HitResult.Miss));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Rank, Is.EqualTo(expected));
        }

        [Test]
        public void TestSwellHitCountRollback()
        {
            (StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell) = createSwellProcessor(8, 9);
            var ticks = swell.NestedHitObjects.OfType<StableSwellTick>().ToList();

            var applied = new List<JudgementResult>();

            for (int i = 0; i < 5; i++)
            {
                var result = createResult(ticks[i], HitResult.IgnoreHit);
                processor.ApplyResult(result);
                applied.Add(result);
            }

            // Rewinding two hits leaves three hits, which is below the 100 threshold.
            processor.RevertResult(applied[^1]);
            processor.RevertResult(applied[^2]);

            processor.ApplyResult(createResult(swell, HitResult.IgnoreMiss));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Ok], Is.Zero);
            Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(1));
        }

        [TestCase(9, 1, 0, 0)]
        [TestCase(1, 0, 1, 0)]
        [TestCase(0, 0, 0, 1)]
        public void TestDrumRollCompletionTiers(int ticksHit, int expected300, int expected100, int expectedMiss)
        {
            var drumRoll = new DrumRoll
            {
                StartTime = 0,
                Duration = 1000,
            };

            TaikoBeatmap beatmap = createBeatmap(new[] { drumRoll });
            applyStableTaikoBeatmapProcessor(beatmap);

            var stableDrumRoll = (DrumRoll)beatmap.HitObjects[0];
            var ticks = stableDrumRoll.NestedHitObjects.OfType<DrumRollTick>().ToList();

            var processor = new StableTaikoScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenTaikoRuleset());
            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < ticksHit; i++)
                processor.ApplyResult(createResult(ticks[i], HitResult.SmallBonus));

            for (int i = ticksHit; i < ticks.Count; i++)
                processor.ApplyResult(createResult(ticks[i], HitResult.IgnoreMiss));

            processor.ApplyResult(createResult(stableDrumRoll, HitResult.IgnoreHit));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(expected300));
            Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(expected100));
            Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(expectedMiss));
        }

        [Test]
        public void TestDrumRollCompletionUsesPostModDifficulty()
        {
            var drumRoll = new DrumRoll
            {
                StartTime = 0,
                Duration = 1000,
            };

            TaikoBeatmap beatmap = createBeatmap(new[] { drumRoll }, overallDifficulty: 8);
            beatmap.BeatmapInfo.Difficulty.OverallDifficulty = 0;
            applyStableTaikoBeatmapProcessor(beatmap);

            var stableDrumRoll = (DrumRoll)beatmap.HitObjects[0];
            var ticks = stableDrumRoll.NestedHitObjects.OfType<DrumRollTick>().ToList();

            var processor = new StableTaikoScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenTaikoRuleset());
            processor.ApplyBeatmap(beatmap);

            int hits = (int)Math.Ceiling(ticks.Count * 0.3) - 1;

            for (int i = 0; i < hits; i++)
                processor.ApplyResult(createResult(ticks[i], HitResult.SmallBonus));

            for (int i = hits; i < ticks.Count; i++)
                processor.ApplyResult(createResult(ticks[i], HitResult.IgnoreMiss));

            processor.ApplyResult(createResult(stableDrumRoll, HitResult.IgnoreHit));

            ScoreInfo score = populateScore(processor);
            Assert.That(score.Statistics[HitResult.Great], Is.Zero);
            Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(1));
        }

        private static (StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell) createSwellProcessor(int rotationRequirement, int requiredHits)
        {
            var swell = new StableTaikoSwell
            {
                StartTime = 0,
                Duration = 1000,
                StableRotationRequirement = rotationRequirement,
                RequiredHits = requiredHits,
            };

            var beatmap = new TaikoBeatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty { OverallDifficulty = 5 },
                    Ruleset = new TaikoRuleset().RulesetInfo,
                },
            };

            beatmap.HitObjects.Add(swell);
            swell.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var processor = new StableTaikoScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenTaikoRuleset());
            processor.ApplyBeatmap(beatmap);

            return (processor, swell);
        }

        private static void applySwellTicks(StableTaikoScoreV2ScoreProcessor processor, StableTaikoSwell swell, int hitCount)
        {
            var ticks = swell.NestedHitObjects.OfType<StableSwellTick>().ToList();

            for (int i = 0; i < ticks.Count; i++)
                processor.ApplyResult(createResult(ticks[i], i < hitCount ? HitResult.IgnoreHit : HitResult.IgnoreMiss));
        }

        private static ScoreInfo populateScore(StableTaikoScoreV2ScoreProcessor processor)
        {
            var score = new ScoreInfo();
            processor.PopulateScore(score);
            return score;
        }

        private static JudgementResult createResult(osu.Game.Rulesets.Objects.HitObject hitObject, HitResult result)
            => new JudgementResult(hitObject, hitObject.CreateJudgement()) { Type = result };

        private static TaikoBeatmap createBeatmap(IEnumerable<TaikoHitObject> hitObjects, double overallDifficulty = 5)
        {
            var beatmap = new TaikoBeatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        OverallDifficulty = (float)overallDifficulty,
                        SliderTickRate = 4,
                    },
                    Ruleset = new TaikoRuleset().RulesetInfo,
                },
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.HitObjects.AddRange(hitObjects);

            return beatmap;
        }

        private static void applyStableTaikoBeatmapProcessor(TaikoBeatmap beatmap)
        {
            var processor = new StableTaikoBeatmapProcessor(beatmap);
            processor.PreProcess();
            processor.PostProcess();

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }
    }
}
