using System.Diagnostics;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Generator;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Binary density-function operations from Minecraft Java 26.3.
/// </summary>
public sealed record BinaryFunction(
    BinaryFunction.Type Operation,
    DensityFunction Left,
    DensityFunction Right) : DensityFunction
{
    public enum Type
    {
        ADD,
        SUB,
        MUL,
        DIV,
        MIN,
        MAX
    }

    public static string GetId(Type type) =>
        type switch
        {
            Type.ADD => "add",
            Type.SUB => "sub",
            Type.MUL => "mul",
            Type.DIV => "div",
            Type.MIN => "min",
            Type.MAX => "max",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        // Vanilla compiles both children before selecting the specialized sampler.
        DensitySampler left = Left.CompileSampler(context);
        DensitySampler right = Right.CompileSampler(context);

        switch (Operation)
        {
            case Type.ADD:
                if (Left is ConstantFunction leftAdd)
                    return new ConstAddSampler(right, leftAdd.Value);
                if (Right is ConstantFunction rightAdd)
                    return new ConstAddSampler(left, rightAdd.Value);
                return new AddSampler(left, right);

            case Type.SUB:
                if (Left is ConstantFunction leftSub)
                    return new ConstSubSampler(leftSub.Value, right);
                if (Right is ConstantFunction rightSub)
                    return new ConstAddSampler(left, -rightSub.Value);
                return new SubSampler(left, right);

            case Type.MUL:
                if (Left is ConstantFunction leftMul)
                    return new ConstMulSampler(right, leftMul.Value);
                if (Right is ConstantFunction rightMul)
                    return new ConstMulSampler(left, rightMul.Value);
                return new MulSampler(left, right);

            case Type.DIV:
                if (Left is ConstantFunction leftDiv)
                    return new ConstDivSampler(leftDiv.Value, right);
                if (Right is ConstantFunction rightDiv)
                    return new ConstMulSampler(left, 1.0f / rightDiv.Value);
                return new DivSampler(left, right);

            case Type.MIN:
            {
                Interval leftRange = Left.Range();
                Interval rightRange = Right.Range();
                if (leftRange.Max() < rightRange.Min())
                {
                    WarnNonIntersecting(leftRange, rightRange);
                    return left;
                }

                if (rightRange.Max() < leftRange.Min())
                {
                    WarnNonIntersecting(leftRange, rightRange);
                    return right;
                }

                if (Left is ConstantFunction leftMin)
                    return new ConstMinSampler(right, leftMin.Value);
                if (Right is ConstantFunction rightMin)
                    return new ConstMinSampler(left, rightMin.Value);
                return new MinSampler(left, right, rightRange.Min());
            }

            case Type.MAX:
            {
                Interval leftRange = Left.Range();
                Interval rightRange = Right.Range();
                if (leftRange.Min() > rightRange.Max())
                {
                    WarnNonIntersecting(leftRange, rightRange);
                    return left;
                }

                if (rightRange.Min() > leftRange.Max())
                {
                    WarnNonIntersecting(leftRange, rightRange);
                    return right;
                }

                if (Left is ConstantFunction leftMax)
                    return new ConstMaxSampler(right, leftMax.Value);
                if (Right is ConstantFunction rightMax)
                    return new ConstMaxSampler(left, rightMax.Value);
                return new MaxSampler(left, right, rightRange.Max());
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(Operation));
        }
    }

    private void WarnNonIntersecting(Interval leftRange, Interval rightRange)
    {
        Trace.TraceWarning(
            "Compiling a {0} function between two non-overlapping inputs: {1} ({2}) and {3} ({4})",
            Operation,
            Left,
            leftRange,
            Right,
            rightRange);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction left = rule(Left);
        DensityFunction right = rule(Right);
        return ReferenceEquals(left, Left) && ReferenceEquals(right, Right)
            ? this
            : new BinaryFunction(Operation, left, right);
    }

    public Interval Range()
    {
        Interval left = Left.Range();
        Interval right = Right.Range();
        return Operation switch
        {
            Type.ADD => Interval.Add(left, right),
            Type.SUB => Interval.Sub(left, right),
            Type.MUL => Interval.Mul(left, right),
            Type.DIV => Interval.Div(left, right),
            Type.MIN => Interval.Min(left, right),
            Type.MAX => Interval.Max(left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(Operation))
        };
    }

    public int DomainAxes() =>
        Left.DomainAxes() | Right.DomainAxes();

    public sealed record ConstAddSampler(DensitySampler Left, float Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.AddTo(index, Right);
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left.SampleValue(context, blockX, blockY, blockZ) + Right;
    }

    public sealed record AddSampler(DensitySampler Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.AddTo(index, rightBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left.SampleValue(context, blockX, blockY, blockZ) +
            Right.SampleValue(context, blockX, blockY, blockZ);
    }

    public sealed record ConstSubSampler(float Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Right.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, Left - outputBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left - Right.SampleValue(context, blockX, blockY, blockZ);
    }

    public sealed record SubSampler(DensitySampler Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.AddTo(index, -rightBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left.SampleValue(context, blockX, blockY, blockZ) -
            Right.SampleValue(context, blockX, blockY, blockZ);
    }

    public sealed record ConstMulSampler(DensitySampler Left, float Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, outputBuffer.Get(index) * Right);
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left.SampleValue(context, blockX, blockY, blockZ) * Right;
    }

    public sealed record MulSampler(DensitySampler Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, outputBuffer.Get(index) * rightBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float left = Left.SampleValue(context, blockX, blockY, blockZ);
            if (left == 0.0f)
                return 0.0f;

            return left * Right.SampleValue(context, blockX, blockY, blockZ);
        }
    }

    public sealed record ConstDivSampler(float Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Right.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, Left / outputBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Left / Right.SampleValue(context, blockX, blockY, blockZ);
    }

    public sealed record DivSampler(DensitySampler Left, DensitySampler Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
                outputBuffer.Set(index, outputBuffer.Get(index) / rightBuffer.Get(index));
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float left = Left.SampleValue(context, blockX, blockY, blockZ);
            if (left == 0.0f)
                return 0.0f;

            return left / Right.SampleValue(context, blockX, blockY, blockZ);
        }
    }

    public sealed record ConstMinSampler(DensitySampler Left, float Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                if (Right < outputBuffer.Get(index))
                    outputBuffer.Set(index, Right);
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Math.Min(Left.SampleValue(context, blockX, blockY, blockZ), Right);
    }

    public sealed record MinSampler(
        DensitySampler Left,
        DensitySampler Right,
        float RightMinValue) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float rightValue = rightBuffer.Get(index);
                if (rightValue < outputBuffer.Get(index))
                    outputBuffer.Set(index, rightValue);
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float left = Left.SampleValue(context, blockX, blockY, blockZ);
            if (left <= RightMinValue)
                return left;

            return Math.Min(left, Right.SampleValue(context, blockX, blockY, blockZ));
        }
    }

    public sealed record ConstMaxSampler(DensitySampler Left, float Right) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                if (Right > outputBuffer.Get(index))
                    outputBuffer.Set(index, Right);
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Math.Max(Left.SampleValue(context, blockX, blockY, blockZ), Right);
    }

    public sealed record MaxSampler(
        DensitySampler Left,
        DensitySampler Right,
        float RightMaxValue) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Left.SampleVolume(context, outputBuffer, volume);
            using ScopedDensityBuffer rightBuffer = context.AcquireBuffer(volume);
            Right.SampleVolume(context, rightBuffer, volume);
            for (int index = 0; index < outputBuffer.Size(); index++)
            {
                float rightValue = rightBuffer.Get(index);
                if (rightValue > outputBuffer.Get(index))
                    outputBuffer.Set(index, rightValue);
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            float left = Left.SampleValue(context, blockX, blockY, blockZ);
            if (left >= RightMaxValue)
                return left;

            return Math.Max(left, Right.SampleValue(context, blockX, blockY, blockZ));
        }
    }
}
