using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.BlockScan;

/// <summary>
/// Scans a bounded region of one chunk section in vanilla Y/Z/X traversal order.
/// </summary>
public static class BlockScanUtils
{
    public static bool FindBlocksInSection(
        ChunkSection section,
        BlockPos origin,
        int fromX,
        int fromY,
        int fromZ,
        int toX,
        int toY,
        int toZ,
        Predicate<BlockState> predicate,
        BlockStateConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(consumer);

        var mutablePosition = new BlockPos.MutableBlockPos();
        for (int y = fromY; y <= toY; y++)
        {
            for (int z = fromZ; z <= toZ; z++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    BlockState state = section.GetBlockState(x, y, z);
                    if (!predicate(state))
                        continue;

                    mutablePosition.SetWithOffset(origin, x, y, z);
                    if (consumer(mutablePosition, state).ShouldAbort())
                        return true;
                }
            }
        }

        return false;
    }
}
