using Jm.Core;
using Newtonsoft.Json;
using Xunit;

namespace Jm.Core.Tests;

public class ModelTests
{
    [Fact]
    public void Search_page_parses_with_numeric_and_string_ids()
    {
        // 真实接口里 id 可能是数字也可能是字符串，两种都要兼容
        var json = """
        {
          "search_query": "love",
          "total": 42,
          "content": [
            { "id": 123456, "name": "A", "author": "x" },
            { "id": "654321", "name": "B", "author": "y" }
          ]
        }
        """;
        var page = JsonConvert.DeserializeObject<JmSearchPage>(json);

        Assert.Equal("love", page.Query);
        Assert.Equal("42", page.Total);
        Assert.Equal(2, page.Content.Count);
        Assert.Equal("123456", page.Content[0].Id);   // 数字 -> 字符串
        Assert.Equal("654321", page.Content[1].Id);   // 字符串原样
    }

    [Fact]
    public void Album_detail_parses_series_and_images()
    {
        var json = """
        {
          "id": 999,
          "name": "标题",
          "author": ["作者甲", "作者乙"],
          "tags": ["t1", "t2"],
          "likes": "100",
          "series": [ { "id": "1001", "name": "第1话", "sort": "1" } ],
          "images": ["00001.webp", "00002.webp"]
        }
        """;
        var a = JsonConvert.DeserializeObject<JmAlbumDetail>(json);

        Assert.Equal("999", a.Id);
        Assert.Equal("标题", a.Name);
        Assert.Equal(2, a.Author.Count);
        Assert.Single(a.Series);
        Assert.Equal("第1话", a.Series[0].Name);
        Assert.Equal(2, a.Images.Count);
    }
}
