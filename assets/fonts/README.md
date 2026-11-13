# Bundled fonts

Put redistributable TrueType/OpenType fonts (SIL OFL or similar) here. Every `*.ttf` /
`*.otf` file in this folder is copied to `assets/fonts` next to the executable and can be
picked up by the text renderer.

The M0 text backend is FontStashSharp, which needs a font that covers the characters the UI
shows. For CJK text a font such as *Noto Sans SC* or *Source Han Sans SC* is required. When
this folder is empty the client falls back to a Windows system font (`simhei.ttf`, then
`msyh.ttc`, then `simsun.ttc`, then `segoeui.ttf`) and finally to English-only diagnostics.

The original Heroes II bitmap fonts (`BIGFONT.FNT`, `SMALFONT.FNT`, `FONT.ICN`) come from the
game archives and are **never** stored in this repository.
