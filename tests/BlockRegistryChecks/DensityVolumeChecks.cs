using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Structure;

internal static class DensityVolumeChecks
{
    public static void Run()
    {
        var volume = new DensityVolume(2, 3, 4, -8, 10, 32);
        Require(volume.SizeX == 2 && volume.SizeY == 3 && volume.SizeZ == 4 &&
            volume.MinBlockX == -8 && volume.MinBlockY == 10 && volume.MinBlockZ == 32 &&
            volume.StepBlockX == 1 && volume.StepBlockY == 1 && volume.StepBlockZ == 1,
            "The six-argument DensityVolume constructor must default all step values to one.");
        Require(volume.Size() == 24 &&
            volume.MaxBlockX() == -7 && volume.MaxBlockY() == 12 && volume.MaxBlockZ() == 35,
            "DensityVolume size and maximum coordinates must reflect inclusive block bounds.");
        Require(volume.BlockX(1) == -7 && volume.BlockY(2) == 12 && volume.BlockZ(3) == 35,
            "DensityVolume block-coordinate accessors must apply the per-axis minimum and step.");
        Require(volume.IndexUnchecked(1, 2, 3) == 23 &&
            volume.IndexUnchecked(0, 1, 0) == 1,
            "DensityVolume.indexUnchecked must use Y-minor, then X, then Z storage order.");

        Require(volume.IndexOfBlock(-8, 10, 32) == 0 &&
            volume.IndexOfBlock(-7, 11, 33) == 10 &&
            volume.IndexOfBlock(-7, 12, 35) == 23 &&
            volume.IndexOfBlock(-6, 10, 32) == DensityVolume.NO_BLOCK &&
            volume.IndexOfBlock(-8, 13, 32) == DensityVolume.NO_BLOCK,
            "Unit-step DensityVolume.indexOfBlock must resolve valid cells and reject coordinates outside each axis.");

        var stepped = new DensityVolume(2, 3, 2, 100, -10, 200, 4, 2, 5);
        Require(stepped.Size() == 12 &&
            stepped.BlockX(1) == 104 && stepped.BlockY(2) == -6 && stepped.BlockZ(1) == 205 &&
            stepped.MaxBlockX() == 107 && stepped.MaxBlockY() == -5 && stepped.MaxBlockZ() == 209,
            "Stepped DensityVolume coordinates and maxima must use the complete step width.");
        Require(stepped.IndexOfBlock(100, -10, 200) == 0 &&
            stepped.IndexOfBlock(104, -8, 205) == 10 &&
            stepped.IndexOfBlock(107, -5, 209) == DensityVolume.NO_BLOCK &&
            stepped.IndexOfBlock(105, -8, 205) == DensityVolume.NO_BLOCK &&
            stepped.IndexOfBlock(104, -7, 205) == DensityVolume.NO_BLOCK,
            "Stepped DensityVolume.indexOfBlock must accept only aligned coordinates within each stepped axis.");

        Require(volume.Intersects(new BoundingBox(-8, 12, 35, -7, 14, 40)) &&
            !volume.Intersects(new BoundingBox(-6, 10, 32, 0, 20, 40)),
            "DensityVolume.intersects must use its inclusive world-space bounds.");

        RequireThrows<ArgumentException>(() => new DensityVolume(0, 1, 1, 0, 0, 0),
            "DensityVolume must reject non-positive sizes.");
        RequireThrows<ArgumentException>(() => new DensityVolume(1, -1, 1, 0, 0, 0),
            "DensityVolume must reject a negative size on any axis.");
        RequireThrows<ArgumentException>(() => new DensityVolume(1, 1, 1, 0, 0, 0, 1, 0, 1),
            "DensityVolume must reject non-positive steps.");
        ArgumentException sizeException = Capture<ArgumentException>(
            () => new DensityVolume(0, 2, 3, 0, 0, 0),
            "DensityVolume must report the invalid size tuple.");
        Require(sizeException.Message == "Size must be positive, was: 0x2x3",
            "DensityVolume size validation must retain the reference diagnostic.");
        ArgumentException stepException = Capture<ArgumentException>(
            () => new DensityVolume(1, 2, 3, 0, 0, 0, 4, -2, 6),
            "DensityVolume must report the invalid step tuple.");
        Require(stepException.Message == "Step must be positive, was: 4; -2; 6",
            "DensityVolume step validation must retain the reference diagnostic.");

        Require(volume == new DensityVolume(2, 3, 4, -8, 10, 32),
            "DensityVolume record equality must compare the full coordinate and sampling-step tuple.");
    }

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException exception) { return exception; }
        throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
