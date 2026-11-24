using Asteria.Content.Blocks;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>Compiled material-rule evaluator from Minecraft Java 26.3.</summary>
public delegate BlockState? RuleEvaluator(int blockX, int blockY, int blockZ);
