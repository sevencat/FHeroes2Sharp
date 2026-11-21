using FHeroes2.Game.System;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Switches the interface graphics for the duration of a scope, a port of
/// <c>GameInterfaceTypeRestorer</c> (<c>src/fheroes2/gui/ui_tool.cpp:379-393</c>, declared at
/// <c>ui_tool.h:290-303</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考构造时记下当时的界面类型，与请求的不同就切过去；析构时若两者不同就切回来。
/// 战役与自动测试用它临时换「好/坏」两套界面。
/// </para>
/// <para>
/// 差异：参考直接取 <c>Settings::Get()</c> 单例，端口把 <see cref="Settings"/> 作为构造参数注入，
/// 方便测试里换一份不污染全局的实例。
/// </para>
/// </remarks>
public sealed class GameInterfaceTypeRestorer : IDisposable
{
    private readonly Settings _settings;
    private bool _disposed;

    /// <summary>Switches the interface type and remembers the previous one.</summary>
    /// <param name="settings">Settings holding the interface type.</param>
    /// <param name="interfaceType">Interface type the scope needs, <c>interfaceType</c> of the reference.</param>
    /// <remarks>参考：<c>ui_tool.cpp:379-386</c>。</remarks>
    public GameInterfaceTypeRestorer(Settings settings, InterfaceType interfaceType)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        InterfaceType = interfaceType;
        OriginalInterfaceType = settings.InterfaceType;

        if (interfaceType != OriginalInterfaceType)
        {
            settings.SetInterfaceType(interfaceType);
        }
    }

    /// <summary>Interface type the scope asked for, <c>interfaceType</c>.</summary>
    public InterfaceType InterfaceType { get; }

    /// <summary>Interface type that was active before the scope, <c>originalInterfaceType</c>.</summary>
    public InterfaceType OriginalInterfaceType { get; }

    /// <summary>Switches the previous interface type back when it had been changed.</summary>
    /// <remarks>参考：<c>ui_tool.cpp:388-393</c>。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (InterfaceType != OriginalInterfaceType)
        {
            _settings.SetInterfaceType(OriginalInterfaceType);
        }
    }
}
