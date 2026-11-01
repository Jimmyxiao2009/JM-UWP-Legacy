using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Jm.Core
{
    /// <summary>一次 API 调用的结果：已解密的 data 文本 + 服务器返回码。</summary>
    public sealed class JmApiResult
    {
        public int Code { get; set; }
        public string ErrorMsg { get; set; }
        /// <summary>已解密的 data JSON 文本（失败时为 null）。</summary>
        public string Data { get; set; }
        public string DomainUsed { get; set; }
        public bool IsOk => Code == 200 && Data != null;
    }

    /// <summary>
    /// 禁漫移动端 API 客户端。负责域名轮换、动态域名拉取、token 头注入、响应解密。
    /// 目标 netstandard1.4，便于被经典 UWP 工程引用。
    /// </summary>
    public sealed class JmClient : IDisposable
    {
        // API 节点域名经常被封/更换，这里给一组候选，逐个尝试。
        // 最好用 RefreshApiDomainsAsync() 从域名服务器拉最新的覆盖它。
        public static readonly string[] DefaultApiDomains =
        {
            // 当前主用（对齐 jmcomic 上游）
            "www.cdnaspa.club",
            "www.cdnaspa.vip",
            "www.cdnplaystation6.cc",
            "www.cdnplaystation6.vip",
            // 历史备用，多一层兜底
            "www.cdnmhwscc.vip",
            "www.cdnblackmyth.club",
            "www.cdnblackmyth.xyz",
            "www.cdnuc.vip",
            "www.jmapinode1.cc",
            "www.jmapinode2.cc",
            "www.jmapinode3.cc",
            "www.jmapinode.biz",
            "www.jmapinode.top",
            "www.jmapinode.xyz",
            "www.jmapibranch1.cc",
        };

        // 域名服务器(发布页)：返回加密的当前可用域名列表。多个镜像逐个试。
        public static readonly string[] DomainServerUrls =
        {
            "https://rup4a04-c01.tos-ap-southeast-1.bytepluses.com/newsvr-2025.txt",
            "https://rup4a04-c02.tos-cn-hongkong.bytepluses.com/newsvr-2025.txt",
            "https://rup4a04-c03.tos-cn-beijing.bytepluses.com.cn/newsvr-2025.txt",
        };

        private const string UserAgent =
            "Mozilla/5.0 (Linux; Android 12; M2012K11AC Build/SKQ1.211019.001; wv) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/91.0.4472.114 Mobile Safari/537.36";

        private readonly HttpClient _http;
        private readonly List<string> _domains;
        private readonly Func<long> _nowUnixSeconds;

        /// <summary>日志钩子：每次请求尝试的细节都会回调这里（调用方可写文件/打屏）。</summary>
        public Action<string> Logger { get; set; }
        private void Log(string msg) => Logger?.Invoke(msg);

        /// <summary>把异常链拆成可读的一行（含底层 socket/TLS 原因）。</summary>
        private static string Describe(Exception ex)
        {
            var parts = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
                parts.Add(e.GetType().Name + ": " + e.Message);
            return string.Join(" <- ", parts);
        }

        public JmClient(IEnumerable<string> domains = null, HttpMessageHandler handler = null, Func<long> nowProvider = null)
        {
            // 默认 handler 必须开自动解压：服务器会 gzip 响应，不解压拿到的就是二进制乱码。
            if (handler == null)
                handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(20);
            _domains = domains != null ? new List<string>(domains) : new List<string>(DefaultApiDomains);
            _nowUnixSeconds = nowProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        /// <summary>当前使用的 API 域名列表（按尝试顺序）。</summary>
        public IReadOnlyList<string> Domains => _domains;

        /// <summary>用一组新域名替换当前列表（非空才替换）。</summary>
        public void SetApiDomains(IEnumerable<string> domains)
        {
            if (domains == null) return;
            var list = new List<string>(domains);
            if (list.Count == 0) return;
            _domains.Clear();
            _domains.AddRange(list);
        }

        /// <summary>
        /// 站内搜索。orderBy 传 <see cref="JmSearchOrder"/> 里的值改变排序（null/空=综合，不传 o）。
        /// category 传 <see cref="JmCategory"/> 里的值筛选分类（null/空/0=全部）。
        /// 标签的"包含/排除"直接写进 query：空格分隔表示同时包含，前缀 '-' 表示排除，
        /// 例如 "排球 -人妻 -3D"。
        /// </summary>
        public Task<JmApiResult> SearchAsync(string query, int page = 1, string orderBy = null, string category = null, CancellationToken ct = default)
        {
            var q = new Dictionary<string, string>
            {
                ["main_tag"] = string.IsNullOrEmpty(category) ? "0" : category,
                ["search_query"] = query,
                ["page"] = page.ToString(),
            };
            if (!string.IsNullOrEmpty(orderBy)) q["o"] = orderBy;
            return GetAsync("/search", q, ct);
        }

        public Task<JmApiResult> GetAlbumAsync(int albumId, CancellationToken ct = default)
            => GetAsync("/album", new Dictionary<string, string> { ["id"] = albumId.ToString() }, ct);

        public Task<JmApiResult> GetChapterAsync(int photoId, CancellationToken ct = default)
            => GetAsync("/chapter", new Dictionary<string, string> { ["id"] = photoId.ToString() }, ct);

        /// <summary>对某个 API 路径发 GET，逐个域名尝试，返回解密后的结果。</summary>
        public async Task<JmApiResult> GetAsync(string path, IDictionary<string, string> query, CancellationToken ct = default)
        {
            var qs = BuildQueryString(query);
            Exception last = null;
            var attempts = new List<string>();
            Log($"--- GET {path}{qs}  共 {_domains.Count} 个域名 ---");

            foreach (var domain in _domains)
            {
                ct.ThrowIfCancellationRequested();
                var ts = _nowUnixSeconds();
                var token = JmCrypto.BuildToken(ts);
                var url = "https://" + domain + path + qs;
                var sw = DateTimeOffset.UtcNow;

                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.TryAddWithoutValidation("token", token.Token);
                    req.Headers.TryAddWithoutValidation("tokenparam", token.TokenParam);
                    req.Headers.TryAddWithoutValidation("user-agent", UserAgent);
                    // 不手动加 Accept-Encoding：交给 handler 的 AutomaticDecompression，
                    // 否则 handler 认为你自己处理压缩、就不会自动解压了。

                    try
                    {
                        using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                        {
                            var ms = (int)(DateTimeOffset.UtcNow - sw).TotalMilliseconds;
                            if (!resp.IsSuccessStatusCode)
                            {
                                var line = $"{domain}  HTTP {(int)resp.StatusCode}  ({ms}ms)";
                                attempts.Add(line); Log("  ✗ " + line);
                                last = new HttpRequestException(line);
                                continue;
                            }
                            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            // 连上了但返回的不是 JSON(Cloudflare 诱饵页 / 杀软拦截页 / 运营商劫持)
                            var head = body.TrimStart();
                            if (head.Length == 0 || head[0] != '{')
                            {
                                var snip = head.Length > 120 ? head.Substring(0, 120) : head;
                                snip = snip.Replace('\n', ' ').Replace('\r', ' ');
                                var line = $"{domain}  非JSON({body.Length}字节,{ms}ms): {snip}";
                                attempts.Add(line); Log("  ✗ " + line);
                                last = new JmException(line);
                                continue;
                            }
                            Log($"  ✓ {domain}  OK ({ms}ms)");
                            var result = Decode(body, ts);
                            result.DomainUsed = domain;
                            return result;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        var ms = (int)(DateTimeOffset.UtcNow - sw).TotalMilliseconds;
                        var line = $"{domain}  {Describe(ex)}  ({ms}ms)";
                        attempts.Add(line); Log("  ✗ " + line);
                        last = ex; // 这个域名挂了，换下一个
                    }
                }
            }

            throw new JmException("所有 API 域名都失败了:\n" + string.Join("\n", attempts), last);
        }

        /// <summary>
        /// 从域名服务器(发布页)拉取当前可用的 API 域名列表。多个镜像逐个试，成功即返回。
        /// 失败抛 JmException。
        /// </summary>
        public async Task<List<string>> FetchApiDomainsAsync(CancellationToken ct = default)
        {
            Exception last = null;
            foreach (var url in DomainServerUrls)
            {
                ct.ThrowIfCancellationRequested();
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.TryAddWithoutValidation("user-agent", UserAgent);
                    try
                    {
                        using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                        {
                            if (!resp.IsSuccessStatusCode)
                            {
                                last = new HttpRequestException($"{(int)resp.StatusCode} from {url}");
                                continue;
                            }
                            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var domains = ParseDomainServer(text);
                            if (domains.Count > 0)
                                return domains;
                            last = new JmException("域名服务器返回内容解析为空：" + url);
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        last = ex; // 换下一个镜像
                    }
                }
            }
            throw new JmException("域名服务器(发布页)全部获取失败。", last);
        }

        /// <summary>抓取域名服务器并返回解密后的原始 JSON（调试用，看里面到底有哪些字段/域名）。</summary>
        public async Task<string> FetchDomainServerRawAsync(CancellationToken ct = default)
        {
            Exception last = null;
            foreach (var url in DomainServerUrls)
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.TryAddWithoutValidation("user-agent", UserAgent);
                    try
                    {
                        using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                        {
                            if (!resp.IsSuccessStatusCode) { last = new HttpRequestException($"{(int)resp.StatusCode} {url}"); continue; }
                            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            int i = 0; while (i < text.Length && text[i] > 127) i++;
                            return JmCrypto.DecodeData(text.Substring(i).Trim(), "", JmCrypto.DomainServerSecret);
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException)) { last = ex; }
                }
            }
            throw new JmException("域名服务器全部失败。", last);
        }

        /// <summary>拉取并应用最新域名；成功返回 true，失败返回 false（不抛异常，便于"失败兜底"用）。</summary>
        public async Task<bool> RefreshApiDomainsAsync(CancellationToken ct = default)
        {
            try
            {
                var fresh = await FetchApiDomainsAsync(ct).ConfigureAwait(false);
                SetApiDomains(fresh);
                return fresh.Count > 0;
            }
            catch (JmException)
            {
                return false;
            }
        }

        /// <summary>解码域名服务器返回的文本，取出域名列表。</summary>
        public static List<string> ParseDomainServer(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            // 剥掉开头的非 ASCII 字符(可能有 BOM/噪声)，再去掉首尾空白，剩下应为纯 base64
            int i = 0;
            while (i < text.Length && text[i] > 127) i++;
            text = text.Substring(i).Trim();

            // 域名服务器用空 ts + 专用 secret 解密
            var json = JmCrypto.DecodeData(text, "", JmCrypto.DomainServerSecret);

            var root = JObject.Parse(json);
            var list = new List<string>();
            if (root["Server"] is JArray server)
            {
                foreach (var d in server)
                {
                    var s = (string)d;
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s.Trim());
                }
            }
            return list;
        }

        /// <summary>解析外层 {code,data,error_msg}，并解密 data。</summary>
        private static JmApiResult Decode(string body, long ts)
        {
            var root = JObject.Parse(body);
            var result = new JmApiResult
            {
                Code = (int?)root["code"] ?? -1,
                ErrorMsg = (string)root["error_msg"],
            };

            var raw = (string)root["data"];
            if (!string.IsNullOrEmpty(raw))
                result.Data = JmCrypto.DecodeResponseData(raw, ts);

            return result;
        }

        private static string BuildQueryString(IDictionary<string, string> query)
        {
            if (query == null || query.Count == 0) return string.Empty;
            var sb = new StringBuilder("?");
            bool first = true;
            foreach (var kv in query)
            {
                if (!first) sb.Append('&');
                first = false;
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value ?? string.Empty));
            }
            return sb.ToString();
        }

        public void Dispose() => _http.Dispose();
    }

    public sealed class JmException : Exception
    {
        public JmException(string message, Exception inner = null) : base(message, inner) { }
    }
}
