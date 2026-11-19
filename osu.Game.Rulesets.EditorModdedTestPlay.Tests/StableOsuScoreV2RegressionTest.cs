#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.EditorModdedTestPlay.Scoring;
using osuTK;

namespace osu.Game.Rulesets.EditorModdedTestPlay.Tests
{
    [TestFixture]
    public class StableOsuScoreV2RegressionTest
    {
        [TestCase(2000, 5, 10, 15, 9, 15, 6000)]
        [TestCase(100, 0, 0, 0, 0, -1, 0)]
        [TestCase(1667, 6, 9, 6, 7, 14, 4100)]
        public void TestSpinnerHalfSpinTicks(double duration, double overallDifficulty, int requiredHalfSpins, int expectedNormalTicks, int expectedBonusTicks, int firstBonusHalfSpin, int expectedBonusScore)
        {
            var spinner = new StableSpinner
            {
                StartTime = 1000,
                Duration = duration,
            };

            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = (float)overallDifficulty });

            var ticks = spinner.NestedHitObjects.OfType<SpinnerTick>().ToList();
            var normalTicks = ticks.Where(t => t is not SpinnerBonusTick).ToList();
            var bonusTicks = ticks.OfType<SpinnerBonusTick>().ToList();

            Assert.That(spinner.LegacyHalfSpinsRequired, Is.EqualTo(requiredHalfSpins));
            Assert.That(normalTicks.Count, Is.EqualTo(expectedNormalTicks));
            Assert.That(bonusTicks.Count, Is.EqualTo(expectedBonusTicks));

            if (expectedBonusTicks > 0)
                Assert.That(((ILegacyHalfSpinTick)bonusTicks.MinBy(t => t.StartTime)!).LegacyHalfSpinIndex, Is.EqualTo(firstBonusHalfSpin));

            Assert.That(normalTicks.Count * 100 + bonusTicks.Count * 500, Is.EqualTo(expectedBonusScore));
        }

        [Test]
        public void TestSpinnerTickTimesUseStableHalfSpinRate()
        {
            var spinner = new StableSpinner
            {
                StartTime = 1000,
                Duration = 2000,
            };

            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 5 });

            foreach (SpinnerTick tick in spinner.NestedHitObjects.OfType<SpinnerTick>())
            {
                var legacyTick = (ILegacyHalfSpinTick)tick;
                Assert.That(tick.StartTime, Is.EqualTo(1000 + legacyTick.LegacyHalfSpinIndex * System.Math.PI / 0.05).Within(0.0001));
            }

            Assert.That(spinner.NestedHitObjects.OfType<SpinnerTick>().First().StartTime, Is.EqualTo(1000 + 2 * System.Math.PI / 0.05).Within(0.0001));
        }

        [TestCase(HitResult.Great, 4, 4, HitResult.Great)]
        [TestCase(HitResult.Great, 0, 4, HitResult.Miss)]
        [TestCase(HitResult.Great, 2, 4, HitResult.Ok)]
        [TestCase(HitResult.Great, 1, 4, HitResult.Meh)]
        [TestCase(HitResult.Ok, 4, 4, HitResult.Ok)]
        [TestCase(HitResult.Ok, 2, 4, HitResult.Ok)]
        [TestCase(HitResult.Ok, 1, 4, HitResult.Meh)]
        [TestCase(HitResult.Meh, 4, 4, HitResult.Meh)]
        [TestCase(HitResult.Miss, 3, 4, HitResult.Meh)]
        [TestCase(HitResult.Miss, 4, 4, HitResult.Meh)]
        [TestCase(HitResult.Miss, 0, 4, HitResult.Miss)]
        public void TestSliderAggregate(HitResult headResult, int hitCount, int totalCount, HitResult expected)
        {
            Assert.That(StableSliderJudgement.ComputeAggregate(headResult, hitCount, totalCount), Is.EqualTo(expected));
        }

        [Test]
        public void TestSliderTrackingThresholds()
        {
            Assert.That(StableSliderJudgement.ComputeTracking(10, 10), Is.EqualTo(HitResult.Great));
            Assert.That(StableSliderJudgement.ComputeTracking(5, 10), Is.EqualTo(HitResult.Ok));
            Assert.That(StableSliderJudgement.ComputeTracking(4, 10), Is.EqualTo(HitResult.Meh));
            Assert.That(StableSliderJudgement.ComputeTracking(1, 10), Is.EqualTo(HitResult.Meh));
            Assert.That(StableSliderJudgement.ComputeTracking(0, 10), Is.EqualTo(HitResult.Miss));
        }

        [TestCase(0.5, 0.05)]
        [TestCase(0.75, 0.3)]
        [TestCase(0.8, 0.35)]
        [TestCase(0.99, 0.54)]
        public void TestHalfTimeRateMultiplier(double speed, double expected)
        {
            var calculator = new StableOsuScoreV2MultiplierCalculator(new ScoreMultiplierContext(new BeatmapDifficulty()));

            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModHalfTime { SpeedChange = { Value = speed } } }), Is.EqualTo(expected).Within(0.0001));
            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModDaycore { SpeedChange = { Value = speed } } }), Is.EqualTo(expected).Within(0.0001));
        }

        [TestCase(1.01, 1.004)]
        [TestCase(1.1, 1.04)]
        [TestCase(1.25, 1.1)]
        [TestCase(1.5, 1.2)]
        [TestCase(2.0, 1.4)]
        public void TestDoubleTimeRateMultiplier(double speed, double expected)
        {
            var calculator = new StableOsuScoreV2MultiplierCalculator(new ScoreMultiplierContext(new BeatmapDifficulty()));

            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = speed } } }), Is.EqualTo(expected).Within(0.0001));
            Assert.That(calculator.CalculateFor(new Mod[] { new OsuModNightcore { SpeedChange = { Value = speed } } }), Is.EqualTo(expected).Within(0.0001));
        }

        [TestCase(HitResult.SmallTickMiss, 1)]
        [TestCase(HitResult.SmallTickHit, 2)]
        public void TestSliderTailComboAndRewind(HitResult tailResult, int expectedCombo)
        {
            var (processor, circle, slider) = createSliderProcessor();

            apply(processor, circle, HitResult.Great);
            var tail = new JudgementResult(slider.TailCircle, slider.TailCircle.CreateJudgement()) { Type = tailResult };
            processor.ApplyResult(tail);

            var parent = new JudgementResult(slider, slider.CreateJudgement()) { Type = HitResult.Ok };
            processor.ApplyResult(parent);

            Assert.That(processor.Combo.Value, Is.EqualTo(expectedCombo));
            Assert.That(processor.HighestCombo.Value, Is.EqualTo(expectedCombo));
            Assert.That(parent.ComboAfterJudgement, Is.EqualTo(expectedCombo));

            long score = processor.TotalScoreWithoutMods.Value;

            processor.RevertResult(parent);
            Assert.That(processor.Combo.Value, Is.EqualTo(1));
            Assert.That(processor.HighestCombo.Value, Is.EqualTo(1));

            processor.ApplyResult(parent);
            Assert.That(processor.Combo.Value, Is.EqualTo(expectedCombo));
            Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(score));

            // Rewinding the tail must also clear its missed status, so a subsequently
            // successful tail restores both the parent combo and its combo-weighted score.
            processor.RevertResult(parent);
            processor.RevertResult(tail);
            tail.Type = HitResult.SmallTickHit;
            processor.ApplyResult(tail);
            processor.ApplyResult(parent);

            Assert.That(processor.Combo.Value, Is.EqualTo(2));
            Assert.That(processor.HighestCombo.Value, Is.EqualTo(2));
            Assert.That(parent.ComboAfterJudgement, Is.EqualTo(2));
        }

        [Test]
        public void TestMissedSliderParentBreaksCombo()
        {
            var (processor, circle, slider) = createSliderProcessor();

            apply(processor, circle, HitResult.Great);

            var tail = new JudgementResult(slider.TailCircle, slider.TailCircle.CreateJudgement()) { Type = HitResult.SmallTickHit };
            processor.ApplyResult(tail);

            var parent = new JudgementResult(slider, slider.CreateJudgement()) { Type = HitResult.Miss };
            processor.ApplyResult(parent);

            Assert.That(processor.Combo.Value, Is.Zero);
            Assert.That(processor.HighestCombo.Value, Is.EqualTo(1));

            processor.RevertResult(parent);
            Assert.That(processor.Combo.Value, Is.EqualTo(1));
        }

        [TestCase(0, 50)]
        [TestCase(0, 150)]
        [TestCase(0, 350)]
        [TestCase(1, 50)]
        [TestCase(1, 150)]
        [TestCase(2, 150)]
        public void TestSliderSimulationComboOrdering(int repeatCount, double length)
        {
            var slider = new Slider
            {
                StartTime = 1000,
                ClassicSliderBehaviour = true,
                RepeatCount = repeatCount,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2((float)length, 0) }, length),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1 });

            var processor = new StableOsuScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenOsuRuleset());
            processor.ApplyBeatmap(createBeatmap(new HitObject[] { slider }));

            int checkpoints = repeatCount + 2;
            int ticks = slider.NestedHitObjects.OfType<SliderTick>().Count();

            Assert.That(processor.MaximumCombo, Is.EqualTo(checkpoints + ticks));
        }

        [TestCase(0, 50)]
        [TestCase(0, 150)]
        [TestCase(0, 350)]
        [TestCase(1, 50)]
        [TestCase(1, 150)]
        [TestCase(2, 150)]
        public void TestSliderLiveScoreMatchesStableNormalisation(int repeatCount, double length)
        {
            var slider = new Slider
            {
                StartTime = 1000,
                ClassicSliderBehaviour = true,
                RepeatCount = repeatCount,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2((float)length, 0) }, length),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1 });

            var processor = new StableOsuScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenOsuRuleset());
            processor.ApplyBeatmap(createBeatmap(new HitObject[] { slider }));

            int checkpoints = repeatCount + 2;
            int ticks = slider.NestedHitObjects.OfType<SliderTick>().Count();
            int combo = 0;
            double expectedMaximum = 0;

            for (int i = 0; i < checkpoints; i++)
                expectedMaximum += 30 * (1 + ++combo / 10.0);

            for (int i = 0; i < ticks; i++)
                expectedMaximum += 10 * (1 + ++combo / 10.0);

            expectedMaximum += 300 * (1 + combo / 10.0);

            combo = 0;
            double expectedLive = 0;

            foreach (HitObject nested in slider.NestedHitObjects)
            {
                double baseScore = nested is SliderTick ? 10 : 30;
                expectedLive += baseScore * (1 + ++combo / 10.0);
                apply(processor, nested, nested.Judgement.MaxResult);
            }

            expectedLive += 300 * (1 + combo / 10.0);
            apply(processor, slider, slider.Judgement.MaxResult);

            long expectedScore = (long)System.Math.Round(700000 * expectedLive / expectedMaximum + 300000);
            Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(expectedScore));
        }

        [TestCase(false, 25667, 25667)]
        [TestCase(true, 25667, 27207)]
        public void TestScoreV2RoundsCompleteFormulaMisses(bool hidden, long expectedWithoutMods, long expectedTotal)
        {
            runRoundsCompleteFormula(hidden, new[] { HitResult.Miss, HitResult.Miss, HitResult.Miss, HitResult.Meh }, expectedWithoutMods, expectedTotal);
        }

        [TestCase(false, 219338, 219338)]
        [TestCase(true, 219338, 232499)]
        public void TestScoreV2RoundsCompleteFormulaHits(bool hidden, long expectedWithoutMods, long expectedTotal)
        {
            runRoundsCompleteFormula(hidden, new[] { HitResult.Miss, HitResult.Miss, HitResult.Ok, HitResult.Great }, expectedWithoutMods, expectedTotal);
        }

        private static void runRoundsCompleteFormula(bool hidden, HitResult[] results, long expectedWithoutMods, long expectedTotal)
        {
            var circles = Enumerable.Range(0, 4).Select(i => new HitCircle { StartTime = i * 1000 }).ToArray();

            foreach (HitCircle circle in circles)
                circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            var processor = new StableOsuScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenOsuRuleset());
            processor.Mods.Value = hidden ? new Mod[] { new OsuModHidden() } : System.Array.Empty<Mod>();
            processor.ApplyBeatmap(createBeatmap(circles));

            Assert.That(processor.MaximumCombo, Is.EqualTo(4));

            for (int i = 0; i < results.Length; i++)
                apply(processor, circles[i], results[i]);

            Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(expectedWithoutMods));

            var score = new osu.Game.Scoring.ScoreInfo();
            processor.PopulateScore(score);
            Assert.That(score.TotalScoreWithoutMods, Is.EqualTo(expectedWithoutMods));
            Assert.That(score.TotalScore, Is.EqualTo(expectedTotal));
        }

        [Test]
        public void TestSpinnerCompletedFullSpinsIsMutableBindable()
        {
            var spinner = new StableDrawableSpinner();
            Assert.That(spinner.CompletedFullSpins, Is.InstanceOf<Bindable<int>>());
        }

        [Test]
        public void TestSpinnerBonusRewind()
        {
            var spinner = new StableSpinner { StartTime = 1000, Duration = 2000 };
            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 5 });

            var processor = new StableOsuScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenOsuRuleset());
            processor.ApplyBeatmap(createBeatmap(new HitObject[] { spinner }));

            var normalTick = spinner.NestedHitObjects.OfType<SpinnerTick>().First(t => t is not SpinnerBonusTick);
            var bonusTick = spinner.NestedHitObjects.OfType<SpinnerBonusTick>().First();

            var normalResult = new JudgementResult(normalTick, normalTick.CreateJudgement()) { Type = HitResult.SmallBonus };
            var bonusResult = new JudgementResult(bonusTick, bonusTick.CreateJudgement()) { Type = HitResult.LargeBonus };
            processor.ApplyResult(normalResult);
            processor.ApplyResult(bonusResult);

            Assert.That(processor.Combo.Value, Is.Zero);
            Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(600));

            processor.RevertResult(bonusResult);
            Assert.That(processor.TotalScoreWithoutMods.Value, Is.EqualTo(100));

            processor.RevertResult(normalResult);
            Assert.That(processor.TotalScoreWithoutMods.Value, Is.Zero);
        }

        private static (StableOsuScoreV2ScoreProcessor processor, HitCircle circle, Slider slider) createSliderProcessor()
        {
            var circle = new HitCircle { StartTime = 0 };
            circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            var slider = new Slider
            {
                StartTime = 1000,
                ClassicSliderBehaviour = true,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 100),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { SliderMultiplier = 1, SliderTickRate = 1 });

            var processor = new StableOsuScoreV2ScoreProcessor(new HiddenScoreV2Rulesets.HiddenOsuRuleset());
            processor.ApplyBeatmap(createBeatmap(new HitObject[] { circle, slider }));
            return (processor, circle, slider);
        }

        private static Beatmap createBeatmap(IEnumerable<HitObject> hitObjects)
        {
            return new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty(),
                    Ruleset = new OsuRuleset().RulesetInfo,
                },
                HitObjects = hitObjects.ToList(),
            };
        }

        private static void apply(StableOsuScoreV2ScoreProcessor processor, HitObject hitObject, HitResult result)
        {
            processor.ApplyResult(new JudgementResult(hitObject, hitObject.CreateJudgement()) { Type = result });
        }
    }
}
