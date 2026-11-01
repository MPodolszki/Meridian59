using System.Text;
using Meridian.Protocol;

namespace Meridian.Tests;

public class ProtocolTests
{
    [Fact]
    public void Crc32_MatchesStandardCheckValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Frame_HasSevenByteHeaderWithLengthCopyAndCrc()
    {
        byte[] payload = [21];
        byte[] frame = Framing.FrameServerMessage(payload);

        Assert.Equal(8, frame.Length);
        var header = FrameHeader.Parse(frame);
        Assert.Equal(1, header.Length);
        Assert.Equal(1, header.LengthVerify);
        Assert.Equal((ushort)(Crc32.Compute(payload) & 0xFFFF), header.Crc);
        Assert.Equal(0, header.Epoch);
        Assert.Equal(21, frame[7]);
    }

    [Fact]
    public async Task FrameReader_RejectsInconsistentHeader()
    {
        byte[] bad = [1, 0, 0, 0, 2, 0, 0, 21];
        var reader = new FrameReader(new MemoryStream(bad), Framing.MaxClientPayload);
        await Assert.ThrowsAsync<ProtocolException>(() => reader.ReadAsync(default));
    }

    [Fact]
    public void PasswordDigest_IsMd5WithZeroBytesReplaced()
    {
        // MD5("") = d41d8cd98f00b204e9800998ecf8427e – enthält 0x00 an Index 6.
        byte[] digest = PasswordDigest.Compute("");
        Assert.Equal("D41D8CD98F01B204E9800998ECF8427E", Convert.ToHexString(digest));
    }

    [Fact]
    public void LoginRequest_RoundTrip_HasOriginalFixedSize()
    {
        var login = new LoginRequest(7, 37, 2, "Tester", PasswordDigest.Compute("geheim"));
        byte[] payload = login.ToPayload();

        // 39 Byte feste Felder inkl. Type, dann zwei Strings (synched.c Mindestlänge 39).
        Assert.Equal(39 + 2 + 6 + 2 + 16, payload.Length);
        var parsed = LoginRequest.Parse(payload);
        Assert.Equal("Tester", parsed.Username);
        Assert.Equal(login.PasswordDigest, parsed.PasswordDigest);
        Assert.Equal(37, parsed.VersionMinor);
    }

    [Fact]
    public void SecurityStream_StepMatchesOriginalFormula()
    {
        var stream = new SecurityStream([1, 2, 3, 4, 5]);
        // seeds' = (s*9301+49297) % 233280 → 58598, 67899, 77200, 86501, 95802; 95802 % 4 = 2
        Assert.Equal(77200u, stream.Step());
        Assert.Equal([58598u, 67899u, 77200u, 86501u, 95802u], stream.Seeds.ToArray());
    }

    [Fact]
    public void SecurityStream_WrapsMultiplicationAt32Bit()
    {
        var stream = new SecurityStream([0xFFFFFFFF, 0, 0, 0, 0]);
        stream.Step();
        uint expected = unchecked(0xFFFFFFFFu * 9301u + 49297u) % 233280u;
        Assert.Equal(expected, stream.Seeds[0]);
    }

    [Fact]
    public void SecurityStream_HashSignExtendsTypeByte()
    {
        uint[] seeds = [10, 20, 30, 40, 50];
        byte[] payload = [155, 1, 2];   // BP_USERCOMMAND ≥ 0x80

        ushort hash = new SecurityStream(seeds).NextHash(payload);

        ushort step = (ushort)new SecurityStream(seeds).Step();
        ushort expected = (ushort)(step ^ 3 ^ ((0xFF00 | 155) << 4) ^ (Crc32.Compute(payload) & 0xFFFF));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ServerToken_IsNeutralUntilReset_ThenSlidesThroughRedbook()
    {
        var token = new ServerToken();
        Assert.Equal(42, token.Apply(42));

        token.Reset(0x10, "AB");
        Assert.Equal(42 ^ 0x10, token.Apply(42));                 // danach +'A'
        Assert.Equal(42 ^ ((0x10 + 'A') & 0xFF), token.Apply(42)); // danach +'B', Wrap auf 'A'
        Assert.Equal(42 ^ ((0x10 + 'A' + 'B') & 0xFF), token.Apply(42));
    }
}
