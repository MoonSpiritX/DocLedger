using Xunit;
using Ava.Services;

namespace 文件台账工具Ava.Tests;

/// <summary>
/// Services_OfficialDocument 别名匹配测试。
/// 测试 MatchField 方法能否正确匹配各种中文别名，
/// 包括含空格、全半角差异、部分匹配等边界情况。
/// </summary>
public class Services_OfficialDocumentTests
{
    // ── 发文 ──
    [Fact] public void MatchField_发文日期_匹配() => Assert.Equal("FaWenDate", Services_OfficialDocument.MatchField("发文日期"));
    [Fact] public void MatchField_发文时间_匹配() => Assert.Equal("FaWenDate", Services_OfficialDocument.MatchField("发文时间"));

    // ── 收文 ──
    [Fact] public void MatchField_来文单位_匹配() => Assert.Equal("LaiWenUnit", Services_OfficialDocument.MatchField("来文单位"));
    [Fact] public void MatchField_收文日期_匹配() => Assert.Equal("ShouWenDate", Services_OfficialDocument.MatchField("收文日期"));

    // ── 通用 ──
    [Fact] public void MatchField_文号_匹配FileNumber() => Assert.Equal("FileNumber", Services_OfficialDocument.MatchField("文号"));
    [Fact] public void MatchField_发文字号_匹配FileNumber() => Assert.Equal("FileNumber", Services_OfficialDocument.MatchField("发文字号"));
    [Fact] public void MatchField_文件名称_匹配FileName() => Assert.Equal("FileName", Services_OfficialDocument.MatchField("文件名称"));
    [Fact] public void MatchField_标题_匹配FileName() => Assert.Equal("FileName", Services_OfficialDocument.MatchField("标题"));
    [Fact] public void MatchField_附件_匹配Attachments() => Assert.Equal("Attachments", Services_OfficialDocument.MatchField("附件"));
    [Fact] public void MatchField_文件大小_匹配FileSize() => Assert.Equal("FileSize", Services_OfficialDocument.MatchField("文件大小"));
    [Fact] public void MatchField_路径_匹配Location() => Assert.Equal("Location", Services_OfficialDocument.MatchField("路径"));
    [Fact] public void MatchField_序号_匹配XuHao() => Assert.Equal("XuHao", Services_OfficialDocument.MatchField("序号"));

    // ── 边界 ──
    [Fact] public void MatchField_含空格_应归一化后匹配() => Assert.Equal("FaWenDate", Services_OfficialDocument.MatchField(" 发文 日期 "));
    [Fact] public void MatchField_全角空格_应归一化后匹配() => Assert.Equal("FaWenDate", Services_OfficialDocument.MatchField("发文　日期"));
    [Fact] public void MatchField_为空_返回空() => Assert.Null(Services_OfficialDocument.MatchField(""));
    [Fact] public void MatchField_为Null_返回空() => Assert.Null(Services_OfficialDocument.MatchField(null!));
    [Fact] public void MatchField_未知列名_返回空() => Assert.Null(Services_OfficialDocument.MatchField("完全没有匹配的列名"));
    [Fact] public void MatchField_文件编号_部分匹配() => Assert.Equal("FileNumber", Services_OfficialDocument.MatchField("文件编号"));
}
