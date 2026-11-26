using FHeroes2.Core.Diagnostics;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The block writer of the text support channel, <c>START_TEXT_SUPPORT_MODE</c> of
/// <c>logging.h:179-201</c>.
/// </summary>
[Collection(TextSupportCollection.Name)]
public sealed class TextSupportTests
{
    /// <summary>
    /// The channel is a process-wide switch plus a sink, so these tests must not share the process with anyone
    /// else flipping them.
    /// </summary>
    public TextSupportTests()
    {
        TextSupport.IsEnabled = false;
        TextSupport.Output = null;
    }

    [Fact]
    public void NothingIsWrittenWhileTheModeIsOff()
    {
        StringWriter sink = new();
        TextSupport.Output = sink;
        TextSupport.IsEnabled = false;

        TextSupport.WriteBlock(["Scenario Information"]);

        Assert.Equal(string.Empty, sink.ToString());
    }

    [Fact]
    public void ABlockIsFramedByTheSeparators()
    {
        StringWriter sink = new();
        TextSupport.Output = sink;
        TextSupport.IsEnabled = true;

        TextSupport.WriteBlock(["Scenario Information", "'Roland' campaign, scenario 1: Force of Arms"]);

        string[] lines = sink.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(
            [
                TextSupport.Separator,
                "Scenario Information",
                "'Roland' campaign, scenario 1: Force of Arms",
                TextSupport.Separator,
            ],
            lines);
    }

    [Fact]
    public void AnEmptyBlockStillWritesItsSeparators()
    {
        StringWriter sink = new();
        TextSupport.Output = sink;
        TextSupport.IsEnabled = true;

        TextSupport.WriteBlock([]);

        Assert.Equal(
            $"{TextSupport.Separator}{Environment.NewLine}{TextSupport.Separator}{Environment.NewLine}",
            sink.ToString());
    }
}
