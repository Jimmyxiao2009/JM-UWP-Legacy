using System;
using System.Security.Cryptography;
using System.Text;
using Jm.Core;
using Xunit;

namespace Jm.Core.Tests;

public class CryptoTests
{
    [Theory]
    [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
    [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
    public void Md5Hex_matches_known_vectors(string input, string expected)
        => Assert.Equal(expected, JmCrypto.Md5Hex(input));

    [Fact]
    public void BuildToken_is_deterministic_and_correct()
    {
        var (token, tokenParam) = JmCrypto.BuildToken(1700000000, "2.0.26");
        // token = md5("1700000000" + "18comicAPPContent")
        Assert.Equal(JmCrypto.Md5Hex("170000000018comicAPPContent"), token);
        Assert.Equal("8be524e958f97f014ddf2a570b011305", token);
        Assert.Equal("1700000000,2.0.26", tokenParam);
    }

    [Fact]
    public void DecodeResponseData_round_trips_real_server_encoding()
    {
        // 模拟服务器侧：用同一 ts 派生 key，AES-ECB + PKCS7 加密，再 base64。
        long ts = 1722334455;
        var plaintext = "{\"code\":200,\"msg\":\"日本語ok ✓\"}";
        var key = Encoding.UTF8.GetBytes(JmCrypto.Md5Hex(ts + JmCrypto.DataSecret));

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);
        var b64 = Convert.ToBase64String(cipher);

        // 客户端侧解密应还原原文
        Assert.Equal(plaintext, JmCrypto.DecodeResponseData(b64, ts));
    }

    [Fact]
    public void DecodeResponseData_fails_with_wrong_ts()
    {
        long ts = 1722334455;
        var key = Encoding.UTF8.GetBytes(JmCrypto.Md5Hex(ts + JmCrypto.DataSecret));
        using var aes = Aes.Create();
        aes.Key = key; aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.PKCS7;
        var bytes = Encoding.UTF8.GetBytes("hello world hello world");
        var b64 = Convert.ToBase64String(aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length));

        // 用错 ts 派生的 key：要么抛异常(无效 padding/长度)，要么解出乱码，
        // 总之绝不能等于原文。
        string wrong = null;
        try { wrong = JmCrypto.DecodeResponseData(b64, ts + 1); }
        catch { /* 抛异常也算通过 */ }
        Assert.NotEqual("hello world hello world", wrong);
    }
}
