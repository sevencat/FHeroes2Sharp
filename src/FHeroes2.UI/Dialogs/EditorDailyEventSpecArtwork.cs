using FHeroes2.Assets.Icn;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The artwork of the daily event spec window, <c>editDailyEvent()</c>
/// (<c>editor_daily_event_spec_window.cpp:83-209</c>).
/// </summary>
/// <remarks>
/// <para>
/// 窗口自己要三样：两个数字控件（<c>TOWNWIND</c> 4..8 帧：编辑框、上下箭头的按/抬），OKAY/CANCEL
/// （<c>renderOkayCancelButtons()</c> 用的 <c>BUTTON_SMALL_OKAY_*</c>/<c>BUTTON_SMALL_CANCEL_*</c> 运行期表）。
/// 复选框的框与勾、资源图标都由 <see cref="FHeroes2.Game.Editor.EditorUiHelper"/> 从归档现取，不进这里。
/// </para>
/// </remarks>
public sealed class EditorDailyEventSpecArtwork
{
    /// <summary>
    /// The <c>TOWNWIND</c> frames a <see cref="ValueSelectionElement"/> indexes into, at least
    /// <see cref="ValueSelectionElement.RequiredFrameCount"/> of them.
    /// </summary>
    public IReadOnlyList<Sprite> ValueFrames { get; private init; } = [];

    /// <summary>The OKAY button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Okay { get; private init; }

    /// <summary>The CANCEL button, released and pressed.</summary>
    public (Sprite Released, Sprite Pressed) Cancel { get; private init; }

    /// <summary>
    /// Reads the artwork out of the installed game data.
    /// </summary>
    /// <param name="isEvilInterface">Whether the evil look is used.</param>
    /// <returns>The artwork, or <see langword="null"/> when a file or frame it needs is missing.</returns>
    /// <remarks>依赖 <see cref="GameAssets.Current"/> 已指向游戏数据。</remarks>
    public static EditorDailyEventSpecArtwork? TryCreate(bool isEvilInterface)
    {
        string townwindFile = GameAssets.GetFileName(IcnType.Townwind);

        List<Sprite> valueFrames = [];

        for (int frame = 0; frame < ValueSelectionElement.RequiredFrameCount; ++frame)
        {
            if (GameAssets.GetImage(townwindFile, frame) is { } sprite)
            {
                valueFrames.Add(sprite);
            }
        }

        (Sprite Released, Sprite Pressed)? okay = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallOkayEvil : IcnType.ButtonSmallOkayGood);

        (Sprite Released, Sprite Pressed)? cancel = ReadButtons(
            isEvilInterface ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (valueFrames.Count < ValueSelectionElement.RequiredFrameCount || okay is null || cancel is null)
        {
            return null;
        }

        return new EditorDailyEventSpecArtwork
        {
            ValueFrames = valueFrames,
            Okay = okay.Value,
            Cancel = cancel.Value
        };
    }

    private static (Sprite Released, Sprite Pressed)? ReadButtons(IcnType type)
    {
        string fileName = GameAssets.GetFileName(type);

        Sprite? released = GameAssets.GetImage(fileName, 0);
        Sprite? pressed = GameAssets.GetImage(fileName, 1);

        return released is null || pressed is null ? null : (released, pressed);
    }
}
