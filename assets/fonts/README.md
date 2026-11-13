# Bundled fonts

Put redistributable TrueType/OpenType fonts (SIL OFL or similar) here. Every `*.ttf` /
`*.otf` file in this folder is copied to `assets/fonts` next to the executable and can be
picked up by the text renderer.

The M0 text backend is SDL3_ttf, which needs a font that covers the characters the UI
shows. For CJK text a font such as *Noto Sans SC* or *Source Han Sans SC* is required. When
this folder is empty the client falls back to a Windows system font (`simsun.ttc`, then
`simhei.ttf`, then `msyh.ttc`, then `segoeui.ttf`) and finally to English-only diagnostics.
SimSun is preferred because it carries embedded bitmap strikes at 12 px, which keeps the
640x480 overlay crisp; set `FHEROES2_TEXT_CRISP=1` to quantize the glyph coverage to hard
pixels (only useful together with such a font).

The original Heroes II bitmap fonts come from the game archives and are **never** stored in this
repository: the UI font is the `FONT.ICN` / `SMALFONT.ICN` sprite tables (see ADR-018), which
`FHeroes2.Assets.Fonts.BitmapFont` decodes. `BIGFONT.FNT` / `SMALFONT.FNT` exist in the archive but
the reference implementation ignores them.

A TrueType face is only needed for characters the ICN tables cannot express (CJK, anything above
`U+00FF`). `FHeroes2.Assets.Fonts.TrueTypeGlyphSource` rasterizes those characters with
`Sevencat.GlyphRaster`, which embeds **WenQuanYi Micro Hei** inside the assembly (ADR-019): a CJK UI
works with this folder empty and without any font installed on the machine.

Text colour is not a separate face either: the yellow and gray variants of `FONT.ICN` are that
single sprite table with its palette indices converted by `FHeroes2.Core.Imaging.PaletteTable`
(ADR-020). The same table recolours the TrueType coverage ramp, so a CJK glyph inherits the colour of
the bitmap font it is mixed with - see `GlyphCoverageRamp.WithPalette(PaletteId.YellowFont)`.

The files below are only interesting when a different face is wanted - load one with
`TextFont.LoadFromFile(...)` and pass it to the `TrueTypeGlyphSource` constructor (automatic probing
of this folder is not wired into the UI yet).

| File | Size | Notes |
| --- | --- | --- |
| `WenQuanYiMicroHei.ttf` | 4.5 MB | the face the rasterizer package embeds (Apache-2.0 / GPLv3 dual, with embedding exception) |
| `Huiwenmincho-improved.ttf` | 24 MB | much larger, only worth it for the serif look |
