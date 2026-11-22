using System.Buffers.Binary;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Maps;

/// <summary>
/// A sphinx with a riddle, the reference's <c>MapSphinx</c> (<c>maps_objects.h:99</c>), read from its
/// information block by <c>MapSphinx::LoadFromMP2()</c> (<c>maps_objects.cpp:166</c>).
/// </summary>
/// <remarks>
/// <para>
/// The block is: marker <c>0x00</c>, seven 32 bit resource amounts, a 16 bit artifact id, the number of
/// correct answers, eight answers of 13 bytes each and the question, which runs to the end of the block.
/// The answers are stored in lower case, as the reference does when it reads them
/// (<c>maps_objects.cpp:257</c>).
/// </para>
/// <para>
/// The original game compares only the first four characters of an answer, because the editors cut the
/// answers to 13 bytes and players typed them on a keyboard that could not keep up. The reference keeps
/// that behaviour behind <c>isTruncatedAnswer</c>, which the MP2 loader leaves at its default
/// <see langword="true"/> value; only the newer map format turns it off.
/// </para>
/// <para>
/// <b>What is missing</b>: the reference calls <c>validate()</c> after reading and drops the object when
/// the riddle or the reward is missing (<c>world_loadmap.cpp:646</c>). The port stores the same
/// <see cref="IsValid"/> flag, but the decision belongs to the caller that adds the object.
/// </para>
/// </remarks>
public sealed partial class MapSphinx : MapObject
{
    /// <summary>Smallest block that can be a sphinx, <c>MP2::MP2_RIDDLE_STRUCTURE_MIN_SIZE</c> (<c>mp2.h:39</c>).</summary>
    public const int MinimumStructureSize = 137;

    /// <summary>The first byte of a sphinx block, <c>data[0] == 0</c> (<c>maps_objects.cpp:231</c>).</summary>
    private const byte SphinxMarker = 0x00;

    /// <summary>Offset of the wood amount; the seven amounts follow each other.</summary>
    private const int ResourceOffset = 1;

    /// <summary>Offset of the artifact id.</summary>
    private const int ArtifactOffset = 29;

    /// <summary>Offset of the number of answers that are correct.</summary>
    private const int AnswerCountOffset = 31;

    /// <summary>Offset of the first answer.</summary>
    private const int AnswerOffset = 32;

    /// <summary>Every answer takes 13 bytes, terminator and padding included.</summary>
    private const int AnswerSize = 13;

    /// <summary>The block always has room for eight answers.</summary>
    private const int AnswerSlots = 8;

    /// <summary>Offset of the riddle, after the eight answer slots.</summary>
    private const int RiddleOffset = AnswerOffset + (AnswerSize * AnswerSlots);

    /// <summary>How many characters of an answer the original game compares (<c>maps_objects.cpp:276</c>).</summary>
    private const int TruncatedAnswerLength = 4;

    private MapSphinx()
    {
    }

    /// <summary>The resources the sphinx gives to a hero who answers correctly.</summary>
    /// <remarks>不是 <c>init</c>：答案对了之后由 <see cref="Reset"/> 清空，第二次来就没什么可给了。</remarks>
    public Funds Resources { get; private set; }

    /// <summary>The artifact the sphinx gives; <see cref="ArtifactType.Unknown"/> for none.</summary>
    public ArtifactType Artifact { get; private set; }

    /// <summary>The correct answers, in lower case.</summary>
    public IReadOnlyList<string> Answers { get; private set; } = [];

    /// <summary>The riddle the sphinx asks.</summary>
    public string Riddle { get; private set; } = string.Empty;

    /// <summary>
    /// Restores the stolen French characters of the answers and the riddle,
    /// <c>MapSphinx::fixFrenchCharacters()</c> (<c>world.cpp:1311-1320</c>).
    /// </summary>
    internal void FixFrenchCharacters()
    {
        string[] answers = new string[Answers.Count];

        for (int i = 0; i < answers.Length; ++i)
        {
            answers[i] = MapTextCodec.FixFrenchCharacters(Answers[i]);
        }

        Answers = answers;
        Riddle = MapTextCodec.FixFrenchCharacters(Riddle);
    }

    /// <summary>
    /// Whether the sphinx is worth asking at all, <c>MapSphinx::valid</c> (<c>maps_objects.h:117</c>): a
    /// sphinx without a reward or without a riddle is dropped by the caller.
    /// </summary>
    /// <remarks>
    /// 答对一次就由 <see cref="Reset"/> 清成 <see langword="false"/>：`valid` 在这里的意思是「还有奖可拿」，
    /// 不是「这块瓦片还在」。
    /// </remarks>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Whether only the first four characters of an answer count, <c>isTruncatedAnswer</c>. The MP2 loader
    /// always keeps this <see langword="true"/>; the newer map format sets it to <see langword="false"/>.
    /// </summary>
    public bool IsTruncatedAnswer { get; private init; } = true;

    /// <summary>
    /// Whether an answer the player typed is one of the correct ones, <c>MapSphinx::isCorrectAnswer()</c>
    /// (<c>maps_objects.cpp:273</c>).
    /// </summary>
    /// <param name="answer">The answer the player typed.</param>
    /// <returns><see langword="true"/> when the answer is correct.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public bool IsCorrectAnswer(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        string probe = answer.ToLowerInvariant();

        if (IsTruncatedAnswer)
        {
            probe = Truncate(probe);

            foreach (string candidate in Answers)
            {
                if (Truncate(candidate) == probe)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (string candidate in Answers)
        {
            if (candidate == probe)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Empties the sphinx once it has paid out, <c>MapSphinx::reset()</c> (<c>maps_objects.h:120</c>).
    /// </summary>
    /// <remarks>
    /// 谜语与答案都留在瓦片上（参考也一样，没人会去擦它们），清的只有<b>奖品</b>和 `valid`：再来的人听到的
    /// 还是同一段谜语，只是答对了也没什么可拿。
    /// </remarks>
    public void Reset()
    {
        Artifact = ArtifactType.Unknown;
        Resources = default;
        IsValid = false;
    }

    /// <summary>
    /// Reads the block of a sphinx, <c>MapSphinx::LoadFromMP2()</c> (<c>maps_objects.cpp:166</c>).
    /// </summary>
    /// <remarks>
    /// The reference asserts the size and the marker and logs a warning when the riddle is empty; the port
    /// reports the same decision through the return value and keeps the sphinx, so that a caller can still
    /// record the gap.
    /// </remarks>
    /// <param name="data">Payload of the information block, without its size prefix.</param>
    /// <param name="tileIndex">Index of the tile the block belongs to.</param>
    /// <param name="sphinx">The sphinx when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="data"/> is a sphinx block.</returns>
    public static bool TryRead(ReadOnlySpan<byte> data, int tileIndex, out MapSphinx? sphinx)
    {
        sphinx = null;

        if (data.Length < MinimumStructureSize || data[0] != SphinxMarker)
        {
            return false;
        }

        // The file stores the amounts as wood, mercury, ore, sulfur, crystal, gems, gold; the constructor of
        // Funds takes ore first, so the names are spelled out.
        Funds resources = new(
            ore: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 8)..]),
            wood: BinaryPrimitives.ReadInt32LittleEndian(data[ResourceOffset..]),
            mercury: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 4)..]),
            sulfur: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 12)..]),
            crystal: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 16)..]),
            gems: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 20)..]),
            gold: BinaryPrimitives.ReadInt32LittleEndian(data[(ResourceOffset + 24)..]));

        // The artifact counts from 0 in the file and from 1 in the engine (maps_objects.cpp:246).
        ArtifactType artifact = (ArtifactType)(BinaryPrimitives.ReadUInt16LittleEndian(data[ArtifactOffset..]) + 1);

        int answerCount = data[AnswerCountOffset];
        List<string> answers = [];

        // Only the first answerCount slots are answers; the rest may hold whatever the editor left there.
        for (int slot = 0; slot < AnswerSlots && answerCount > 0; ++slot)
        {
            --answerCount;

            string answer = MapTextCodec.Decode(data.Slice(AnswerOffset + (slot * AnswerSize), AnswerSize));

            if (answer.Length > 0)
            {
                answers.Add(answer.ToLowerInvariant());
            }
        }

        string riddle = MapTextCodec.Decode(data[RiddleOffset..]);

        MapSphinx result = new()
        {
            Resources = resources,
            Artifact = artifact,
            Answers = answers,
            Riddle = riddle,
            IsValid = riddle.Length > 0 && answers.Count > 0 && (artifact != ArtifactType.Unknown || resources.GetValidItemsCount() > 0),
        };

        result.PlaceOn(tileIndex);

        sphinx = result;

        return true;
    }

    /// <summary>The first <see cref="TruncatedAnswerLength"/> characters of an answer.</summary>
    /// <param name="answer">A lower case answer.</param>
    private static string Truncate(string answer) =>
        answer.Length <= TruncatedAnswerLength ? answer : answer[..TruncatedAnswerLength];
}
