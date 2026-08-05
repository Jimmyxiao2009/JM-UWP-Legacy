using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jm.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// 用法:
//   dotnet run -- search 排球           搜索
//   dotnet run -- album 123456          专辑详情
//   dotnet run -- chapter 123456        章节(图片列表)
//   dotnet run -- token                 只验证 token/解密逻辑(离线)
//
// 环境变量(应对当前域名被网络层拦截的情况):
//   JM_PROXY=http://127.0.0.1:7890    走代理
//   JM_DOMAIN=www.xxx.vip             覆盖 API 域名(逗号分隔可多个)

static void PrintJson(string json)
{
    try { Console.WriteLine(JToken.Parse(json).ToString(Formatting.Indented)); }
    catch { Console.WriteLine(json); }
}

var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "search";
var arg = args.Length > 1 ? args[1] : "love";

// 离线自检：固定 ts，确认 token 生成稳定（无需联网）
if (cmd == "token")
{
    long ts = 1700000000;
    var (token, tokenParam) = JmCrypto.BuildToken(ts);
    Console.WriteLine($"ts          = {ts}");
    Console.WriteLine($"token       = {token}");
    Console.WriteLine($"tokenparam  = {tokenParam}");
    Console.WriteLine($"dataKey(md5)= {JmCrypto.Md5Hex(ts + JmCrypto.DataSecret)}");
    return;
}

// 代理 / 域名覆盖（应对域名被网络层拦截）
HttpClientHandler handler = null;
var proxy = Environment.GetEnvironmentVariable("JM_PROXY");
if (!string.IsNullOrWhiteSpace(proxy))
{
    handler = new HttpClientHandler
    {
        Proxy = new WebProxy(proxy),
        UseProxy = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
    };
    Console.WriteLine($"(proxy = {proxy})");
}
var domainEnv = Environment.GetEnvironmentVariable("JM_DOMAIN");
string[] domains = string.IsNullOrWhiteSpace(domainEnv)
    ? null
    : domainEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (domains != null) Console.WriteLine($"(domains = {string.Join(", ", domains)})");

using var client = new JmClient(domains, handler);

// domains 命令：打印发布页解密后的原始 JSON + 解析出的域名
if (cmd == "domains")
{
    Console.WriteLine("原始 JSON:\n" + await client.FetchDomainServerRawAsync());
    var ds = await client.FetchApiDomainsAsync();
    Console.WriteLine("\nServer 域名: " + string.Join(", ", ds));
    return;
}

// 先从发布页刷新一次域名（内置的多半已过期）
if (domains == null)
{
    Console.Write("刷新域名… ");
    Console.WriteLine(await client.RefreshApiDomainsAsync() ? $"OK ({client.Domains.Count} 个)" : "失败(用内置)");
}

Console.WriteLine($"[{cmd}] {arg} ...\n");

try
{
    JmApiResult r = cmd switch
    {
        "album"   => await client.GetAlbumAsync(int.Parse(arg)),
        "chapter" => await client.GetChapterAsync(int.Parse(arg)),
        _         => await client.SearchAsync(arg),
    };

    Console.WriteLine($"domain = {r.DomainUsed}");
    Console.WriteLine($"code   = {r.Code}   error_msg = {r.ErrorMsg ?? "<null>"}\n");

    if (!r.IsOk)
    {
        Console.WriteLine("(无 data 或返回码异常)");
        return;
    }

    // 强类型解析出的摘要，验证模型映射是否正确
    Console.WriteLine("---- 解析摘要 ----");
    switch (cmd)
    {
        case "album":
            var a = r.AsAlbum();
            Console.WriteLine($"[{a.Id}] {a.Name}  作者:{string.Join("/", a.Author)}  章节:{a.Series.Count}  图:{a.Images.Count}");
            break;
        case "chapter":
            var ch = r.AsChapter();
            Console.WriteLine($"[{ch.Id}] {ch.Name}  图片数:{ch.Images.Count}");
            break;
        default:
            var page = r.AsSearchPage();
            Console.WriteLine($"total={page.Total}  本页 {page.Content.Count} 条:");
            foreach (var item in page.Content)
                Console.WriteLine($"  [{item.Id}] {item.Name}");
            break;
    }
    Console.WriteLine("\n---- 原始 JSON ----");
    PrintJson(r.Data);
}
catch (JmException ex)
{
    Console.WriteLine("失败: " + ex.Message);
    if (ex.InnerException != null)
        Console.WriteLine("最后错误: " + ex.InnerException.Message);
}
