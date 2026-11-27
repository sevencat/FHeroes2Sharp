using System.Buffers.Binary;
using System.Text;


namespace FHeroes2.Assets.Maps;

/// <summary>
/// The information block of a sign or a bottle, <c>MapSign::LoadFromMP2()</c>
/// (<c>maps_objects.cpp:286-313</c>).
/// </summary>
/// <remarks>
/// <para>
/// 布局：<c>1</c> 一字节（表明这是路牌或瓶中信）、八个未用字节、然后是 NUL 结尾的文本，直到块尾。
/// </para>
/// <para>
/// 参考的读端在没有文本时给一条默认消息；端口保留空串，由调用方决定要不要那句默认话——写回时空文本会得到一个只有
/// 前缀的块，参考读它时仍然会补默认消息，所以往返是稳定的。
/// </para>
/// </remarks>
public sealed class Mp2SignInfo
{
    /// <summary>Bytes before the text, the <c>skip( 9 )</c> of the reference.</summary>
    public const int TextOffset = 9;

    /// <summary>Smallest block the reference accepts, <c>MP2_SIGN_STRUCTURE_MIN_SIZE</c>.</summary>
    public const int MinimumSize = 10;

    /// <summary>The text of the sign, empty when it has none.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Reads a sign or bottle block.</summary>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="info">The block when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> looks like a sign block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, out Mp2SignInfo info)
    {
        info = new Mp2SignInfo();

        if (data.Length < MinimumSize || data[0] != 0x01)
        {
            return false;
        }

        info = new Mp2SignInfo { Message = DecodeText(data[TextOffset..]) };

        return true;
    }

    /// <summary>Writes the block back.</summary>
    /// <returns>The payload, without its size prefix.</returns>
    public byte[] ToBlock()
    {
        byte[] text = EncodeText(Message);
        byte[] block = new byte[TextOffset + text.Length];

        block[0] = 0x01;
        text.CopyTo(block, TextOffset);

        return block;
    }

    /// <summary>Decodes a NUL terminated string that runs to the end of the block.</summary>
    /// <param name="data">The remaining bytes.</param>
    /// <returns>The text.</returns>
    internal static string DecodeText(ReadOnlySpan<byte> data)
    {
        int terminator = data.IndexOf((byte)0);

        return MapTextCodec.Decode(terminator >= 0 ? data[..terminator] : data);
    }

    /// <summary>Encodes a string with a NUL terminator.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes, terminator included.</returns>
    /// <remarks>
    /// 结尾的 NUL 一定要写：块的读取端是「读到块尾」，而参考对最短长度的检查（<c>MP2_SIGN_STRUCTURE_MIN_SIZE = 10</c>）
    /// 正好等于「九个前缀字节 + 一个结尾 NUL」。不留这个字节，空文本的路牌块就短过下限、被读端拒绝。
    /// </remarks>
    internal static byte[] EncodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // MP2 的文本是单字节编码；非 ASCII 时按地图头的同一套规则（GBK 优先）选编码。
        byte[] bytes = MapTextCodec.Encode(text, Math.Max(1, text.Length * 2) + 1);
        int length = Array.IndexOf(bytes, (byte)0);

        if (length < 0)
        {
            length = bytes.Length;
        }

        byte[] result = new byte[length + 1];
        Array.Copy(bytes, result, length);

        return result;
    }
}

/// <summary>
/// The information block of a ground event, <c>MapEvent::LoadFromMP2()</c>
/// (<c>maps_objects.cpp:44-166</c>).
/// </summary>
/// <remarks>
/// 布局：<c>1</c> 一字节、七个 <c>int32</c>（木/水银/矿/硫/水晶/宝石/金，可为负）、一个 <c>uint16</c> 神器号（文件里从
/// 0 起）、一字节「电脑也算」、一字节「只发生一次」、十个未用字节、六个颜色标志（蓝绿红黄橙紫各一字节）、最后是
/// NUL 结尾的文本。
/// </remarks>
public sealed class Mp2EventInfo
{
    /// <summary>Smallest block the reference accepts, <c>MP2_EVENT_STRUCTURE_MIN_SIZE</c>.</summary>
    public const int MinimumSize = 50;

    /// <summary>Offset of the seven resource amounts.</summary>
    private const int ResourcesOffset = 1;

    /// <summary>Offset of the artifact id, after the seven int32 values.</summary>
    private const int ArtifactOffset = ResourcesOffset + (7 * 4);

    /// <summary>Offset of the "the computer gets it too" flag.</summary>
    private const int ComputerAllowedOffset = ArtifactOffset + 2;

    /// <summary>Offset of the "happens only once" flag.</summary>
    private const int SingleTimeOffset = ComputerAllowedOffset + 1;

    /// <summary>Bytes the reference skips before the colour flags, the <c>skip( 10 )</c>.</summary>
    private const int UnusedLength = 10;

    /// <summary>Offset of the six colour flags, one byte each in colour order.</summary>
    private const int ColorsOffset = SingleTimeOffset + 1 + UnusedLength;

    /// <summary>Offset of the event text.</summary>
    private const int TextOffset = ColorsOffset + 6;

    /// <summary>The resources the event hands out, as seven numbers in the file's order; amounts may be negative.</summary>
    /// <remarks>
    /// 顺序是木、水银、矿、硫、水晶、宝石、金——参考的 <c>Funds</c> 字段顺序。端口在 Assets 层不依赖 Game 层的
    /// <c>Funds</c>（那是相反的依赖方向），所以这里保留七个原始数字。
    /// </remarks>
    public IReadOnlyList<int> Resources { get; init; } = new int[7];

    /// <summary>The artifact the event hands out, raw MP2 id (the engine id is <c>+ 1</c>).</summary>
    public int ArtifactId { get; init; }

    /// <summary>Whether the computer players get the event as well, <c>isComputerPlayerAllowed</c>.</summary>
    public bool IsComputerAllowed { get; init; }

    /// <summary>Whether the event happens only once, <c>isSingleTimeEvent</c>.</summary>
    public bool IsSingleTime { get; init; }

    /// <summary>The colours that receive the event.</summary>
    public PlayerColor Colors { get; init; }

    /// <summary>The text shown when the event fires.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Reads an event block.</summary>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="info">The block when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> looks like an event block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, out Mp2EventInfo info)
    {
        info = new Mp2EventInfo();

        if (data.Length < MinimumSize || data[0] != 0x01)
        {
            return false;
        }

        int[] resources =
        [
            ReadInt32(data, ResourcesOffset),
            ReadInt32(data, ResourcesOffset + 4),
            ReadInt32(data, ResourcesOffset + 8),
            ReadInt32(data, ResourcesOffset + 12),
            ReadInt32(data, ResourcesOffset + 16),
            ReadInt32(data, ResourcesOffset + 20),
            ReadInt32(data, ResourcesOffset + 24),
        ];

        PlayerColor colors = PlayerColor.None;
        IReadOnlyList<PlayerColor> ordered = PlayerColors.Ordered;

        for (int index = 0; index < ordered.Count && ColorsOffset + index < data.Length; ++index)
        {
            if (data[ColorsOffset + index] != 0)
            {
                colors |= ordered[index];
            }
        }

        info = new Mp2EventInfo
        {
            Resources = resources,
            ArtifactId = BinaryPrimitives.ReadUInt16LittleEndian(data[ArtifactOffset..]),
            IsComputerAllowed = data[ComputerAllowedOffset] != 0,
            IsSingleTime = data[SingleTimeOffset] != 0,
            Colors = colors,
            Message = Mp2SignInfo.DecodeText(data[TextOffset..]),
        };

        return true;
    }

    /// <summary>Writes the block back.</summary>
    /// <returns>The payload, without its size prefix.</returns>
    public byte[] ToBlock()
    {
        byte[] text = Mp2SignInfo.EncodeText(Message);
        byte[] block = new byte[TextOffset + text.Length];

        block[0] = 0x01;

        for (int index = 0; index < 7; ++index)
        {
            WriteInt32(block, ResourcesOffset + (index * 4), index < Resources.Count ? Resources[index] : 0);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(ArtifactOffset), (ushort)ArtifactId);

        block[ComputerAllowedOffset] = IsComputerAllowed ? (byte)1 : (byte)0;
        block[SingleTimeOffset] = IsSingleTime ? (byte)1 : (byte)0;

        IReadOnlyList<PlayerColor> ordered = PlayerColors.Ordered;

        for (int index = 0; index < ordered.Count; ++index)
        {
            block[ColorsOffset + index] = (Colors & ordered[index]) != 0 ? (byte)1 : (byte)0;
        }

        text.CopyTo(block, TextOffset);

        return block;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);

    private static void WriteInt32(Span<byte> data, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(data[offset..], value);
}

/// <summary>
/// The information block of a sphinx, <c>MapSphinx::LoadFromMP2()</c> (<c>maps_objects.cpp:168-270</c>).
/// </summary>
/// <remarks>
/// 布局：<c>0</c> 一字节、七个 <c>uint32</c> 资源、一个 <c>uint16</c> 神器号（文件里从 0 起）、一字节答案个数、
/// 八段各 13 字节的定长答案、最后是 NUL 结尾的谜题。答案按参考的规则只取前「答案个数」段、跳过空段，并全部小写。
/// </remarks>
public sealed class Mp2SphinxInfo
{
    /// <summary>Answers the block can hold, the eight 13 byte fields.</summary>
    public const int AnswerSlots = 8;

    /// <summary>Size of one answer field.</summary>
    public const int AnswerFieldSize = 13;

    /// <summary>Offset of the seven resource amounts.</summary>
    private const int ResourcesOffset = 1;

    /// <summary>Offset of the artifact id.</summary>
    private const int ArtifactOffset = ResourcesOffset + (7 * 4);

    /// <summary>Offset of the answer count.</summary>
    private const int AnswerCountOffset = ArtifactOffset + 2;

    /// <summary>Offset of the first answer field.</summary>
    private const int AnswersOffset = AnswerCountOffset + 1;

    /// <summary>Offset of the riddle.</summary>
    private const int RiddleOffset = AnswersOffset + (AnswerSlots * AnswerFieldSize);

    /// <summary>The resources a correct answer hands out, as seven numbers in the file's order.</summary>
    /// <remarks>顺序同 <see cref="Mp2EventInfo.Resources"/>：木、水银、矿、硫、水晶、宝石、金。</remarks>
    public IReadOnlyList<int> Resources { get; init; } = new int[7];

    /// <summary>The artifact a correct answer hands out, raw MP2 id (the engine id is <c>+ 1</c>).</summary>
    public int ArtifactId { get; init; }

    /// <summary>The accepted answers, lower case.</summary>
    public IReadOnlyList<string> Answers { get; init; } = [];

    /// <summary>The riddle.</summary>
    public string Riddle { get; init; } = string.Empty;

    /// <summary>Reads a sphinx block.</summary>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="info">The block when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> looks like a sphinx block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, out Mp2SphinxInfo info)
    {
        info = new Mp2SphinxInfo();

        if (data.Length < RiddleOffset || data[0] != 0x00)
        {
            return false;
        }

        int[] resources =
        [
            (int)ReadUInt32(data, ResourcesOffset),
            (int)ReadUInt32(data, ResourcesOffset + 4),
            (int)ReadUInt32(data, ResourcesOffset + 8),
            (int)ReadUInt32(data, ResourcesOffset + 12),
            (int)ReadUInt32(data, ResourcesOffset + 16),
            (int)ReadUInt32(data, ResourcesOffset + 20),
            (int)ReadUInt32(data, ResourcesOffset + 24),
        ];

        int remaining = data[AnswerCountOffset];
        List<string> answers = [];

        for (int slot = 0; slot < AnswerSlots && remaining > 0; ++slot)
        {
            --remaining;

            string answer = MapTextCodec.Decode(
                data.Slice(AnswersOffset + (slot * AnswerFieldSize), AnswerFieldSize));

            if (answer.Length > 0)
            {
                answers.Add(answer.ToLowerInvariant());
            }
        }

        info = new Mp2SphinxInfo
        {
            Resources = resources,
            ArtifactId = BinaryPrimitives.ReadUInt16LittleEndian(data[ArtifactOffset..]),
            Answers = answers,
            Riddle = MapTextCodec.Decode(data[RiddleOffset..]),
        };

        return true;
    }

    /// <summary>Writes the block back.</summary>
    /// <returns>The payload, without its size prefix.</returns>
    public byte[] ToBlock()
    {
        byte[] riddle = Mp2SignInfo.EncodeText(Riddle);
        byte[] block = new byte[RiddleOffset + riddle.Length];

        // 首个字节是 0：与路牌/事件的 1 相反，参考用它区分「这是斯芬克斯」。
        block[0] = 0x00;

        for (int index = 0; index < 7; ++index)
        {
            WriteUInt32(block, ResourcesOffset + (index * 4),
                (uint)(index < Resources.Count ? Resources[index] : 0));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(ArtifactOffset), (ushort)ArtifactId);

        int count = Math.Min(Answers.Count, AnswerSlots);

        block[AnswerCountOffset] = (byte)count;

        for (int slot = 0; slot < count; ++slot)
        {
            // 答案字段是定长的 13 字节，编码器负责截断与补零。
            MapTextCodec.Encode(Answers[slot], AnswerFieldSize)
                .CopyTo(block, AnswersOffset + (slot * AnswerFieldSize));
        }

        riddle.CopyTo(block, RiddleOffset);

        return block;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    private static void WriteUInt32(Span<byte> data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data[offset..], value);
}
