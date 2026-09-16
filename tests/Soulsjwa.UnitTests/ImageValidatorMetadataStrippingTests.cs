using System.Text;
using FluentAssertions;
using Soulsjwa.Api.Features.Media.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ImageValidatorMetadataStrippingTests
{
    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    private static void WriteUInt32BigEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)value;
    }

    private static void WriteUInt32LittleEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)value;
        b[offset + 1] = (byte)(value >> 8);
        b[offset + 2] = (byte)(value >> 16);
        b[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt24LittleEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)value;
        b[offset + 1] = (byte)(value >> 8);
        b[offset + 2] = (byte)(value >> 16);
    }

    private static byte[] BuildJpegWithMetadata(int width, int height)
    {
        var b = new List<byte>();

        void Segment(byte marker, byte[] payload)
        {
            b.Add(0xFF);
            b.Add(marker);
            var len = (ushort)(payload.Length + 2);
            b.Add((byte)(len >> 8));
            b.Add((byte)len);
            b.AddRange(payload);
        }

        b.Add(0xFF);
        b.Add(0xD8); // SOI

        // APP1: Exif carrying a fake GPS block — must be dropped.
        Segment(0xE1, [.. Encoding.ASCII.GetBytes("Exif\0\0"), 1, 2, 3, 4]);

        // APP2: ICC profile — must survive.
        Segment(0xE2, [.. Encoding.ASCII.GetBytes("ICC_PROFILE\0"), 9, 9, 9, 9]);

        // SOF0.
        var sof = new byte[15];
        sof[0] = 8;
        sof[1] = (byte)(height >> 8);
        sof[2] = (byte)height;
        sof[3] = (byte)(width >> 8);
        sof[4] = (byte)width;
        sof[5] = 3;
        byte[] components = [1, 0x11, 0, 2, 0x11, 1, 3, 0x11, 1];
        components.CopyTo(sof, 6);
        Segment(0xC0, sof);

        // SOS header, then raw entropy-coded scan data (with a stuffed 0xFF00 byte to
        // exercise the scan-data walk), then EOI.
        Segment(0xDA, new byte[10]);
        b.AddRange([0x12, 0x34, 0xFF, 0x00, 0x56]);
        b.Add(0xFF);
        b.Add(0xD9); // EOI

        return [.. b];
    }

    private static byte[] BuildPngWithMetadata(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void Chunk(string type, byte[] payload)
        {
            var len = new byte[4];
            WriteUInt32BigEndian(len, 0, (uint)payload.Length);
            b.AddRange(len);
            b.AddRange(Encoding.ASCII.GetBytes(type));
            b.AddRange(payload);
            b.AddRange(new byte[4]); // CRC, unchecked by ImageValidator
        }

        var ihdr = new byte[13];
        WriteUInt32BigEndian(ihdr, 0, (uint)width);
        WriteUInt32BigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // color type
        Chunk("IHDR", ihdr);

        Chunk("eXIf", [1, 2, 3, 4]); // must be dropped
        Chunk("iCCP", [.. Encoding.ASCII.GetBytes("sRGB\0"), 0, 1, 2, 3]); // must survive
        Chunk("IDAT", [0xAA, 0xBB]);
        Chunk("IEND", []);

        return [.. b];
    }

    private static byte[] BuildWebpVp8XWithMetadata(int width, int height)
    {
        var b = new List<byte>();
        b.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        b.AddRange(new byte[4]); // size, patched below
        b.AddRange(Encoding.ASCII.GetBytes("WEBP"));

        void Chunk(string fourCc, byte[] payload)
        {
            b.AddRange(Encoding.ASCII.GetBytes(fourCc));
            var size = new byte[4];
            WriteUInt32LittleEndian(size, 0, (uint)payload.Length);
            b.AddRange(size);
            b.AddRange(payload);
            if (payload.Length % 2 == 1)
                b.Add(0);
        }

        var vp8x = new byte[10];
        WriteUInt24LittleEndian(vp8x, 4, (uint)(width - 1));
        WriteUInt24LittleEndian(vp8x, 7, (uint)(height - 1));
        Chunk("VP8X", vp8x);
        Chunk("ICCP", Encoding.ASCII.GetBytes("fake icc profile")); // must survive
        Chunk("EXIF", Encoding.ASCII.GetBytes("fake exif data")); // must be dropped

        var bytes = b.ToArray();
        var riffSize = (uint)(bytes.Length - 8);
        WriteUInt32LittleEndian(bytes, 4, riffSize);
        return bytes;
    }

    [Fact]
    public void StripMetadata_Jpeg_DropsApp1_KeepsIcc_PreservesDimensions()
    {
        var original = BuildJpegWithMetadata(640, 480);
        ImageValidator.TryDetect(original, out var originalInfo).Should().BeTrue();

        var stripped = ImageValidator.StripMetadata(original, DetectedImageFormat.Jpeg);

        Contains(stripped, [0xFF, 0xE1]).Should().BeFalse("APP1 (Exif/GPS) must be dropped");
        Contains(stripped, Encoding.ASCII.GetBytes("ICC_PROFILE")).Should().BeTrue("APP2/ICC must survive");
        stripped.Should().EndWith((byte[])[0x12, 0x34, 0xFF, 0x00, 0x56, 0xFF, 0xD9], "the entropy-coded scan data and EOI must be preserved byte-for-byte");

        ImageValidator.TryDetect(stripped, out var strippedInfo).Should().BeTrue("the stripped file must still parse as a valid JPEG");
        strippedInfo!.Width.Should().Be(originalInfo!.Width);
        strippedInfo.Height.Should().Be(originalInfo.Height);
    }

    [Fact]
    public void StripMetadata_Png_DropsExif_KeepsIccp_PreservesDimensions()
    {
        var original = BuildPngWithMetadata(100, 50);
        ImageValidator.TryDetect(original, out var originalInfo).Should().BeTrue();

        var stripped = ImageValidator.StripMetadata(original, DetectedImageFormat.Png);

        Contains(stripped, Encoding.ASCII.GetBytes("eXIf")).Should().BeFalse();
        Contains(stripped, Encoding.ASCII.GetBytes("iCCP")).Should().BeTrue();
        Contains(stripped, Encoding.ASCII.GetBytes("IDAT")).Should().BeTrue();
        Contains(stripped, Encoding.ASCII.GetBytes("IEND")).Should().BeTrue();

        ImageValidator.TryDetect(stripped, out var strippedInfo).Should().BeTrue();
        strippedInfo!.Width.Should().Be(originalInfo!.Width);
        strippedInfo.Height.Should().Be(originalInfo.Height);
    }

    [Fact]
    public void StripMetadata_Webp_DropsExif_KeepsIccp_PreservesDimensions()
    {
        var original = BuildWebpVp8XWithMetadata(100, 50);
        ImageValidator.TryDetect(original, out var originalInfo).Should().BeTrue();

        var stripped = ImageValidator.StripMetadata(original, DetectedImageFormat.Webp);

        Contains(stripped, Encoding.ASCII.GetBytes("EXIF")).Should().BeFalse();
        Contains(stripped, Encoding.ASCII.GetBytes("ICCP")).Should().BeTrue();

        ImageValidator.TryDetect(stripped, out var strippedInfo).Should().BeTrue();
        strippedInfo!.Width.Should().Be(originalInfo!.Width);
        strippedInfo.Height.Should().Be(originalInfo.Height);

        var declaredRiffSize = stripped[4] | (stripped[5] << 8) | (stripped[6] << 16) | (stripped[7] << 24);
        declaredRiffSize.Should().Be(stripped.Length - 8, "the RIFF size field must be corrected after chunks are removed");
    }
}
