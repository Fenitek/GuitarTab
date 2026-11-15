using GuitarStudio.Audio;

namespace GuitarStudio.Tests;

public sealed class PreviewRendererTests
{
    private static PreviewRenderer Create() => new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "GeneralUser-GS.sf2")));

    [Theory]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(33)]
    [InlineData(36)]
    public void GuitarAndBassProduceFiniteNonSilentSamples(int program)
    {
        var renderer = Create();
        renderer.Start(program, 64);
        var samples = new float[9600];
        renderer.Read(samples, 0, samples.Length);
        Assert.All(samples, sample => Assert.True(float.IsFinite(sample)));
        Assert.Contains(samples, sample => Math.Abs(sample) > 0.001);
    }

    [Fact]
    public void NewNoteSilencesPreviousVoicesAndStopClearsPendingSamples()
    {
        var renderer = Create();
        var scratch = new float[9600];
        var reference = Create();
        reference.Start(25, 64);
        var expected = new float[scratch.Length]; reference.Read(expected, 0, expected.Length);
        foreach (var previousLength in new[] { 777, 30000 })
        {
            renderer.Start(33, 43); renderer.Read(new float[previousLength], 0, previousLength);
            renderer.Start(25, 64); renderer.Read(scratch, 0, scratch.Length);
            Assert.Equal(expected, scratch);
        }
        renderer.Stop(); renderer.Read(scratch, 0, scratch.Length);
        Assert.All(scratch, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void PitchAndProgramChangeTheSoundAndReadRespectsItsSlice()
    {
        var renderer = Create();
        float[] Render(int program, int key)
        {
            renderer.Start(program, key);
            var buffer = Enumerable.Repeat(123f, 9604).ToArray();
            renderer.Read(buffer, 2, 9600);
            Assert.Equal(123f, buffer[0]); Assert.Equal(123f, buffer[1]);
            Assert.Equal(123f, buffer[^1]); Assert.Equal(123f, buffer[^2]);
            return buffer[2..^2];
        }
        var guitar = Render(25, 64);
        Assert.False(guitar.SequenceEqual(Render(25, 66)));
        Assert.False(guitar.SequenceEqual(Render(33, 64)));
    }

    [Fact]
    public void InvalidSoundFontCannotBecomeAReadyPreviewEngine()
    {
        Assert.ThrowsAny<Exception>(() => new PreviewRenderer([1, 2, 3, 4]));
    }
}
