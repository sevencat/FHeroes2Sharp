using System.Globalization;

using FHeroes2.Core.Random;

namespace FHeroes2.Game.Resources;

/// <summary>
/// The resource type bit flags of the reference's <c>Resource</c> namespace.
/// </summary>
/// <remarks>参考：<c>src/fheroes2/resource/resource.h:48</c> namespace Resource 的匿名枚举。</remarks>
public static class Resource
{
    /// <summary>No resource.</summary>
    public const int Unknown = 0x00;

    /// <summary>Wood.</summary>
    public const int Wood = 0x01;

    /// <summary>Mercury.</summary>
    public const int Mercury = 0x02;

    /// <summary>Ore.</summary>
    public const int Ore = 0x04;

    /// <summary>Sulfur.</summary>
    public const int Sulfur = 0x08;

    /// <summary>Crystal.</summary>
    public const int Crystal = 0x10;

    /// <summary>Gems.</summary>
    public const int Gems = 0x20;

    /// <summary>Gold.</summary>
    public const int Gold = 0x40;

    /// <summary>All resources.</summary>
    public const int All = Wood | Mercury | Ore | Sulfur | Crystal | Gems | Gold;

    /// <summary>
    /// One resource, drawn uniformly, <c>Resource::Rand()</c> (<c>resource.cpp:94</c>).
    /// </summary>
    /// <param name="includeGold">
    /// <see langword="true"/> to let gold be drawn as well; the reference passes <see langword="false"/> where the
    /// caller wants a resource pile (the windmill), which never holds gold.
    /// </param>
    /// <returns>The flag of the drawn resource.</returns>
    /// <remarks>
    /// The draw is over <b>positions</b>, not over the flag values: the seven resources are not a dense sequence of
    /// bits, so a random value of <see cref="All"/> would be wrong. The reference switches over <c>Rand::Get( 1, 7 )</c>
    /// for exactly this reason.
    /// </remarks>
    public static int Rand(bool includeGold = true)
    {
        // Fully qualified: the method's own name shadows the generator class inside the class body.
        return FHeroes2.Core.Random.Rand.Get(1, includeGold ? 7 : 6) switch
        {
            1 => Wood,
            2 => Mercury,
            3 => Ore,
            4 => Sulfur,
            5 => Crystal,
            6 => Gems,
            7 => Gold,
            _ => Unknown,
        };
    }

    /// <summary>
    /// <c>Resource::CalculateEventResourceUpdate</c>: the part of an event's resources that fits into the current funds.
    /// </summary>
    /// <param name="currentFunds">Resources the player already has.</param>
    /// <param name="eventFunds">Resources the event grants.</param>
    /// <returns>The clamped delta.</returns>
    /// <remarks>参考：<c>src/fheroes2/resource/resource.cpp:469</c> Resource::CalculateEventResourceUpdate。</remarks>
    public static Funds CalculateEventResourceUpdate(in Funds currentFunds, in Funds eventFunds)
    {
        Funds funds = currentFunds + eventFunds;
        funds.Trim();

        return funds - currentFunds;
    }
}

/// <summary>
/// The price of a building or a unit, the reference's <c>Cost</c>.
/// </summary>
/// <remarks>参考：<c>src/fheroes2/resource/resource.h:37</c> struct Cost。</remarks>
public struct Cost
{
    /// <summary>Gold part of the price.</summary>
    public ushort Gold;

    /// <summary>Wood part of the price.</summary>
    public byte Wood;

    /// <summary>Mercury part of the price.</summary>
    public byte Mercury;

    /// <summary>Ore part of the price.</summary>
    public byte Ore;

    /// <summary>Sulfur part of the price.</summary>
    public byte Sulfur;

    /// <summary>Crystal part of the price.</summary>
    public byte Crystal;

    /// <summary>Gems part of the price.</summary>
    public byte Gems;
}

/// <summary>
/// A bag of the seven kingdom resources, the reference's <c>Funds</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/resource/resource.h:64</c> struct Funds（字段 128–134、运算符 94–111、查询 113–126）、
/// 实现 <c>src/fheroes2/resource/resource.cpp:58</c>（<c>Funds( type, count )</c>）、<c>:118</c>（GetPtr）、
/// <c>:141</c>（Get）、<c>:164</c>（operator=）、<c>:177/192/205/220</c>（+ += - -=）、<c>:233</c>（max）、
/// <c>:248</c>（getLowestQuotient）、<c>:270/285/302</c>（* / *=）、<c>:315/321</c>（== &gt;=）、
/// <c>:327</c>（String）、<c>:476/498</c>（GetValidItems / GetValidItemsCount）、<c>:520</c>（Trim）、
/// <c>:538</c>（getFirstValidResource）、<c>:573</c>（Reset）。
/// </para>
/// <para>
/// 差异：<c>operator=( const Cost &amp; )</c> 在 C# 里不能重载赋值运算符，改名 <see cref="SetFrom"/>；
/// <c>GetPtr</c> 在参考里用 <c>nullptr</c> 表示非法资源，C# 的 <c>ref</c> 不能为 null，改为抛
/// <see cref="ArgumentOutOfRangeException"/>；重载 <c>==</c> 时 C# 强制同时重写 <c>Equals</c>/<c>GetHashCode</c>
/// （CS0660/CS0661），这是语言要求，不是新增行为。
/// </para>
/// <para>
/// 未搬：<c>Resource::String</c>（要 <c>Translation</c>）、<c>Resource::Rand</c>（要 <c>Rand::Get</c>）、
/// <c>GetIndexSprite</c>/<c>FromIndexSprite</c>/<c>getIconIcnIndex</c>/<c>getResourceTypeFromIconIndex</c>
/// （要 <c>ICN</c> 序号映射表）、<c>Resource::BoxSprite</c>（要 <c>Display</c>）、
/// <c>operator&lt;&lt;</c>/<c>operator&gt;&gt;</c>（要 <c>IStreamBase</c>）。
/// </para>
/// </remarks>
public struct Funds
{
    /// <summary>Creates an empty bag; every field is zero, like <c>Funds() = default</c>.</summary>
    /// <remarks>参考：<c>resource.h:66</c> Funds() = default。</remarks>
    public Funds()
    {
    }

    /// <summary>Creates a bag with the given amounts.</summary>
    /// <param name="ore">Ore.</param>
    /// <param name="wood">Wood.</param>
    /// <param name="mercury">Mercury.</param>
    /// <param name="sulfur">Sulfur.</param>
    /// <param name="crystal">Crystal.</param>
    /// <param name="gems">Gems.</param>
    /// <param name="gold">Gold.</param>
    /// <remarks>参考：<c>resource.h:68</c> Funds( _ore, _wood, _mercury, _sulfur, _crystal, _gems, _gold )——注意 ore 在第一位。</remarks>
    public Funds(int ore, int wood, int mercury, int sulfur, int crystal, int gems, int gold)
    {
        Ore = ore;
        Wood = wood;
        Mercury = mercury;
        Sulfur = sulfur;
        Crystal = crystal;
        Gems = gems;
        Gold = gold;
    }

    /// <summary>Creates a bag holding <paramref name="count"/> of one resource.</summary>
    /// <param name="type">One of the <see cref="Resource"/> flags.</param>
    /// <param name="count">Amount; 0 leaves the bag empty.</param>
    /// <remarks>参考：<c>resource.cpp:58</c> Funds( const int type, const uint32_t count )。</remarks>
    public Funds(int type, uint count)
    {
        // A zero count adds nothing, so every field keeps the default value.
        if (count == 0)
        {
            return;
        }

        switch (type)
        {
            case Resource.Ore:
                Ore = (int)count;
                break;
            case Resource.Wood:
                Wood = (int)count;
                break;
            case Resource.Mercury:
                Mercury = (int)count;
                break;
            case Resource.Sulfur:
                Sulfur = (int)count;
                break;
            case Resource.Gems:
                Gems = (int)count;
                break;
            case Resource.Crystal:
                Crystal = (int)count;
                break;
            case Resource.Gold:
                Gold = (int)count;
                break;
            default:
                // Unknown resource: ignore it, like the reference's DEBUG_LOG path.
                break;
        }
    }

    /// <summary>Creates a bag from a price.</summary>
    /// <param name="cost">The price.</param>
    /// <remarks>参考：<c>resource.h:82</c> explicit Funds( const Cost &amp; )。</remarks>
    public Funds(in Cost cost)
    {
        Wood = cost.Wood;
        Mercury = cost.Mercury;
        Ore = cost.Ore;
        Sulfur = cost.Sulfur;
        Crystal = cost.Crystal;
        Gems = cost.Gems;
        Gold = cost.Gold;
    }

    /// <summary>Wood.</summary>
    public int Wood;

    /// <summary>Mercury.</summary>
    public int Mercury;

    /// <summary>Ore.</summary>
    public int Ore;

    /// <summary>Sulfur.</summary>
    public int Sulfur;

    /// <summary>Crystal.</summary>
    public int Crystal;

    /// <summary>Gems.</summary>
    public int Gems;

    /// <summary>Gold.</summary>
    public int Gold;

    /// <summary>Sum of two bags.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>A new bag.</returns>
    /// <remarks>参考：<c>resource.cpp:177</c> Funds::operator+。</remarks>
    public static Funds operator +(in Funds left, in Funds right) => new(left.Ore + right.Ore, left.Wood + right.Wood,
        left.Mercury + right.Mercury, left.Sulfur + right.Sulfur, left.Crystal + right.Crystal, left.Gems + right.Gems,
        left.Gold + right.Gold);

    /// <summary>Difference of two bags.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>A new bag; fields may go negative.</returns>
    /// <remarks>参考：<c>resource.cpp:205</c> Funds::operator-。</remarks>
    public static Funds operator -(in Funds left, in Funds right) => new(left.Ore - right.Ore, left.Wood - right.Wood,
        left.Mercury - right.Mercury, left.Sulfur - right.Sulfur, left.Crystal - right.Crystal, left.Gems - right.Gems,
        left.Gold - right.Gold);

    /// <summary>Multiplies every field.</summary>
    /// <param name="funds">The bag.</param>
    /// <param name="mul">Multiplier.</param>
    /// <returns>A new bag.</returns>
    /// <remarks>参考：<c>resource.cpp:270</c> Funds::operator*。</remarks>
    public static Funds operator *(in Funds funds, uint mul) => new(funds.Ore * (int)mul, funds.Wood * (int)mul,
        funds.Mercury * (int)mul, funds.Sulfur * (int)mul, funds.Crystal * (int)mul, funds.Gems * (int)mul,
        funds.Gold * (int)mul);

    /// <summary>Divides every field.</summary>
    /// <param name="funds">The bag.</param>
    /// <param name="div">Divisor, must be positive.</param>
    /// <returns>A new bag.</returns>
    /// <remarks>参考：<c>resource.cpp:285</c> Funds::operator/（参考用 <c>assert( div &gt; 0 )</c>）。</remarks>
    public static Funds operator /(in Funds funds, int div)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(div);

        return new(funds.Ore / div, funds.Wood / div, funds.Mercury / div, funds.Sulfur / div, funds.Crystal / div,
            funds.Gems / div, funds.Gold / div);
    }

    /// <summary>Tests two bags for equality.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when every field matches.</returns>
    /// <remarks>参考：<c>resource.cpp:315</c> Funds::operator==。</remarks>
    public static bool operator ==(in Funds left, in Funds right) => left.Wood == right.Wood && left.Mercury == right.Mercury
        && left.Ore == right.Ore && left.Sulfur == right.Sulfur && left.Crystal == right.Crystal && left.Gems == right.Gems
        && left.Gold == right.Gold;

    /// <summary>Tests two bags for inequality.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when any field differs.</returns>
    public static bool operator !=(in Funds left, in Funds right) => !(left == right);

    /// <summary>Tests whether the left bag holds at least as much of every resource.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when every field of the left bag is at least the right one.</returns>
    /// <remarks>参考：<c>resource.cpp:321</c> Funds::operator&gt;=。</remarks>
    public static bool operator >=(in Funds left, in Funds right) => left.Wood >= right.Wood && left.Mercury >= right.Mercury
        && left.Ore >= right.Ore && left.Sulfur >= right.Sulfur && left.Crystal >= right.Crystal && left.Gems >= right.Gems
        && left.Gold >= right.Gold;

    /// <summary>Tests whether the left bag lacks some resource of the right one.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The negation of <c>&gt;=</c>.</returns>
    /// <remarks>参考：<c>resource.h:106</c> Funds::operator&lt;。</remarks>
    public static bool operator <(in Funds left, in Funds right) => !(left >= right);

    /// <summary>Tests whether the left bag exceeds the right one.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The negation of <c>&lt;=</c>.</returns>
    public static bool operator >(in Funds left, in Funds right) => right < left;

    /// <summary>Tests whether the left bag is within the right one.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The negation of <c>&gt;</c>.</returns>
    public static bool operator <=(in Funds left, in Funds right) => !(left > right);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Funds other && this == other;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Wood, Mercury, Ore, Sulfur, Crystal, Gems, Gold);

    /// <summary>Adds another bag in place.</summary>
    /// <param name="other">The bag to add.</param>
    /// <remarks>参考：<c>resource.cpp:192</c> Funds::operator+=。</remarks>
    public void Add(in Funds other)
    {
        Wood += other.Wood;
        Mercury += other.Mercury;
        Ore += other.Ore;
        Sulfur += other.Sulfur;
        Crystal += other.Crystal;
        Gems += other.Gems;
        Gold += other.Gold;
    }

    /// <summary>Subtracts another bag in place.</summary>
    /// <param name="other">The bag to subtract.</param>
    /// <remarks>参考：<c>resource.cpp:220</c> Funds::operator-=。</remarks>
    public void Subtract(in Funds other)
    {
        Wood -= other.Wood;
        Mercury -= other.Mercury;
        Ore -= other.Ore;
        Sulfur -= other.Sulfur;
        Crystal -= other.Crystal;
        Gems -= other.Gems;
        Gold -= other.Gold;
    }

    /// <summary>Multiplies every field in place.</summary>
    /// <param name="mul">Multiplier.</param>
    /// <remarks>参考：<c>resource.cpp:302</c> Funds::operator*=。</remarks>
    public void Multiply(uint mul)
    {
        Wood *= (int)mul;
        Mercury *= (int)mul;
        Ore *= (int)mul;
        Sulfur *= (int)mul;
        Crystal *= (int)mul;
        Gems *= (int)mul;
        Gold *= (int)mul;
    }

    /// <summary>Copies a price into this bag.</summary>
    /// <param name="cost">The price.</param>
    /// <remarks>参考：<c>resource.cpp:164</c> Funds::operator=( const Cost &amp; )。C# 不能重载赋值运算符。</remarks>
    public void SetFrom(in Cost cost)
    {
        Wood = cost.Wood;
        Mercury = cost.Mercury;
        Ore = cost.Ore;
        Sulfur = cost.Sulfur;
        Crystal = cost.Crystal;
        Gems = cost.Gems;
        Gold = cost.Gold;
    }

    /// <summary>Field-wise maximum of two bags.</summary>
    /// <param name="other">The other bag.</param>
    /// <returns>A new bag with the larger of each field.</returns>
    /// <remarks>参考：<c>resource.cpp:233</c> Funds::max。</remarks>
    public Funds Max(in Funds other) => new(Math.Max(Ore, other.Ore), Math.Max(Wood, other.Wood),
        Math.Max(Mercury, other.Mercury), Math.Max(Sulfur, other.Sulfur), Math.Max(Crystal, other.Crystal),
        Math.Max(Gems, other.Gems), Math.Max(Gold, other.Gold));

    /// <summary>Reads one resource by its flag.</summary>
    /// <param name="type">One of the <see cref="Resource"/> flags.</param>
    /// <returns>The amount, or 0 for an unknown flag.</returns>
    /// <remarks>参考：<c>resource.cpp:141</c> Funds::Get。</remarks>
    public int Get(int type) => type switch
    {
        Resource.Ore => Ore,
        Resource.Wood => Wood,
        Resource.Mercury => Mercury,
        Resource.Sulfur => Sulfur,
        Resource.Gems => Gems,
        Resource.Crystal => Crystal,
        Resource.Gold => Gold,
        _ => 0,
    };

    /// <summary>Gets a reference to one resource field of <paramref name="funds"/>.</summary>
    /// <param name="funds">The bag; taken by reference so the returned reference stays usable.</param>
    /// <param name="type">One of the <see cref="Resource"/> flags.</param>
    /// <returns>A reference to the field, so the caller can read or write it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a single resource flag.</exception>
    /// <remarks>
    /// 参考：<c>resource.cpp:118</c> Funds::GetPtr。两处语言差异：参考用 <c>nullptr</c> 表示非法资源，C# 的
    /// <c>ref</c> 不能为 null，改为抛 <see cref="ArgumentOutOfRangeException"/>；C# 禁止结构的实例方法返回自身
    /// 字段的引用（CS8170，因为结构可能被复制后悬空），所以 <c>this</c> 变成显式的 <c>ref</c> 参数。
    /// </remarks>
    public static ref int GetPtr(ref Funds funds, int type)
    {
        switch (type)
        {
            case Resource.Ore:
                return ref funds.Ore;
            case Resource.Wood:
                return ref funds.Wood;
            case Resource.Mercury:
                return ref funds.Mercury;
            case Resource.Sulfur:
                return ref funds.Sulfur;
            case Resource.Gems:
                return ref funds.Gems;
            case Resource.Crystal:
                return ref funds.Crystal;
            case Resource.Gold:
                return ref funds.Gold;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Not a single resource flag.");
        }
    }

    /// <summary>How many times <paramref name="divisor"/> fits into this bag, the smallest per-resource quotient.</summary>
    /// <param name="divisor">The bag to divide by.</param>
    /// <returns>The smallest quotient; a zero divisor field does not constrain the result.</returns>
    /// <remarks>参考：<c>resource.cpp:248</c> Funds::getLowestQuotient。</remarks>
    public int GetLowestQuotient(in Funds divisor)
    {
        int result = divisor.Gold != 0 ? Gold / divisor.Gold : Gold;

        void Divide(int left, int right)
        {
            if (right > 0)
            {
                int value = left / right;
                if (value < result)
                {
                    result = value;
                }
            }
        }

        Divide(Wood, divisor.Wood);
        Divide(Ore, divisor.Ore);
        Divide(Crystal, divisor.Crystal);
        Divide(Gems, divisor.Gems);
        Divide(Mercury, divisor.Mercury);
        Divide(Sulfur, divisor.Sulfur);

        return result;
    }

    /// <summary>The resource flags whose amount is non-zero.</summary>
    /// <returns>An OR of <see cref="Resource"/> flags.</returns>
    /// <remarks>参考：<c>resource.cpp:476</c> Funds::GetValidItems。</remarks>
    public int GetValidItems()
    {
        int result = 0;

        if (Wood != 0)
        {
            result |= Resource.Wood;
        }

        if (Ore != 0)
        {
            result |= Resource.Ore;
        }

        if (Mercury != 0)
        {
            result |= Resource.Mercury;
        }

        if (Sulfur != 0)
        {
            result |= Resource.Sulfur;
        }

        if (Crystal != 0)
        {
            result |= Resource.Crystal;
        }

        if (Gems != 0)
        {
            result |= Resource.Gems;
        }

        if (Gold != 0)
        {
            result |= Resource.Gold;
        }

        return result;
    }

    /// <summary>How many resources have a non-zero amount.</summary>
    /// <returns>The count.</returns>
    /// <remarks>参考：<c>resource.cpp:498</c> Funds::GetValidItemsCount。</remarks>
    public uint GetValidItemsCount()
    {
        uint result = 0;

        if (Wood != 0)
        {
            ++result;
        }

        if (Ore != 0)
        {
            ++result;
        }

        if (Mercury != 0)
        {
            ++result;
        }

        if (Sulfur != 0)
        {
            ++result;
        }

        if (Crystal != 0)
        {
            ++result;
        }

        if (Gems != 0)
        {
            ++result;
        }

        if (Gold != 0)
        {
            ++result;
        }

        return result;
    }

    /// <summary>The first resource with a positive amount, in the reference's order.</summary>
    /// <returns>The <see cref="Resource"/> flag and its amount.</returns>
    /// <remarks>参考：<c>resource.cpp:538</c> Funds::getFirstValidResource（参考用 <c>assert( 0 )</c> 兜底）。</remarks>
    public (int Type, int Amount) GetFirstValidResource()
    {
        if (Wood > 0)
        {
            return (Resource.Wood, Wood);
        }

        if (Ore > 0)
        {
            return (Resource.Ore, Ore);
        }

        if (Mercury > 0)
        {
            return (Resource.Mercury, Mercury);
        }

        if (Sulfur > 0)
        {
            return (Resource.Sulfur, Sulfur);
        }

        if (Crystal > 0)
        {
            return (Resource.Crystal, Crystal);
        }

        if (Gems > 0)
        {
            return (Resource.Gems, Gems);
        }

        if (Gold > 0)
        {
            return (Resource.Gold, Gold);
        }

        // The reference asserts here: the caller must pass a non-empty bag.
        return (Resource.Unknown, 0);
    }

    /// <summary>Clamps every field to be at least zero.</summary>
    /// <remarks>参考：<c>resource.cpp:520</c> Funds::Trim。</remarks>
    public void Trim()
    {
        if (Wood < 0)
        {
            Wood = 0;
        }

        if (Ore < 0)
        {
            Ore = 0;
        }

        if (Mercury < 0)
        {
            Mercury = 0;
        }

        if (Sulfur < 0)
        {
            Sulfur = 0;
        }

        if (Crystal < 0)
        {
            Crystal = 0;
        }

        if (Gems < 0)
        {
            Gems = 0;
        }

        if (Gold < 0)
        {
            Gold = 0;
        }
    }

    /// <summary>Zeroes every field.</summary>
    /// <remarks>参考：<c>resource.cpp:573</c> Funds::Reset。</remarks>
    public void Reset()
    {
        Wood = 0;
        Ore = 0;
        Mercury = 0;
        Sulfur = 0;
        Crystal = 0;
        Gems = 0;
        Gold = 0;
    }

    /// <summary>The debug dump of the reference's <c>String()</c>.</summary>
    /// <returns>The amounts in the reference's order.</returns>
    /// <remarks>参考：<c>resource.cpp:327</c> Funds::String；C# 里用 <c>ToString</c> 覆盖。</remarks>
    public override string ToString() =>
        $"ore: {Ore.ToString(CultureInfo.InvariantCulture)}, wood: {Wood.ToString(CultureInfo.InvariantCulture)}, "
        + $"mercury: {Mercury.ToString(CultureInfo.InvariantCulture)}, sulfur: {Sulfur.ToString(CultureInfo.InvariantCulture)}, "
        + $"crystal: {Crystal.ToString(CultureInfo.InvariantCulture)}, gems: {Gems.ToString(CultureInfo.InvariantCulture)}, "
        + $"gold: {Gold.ToString(CultureInfo.InvariantCulture)}";
}
