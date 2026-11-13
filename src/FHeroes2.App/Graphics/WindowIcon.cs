using System.Runtime.InteropServices;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using SDL3;

namespace FHeroes2.App.Graphics;

/// <summary>
/// The window icon: <c>assets/logo.ico</c> (embedded into the assembly) decoded into SDL surfaces
/// and handed to <c>SDL_SetWindowIcon</c>.
/// </summary>
/// <remarks>
/// The same file is compiled into the executable as its Windows icon, so the taskbar entry, the
/// Explorer view and the title bar all show one artwork instead of SDL's default. The 64 pixel
/// entry is the 100% scale icon and every larger entry is registered as an alternate
/// representation (<c>SDL_AddSurfaceAlternateImage</c>), which is how SDL picks a sharper image on
/// high DPI displays.
/// <para>
/// <see cref="SDL.CreateSurfaceFrom"/> does not copy the pixels and SDL keeps referring to the
/// surfaces for as long as the window lives, so both the surfaces and the unmanaged pixel buffers
/// stay allocated until <see cref="Dispose"/> — which the presenter calls after
/// <c>SDL_DestroyWindow</c>.
/// </para>
/// </remarks>
internal sealed class WindowIcon : IDisposable
{
    /// <summary>Logical name of the embedded resource, set by the client project file.</summary>
    private const string ResourceName = "logo.ico";

    /// <summary>Edge length of the 100% scale icon; larger entries become HiDPI alternates.</summary>
    private const int BaseSize = 64;

    private readonly List<IntPtr> _surfaces = [];
    private readonly List<IntPtr> _pixels = [];

    private WindowIcon()
    {
    }

    /// <summary>
    /// Decodes the embedded icon. Returns <c>null</c> after logging a warning when the resource is
    /// missing or malformed: a cosmetic icon must never stop the game from starting.
    /// </summary>
    public static WindowIcon? TryLoad()
    {
        try
        {
            using Stream? stream = typeof(WindowIcon).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                GameLog.Warn($"The embedded window icon '{ResourceName}' is missing from the assembly.");
                return null;
            }

            IcoFile icon = IcoFile.Load(stream);
            PngImage baseImage = icon.Closest(BaseSize);

            WindowIcon result = new();
            IntPtr baseSurface = result.AddSurface(baseImage);
            if (baseSurface == IntPtr.Zero)
            {
                result.Dispose();
                return null;
            }

            foreach (PngImage image in icon.Images)
            {
                if (image.Width <= baseImage.Width)
                {
                    continue;
                }

                IntPtr alternate = result.AddSurface(image);
                if (alternate == IntPtr.Zero)
                {
                    continue;
                }

                if (!SDL.AddSurfaceAlternateImage(baseSurface, alternate))
                {
                    GameLog.Warn($"Icon alternate {image.Width}x{image.Height} was rejected: {SDL.GetError()}");
                }
            }

            return result;
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException)
        {
            GameLog.Warn($"The window icon could not be loaded: {exception.Message}");
            return null;
        }
    }

    /// <summary>Applies the icon to <paramref name="window"/>; failures are only logged.</summary>
    public void Apply(IntPtr window)
    {
        if (window == IntPtr.Zero || _surfaces.Count == 0)
        {
            return;
        }

        if (!SDL.SetWindowIcon(window, _surfaces[0]))
        {
            GameLog.Warn($"The window icon could not be set: {SDL.GetError()}");
            return;
        }

        GameLog.Info($"Window icon set ({_surfaces.Count} embedded image(s)).");
    }

    public void Dispose()
    {
        foreach (IntPtr surface in _surfaces)
        {
            SDL.DestroySurface(surface);
        }

        foreach (IntPtr pixels in _pixels)
        {
            Marshal.FreeHGlobal(pixels);
        }

        _surfaces.Clear();
        _pixels.Clear();
    }

    private IntPtr AddSurface(PngImage image)
    {
        IntPtr pixels = Marshal.AllocHGlobal(image.Rgba.Length);
        Marshal.Copy(image.Rgba, 0, pixels, image.Rgba.Length);

        // ABGR8888 is stored as R, G, B, A in memory, which is the layout of PngImage.
        IntPtr surface = SDL.CreateSurfaceFrom(
            image.Width,
            image.Height,
            SDL.PixelFormat.ABGR8888,
            pixels,
            image.Stride);

        if (surface == IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pixels);
            GameLog.Warn($"A {image.Width}x{image.Height} icon surface could not be created: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        _surfaces.Add(surface);
        _pixels.Add(pixels);
        return surface;
    }
}
