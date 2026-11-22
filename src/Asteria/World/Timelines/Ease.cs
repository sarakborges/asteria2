namespace Asteria.World.Timelines;

/// <summary>Built-in easing functions used by keyframe tracks.</summary>
internal static class Ease
{
    public static float InBack(float x) =>
        x * x * (2.70158F * x - 1.70158F);

    public static float InBounce(float x) =>
        1.0F - OutBounce(1.0F - x);

    public static float InCirc(float x) =>
        (float)(-Math.Sqrt(1.0F - x * x)) + 1.0F;

    public static float InCubic(float x) =>
        x * x * x;

    public static float InElastic(float x)
    {
        if (x == 0.0F) return 0.0F;
        if (x == 1.0F) return 1.0F;

        return (float)(-Math.Pow(2.0, 10.0 * x - 10.0) *
            Math.Sin((x * 10.0 - 10.75) * (float)(Math.PI * 2.0 / 3.0)));
    }

    public static float InExpo(float x) =>
        x == 0.0F ? 0.0F : (float)Math.Pow(2.0, 10.0 * x - 10.0);

    public static float InQuad(float x) =>
        x * x;

    public static float InQuart(float x)
    {
        float squared = x * x;
        return squared * squared;
    }

    public static float InQuint(float x)
    {
        float squared = x * x;
        return squared * squared * x;
    }

    public static float InSine(float x) =>
        1.0F - MathF.Cos(x * (float)(Math.PI / 2.0));

    public static float InOutBack(float x)
    {
        if (x < 0.5F)
            return 4.0F * x * x * (7.189819F * x - 2.5949094F) / 2.0F;

        float delta = 2.0F * x - 2.0F;
        return (delta * delta * (3.5949094F * delta + 2.5949094F) + 2.0F) / 2.0F;
    }

    public static float InOutBounce(float x) =>
        x < 0.5F
            ? (1.0F - OutBounce(1.0F - 2.0F * x)) / 2.0F
            : (1.0F + OutBounce(2.0F * x - 1.0F)) / 2.0F;

    public static float InOutCirc(float x) =>
        x < 0.5F
            ? (float)((1.0 - Math.Sqrt(1.0 - Math.Pow(2.0 * x, 2.0))) / 2.0)
            : (float)((Math.Sqrt(1.0 - Math.Pow(-2.0 * x + 2.0, 2.0)) + 1.0) / 2.0);

    public static float InOutCubic(float x) =>
        x < 0.5F
            ? 4.0F * x * x * x
            : (float)(1.0 - Math.Pow(-2.0 * x + 2.0, 3.0) / 2.0);

    public static float InOutElastic(float x)
    {
        if (x == 0.0F) return 0.0F;
        if (x == 1.0F) return 1.0F;

        double sine = Math.Sin((20.0 * x - 11.125) * (float)Math.PI * 4.0F / 9.0F);
        return x < 0.5F
            ? (float)(-(Math.Pow(2.0, 20.0 * x - 10.0) * sine) / 2.0)
            : (float)(Math.Pow(2.0, -20.0 * x + 10.0) * sine / 2.0 + 1.0);
    }

    public static float InOutExpo(float x)
    {
        if (x < 0.5F)
            return x == 0.0F ? 0.0F : (float)(Math.Pow(2.0, 20.0 * x - 10.0) / 2.0);

        return x == 1.0F
            ? 1.0F
            : (float)((2.0 - Math.Pow(2.0, -20.0 * x + 10.0)) / 2.0);
    }

    public static float InOutQuad(float x) =>
        x < 0.5F
            ? 2.0F * x * x
            : (float)(1.0 - Math.Pow(-2.0 * x + 2.0, 2.0) / 2.0);

    public static float InOutQuart(float x)
    {
        if (x < 0.5F)
        {
            float squared = x * x;
            return 8.0F * squared * squared;
        }

        return (float)(1.0 - Math.Pow(-2.0 * x + 2.0, 4.0) / 2.0);
    }

    public static float InOutQuint(float x) =>
        x < 0.5F
            ? 16.0F * x * x * x * x * x
            : (float)(1.0 - Math.Pow(-2.0 * x + 2.0, 5.0) / 2.0);

    public static float InOutSine(float x) =>
        -(MathF.Cos((float)Math.PI * x) - 1.0F) / 2.0F;

    public static float OutBack(float x)
    {
        float delta = x - 1.0F;
        return 1.0F + 2.70158F * delta * delta * delta + 1.70158F * delta * delta;
    }

    public static float OutBounce(float x)
    {
        if (x < 0.36363637F)
            return 7.5625F * x * x;

        if (x < 0.72727275F)
        {
            float delta = x - 0.54545456F;
            return 7.5625F * delta * delta + 0.75F;
        }

        if (x < 0.9090909090909091)
        {
            float delta = x - 0.8181818F;
            return 7.5625F * delta * delta + 0.9375F;
        }

        float finalDelta = x - 0.95454544F;
        return 7.5625F * finalDelta * finalDelta + 0.984375F;
    }

    public static float OutCirc(float x)
    {
        float delta = x - 1.0F;
        return (float)Math.Sqrt(1.0F - delta * delta);
    }

    public static float OutCubic(float x)
    {
        float delta = 1.0F - x;
        return 1.0F - delta * delta * delta;
    }

    public static float OutElastic(float x)
    {
        if (x == 0.0F) return 0.0F;
        if (x == 1.0F) return 1.0F;

        return (float)(Math.Pow(2.0, -10.0 * x) *
            Math.Sin((x * 10.0 - 0.75) * (float)(Math.PI * 2.0 / 3.0)) + 1.0);
    }

    public static float OutExpo(float x) =>
        x == 1.0F ? 1.0F : 1.0F - (float)Math.Pow(2.0, -10.0 * x);

    public static float OutQuad(float x)
    {
        float delta = 1.0F - x;
        return 1.0F - delta * delta;
    }

    public static float OutQuart(float x)
    {
        float delta = 1.0F - x;
        float squared = delta * delta;
        return 1.0F - squared * squared;
    }

    public static float OutQuint(float x) =>
        1.0F - (float)Math.Pow(1.0 - x, 5.0);

    public static float OutSine(float x) =>
        MathF.Sin(x * (float)(Math.PI / 2.0));
}
