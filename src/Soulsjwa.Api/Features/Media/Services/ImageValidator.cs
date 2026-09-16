namespace Soulsjwa.Api.Features.Media.Services;

public enum DetectedImageFormat
{
    Png,
    Jpeg,
    Webp,
}

public sealed record ImageInfo(DetectedImageFormat Format, string ContentType, int Width, int Height);

/// <summary>
/// Detects image format and pixel dimensions by parsing file headers with
/// pure <see cref="ReadOnlySpan{T}"/> code — no decoder, no native library,
/// no pixel buffer ever allocated. The declared Content-Type and filename
/// extension are never consulted; only the bytes decide.
/// </summary>
public static class ImageValidator
{
    /// <summary>Only the first 64 KiB of a file is scanned for headers.</summary>
    private const int MaxHeaderScanBytes = 64 * 1024;

    /// <summary>Maximum accepted image edge, in pixels.</summary>
    private const int MaxDimension = 4096;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Attempts to detect the image format and dimensions from the start of
    /// a file's bytes. Returns false for anything unrecognised (including
    /// SVG, GIF, HTML), a truncated/malformed header, or dimensions that are
    /// zero, negative, or exceed <see cref="MaxDimension"/>.
    /// </summary>
    public static bool TryDetect(ReadOnlySpan<byte> bytes, out ImageInfo? info)
    {
        var scan = bytes.Length > MaxHeaderScanBytes ? bytes[..MaxHeaderScanBytes] : bytes;

        if (!(TryDetectPng(scan, out info) || TryDetectJpeg(scan, out info) || TryDetectWebp(scan, out info)))
        {
            info = null;
            return false;
        }

        if (info is null || !HasValidDimensions(info.Width, info.Height))
        {
            info = null;
            return false;
        }

        return true;
    }

    private static bool HasValidDimensions(int width, int height) =>
        width > 0 && height > 0 && width <= MaxDimension && height <= MaxDimension;

    /// <summary>
    /// Checks only the outer container signature (PNG's 8-byte magic, JPEG's
    /// SOI, or WebP's RIFF/WEBP marker) — no chunk/segment walk, no
    /// dimension check. Lets a caller tell "this isn't one of our accepted
    /// formats at all" (415 Unsupported Media Type) apart from "this claims
    /// to be one of our formats but is malformed" (400 Bad Request), which
    /// <see cref="TryDetect"/> alone can't distinguish.
    /// </summary>
    public static DetectedImageFormat? SniffContainer(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(PngSignature))
            return DetectedImageFormat.Png;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            return DetectedImageFormat.Jpeg;
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return DetectedImageFormat.Webp;
        return null;
    }

    /// <summary>
    /// Rewrites the container while copying, dropping metadata segments that
    /// can carry EXIF (and therefore GPS) data — no decode, no re-encode, no
    /// quality loss. <paramref name="format"/> should come from a prior
    /// <see cref="TryDetect"/> call on the same bytes.
    /// </summary>
    public static byte[] StripMetadata(ReadOnlySpan<byte> bytes, DetectedImageFormat format) => format switch
    {
        DetectedImageFormat.Png => StripPngMetadata(bytes),
        DetectedImageFormat.Jpeg => StripJpegMetadata(bytes),
        DetectedImageFormat.Webp => StripWebpMetadata(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Keeps IHDR/PLTE/IDAT/IEND/tRNS/gAMA/cHRM/sRGB/iCCP; drops eXIf/tEXt/iTXt/zTXt (and anything else).</summary>
    private static byte[] StripPngMetadata(ReadOnlySpan<byte> bytes)
    {
        using var output = new MemoryStream(bytes.Length);
        output.Write(bytes[..8]); // signature

        var pos = 8;
        while (pos + 8 <= bytes.Length)
        {
            var length = (uint)ReadUInt32BigEndian(bytes.Slice(pos, 4));
            var type = bytes.Slice(pos + 4, 4);
            var chunkTotal = 12L + length; // length(4) + type(4) + data(length) + crc(4)
            if (chunkTotal > bytes.Length - pos)
                break; // truncated trailing chunk

            if (IsAllowedPngChunkType(type))
                output.Write(bytes.Slice(pos, (int)chunkTotal));

            var isIEnd = type.SequenceEqual("IEND"u8);
            pos += (int)chunkTotal;
            if (isIEnd)
                break;
        }

        return output.ToArray();
    }

    private static bool IsAllowedPngChunkType(ReadOnlySpan<byte> type) =>
        type.SequenceEqual("IHDR"u8)
        || type.SequenceEqual("PLTE"u8)
        || type.SequenceEqual("IDAT"u8)
        || type.SequenceEqual("IEND"u8)
        || type.SequenceEqual("tRNS"u8)
        || type.SequenceEqual("gAMA"u8)
        || type.SequenceEqual("cHRM"u8)
        || type.SequenceEqual("sRGB"u8)
        || type.SequenceEqual("iCCP"u8);

    /// <summary>Drops APP1 (Exif/XMP), APP13 (IPTC), and COM segments. APP2 (ICC) is kept.</summary>
    private static byte[] StripJpegMetadata(ReadOnlySpan<byte> bytes)
    {
        using var output = new MemoryStream(bytes.Length);
        output.Write(bytes[..2]); // SOI

        var pos = 2;
        while (pos + 1 < bytes.Length)
        {
            if (bytes[pos] != 0xFF)
            {
                output.Write(bytes[pos..]); // malformed tail; preserve rather than lose data
                break;
            }

            var marker = bytes[pos + 1];

            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                output.Write(bytes.Slice(pos, 2));
                pos += 2;
                continue;
            }

            if (marker == 0xD9)
            {
                output.Write(bytes.Slice(pos, 2));
                pos += 2;
                break;
            }

            if (pos + 4 > bytes.Length)
            {
                output.Write(bytes[pos..]);
                break;
            }

            var segmentLength = ReadUInt16BigEndian(bytes.Slice(pos + 2, 2));
            var segmentEnd = pos + 2 + segmentLength;
            if (segmentLength < 2 || segmentEnd > bytes.Length)
            {
                output.Write(bytes[pos..]);
                break;
            }

            if (marker == 0xDA)
            {
                // SOS header, then raw entropy-coded scan data with no length
                // prefix — copy through byte-for-byte up to the next real
                // marker (0xFF00 stuffing and RSTn are part of the scan).
                output.Write(bytes.Slice(pos, segmentEnd - pos));
                var scanPos = segmentEnd;
                while (scanPos < bytes.Length)
                {
                    if (bytes[scanPos] == 0xFF && scanPos + 1 < bytes.Length)
                    {
                        var next = bytes[scanPos + 1];
                        if (next == 0x00 || (next >= 0xD0 && next <= 0xD7))
                        {
                            scanPos += 2;
                            continue;
                        }
                        break;
                    }
                    scanPos++;
                }
                output.Write(bytes.Slice(segmentEnd, scanPos - segmentEnd));
                pos = scanPos;
                continue;
            }

            var drop = marker is 0xE1 or 0xED or 0xFE; // APP1, APP13, COM
            if (!drop)
                output.Write(bytes.Slice(pos, segmentEnd - pos));
            pos = segmentEnd;
        }

        return output.ToArray();
    }

    /// <summary>Drops EXIF and XMP chunks; every other chunk (including ICCP) is kept.</summary>
    private static byte[] StripWebpMetadata(ReadOnlySpan<byte> bytes)
    {
        using var output = new MemoryStream(bytes.Length);
        output.Write(bytes[..12]); // "RIFF" + size (patched below) + "WEBP"

        var pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            var fourCc = bytes.Slice(pos, 4);
            var chunkSize = (long)ReadUInt32LittleEndian(bytes.Slice(pos + 4, 4));
            var padded = chunkSize % 2 == 1 ? chunkSize + 1 : chunkSize;
            var chunkTotal = 8L + padded;
            if (chunkTotal > bytes.Length - pos)
                break; // truncated trailing chunk

            var isMetadata = fourCc.SequenceEqual("EXIF"u8) || fourCc.SequenceEqual("XMP "u8);
            if (!isMetadata)
                output.Write(bytes.Slice(pos, (int)chunkTotal));

            pos += (int)chunkTotal;
        }

        var result = output.ToArray();
        var riffSize = (uint)(result.Length - 8);
        result[4] = (byte)riffSize;
        result[5] = (byte)(riffSize >> 8);
        result[6] = (byte)(riffSize >> 16);
        result[7] = (byte)(riffSize >> 24);
        return result;
    }

    private static bool TryDetectPng(ReadOnlySpan<byte> bytes, out ImageInfo? info)
    {
        info = null;
        // 8 (signature) + 4 (chunk length) + 4 ("IHDR") + 4 (width) + 4 (height).
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(PngSignature))
            return false;

        if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            return false;

        var width = ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = ReadUInt32BigEndian(bytes.Slice(20, 4));
        info = new ImageInfo(DetectedImageFormat.Png, "image/png", width, height);
        return true;
    }

    private static bool TryDetectJpeg(ReadOnlySpan<byte> bytes, out ImageInfo? info)
    {
        info = null;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return false;

        var pos = 2;
        while (pos + 1 < bytes.Length)
        {
            if (bytes[pos] != 0xFF)
                return false;

            // JPEG allows repeated 0xFF fill bytes before the marker code.
            var markerPos = pos + 1;
            while (markerPos < bytes.Length && bytes[markerPos] == 0xFF)
                markerPos++;
            if (markerPos >= bytes.Length)
                return false;

            var marker = bytes[markerPos];
            pos = markerPos + 1;

            // Markers with no length field: SOI, TEM, and the RSTn range.
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                continue;
            if (marker == 0xD9)
                return false; // EOI reached without finding a SOFn segment.

            if (pos + 2 > bytes.Length)
                return false;
            var segmentLength = ReadUInt16BigEndian(bytes.Slice(pos, 2));
            if (segmentLength < 2)
                return false;

            if (IsStartOfFrameMarker(marker))
            {
                // length(2) precision(1) height(2, BE) width(2, BE).
                if (pos + 7 > bytes.Length)
                    return false;
                var height = ReadUInt16BigEndian(bytes.Slice(pos + 3, 2));
                var width = ReadUInt16BigEndian(bytes.Slice(pos + 5, 2));
                info = new ImageInfo(DetectedImageFormat.Jpeg, "image/jpeg", width, height);
                return true;
            }

            pos += segmentLength;
        }

        return false;
    }

    /// <summary>
    /// SOFn markers C0–C3, C5–C7, C9–CB, CD–CF. C4 (DHT), C8 (reserved "JPG"),
    /// and CC (DAC) are deliberately excluded — they are not frame headers.
    /// </summary>
    private static bool IsStartOfFrameMarker(byte marker) =>
        (marker >= 0xC0 && marker <= 0xC3)
        || (marker >= 0xC5 && marker <= 0xC7)
        || (marker >= 0xC9 && marker <= 0xCB)
        || (marker >= 0xCD && marker <= 0xCF);

    private static bool TryDetectWebp(ReadOnlySpan<byte> bytes, out ImageInfo? info)
    {
        info = null;
        // "RIFF"(4) size(4) "WEBP"(4) fourCC(4) chunkSize(4) = 20 bytes before chunk data.
        if (bytes.Length < 20)
            return false;
        if (bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F')
            return false;
        if (bytes[8] != (byte)'W' || bytes[9] != (byte)'E' || bytes[10] != (byte)'B' || bytes[11] != (byte)'P')
            return false;

        var fourCc = bytes.Slice(12, 4);
        const int chunkDataStart = 20;

        if (fourCc.SequenceEqual("VP8 "u8))
            return TryDetectVp8Lossy(bytes, chunkDataStart, out info);
        if (fourCc.SequenceEqual("VP8L"u8))
            return TryDetectVp8Lossless(bytes, chunkDataStart, out info);
        if (fourCc.SequenceEqual("VP8X"u8))
            return TryDetectVp8Extended(bytes, chunkDataStart, out info);

        return false;
    }

    private static bool TryDetectVp8Lossy(ReadOnlySpan<byte> bytes, int chunkDataStart, out ImageInfo? info)
    {
        info = null;
        // 3-byte frame tag, 3-byte start code, 2-byte width, 2-byte height.
        if (bytes.Length < chunkDataStart + 10)
            return false;
        if (bytes[chunkDataStart + 3] != 0x9D || bytes[chunkDataStart + 4] != 0x01 || bytes[chunkDataStart + 5] != 0x2A)
            return false;

        var frameStart = chunkDataStart + 6;
        var width = ReadUInt16LittleEndian(bytes.Slice(frameStart, 2)) & 0x3FFF;
        var height = ReadUInt16LittleEndian(bytes.Slice(frameStart + 2, 2)) & 0x3FFF;
        info = new ImageInfo(DetectedImageFormat.Webp, "image/webp", width, height);
        return true;
    }

    private static bool TryDetectVp8Lossless(ReadOnlySpan<byte> bytes, int chunkDataStart, out ImageInfo? info)
    {
        info = null;
        // 1-byte signature (0x2F), then 4 bytes packing 14-bit width-1 / height-1.
        if (bytes.Length < chunkDataStart + 5 || bytes[chunkDataStart] != 0x2F)
            return false;

        var packed = ReadUInt32LittleEndian(bytes.Slice(chunkDataStart + 1, 4));
        var width = (int)(packed & 0x3FFF) + 1;
        var height = (int)((packed >> 14) & 0x3FFF) + 1;
        info = new ImageInfo(DetectedImageFormat.Webp, "image/webp", width, height);
        return true;
    }

    private static bool TryDetectVp8Extended(ReadOnlySpan<byte> bytes, int chunkDataStart, out ImageInfo? info)
    {
        info = null;
        // 1-byte flags, 3-byte reserved, 3-byte width-1 (LE), 3-byte height-1 (LE).
        if (bytes.Length < chunkDataStart + 10)
            return false;

        var width = ReadUInt24LittleEndian(bytes.Slice(chunkDataStart + 4, 3)) + 1;
        var height = ReadUInt24LittleEndian(bytes.Slice(chunkDataStart + 7, 3)) + 1;
        info = new ImageInfo(DetectedImageFormat.Webp, "image/webp", width, height);
        return true;
    }

    private static int ReadUInt32BigEndian(ReadOnlySpan<byte> b) =>
        (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];

    private static int ReadUInt16BigEndian(ReadOnlySpan<byte> b) => (b[0] << 8) | b[1];

    private static int ReadUInt16LittleEndian(ReadOnlySpan<byte> b) => b[0] | (b[1] << 8);

    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> b) =>
        (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> b) => b[0] | (b[1] << 8) | (b[2] << 16);
}
