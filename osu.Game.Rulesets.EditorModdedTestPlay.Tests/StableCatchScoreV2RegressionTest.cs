#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.EditorModdedTestPlay.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Tests
{
    [TestFixture]
    public class StableCatchScoreV2RegressionTest
    {
        [Test]
        public void TestPerfectScoreUsesStableTimeOrderedMaximum()
        {
            var beatmap = createOverlappingJuiceStreamBeatmap();
            var processor = new HiddenScoreV2Rulesets.HiddenCatchRuleset().CreateScoreProcessor();

            processor.ApplyBeatmap(beatmap);

            // stable's maximum-score simulation walks the flat time-sorted object list. A juice
            // stream that overlaps a top-level fruit must interleave those objects, otherwise
            // maxSum is wrong and even a perfect play does not reach 1,000,000.
            Assert.That(processor.MaximumTotalScore, Is.EqualTo(1_000_000));

            applyPerfectResults(processor, beatmap);

            var score = new ScoreInfo();
            processor.PopulateScore(score);

            Assert.That(score.TotalScore, Is.EqualTo(1_000_000));
            Assert.That(score.TotalScoreWithoutMods, Is.EqualTo(1_000_000));
            Assert.That(score.Accuracy, Is.EqualTo(1).Within(0.000001));
        }

        [TestCase(100, 0, ScoreRank.X)]
        [TestCase(49, 1, ScoreRank.S)]
        [TestCase(47, 3, ScoreRank.B)]
        [TestCase(90, 10, ScoreRank.C)]
        [TestCase(85, 15, ScoreRank.C)]
        public void TestRankUsesStableFloatAccuracy(int greatCount, int missCount, ScoreRank expected)
        {
            var processor = new HiddenScoreV2Rulesets.HiddenCatchRuleset().CreateScoreProcessor();

            var results = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = greatCount,
                [HitResult.Miss] = missCount,
            };

            Assert.That(processor.RankFromScore((double)greatCount / (greatCount + missCount), results), Is.EqualTo(expected));
        }

        [TestCase(47, 1, 2, ScoreRank.B)]
        [TestCase(90, 5, 5, ScoreRank.C)]
        [TestCase(49, 1, 0, ScoreRank.S)]
        public void TestRankMapsDropletAndTinyMissesToStableCounts(int greatCount, int dropletMissCount, int tinyMissCount, ScoreRank expected)
        {
            var processor = new HiddenScoreV2Rulesets.HiddenCatchRuleset().CreateScoreProcessor();

            var results = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = greatCount,
                [HitResult.LargeTickMiss] = dropletMissCount,
                [HitResult.SmallTickMiss] = tinyMissCount,
            };

            Assert.That(processor.RankFromScore(0, results), Is.EqualTo(expected));
        }

        [Test]
        public void TestBananaBaseScoreMatchesStable()
        {
            var processor = new HiddenScoreV2Rulesets.HiddenCatchRuleset().CreateScoreProcessor();

            Assert.That(processor.GetBaseScoreForResult(HitResult.LargeBonus), Is.EqualTo(200));
        }

        private static CatchBeatmap createOverlappingJuiceStreamBeatmap()
        {
            var beatmap = new CatchBeatmap();
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });
            var difficulty = new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1, OverallDifficulty = 5 };

            for (int i = 0; i < 3; i++)
            {
                var fruit = new Fruit { StartTime = 500 + i * 100, X = 100 };
                fruit.ApplyDefaults(controlPoints, difficulty);
                beatmap.HitObjects.Add(fruit);
            }

            var juiceStream = new JuiceStream
            {
                StartTime = 1000,
                X = 100,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(800, 0) }, 800),
                RepeatCount = 0,
            };
            juiceStream.ApplyDefaults(controlPoints, difficulty);
            beatmap.HitObjects.Add(juiceStream);

            var fruitInsideStream = new Fruit { StartTime = 2000, X = 300 };
            fruitInsideStream.ApplyDefaults(controlPoints, difficulty);
            beatmap.HitObjects.Add(fruitInsideStream);

            var fruitAfterStream = new Fruit { StartTime = 6000, X = 200 };
            fruitAfterStream.ApplyDefaults(controlPoints, difficulty);
            beatmap.HitObjects.Add(fruitAfterStream);

            foreach (var hitObject in beatmap.HitObjects)
            {
                hitObject.ApplyDefaults(controlPoints, difficulty);

                foreach (var nested in hitObject.NestedHitObjects)
                    nested.ApplyDefaults(controlPoints, difficulty);
            }

            return beatmap;
        }

        private static void applyPerfectResults(ScoreProcessor processor, CatchBeatmap beatmap)
        {
            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                HitResult result = hitObject switch
                {
                    Fruit => HitResult.Great,
                    TinyDroplet => HitResult.SmallTickHit,
                    Droplet => HitResult.LargeTickHit,
                    Banana => HitResult.LargeBonus,
                    _ => HitResult.None,
                };

                processor.ApplyResult(new JudgementResult(hitObject, hitObject.Judgement) { Type = result });
            }
        }
    }
}
