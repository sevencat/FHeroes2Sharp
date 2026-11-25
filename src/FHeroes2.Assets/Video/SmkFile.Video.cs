namespace FHeroes2.Assets.Video;

/// <summary>The video and palette halves of the Smacker decoder, <c>smk_render_palette()</c> and <c>smk_render_video()</c>.</summary>
public sealed partial class SmkFile
{
    /// <summary>Decompresses a palette record against the palette of the previous frame.</summary>
    /// <param name="data">The record, without its length byte.</param>
    private void RenderPalette(ReadOnlySpan<byte> data)
    {
        Span<byte> previous = stackalloc byte[PaletteSizeBytes];
        _palette.CopyTo(previous);

        int index = 0;
        int position = 0;

        while (index < 256 && position < data.Length)
        {
            byte header = data[position];

            if ((header & 0x80) != 0)
            {
                // Skip: the colours of the previous palette stay as they are.
                index += (header & 0x7F) + 1;
                ++position;
            }
            else if ((header & 0x40) != 0)
            {
                if (position + 2 > data.Length)
                {
                    return;
                }

                int count = (header & 0x3F) + 1;
                int source = data[position + 1];
                position += 2;

                if (index + count > 256 || source + count > 256 || (source < index && source + count > index))
                {
                    return;
                }

                previous.Slice(source * 3, count * 3).CopyTo(_palette.AsSpan(index * 3, count * 3));
                index += count;
            }
            else
            {
                if (position + 3 > data.Length)
                {
                    return;
                }

                for (int component = 0; component < 3; ++component)
                {
                    byte value = data[position++];

                    if (value > 0x3F)
                    {
                        return;
                    }

                    _palette[(index * 3) + component] = PaletteMap[value];
                }

                ++index;
            }
        }
    }

    /// <summary>Decompresses one video chunk into the retained frame buffer, patching the blocks it covers.</summary>
    /// <param name="data">The video part of the frame chunk.</param>
    /// <returns>Whether the chunk was decoded.</returns>
    private bool RenderVideo(ReadOnlySpan<byte> data)
    {
        SmkBitReader reader = new(data);
        Span<byte> frame = _frame;

        int row = 0;
        int column = 0;

        while (row < Height)
        {
            int unpack = _trees[TreeType].Lookup(ref reader);

            if (unpack < 0)
            {
                return false;
            }

            int type = unpack & 0x0003;
            int blockLength = (unpack & 0x00FC) >> 2;
            byte typeData = (byte)((unpack & 0xFF00) >> 8);

            // Version 4 extended full blocks with two shapes of its own.
            if (type == 1 && Version == '4')
            {
                int bit = reader.ReadBit();

                if (bit > 0)
                {
                    type = 4;
                }
                else
                {
                    bit = reader.ReadBit();

                    if (bit > 0)
                    {
                        type = 5;
                    }
                }
            }

            for (int block = 0; block < BlockSizes[blockLength] && row < Height; ++block)
            {
                int skip = (row * Width) + column;

                switch (type)
                {
                    case 0:
                        if (!DecodeMapBlock(frame, ref reader, skip, out bool failed) || failed)
                        {
                            return false;
                        }

                        break;

                    case 1:
                        for (int k = 0; k < 4; ++k)
                        {
                            int value = _trees[TreeFull].Lookup(ref reader);

                            if (value < 0)
                            {
                                return false;
                            }

                            frame[skip + 3] = (byte)((value & 0xFF00) >> 8);
                            frame[skip + 2] = (byte)(value & 0x00FF);

                            value = _trees[TreeFull].Lookup(ref reader);

                            if (value < 0)
                            {
                                return false;
                            }

                            frame[skip + 1] = (byte)((value & 0xFF00) >> 8);
                            frame[skip] = (byte)(value & 0x00FF);
                            skip += Width;
                        }

                        break;

                    case 2:
                        // Void block: the retained frame already holds these pixels.
                        break;

                    case 3:
                        for (int k = 0; k < 4; ++k)
                        {
                            frame.Slice(skip, 4).Fill(typeData);
                            skip += Width;
                        }

                        break;

                    case 4:
                        for (int k = 0; k < 2; ++k)
                        {
                            int value = _trees[TreeFull].Lookup(ref reader);

                            if (value < 0)
                            {
                                return false;
                            }

                            for (int i = 0; i < 2; ++i)
                            {
                                frame.Slice(skip + 2, 2).Fill((byte)((value & 0xFF00) >> 8));
                                frame.Slice(skip, 2).Fill((byte)(value & 0x00FF));
                                skip += Width;
                            }
                        }

                        break;

                    case 5:
                        for (int k = 0; k < 2; ++k)
                        {
                            int value = _trees[TreeFull].Lookup(ref reader);

                            if (value < 0)
                            {
                                return false;
                            }

                            frame[skip + 3] = (byte)((value & 0xFF00) >> 8);
                            frame[skip + 2] = (byte)(value & 0x00FF);
                            frame[skip + Width + 3] = (byte)((value & 0xFF00) >> 8);
                            frame[skip + Width + 2] = (byte)(value & 0x00FF);

                            value = _trees[TreeFull].Lookup(ref reader);

                            if (value < 0)
                            {
                                return false;
                            }

                            frame[skip + 1] = (byte)((value & 0xFF00) >> 8);
                            frame[skip] = (byte)(value & 0x00FF);
                            frame[skip + Width + 1] = (byte)((value & 0xFF00) >> 8);
                            frame[skip + Width] = (byte)(value & 0x00FF);
                            skip += Width << 1;
                        }

                        break;
                }

                column += 4;

                if (column >= Width)
                {
                    column = 0;
                    row += 4;
                }
            }
        }

        return true;
    }
}
