using System;
using System.Security.Cryptography;
using System.Text;
using Jm.Core;
using Xunit;

namespace Jm.Core.Tests;

public class DomainServerTests
{
    // 模拟域名服务器侧：用空 ts + 域名 secret 派生 key，AES-ECB+PKCS7 加密 JSON，再 base64
    private static string EncodeServerPayload(string json)
    {
        var key = Encoding.UTF8.GetBytes(JmCrypto.Md5Hex("" + JmCrypto.DomainServerSecret));
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        var bytes = Encoding.UTF8.GetBytes(json);
        var cipher = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(cipher);
    }

    [Fact]
    public void ParseDomainServer_decodes_server_list()
    {
        var b64 = EncodeServerPayload("{\"Server\":[\"www.a-cdn.vip\",\"www.b-cdn.cc\"]}");

        var domains = JmClient.ParseDomainServer(b64);

        Assert.Equal(2, domains.Count);
        Assert.Equal("www.a-cdn.vip", domains[0]);
        Assert.Equal("www.b-cdn.cc", domains[1]);
    }

    [Fact]
    public void ParseDomainServer_strips_leading_non_ascii_noise()
    {
        var b64 = EncodeServerPayload("{\"Server\":[\"x.vip\"]}");
        // 真实响应开头可能有 BOM/非 ASCII 噪声，应被剥掉
        var noisy = "﻿​" + b64;

        var domains = JmClient.ParseDomainServer(noisy);

        Assert.Single(domains);
        Assert.Equal("x.vip", domains[0]);
    }

    [Fact]
    public void ParseDomainServer_empty_or_no_server_field_returns_empty()
    {
        Assert.Empty(JmClient.ParseDomainServer(""));
        var b64 = EncodeServerPayload("{\"NotServer\":[\"x\"]}");
        Assert.Empty(JmClient.ParseDomainServer(b64));
    }
}
