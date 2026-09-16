using System.Text;
using FluentAssertions;
using Soulsjwa.Api.Features.Media.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ImageValidatorTests
{
    private static byte[] BuildPng(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        WriteUInt32BigEndian(bytes, 8, 13); // IHDR chunk length (unused by the parser)
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        WriteUInt32BigEndian(bytes, 16, (uint)width);
        WriteUInt32BigEndian(bytes, 20, (uint)height);
        return bytes;
    }

    private static byte[] BuildJpeg(byte sofMarker, int width, int height)
    {
        var bytes = new byte[21];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8; // SOI
        bytes[2] = 0xFF;
        bytes[3] = sofMarker;
        WriteUInt16BigEndian(bytes, 4, 17); // segment length
        bytes[6] = 8; // precision
        WriteUInt16BigEndian(bytes, 7, (ushort)height);
        WriteUInt16BigEndian(bytes, 9, (ushort)width);
        bytes[11] = 3; // component count
        byte[] components = [1, 0x11, 0, 2, 0x11, 1, 3, 0x11, 1];
        components.CopyTo(bytes, 12);
        return bytes;
    }

    private static byte[] BuildWebpVp8(int width, int height)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes.AsSpan(0, 4));
        WriteUInt32LittleEndian(bytes, 4, 22);
        "WEBP"u8.CopyTo(bytes.AsSpan(8, 4));
        "VP8 "u8.CopyTo(bytes.AsSpan(12, 4));
        WriteUInt32LittleEndian(bytes, 16, 10);
        // 3-byte frame tag (contents irrelevant to the parser), then the VP8 start code.
        bytes[20] = 0x10;
        bytes[21] = 0x02;
        bytes[22] = 0x00;
        bytes[23] = 0x9D;
        bytes[24] = 0x01;
        bytes[25] = 0x2A;
        WriteUInt16LittleEndian(bytes, 26, (ushort)width);
        WriteUInt16LittleEndian(bytes, 28, (ushort)height);
        return bytes;
    }

    private static byte[] BuildWebpVp8Lossless(int width, int height)
    {
        var bytes = new byte[25];
        "RIFF"u8.CopyTo(bytes.AsSpan(0, 4));
        WriteUInt32LittleEndian(bytes, 4, 17);
        "WEBP"u8.CopyTo(bytes.AsSpan(8, 4));
        "VP8L"u8.CopyTo(bytes.AsSpan(12, 4));
        WriteUInt32LittleEndian(bytes, 16, 5);
        bytes[20] = 0x2F; // VP8L signature
        var packed = (uint)(width - 1) | ((uint)(height - 1) << 14);
        WriteUInt32LittleEndian(bytes, 21, packed);
        return bytes;
    }

    private static byte[] BuildWebpVp8Extended(int width, int height)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes.AsSpan(0, 4));
        WriteUInt32LittleEndian(bytes, 4, 22);
        "WEBP"u8.CopyTo(bytes.AsSpan(8, 4));
        "VP8X"u8.CopyTo(bytes.AsSpan(12, 4));
        WriteUInt32LittleEndian(bytes, 16, 10);
        bytes[20] = 0; // flags
        // bytes 21-23 reserved, left zero
        WriteUInt24LittleEndian(bytes, 24, (uint)(width - 1));
        WriteUInt24LittleEndian(bytes, 27, (uint)(height - 1));
        return bytes;
    }

    private static void WriteUInt32BigEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)value;
    }

    private static void WriteUInt16BigEndian(byte[] b, int offset, ushort value)
    {
        b[offset] = (byte)(value >> 8);
        b[offset + 1] = (byte)value;
    }

    private static void WriteUInt32LittleEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)value;
        b[offset + 1] = (byte)(value >> 8);
        b[offset + 2] = (byte)(value >> 16);
        b[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt16LittleEndian(byte[] b, int offset, ushort value)
    {
        b[offset] = (byte)value;
        b[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt24LittleEndian(byte[] b, int offset, uint value)
    {
        b[offset] = (byte)value;
        b[offset + 1] = (byte)(value >> 8);
        b[offset + 2] = (byte)(value >> 16);
    }

    [Fact]
    public void TryDetect_Png_ReturnsCorrectDimensions()
    {
        var ok = ImageValidator.TryDetect(BuildPng(100, 50), out var info);

        ok.Should().BeTrue();
        info!.Format.Should().Be(DetectedImageFormat.Png);
        info.ContentType.Should().Be("image/png");
        info.Width.Should().Be(100);
        info.Height.Should().Be(50);
    }

    [Fact]
    public void TryDetect_TruncatedPngHeader_IsRejected()
    {
        var truncated = BuildPng(100, 50)[..10];
        ImageValidator.TryDetect(truncated, out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_OversizedPng_IsRejected()
    {
        ImageValidator.TryDetect(BuildPng(5000, 5000), out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_ZeroDimensionPng_IsRejected()
    {
        ImageValidator.TryDetect(BuildPng(0, 100), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData((byte)0xC0)] // baseline
    [InlineData((byte)0xC2)] // progressive
    public void TryDetect_Jpeg_ReturnsCorrectDimensions(byte sofMarker)
    {
        var ok = ImageValidator.TryDetect(BuildJpeg(sofMarker, 640, 480), out var info);

        ok.Should().BeTrue();
        info!.Format.Should().Be(DetectedImageFormat.Jpeg);
        info.ContentType.Should().Be("image/jpeg");
        info.Width.Should().Be(640);
        info.Height.Should().Be(480);
    }

    [Fact]
    public void TryDetect_JpegWithoutSofSegment_IsRejected()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xD9]; // SOI immediately followed by EOI
        ImageValidator.TryDetect(bytes, out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_WebpVp8Lossy_ReturnsCorrectDimensions()
    {
        var ok = ImageValidator.TryDetect(BuildWebpVp8(100, 50), out var info);

        ok.Should().BeTrue();
        info!.Format.Should().Be(DetectedImageFormat.Webp);
        info.Width.Should().Be(100);
        info.Height.Should().Be(50);
    }

    [Fact]
    public void TryDetect_WebpVp8Lossless_ReturnsCorrectDimensions()
    {
        var ok = ImageValidator.TryDetect(BuildWebpVp8Lossless(100, 50), out var info);

        ok.Should().BeTrue();
        info!.Format.Should().Be(DetectedImageFormat.Webp);
        info.Width.Should().Be(100);
        info.Height.Should().Be(50);
    }

    [Fact]
    public void TryDetect_WebpVp8Extended_ReturnsCorrectDimensions()
    {
        var ok = ImageValidator.TryDetect(BuildWebpVp8Extended(100, 50), out var info);

        ok.Should().BeTrue();
        info!.Format.Should().Be(DetectedImageFormat.Webp);
        info.Width.Should().Be(100);
        info.Height.Should().Be(50);
    }

    [Fact]
    public void TryDetect_Svg_IsRejected()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        ImageValidator.TryDetect(bytes, out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_Gif_IsRejected()
    {
        var bytes = Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[20]).ToArray();
        ImageValidator.TryDetect(bytes, out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_Html_IsRejected()
    {
        var bytes = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>hi</body></html>");
        ImageValidator.TryDetect(bytes, out var info).Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void TryDetect_HtmlDeclaringItselfAsPng_IsStillRejected()
    {
        // The declared Content-Type is never consulted by ImageValidator — only bytes decide.
        var bytes = Encoding.UTF8.GetBytes("<html><body>not an image</body></html>");
        ImageValidator.TryDetect(bytes, out var info).Should().BeFalse();
        info.Should().BeNull();
    }
}
