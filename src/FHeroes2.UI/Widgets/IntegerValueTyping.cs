using System.Globalization;

using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Turns this frame's keystroke into a number in a caller-owned text buffer, a port of
/// <c>fheroes2::processIntegerValueTyping()</c> (<c>src/fheroes2/gui/ui_tool.cpp:766-839</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps a <c>std::string</c> next to the value it edits (<c>typedValueBuf</c> of
/// <c>Dialog::RecruitMonster()</c>, <c>Dialog::SelectCount()</c> and its redistribution loop), and the buffer is the
/// state: digits accumulate there, so typing <c>1</c> then <c>2</c> asks for twelve, and a digit that would push the
/// number out of the allowed range is taken back off the buffer instead of becoming the start of a new number. The
/// buffer is also cleared by the dialog whenever the number is set by a button (<c>resetResult</c>,
/// <c>dialog_recruit.cpp:449-453</c>), which is why it lives with the caller and not here.
/// </para>
/// <para>
/// The four branches, in the order of the reference: <c>KEY_BACKSPACE</c>/<c>KEY_DELETE</c> clear the buffer and
/// answer <c>clamp( 0, min, max )</c>; the minus keys are accepted only for a range that allows negatives and only
/// while the buffer holds nothing but zeros; the digit keys of both the top row and the keypad append and parse; any
/// other key answers nothing and leaves the buffer alone.
/// </para>
/// <para>
/// 偏差：参考从 <c>LocalEvent::Get()</c> 读全局事件，端口接收一个 <see cref="Key"/>，这样对话框模型不必依赖输入状态、
/// 测试也不用造一帧事件。参考的 <c>assert( min &lt;= max )</c> 换成 <see cref="ArgumentOutOfRangeException"/>；
/// <c>std::stoi</c> 对只有减号的缓冲区会抛未捕获的 <c>invalid_argument</c>（参考里走不到，因为减号分支立刻返回），
/// 端口把解析失败一律当成「这一位没接受」。
/// </para>
/// </remarks>
public static class IntegerValueTyping
{
    /// <summary>
    /// Feeds one keystroke to the buffer, <c>processIntegerValueTyping( min, max, valueBuf )</c>.
    /// </summary>
    /// <param name="min">Smallest value the number may take, <c>min</c> of the reference.</param>
    /// <param name="max">Largest value the number may take, <c>max</c> of the reference.</param>
    /// <param name="valueBuf">
    /// The digits typed so far, <c>typedValueBuf</c>; updated in place exactly like the reference.
    /// </param>
    /// <param name="key">The key that went down this frame, <c>LocalEvent::getPressedKeyValue()</c>.</param>
    /// <returns>
    /// The new value when the keystroke changed it, or <see langword="null"/> when the key means nothing for a number
    /// or the digit was refused.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public static int? Process(int min, int max, ref string valueBuf, Key key)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);

        // The reference answers clamp( 0, min, max ) on the two branches that do not parse a number.
        int zeroValue = Math.Clamp(0, min, max);

        if (key is Key.Backspace or Key.Delete)
        {
            valueBuf = string.Empty;

            return zeroValue;
        }

        if (key is Key.Minus or Key.KeyPadMinus)
        {
            // Nothing below zero can be typed into a range that starts at zero.
            if (min >= 0)
            {
                return null;
            }

            // "-" may only replace a buffer that is still zero, so "5" then "-" does not read as "-5".
            if (valueBuf.Any(static ch => ch != '0'))
            {
                return null;
            }

            valueBuf = "-";

            return zeroValue;
        }

        if (DigitOf(key) is not { } digit)
        {
            return null;
        }

        valueBuf += digit;

        // An overflowing number is as impossible as a too large one, and both are rolled back.
        if (!int.TryParse(valueBuf, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            || (min <= 0 && value < min)
            || (max >= 0 && value > max))
        {
            valueBuf = valueBuf[..^1];

            return null;
        }

        // A number that parsed but sits outside the range stays in the buffer without being reported, which is what
        // the reference's last comparison does.
        return value == Math.Clamp(value, min, max) ? value : null;
    }

    private static char? DigitOf(Key key)
    {
        if (key is >= Key.Digit0 and <= Key.Digit9)
        {
            return (char)('0' + ((int)key - (int)Key.Digit0));
        }

        if (key is >= Key.KeyPad0 and <= Key.KeyPad9)
        {
            return (char)('0' + ((int)key - (int)Key.KeyPad0));
        }

        return null;
    }
}
