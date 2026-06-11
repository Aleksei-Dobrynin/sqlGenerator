using SQLFileGenerator;
using Xunit;

namespace SqlGenerator.Tests;

public class PresetTagTests
{
    [Theory]
    [InlineData("default-v1.0", true)]
    [InlineData("clean-arch-v1.0", true)]
    [InlineData("default-v2.11", true)]
    [InlineData("default", false)]
    [InlineData("default-v1", false)]
    [InlineData("default-1.0", false)]
    [InlineData("Default-v1.0", false)]
    public void IsValid_MatchesSchema(string tag, bool expected)
    {
        Assert.Equal(expected, PresetTag.IsValid(tag));
    }

    [Fact]
    public void TryParse_SplitsPresetAndVersion()
    {
        Assert.True(PresetTag.TryParse("clean-arch-v2.5", out var preset, out var major, out var minor));
        Assert.Equal("clean-arch", preset);
        Assert.Equal(2, major);
        Assert.Equal(5, minor);
    }
}
