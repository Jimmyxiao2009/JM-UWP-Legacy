using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Jm.Core
{
    /// <summary>id/数值字段在不同端点上时而是 JSON number、时而是 string，这里统一读成 string。</summary>
    public sealed class FlexibleStringConverter : JsonConverter<string>
    {
        public override string ReadJson(JsonReader reader, Type t, string existing, bool hasExisting, JsonSerializer s)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null: return null;
                case JsonToken.String: return (string)reader.Value;
                default: return reader.Value?.ToString();
            }
        }
        public override void WriteJson(JsonWriter w, string v, JsonSerializer s) => w.WriteValue(v);
    }

    /// <summary>搜索排序方式（API 的 o 参数值）。空/null 表示“综合”（不传 o）。</summary>
    public static class JmSearchOrder
    {
        public const string Latest = "mr";   // 最新
        public const string View = "mv";     // 最多点阅
        public const string Like = "tf";     // 最多爱心
        public const string Picture = "mp";  // 最多图片
    }

    /// <summary>搜索结果页。</summary>
    public sealed class JmSearchPage
    {
        [JsonProperty("search_query")] public string Query { get; set; }
        [JsonProperty("total"), JsonConverter(typeof(FlexibleStringConverter))] public string Total { get; set; }
        [JsonProperty("content")] public List<JmAlbumStub> Content { get; set; } = new();
    }

    /// <summary>列表里的专辑摘要。</summary>
    public sealed class JmAlbumStub
    {
        [JsonProperty("id"), JsonConverter(typeof(FlexibleStringConverter))] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("author")] public string Author { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        /// <summary>封面地址，供列表/网格直接绑定显示。</summary>
        [JsonIgnore] public string CoverUrl => JmImageUrl.CoverDefault(Id);
    }

    /// <summary>专辑详情。</summary>
    public sealed class JmAlbumDetail
    {
        [JsonProperty("id"), JsonConverter(typeof(FlexibleStringConverter))] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("author")] public List<string> Author { get; set; } = new();
        [JsonProperty("tags")] public List<string> Tags { get; set; } = new();
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("total_views"), JsonConverter(typeof(FlexibleStringConverter))] public string TotalViews { get; set; }
        [JsonProperty("likes"), JsonConverter(typeof(FlexibleStringConverter))] public string Likes { get; set; }
        [JsonProperty("series")] public List<JmSeries> Series { get; set; } = new();
        /// <summary>单章专辑会直接带图片名列表；多章专辑图片在各 chapter 里。</summary>
        [JsonProperty("images")] public List<string> Images { get; set; } = new();
        [JsonIgnore] public string CoverUrl => JmImageUrl.CoverDefault(Id);
    }

    /// <summary>章节(话)。</summary>
    public sealed class JmSeries
    {
        [JsonProperty("id"), JsonConverter(typeof(FlexibleStringConverter))] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("sort"), JsonConverter(typeof(FlexibleStringConverter))] public string Sort { get; set; }
    }

    /// <summary>章节详情(含图片文件名列表)。</summary>
    public sealed class JmChapter
    {
        [JsonProperty("id"), JsonConverter(typeof(FlexibleStringConverter))] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("series")] public List<JmSeries> Series { get; set; } = new();
        [JsonProperty("images")] public List<string> Images { get; set; } = new();
    }

    /// <summary>把 JmApiResult.Data 解析成强类型。</summary>
    public static class JmModels
    {
        public static JmSearchPage AsSearchPage(this JmApiResult r) => Deserialize<JmSearchPage>(r);
        public static JmAlbumDetail AsAlbum(this JmApiResult r) => Deserialize<JmAlbumDetail>(r);
        public static JmChapter AsChapter(this JmApiResult r) => Deserialize<JmChapter>(r);

        private static T Deserialize<T>(JmApiResult r)
            => r?.Data == null ? default : JsonConvert.DeserializeObject<T>(r.Data);
    }
}
