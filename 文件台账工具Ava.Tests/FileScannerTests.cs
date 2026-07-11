using Xunit;
using Ava.Logic.OfficialDocument;

namespace 文件台账工具Ava.Tests;

/// <summary>
/// FileScanner 单元测试：
/// 通过创建临时目录和文件来验证文件分类、文号提取、日期提取等核心逻辑。
/// 每个测试方法都是独立的：创建临时目录 → 执行扫描 → 断言结果 → 清理。
/// </summary>
public class FileScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public FileScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"AvaTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ══════════════════════════════════════════
    //  一、文件分类测试
    // ══════════════════════════════════════════

    [Fact]
    public void ScanFolder_有Docx正文_应识别为主公文()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("银河系〔3026〕1号.docx", result!.MainDocument);
    }

    [Fact]
    public void ScanFolder_有Pdf正文_应识别为主公文()
    {
        var folder = CreateFolder("关于安全通知的通报");
        CreateFile(folder, "关于安全通知的通报.pdf");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("关于安全通知的通报.pdf", result!.MainDocument);
    }

    [Fact]
    public void ScanFolder_仅图片文件_应返回空()
    {
        var folder = CreateFolder("杂项");
        CreateFile(folder, "IMG_20260416.jpg");
        CreateFile(folder, "文档_20260416_001.png");
        Assert.Null(new FileScanner().ScanFolder(folder));
    }

    [Fact]
    public void ScanFolder_有附件以附件开头_应正确分类()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        CreateFile(folder, "附件1_名单.xlsx");
        CreateFile(folder, "附件2_方案.pdf");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Attachments.Count);
        Assert.Contains(result.Attachments, a => a.Contains("附件1"));
        Assert.Contains(result.Attachments, a => a.Contains("附件2"));
    }

    [Fact]
    public void ScanFolder_同名不同格式_应优先Docx()
    {
        var folder = CreateFolder("关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.doc");
        CreateFile(folder, "关于安全培训的通知.docx");
        CreateFile(folder, "关于安全培训的通知.pdf");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.EndsWith(".docx", result!.MainDocument);
    }

    [Fact]
    public void ScanFolder_文件名包含关于_的_句式_应识别为主公文()
    {
        var folder = CreateFolder("通知文件");
        CreateFile(folder, "关于进一步加强安全生产管理的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("关于进一步加强安全生产管理的通知.docx", result!.MainDocument);
    }

    [Fact]
    public void ScanFolder_杂音文件IMG_和文档_应被排除()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        CreateFile(folder, "IMG_20260416_001.jpg");
        CreateFile(folder, "文档_20260416_001.png");
        CreateFile(folder, "上级文件处理单_扫描件.jpg");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("银河系〔3026〕1号.docx", result!.MainDocument);
    }

    [Fact]
    public void ScanFolder_空文件夹_应返回空()
    {
        Assert.Null(new FileScanner().ScanFolder(CreateFolder("空文件夹")));
    }

    [Fact]
    public void ScanFolder_附件开头图片_应视为附件非杂音()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        CreateFile(folder, "附件1_现场照片.jpg");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Single(result!.Attachments);
        Assert.Contains("附件1", result.Attachments[0]);
    }

    // ══════════════════════════════════════════
    //  二、文号提取测试
    // ══════════════════════════════════════════

    [Fact]
    public void ExtractDocumentNumber_文件夹名包含完整文号_应正确提取()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("银河系〔3026〕1号", result!.DocumentNumber);
    }

    [Fact]
    public void ExtractDocumentNumber_带已知前缀_应提取完整文号()
    {
        var folder = CreateFolder("银河系〔3026〕15号");
        // 文件名包含已知前缀 + 文号模式 → 被识别为主公文+提取完整文号
        CreateFile(folder, "银河系〔3026〕15号.docx");
        var results = new FileScanner().ScanFolders(new[] { folder }, new List<string> { "银河系" });
        Assert.Single(results);
        Assert.Equal("银河系〔3026〕15号", results[0].DocumentNumber);
    }

    [Fact]
    public void ExtractDocumentNumber_仅有年份序号无前缀_应返回核心部分()
    {
        var folder = CreateFolder("〔3026〕1号");
        CreateFile(folder, "〔3026〕1号.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("〔3026〕1号", result!.DocumentNumber);
    }

    [Fact]
    public void ExtractDocumentNumber_无文号模式_应为空()
    {
        var folder = CreateFolder("关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("", result!.DocumentNumber);
    }

    [Fact]
    public void ExtractDocumentNumber_后缀为号无序号_应正确提取()
    {
        var folder = CreateFolder("文件〔2026〕号");
        CreateFile(folder, "文件〔2026〕号.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("文件〔2026〕号", result!.DocumentNumber);
    }

    // ══════════════════════════════════════════
    //  三、日期提取测试
    // ══════════════════════════════════════════

    [Fact]
    public void ExtractDate_从文件夹名提取横线日期()
    {
        var folder = CreateFolder("2026-04-16 关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("2026-04-16", result!.DocumentDate);
    }

    [Fact]
    public void ExtractDate_从文件夹名提取点号日期()
    {
        var folder = CreateFolder("2026.3.2 关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("2026-3-2", result!.DocumentDate);
    }

    [Fact]
    public void ExtractDate_从文件夹名提取中文日期()
    {
        var folder = CreateFolder("2026年4月16日 关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("2026-4-16", result!.DocumentDate);
    }

    [Fact]
    public void ExtractDate_从文件夹名提取8位数字日期()
    {
        var folder = CreateFolder("20260416 关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("2026-04-16", result!.DocumentDate);
    }

    [Fact]
    public void ExtractDate_无日期_应为空()
    {
        var folder = CreateFolder("关于安全培训的通知");
        CreateFile(folder, "关于安全培训的通知.docx");
        var result = new FileScanner().ScanFolder(folder);
        Assert.NotNull(result);
        Assert.Equal("", result!.DocumentDate);
    }

    // ══════════════════════════════════════════
    //  四、整体扫描流程测试
    // ══════════════════════════════════════════

    [Fact]
    public void ScanFolders_容器文件夹含多个子公文_应全部识别()
    {
        var container = CreateFolder("2026年公文");
        var sub1 = CreateFolder(container, "银河系〔3026〕1号");
        var sub2 = CreateFolder(container, "银河系〔3026〕2号");
        CreateFile(sub1, "银河系〔3026〕1号.docx");
        CreateFile(sub2, "银河系〔3026〕2号.docx");
        Assert.Equal(2, new FileScanner().ScanFolders(new[] { container }).Count);
    }

    [Fact]
    public void ScanFolders_前缀匹配_单位名应被覆盖()
    {
        var folder = CreateFolder("银河系〔3026〕1号");
        CreateFile(folder, "银河系〔3026〕1号.docx");
        var results = new FileScanner().ScanFolders(new[] { folder }, new List<string> { "银河系" });
        Assert.Single(results);
        Assert.Equal("银河系", results[0].IssuingUnit);
    }

    // ══════════════════════════════════════════
    //  辅助方法
    // ══════════════════════════════════════════

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFolder(string parent, string name)
    {
        var path = Path.Combine(parent, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "");
        return path;
    }
}
