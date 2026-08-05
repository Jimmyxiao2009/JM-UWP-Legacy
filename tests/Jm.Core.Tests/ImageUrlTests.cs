using Jm.Core;
using Xunit;

namespace Jm.Core.Tests;

public class ImageUrlTests
{
    [Fact]
    public void Build_composes_media_photos_url()
        => Assert.Equal(
            "https://cdn-msp.jmapiproxy1.cc/media/photos/422866/00001.webp",
            JmImageUrl.Build("cdn-msp.jmapiproxy1.cc", "422866", "00001.webp"));

    [Theory]
    [InlineData("00001.webp", "00001")]
    [InlineData("00012.jpg", "00012")]
    [InlineData("00003", "00003")]   // 没有扩展名时原样返回
    public void NameWithoutExt_strips_extension(string file, string expected)
        => Assert.Equal(expected, JmImageUrl.NameWithoutExt(file));
}
