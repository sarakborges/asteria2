using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.BlockScan;

/// <summary>
/// Receives a matching block state and chooses whether the scan should continue.
/// </summary>
public delegate Continuation BlockStateConsumer(BlockPos position, BlockState state);
