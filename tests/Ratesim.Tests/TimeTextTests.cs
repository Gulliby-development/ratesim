using Ratesim.Core;
using Xunit;

namespace Ratesim.Tests;

public class TimeTextTests
{
    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("10s", 10000)]
    [InlineData("5m", 300000)]
    [InlineData("1h", 3600000)]
    [InlineData("24h", 86400000)]
    [InlineData("1440m", 86400000)]
    [InlineData("86400s", 86400000)]
    [InlineData("999999ms", 999999)]
    [InlineData("1ms", 1)]
    public void ParseDuration_Valid(string text, long expected)
    {
        Assert.True(TimeText.TryParseDuration(text, out long ms));
        Assert.Equal(expected, ms);
    }

    [Theory]
    [InlineData("0s")]
    [InlineData("010s")]
    [InlineData("1000000ms")]
    [InlineData("86401s")]
    [InlineData("25h")]
    [InlineData("10S")]
    [InlineData("10")]
    [InlineData("1.5s")]
    [InlineData("-1s")]
    [InlineData("")]
    [InlineData("s")]
    [InlineData("ms")]
    [InlineData("10d")]
    public void ParseDuration_Invalid(string text)
    {
        Assert.False(TimeText.TryParseDuration(text, out _));
    }

    [Fact]
    public void ParseDuration_Null_Invalid()
    {
        Assert.False(TimeText.TryParseDuration(null, out _));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("999999", 999999)]
    [InlineData("10", 10)]
    public void ParsePositive_Valid(string text, long expected)
    {
        Assert.True(TimeText.TryParsePositive(text, out long value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("1000000")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("")]
    public void ParsePositive_Invalid(string text)
    {
        Assert.False(TimeText.TryParsePositive(text, out _));
    }

    [Theory]
    [InlineData("1970-01-01T00:00:00Z", 0)]
    [InlineData("1970-01-01T00:00:00.5Z", 500)]
    [InlineData("1970-01-01T00:00:00.25Z", 250)]
    [InlineData("1970-01-01T00:00:00.007Z", 7)]
    [InlineData("1970-01-01T00:00:00.250Z", 250)]
    [InlineData("1970-01-01T00:00:01Z", 1000)]
    [InlineData("2024-05-01T10:00:00Z", 1714557600000)]
    [InlineData("2024-02-29T12:00:00Z", 1709208000000)]
    [InlineData("9999-12-31T23:59:59.999Z", 253402300799999)]
    public void ParseTimestamp_Valid(string text, long expected)
    {
        Assert.True(TimeText.TryParseTimestamp(text, out long ms));
        Assert.Equal(expected, ms);
    }

    [Theory]
    [InlineData("2000-02-29T00:00:00Z")]
    [InlineData("2024-02-29T00:00:00Z")]
    public void ParseTimestamp_LeapDay_Valid(string text)
    {
        Assert.True(TimeText.TryParseTimestamp(text, out _));
    }

    [Theory]
    [InlineData("2023-02-29T00:00:00Z")]
    [InlineData("2100-02-29T00:00:00Z")]
    [InlineData("2024-02-30T10:00:00Z")]
    [InlineData("2024-04-31T00:00:00Z")]
    [InlineData("2024-13-01T00:00:00Z")]
    [InlineData("2024-00-01T00:00:00Z")]
    [InlineData("2024-05-00T00:00:00Z")]
    [InlineData("1969-12-31T23:59:59Z")]
    [InlineData("2024-05-01T24:00:00Z")]
    [InlineData("2024-05-01T10:60:00Z")]
    [InlineData("2024-05-01T10:00:60Z")]
    [InlineData("2024-05-01T10:00:00.1234Z")]
    [InlineData("2024-05-01T10:00:00.Z")]
    [InlineData("2024-05-01t10:00:00z")]
    [InlineData("2024-05-01T10:00:00")]
    [InlineData("2024-05-01T10:00:00+00:00")]
    [InlineData("24-05-01T10:00:00Z")]
    [InlineData("")]
    public void ParseTimestamp_Invalid(string text)
    {
        Assert.False(TimeText.TryParseTimestamp(text, out _));
    }

    [Fact]
    public void ParseTimestamp_Null_Invalid()
    {
        Assert.False(TimeText.TryParseTimestamp(null, out _));
    }
}
