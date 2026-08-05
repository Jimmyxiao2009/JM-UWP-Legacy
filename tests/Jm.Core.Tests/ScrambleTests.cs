using System.Collections.Generic;
using Jm.Core;
using Xunit;

namespace Jm.Core.Tests;

public class ScrambleTests
{
    [Fact]
    public void SegmentCount_no_scramble_before_220980()
        => Assert.Equal(0, JmImageScramble.GetSegmentCount(220979, "00001"));

    [Fact]
    public void SegmentCount_is_10_between_220980_and_268850()
    {
        Assert.Equal(10, JmImageScramble.GetSegmentCount(220980, "00001"));
        Assert.Equal(10, JmImageScramble.GetSegmentCount(268849, "00001"));
    }

    [Theory]
    [InlineData(300000)]   // x = 10
    [InlineData(500000)]   // x = 8  (>= 421926)
    public void SegmentCount_modern_is_even_and_in_range(int aid)
    {
        int x = aid < JmImageScramble.Scramble421926 ? 10 : 8;
        int n = JmImageScramble.GetSegmentCount(aid, "00001");
        Assert.True(n >= 2 && n <= x * 2, $"n={n} out of [2,{x * 2}]");
        Assert.True(n % 2 == 0, "条带数应为偶数");
    }

    [Fact]
    public void Strips_two_even_halves_is_a_swap()
    {
        var strips = JmImageScramble.GetStrips(2, 10);
        Assert.Equal(2, strips.Count);
        // 输出上半 <= 源下半，输出下半 <= 源上半
        Assert.Equal(new ImageStrip(5, 0, 5).SrcY, strips[0].SrcY);
        Assert.Equal(0, strips[0].DstY);
        Assert.Equal(0, strips[1].SrcY);
        Assert.Equal(5, strips[1].DstY);
    }

    [Theory]
    [InlineData(2, 10)]
    [InlineData(3, 10)]    // 有余数
    [InlineData(8, 1000)]
    [InlineData(10, 1337)] // 余数 7
    [InlineData(6, 999)]
    public void Strips_tile_destination_and_source_exactly(int num, int height)
    {
        var strips = JmImageScramble.GetStrips(num, height);

        // 目标侧必须无缝、无重叠地铺满 [0,height)
        var dst = new List<ImageStrip>(strips);
        dst.Sort((a, b) => a.DstY.CompareTo(b.DstY));
        int cursor = 0;
        int total = 0;
        foreach (var s in dst)
        {
            Assert.Equal(cursor, s.DstY);   // 紧接上一条
            cursor += s.Height;
            total += s.Height;
        }
        Assert.Equal(height, cursor);
        Assert.Equal(height, total);

        // 源侧同样必须恰好覆盖 [0,height) 一次
        var coverage = new bool[height];
        foreach (var s in strips)
            for (int y = s.SrcY; y < s.SrcY + s.Height; y++)
            {
                Assert.InRange(y, 0, height - 1);
                Assert.False(coverage[y], "源像素被覆盖两次");
                coverage[y] = true;
            }
        foreach (var c in coverage) Assert.True(c, "有源像素未被任何条带覆盖");
    }

    [Fact]
    public void Strips_zero_or_one_is_identity()
    {
        foreach (var n in new[] { 0, 1 })
        {
            var s = JmImageScramble.GetStrips(n, 800);
            Assert.Single(s);
            Assert.Equal(0, s[0].SrcY);
            Assert.Equal(0, s[0].DstY);
            Assert.Equal(800, s[0].Height);
        }
    }
}
