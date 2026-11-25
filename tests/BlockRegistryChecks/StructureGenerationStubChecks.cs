using Asteria.Core;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class StructureGenerationStubChecks
{
    public static void Run()
    {
        CheckDeferredPieceGeneratorCreatesFreshBuilders();
        CheckPrebuiltBuilderIsReturnedByIdentity();
        CheckArgumentsAreRequired();
    }

    private static void CheckDeferredPieceGeneratorCreatesFreshBuilders()
    {
        var position = new BlockPos(-37, 82, 119);
        int generationCalls = 0;

        var stub = new Structure.GenerationStub(
            position,
            builder =>
            {
                generationCalls++;
                builder.AddPiece(new TestPiece(new BoundingBox(
                    generationCalls * 10,
                    20,
                    30,
                    generationCalls * 10 + 4,
                    24,
                    34)));
            });

        StructurePiecesBuilder first = stub.GetPiecesBuilder();
        StructurePiecesBuilder second = stub.GetPiecesBuilder();

        Require(
            ReferenceEquals(stub.Position, position),
            "GenerationStub must preserve the supplied generation position.");
        Require(
            generationCalls == 2,
            "A deferred piece generator must execute every time GetPiecesBuilder is called.");
        Require(
            !ReferenceEquals(first, second),
            "Each deferred GetPiecesBuilder call must allocate a fresh StructurePiecesBuilder.");
        Require(
            first.Build().Pieces.Count == 1 &&
            second.Build().Pieces.Count == 1 &&
            first.GetBoundingBox().MinX() == 10 &&
            second.GetBoundingBox().MinX() == 20,
            "Each generated builder must contain only the pieces produced by its own generator invocation.");
    }

    private static void CheckPrebuiltBuilderIsReturnedByIdentity()
    {
        var position = new BlockPos(8, 64, -12);
        var builder = new StructurePiecesBuilder();
        builder.AddPiece(new TestPiece(new BoundingBox(1, 2, 3, 4, 5, 6)));

        var stub = new Structure.GenerationStub(position, builder);

        StructurePiecesBuilder first = stub.GetPiecesBuilder();
        StructurePiecesBuilder second = stub.GetPiecesBuilder();

        Require(
            ReferenceEquals(first, builder) && ReferenceEquals(second, builder),
            "A prebuilt GenerationStub must return its original builder by identity.");
        Require(
            first.Build().Pieces.Count == 1,
            "A prebuilt GenerationStub must retain the builder's existing pieces without recreating them.");
    }

    private static void CheckArgumentsAreRequired()
    {
        RequireThrows<ArgumentNullException>(
            () => _ = new Structure.GenerationStub(
                null!,
                (Action<StructurePiecesBuilder>)(_ => { })),
            "GenerationStub must reject a null generation position.");

        RequireThrows<ArgumentNullException>(
            () => _ = new Structure.GenerationStub(
                new BlockPos(0, 0, 0),
                (Action<StructurePiecesBuilder>)null!),
            "The deferred GenerationStub constructor must reject a null generator.");

        RequireThrows<ArgumentNullException>(
            () => _ = new Structure.GenerationStub(
                new BlockPos(0, 0, 0),
                (StructurePiecesBuilder)null!),
            "The prebuilt GenerationStub constructor must reject a null builder.");
    }

    private sealed class TestPiece : StructurePiece
    {
        public TestPiece(BoundingBox boundingBox) : base(0, boundingBox)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
