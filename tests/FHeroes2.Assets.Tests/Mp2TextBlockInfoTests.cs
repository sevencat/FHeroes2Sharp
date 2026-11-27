using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the three text information blocks of an MP2 map: the sign, the ground event and the sphinx
/// (<c>maps_objects.cpp:44-313</c>).
/// </summary>
public sealed class Mp2TextBlockInfoTests
{
    [Fact]
    public void ASignBlockRoundTrips()
    {
        Mp2SignInfo block = new() { Message = "North" };

        byte[] data = block.ToBlock();

        Assert.Equal(0x01, data[0]);
        Assert.Equal(Mp2SignInfo.TextOffset + "North".Length + 1, data.Length);

        Assert.True(Mp2SignInfo.TryRead(data, out Mp2SignInfo read));
        Assert.Equal("North", read.Message);
    }

    [Fact]
    public void AnEmptySignBecomesAnEmptyText()
    {
        // 参考对空文本会补一句默认话；端口保留空串，而写出的块仍然合法。
        Assert.True(Mp2SignInfo.TryRead(new Mp2SignInfo().ToBlock(), out Mp2SignInfo read));
        Assert.Equal(string.Empty, read.Message);
    }

    [Fact]
    public void ASignBlockNeedsItsMagicByte()
    {
        byte[] data = new Mp2SignInfo { Message = "X" }.ToBlock();
        data[0] = 0x02;

        Assert.False(Mp2SignInfo.TryRead(data, out _));
    }

    [Fact]
    public void AnEventBlockRoundTrips()
    {
        Mp2EventInfo block = new()
        {
            // 顺序：木、水银、矿、硫、水晶、宝石、金。
            Resources = [1, 2, 3, 4, 5, 6, 7000],
            ArtifactId = 9,
            IsComputerAllowed = true,
            IsSingleTime = true,
            Colors = PlayerColor.Blue | PlayerColor.Red,
            Message = "A gift",
        };

        byte[] data = block.ToBlock();

        Assert.Equal(0x01, data[0]);

        Assert.True(Mp2EventInfo.TryRead(data, out Mp2EventInfo read));

        Assert.Equal([1, 2, 3, 4, 5, 6, 7000], read.Resources);
        Assert.Equal(9, read.ArtifactId);
        Assert.True(read.IsComputerAllowed);
        Assert.True(read.IsSingleTime);
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, read.Colors);
        Assert.Equal("A gift", read.Message);
    }

    [Fact]
    public void AnEventBlockIsAtLeastFiftyBytes()
    {
        // 参考的下限是 50：一字节、七个 int32、一个 uint16、两个标志、十个未用、六个颜色。
        byte[] data = new Mp2EventInfo { Message = "" }.ToBlock();

        Assert.True(data.Length >= Mp2EventInfo.MinimumSize, $"the block is {data.Length} bytes");
    }

    [Fact]
    public void NegativeResourcesAreAllowed()
    {
        Mp2EventInfo block = new() { Resources = [-100, 0, 0, 0, 0, 0, -5] };

        Assert.True(Mp2EventInfo.TryRead(block.ToBlock(), out Mp2EventInfo read));
        Assert.Equal(-100, read.Resources[0]);
        Assert.Equal(-5, read.Resources[6]);
    }

    [Fact]
    public void ASphinxBlockRoundTrips()
    {
        Mp2SphinxInfo block = new()
        {
            Resources = [0, 0, 0, 0, 0, 0, 500],
            ArtifactId = 3,
            Answers = ["Me", "Myself"],
            Riddle = "Who?",
        };

        byte[] data = block.ToBlock();

        // 斯芬克斯的首字节是 0，与路牌/事件的 1 相反。
        Assert.Equal(0x00, data[0]);

        Assert.True(Mp2SphinxInfo.TryRead(data, out Mp2SphinxInfo read));

        Assert.Equal(500, read.Resources[6]);
        Assert.Equal(3, read.ArtifactId);
        Assert.Equal(["me", "myself"], read.Answers);
        Assert.Equal("Who?", read.Riddle);
    }

    [Fact]
    public void ASphinxKeepsAtMostEightAnswers()
    {
        Mp2SphinxInfo block = new()
        {
            Answers = [.. Enumerable.Range(0, 12).Select(index => "a" + index)],
            Riddle = "R",
        };

        Assert.True(Mp2SphinxInfo.TryRead(block.ToBlock(), out Mp2SphinxInfo read));
        Assert.Equal(Mp2SphinxInfo.AnswerSlots, read.Answers.Count);
    }

    [Fact]
    public void ASphinxBlockWithoutAnswersStillReads()
    {
        Mp2SphinxInfo block = new() { Riddle = "Only me" };

        Assert.True(Mp2SphinxInfo.TryRead(block.ToBlock(), out Mp2SphinxInfo read));
        Assert.Empty(read.Answers);
        Assert.Equal("Only me", read.Riddle);
    }

    [Fact]
    public void ASphinxBlockNeedsItsZeroMagicByte()
    {
        byte[] data = new Mp2SphinxInfo { Riddle = "R" }.ToBlock();
        data[0] = 0x01;

        Assert.False(Mp2SphinxInfo.TryRead(data, out _));
    }

    [Fact]
    public void AShortBlockIsRefusedByAllThree()
    {
        Assert.False(Mp2SignInfo.TryRead([0x01, 0x00], out _));
        Assert.False(Mp2EventInfo.TryRead([0x01, 0x00], out _));
        Assert.False(Mp2SphinxInfo.TryRead([0x00, 0x00], out _));
    }
}
