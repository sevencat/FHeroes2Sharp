using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>ui_tool.cpp</c>'s <c>processIntegerValueTyping()</c>.
/// </summary>
public sealed class IntegerValueTypingTests
{
    [Fact]
    public void DigitsAccumulateAndARejectedOneIsRolledBack()
    {
        string buffer = string.Empty;

        Assert.Equal(1, IntegerValueTyping.Process(0, 20, ref buffer, Key.Digit1));
        Assert.Equal("1", buffer);

        Assert.Equal(12, IntegerValueTyping.Process(0, 20, ref buffer, Key.Digit2));
        Assert.Equal("12", buffer);

        // "123" is above the maximum, so the digit is taken back off the buffer.
        Assert.Null(IntegerValueTyping.Process(0, 20, ref buffer, Key.Digit3));
        Assert.Equal("12", buffer);
    }

    [Fact]
    public void ANumberThatOverflowsIsRolledBackLikeATooLargeOne()
    {
        string buffer = string.Empty;
        long expected = 0;

        // Nine nines fit in an int32, and each of them is reported as the new value.
        for (int i = 0; i < 9; ++i)
        {
            expected = (expected * 10) + 9;

            Assert.Equal((int)expected, IntegerValueTyping.Process(0, int.MaxValue, ref buffer, Key.Digit9));
        }

        // The tenth nine does not fit any more: the reference's stoi throws out_of_range, which counts as a refused
        // digit, so the buffer keeps the nine nines it had.
        Assert.Null(IntegerValueTyping.Process(0, int.MaxValue, ref buffer, Key.Digit9));
        Assert.Equal("999999999", buffer);
    }

    [Fact]
    public void BackspaceAndDeleteClearTheBufferAndAnswerTheClampedZero()
    {
        string buffer = "7";

        // clamp( 0, 5, 10 ) is 5.
        Assert.Equal(5, IntegerValueTyping.Process(5, 10, ref buffer, Key.Backspace));
        Assert.Equal(string.Empty, buffer);

        buffer = "7";

        // clamp( 0, -5, 10 ) is 0.
        Assert.Equal(0, IntegerValueTyping.Process(-5, 10, ref buffer, Key.Delete));
        Assert.Equal(string.Empty, buffer);
    }

    [Fact]
    public void TheMinusKeyIsRefusedWhenTheRangeStartsAtZero()
    {
        string buffer = string.Empty;

        Assert.Null(IntegerValueTyping.Process(0, 10, ref buffer, Key.Minus));
        Assert.Equal(string.Empty, buffer);

        Assert.Null(IntegerValueTyping.Process(0, 10, ref buffer, Key.KeyPadMinus));
        Assert.Equal(string.Empty, buffer);
    }

    [Fact]
    public void TheMinusKeyStartsANegativeNumber()
    {
        string buffer = string.Empty;

        // clamp( 0, -10, 10 ) is 0, and the buffer becomes a lone minus sign.
        Assert.Equal(0, IntegerValueTyping.Process(-10, 10, ref buffer, Key.Minus));
        Assert.Equal("-", buffer);

        Assert.Equal(-5, IntegerValueTyping.Process(-10, 10, ref buffer, Key.Digit5));
        Assert.Equal("-5", buffer);
    }

    [Fact]
    public void TheMinusKeyDoesNotTurnAnExistingNumberNegative()
    {
        string buffer = "5";

        Assert.Null(IntegerValueTyping.Process(-10, 10, ref buffer, Key.Minus));
        Assert.Equal("5", buffer);
    }

    [Fact]
    public void KeypadDigitsAreDigitsToo()
    {
        string buffer = string.Empty;

        Assert.Equal(4, IntegerValueTyping.Process(0, 10, ref buffer, Key.KeyPad4));
        Assert.Equal("4", buffer);
    }

    [Fact]
    public void KeysThatMeanNothingForANumberLeaveTheBufferAlone()
    {
        string buffer = "3";

        Assert.Null(IntegerValueTyping.Process(0, 10, ref buffer, Key.A));
        Assert.Null(IntegerValueTyping.Process(0, 10, ref buffer, Key.Enter));
        Assert.Null(IntegerValueTyping.Process(0, 10, ref buffer, Key.Escape));

        Assert.Equal("3", buffer);
    }

    [Fact]
    public void ANumberBelowTheRangeStaysInTheBufferWithoutBeingReported()
    {
        // The reference only checks the lower bound when min <= 0, so a "0" typed into 1..10 parses, fails the final
        // clamp comparison and answers nothing - but it is not rolled back either, so the next digit builds on it.
        string buffer = string.Empty;

        Assert.Null(IntegerValueTyping.Process(1, 10, ref buffer, Key.Digit0));
        Assert.Equal("0", buffer);

        Assert.Equal(5, IntegerValueTyping.Process(1, 10, ref buffer, Key.Digit5));
        Assert.Equal("05", buffer);
    }

    [Fact]
    public void AnInvertedRangeIsRefused()
    {
        string buffer = string.Empty;

        Assert.Throws<ArgumentOutOfRangeException>(() => IntegerValueTyping.Process(10, 0, ref buffer, Key.Digit1));
    }
}
