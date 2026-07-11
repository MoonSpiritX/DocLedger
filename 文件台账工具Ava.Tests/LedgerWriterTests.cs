using Xunit;
using Ava.Logic.OfficialDocument;

namespace 文件台账工具Ava.Tests;

/// <summary>
/// LedgerWriter 单元测试
/// 测试字段取值（GetFieldValue）和去括号（StripOuterBrackets）两个静态方法。
/// 它们不依赖文件系统，纯逻辑，最适合作为单元测试。
/// </summary>
public class LedgerWriterTests
{
    private static OfficialDocumentInfo CreateTestDoc() => new()
    {
        DocumentDate = "2026-04-16",
        IssuingUnit = "银河系安全局",
        DocumentNumber = "银河系〔3026〕1号",
        MainDocument = "关于进一步加强安全生产管理的通知.docx",
        Attachments = new List<string> { "附件1_名单.xlsx", "附件2_方案.pdf" },
        FileSize = "1.2 MB",
        FolderPath = "银河系〔3026〕1号",
    };

    [Fact] public void GetFieldValue_FaWenDate_返回公文日期()
        => Assert.Equal("2026-04-16", LedgerWriter.GetFieldValue(CreateTestDoc(), "FaWenDate"));

    [Fact] public void GetFieldValue_ShouWenDate_返回公文日期()
        => Assert.Equal("2026-04-16", LedgerWriter.GetFieldValue(CreateTestDoc(), "ShouWenDate"));

    [Fact] public void GetFieldValue_FaWenUnit_返回发文单位()
        => Assert.Equal("银河系安全局", LedgerWriter.GetFieldValue(CreateTestDoc(), "FaWenUnit"));

    [Fact] public void GetFieldValue_LaiWenUnit_返回发文单位()
        => Assert.Equal("银河系安全局", LedgerWriter.GetFieldValue(CreateTestDoc(), "LaiWenUnit"));

    [Fact] public void GetFieldValue_FileNumber_返回文号()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.GetFieldValue(CreateTestDoc(), "FileNumber"));

    [Fact]
    public void GetFieldValue_FileNumber_文号带括号_应去掉最外层()
    {
        var doc = new OfficialDocumentInfo { DocumentNumber = "【银河系〔3026〕1号】" };
        Assert.Equal("银河系〔3026〕1号", LedgerWriter.GetFieldValue(doc, "FileNumber"));
    }

    [Fact] public void GetFieldValue_FileName_返回公文文件名()
        => Assert.Equal("关于进一步加强安全生产管理的通知.docx", LedgerWriter.GetFieldValue(CreateTestDoc(), "FileName"));

    [Fact]
    public void GetFieldValue_Attachments_返回附件列表中文号分隔()
        => Assert.Equal("附件1_名单.xlsx；附件2_方案.pdf", LedgerWriter.GetFieldValue(CreateTestDoc(), "Attachments"));

    [Fact]
    public void GetFieldValue_Attachments_无附件_返回空()
        => Assert.Equal("", LedgerWriter.GetFieldValue(new OfficialDocumentInfo(), "Attachments"));

    [Fact] public void GetFieldValue_FileSize_返回文件大小()
        => Assert.Equal("1.2 MB", LedgerWriter.GetFieldValue(CreateTestDoc(), "FileSize"));

    [Fact] public void GetFieldValue_Location_返回文件夹路径()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.GetFieldValue(CreateTestDoc(), "Location"));

    [Fact] public void GetFieldValue_XuHao_返回行号()
        => Assert.Equal("5", LedgerWriter.GetFieldValue(CreateTestDoc(), "XuHao", rowIndex: 5));

    [Fact] public void GetFieldValue_XuHao_默认行号为0()
        => Assert.Equal("0", LedgerWriter.GetFieldValue(CreateTestDoc(), "XuHao"));

    [Fact] public void GetFieldValue_未知字段_返回空()
        => Assert.Equal("", LedgerWriter.GetFieldValue(CreateTestDoc(), "NonExistentField"));

    // ══════════════════════════════════════════
    //  StripOuterBrackets 去括号测试
    // ══════════════════════════════════════════

    [Fact] public void StripOuterBrackets_中文方括号_应去掉()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.StripOuterBrackets("【银河系〔3026〕1号】"));

    [Fact] public void StripOuterBrackets_中文圆括号_应去掉()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.StripOuterBrackets("（银河系〔3026〕1号）"));

    [Fact] public void StripOuterBrackets_英文圆括号_应去掉()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.StripOuterBrackets("(银河系〔3026〕1号)"));

    [Fact] public void StripOuterBrackets_花括号_应去掉()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.StripOuterBrackets("{银河系〔3026〕1号}"));

    [Fact] public void StripOuterBrackets_无括号_原样返回()
        => Assert.Equal("银河系〔3026〕1号", LedgerWriter.StripOuterBrackets("银河系〔3026〕1号"));

    [Fact] public void StripOuterBrackets_空字符串_返回空()
        => Assert.Equal("", LedgerWriter.StripOuterBrackets(""));

    [Fact] public void StripOuterBrackets_只有一个字符_原样返回()
        => Assert.Equal("【", LedgerWriter.StripOuterBrackets("【"));

    [Fact] public void StripOuterBrackets_左右不匹配_原样返回()
        => Assert.Equal("【银河系", LedgerWriter.StripOuterBrackets("【银河系"));
}
