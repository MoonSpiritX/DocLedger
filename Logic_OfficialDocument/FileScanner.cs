using System.Text.RegularExpressions;

namespace Ava.Logic.OfficialDocument;

/// <summary>
/// 文件扫描器：遍历源文件夹 → 智能分类 → 结构化信息提取
/// 全部基于通用结构和格式特征，不依赖具体文字内容
/// </summary>
public partial class FileScanner
{
    /// <summary>已知文号前缀列表（用户配置）</summary>
    private List<string>? _knownPrefixes;

    /// <summary>
    /// 扫描单个文件夹，返回该文件夹的结构化公文信息
    /// </summary>
    public OfficialDocumentInfo? ScanFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return null;

        var dirInfo = new DirectoryInfo(folderPath);
        var allFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories);

        if (allFiles.Length == 0)
            return null;

        var folderName = dirInfo.Name;
        var relativePath = GetRelativePath(folderPath);

        // 分类所有文件
        var classified = ClassifyFiles(allFiles, folderName);

        if (classified.MainDocuments.Count == 0 && classified.Attachments.Count == 0)
            return null;

        // 提取结构化信息
        var documentNumber = ExtractDocumentNumber(folderName, classified.MainDocuments.FirstOrDefault()?.Name ?? "");
        var documentDate = ExtractDate(folderName, classified.MainDocuments.FirstOrDefault()?.Name ?? "");
        var issuingUnit = ExtractIssuingUnit(folderName, documentNumber);
        var fileSize = classified.MainDocuments.Count > 0
            ? FormatFileSize(classified.MainDocuments[0].Length)
            : "";

        return new OfficialDocumentInfo
        {
            FolderPath = relativePath,
            MainDocument = classified.MainDocuments.Count > 0
                ? classified.MainDocuments[0].Name
                : "",
            DocumentNumber = documentNumber,
            DocumentDate = documentDate,
            IssuingUnit = issuingUnit,
            Attachments = classified.Attachments.OrderBy(f => f.Name).Select(f => f.Name).ToList(),
            NoiseFiles = classified.NoiseFiles.Select(f => f.Name).ToList(),
            FileSize = fileSize,
        };
    }

    /// <summary>
    /// 扫描多个文件夹：遍历每个源文件夹的子目录，逐个识别公文
    /// </summary>
    public List<OfficialDocumentInfo> ScanFolders(IEnumerable<string> folderPaths)
    {
        return ScanFolders(folderPaths, null);
    }

    /// <summary>
    /// 扫描多个文件夹，支持传入已知文号前缀列表进行过滤
    /// </summary>
    public List<OfficialDocumentInfo> ScanFolders(IEnumerable<string> folderPaths, List<string>? knownPrefixes)
    {
        _knownPrefixes = knownPrefixes?.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var results = new List<OfficialDocumentInfo>();
        foreach (var path in folderPaths)
        {
            if (!Directory.Exists(path))
                continue;

            // 先尝试将当前文件夹本身作为公文扫描
            // （用户添加的是单个公文文件夹时，主文件直接放在根下）
            var docInfo = ScanFolder(path);
            if (docInfo != null)
            {
                results.Add(docInfo);
            }
            else
            {
                // 扫描失败（如容器文件夹），则递归扫描子目录
                var dirInfo = new DirectoryInfo(path);
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    ScanDirectoryRecursive(subDir.FullName, results);
                }
            }
        }
        return results;
    }

    /// <summary>
    /// 递归扫描目录：尝试作为公文识别，失败则继续深入子目录
    /// </summary>
    private void ScanDirectoryRecursive(string dirPath, List<OfficialDocumentInfo> results)
    {
        var dirInfo = new DirectoryInfo(dirPath);

        // 跳过无意义目录
        if (IsNoiseFolder(dirInfo.Name))
            return;

        // 尝试将当前目录作为公文处理
        var docInfo = ScanFolder(dirPath);
        if (docInfo != null)
        {
            results.Add(docInfo);
        }
        else
        {
            // 识别失败，继续深入子目录
            foreach (var subDir in dirInfo.GetDirectories())
            {
                ScanDirectoryRecursive(subDir.FullName, results);
            }
        }
    }

    // ────────────────────── 文件分类 ──────────────────────
    private ClassificationResult ClassifyFiles(FileInfo[] files, string folderName)
    {
        var result = new ClassificationResult();

        foreach (var file in files)
        {
            var ext = file.Extension.ToLowerInvariant();
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
            // 杂音文件检测
            if (IsNoiseFile(file.Name))
            {
                result.NoiseFiles.Add(file);
                continue;
            }
            // 附件检测
            if (IsAttachment(nameWithoutExt))
            {
                result.Attachments.Add(file);
                continue;
            }
            // 公文正文检测
            if (IsMainDocument(ext, nameWithoutExt, folderName))
            {
                result.MainDocuments.Add(file);
            }
        }
        // 同名去重：同名的 .doc / .docx / .pdf 只保留一个
        DeduplicateMainDocuments(result.MainDocuments);

        return result;
    }

    private class ClassificationResult
    {
        public List<FileInfo> MainDocuments { get; set; } = new();
        public List<FileInfo> Attachments { get; set; } = new();
        public List<FileInfo> NoiseFiles { get; set; } = new();
    }

    // ── 杂音文件判定 ──
    private static readonly string[] NoiseExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

    private static bool IsNoiseFolder(string folderName)
    {
        return folderName == "新建文件夹" || folderName.StartsWith("新建文件夹");
    }

    private static bool IsNoiseFile(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // IMG_xxxx 照片
        if (name.StartsWith("IMG_", StringComparison.OrdinalIgnoreCase))
            return true;

        // 扫描仪输出：文档_日期_时间_xxx
        if (name.StartsWith("文档_", StringComparison.OrdinalIgnoreCase))
            return true;

        // 上级文件处理单（扫描件/照片）
        if (name.Contains("上级文件处理单"))
            return true;

        // 纯图片文件（无其他内容特征时）
        if (NoiseExtensions.Contains(ext))
        {
            // 如果文件名以"附件"开头，交给 IsAttachment 判断，视为附件
            if (name.StartsWith("附件", StringComparison.Ordinal))
                return false;
            // 否则视为杂音（如随意照片、扫描件等）
            return true;
        }

        return false;
    }

    // ── 附件判定 ──

    private static bool IsAttachment(string nameWithoutExt)
    {
        return nameWithoutExt.StartsWith("附件", StringComparison.Ordinal);
    }

    // ── 公文正文判定 ──
    private static readonly string[] DocumentExtensions = { ".doc", ".docx", ".pdf" };

    private bool IsMainDocument(string extension, string nameWithoutExt, string folderName)
    {
        // 1. 扩展名必须在文档格式范围内
        if (!DocumentExtensions.Contains(extension))
            return false;

        // 2. 排除附件
        if (nameWithoutExt.StartsWith("附件", StringComparison.Ordinal))
            return false;

        // 3. 文件名匹配已知文号前缀 → 确定为主公文（最高优先级）
        if (_knownPrefixes != null && _knownPrefixes.Count > 0)
        {
            foreach (var prefix in _knownPrefixes)
            {
                if (nameWithoutExt.Contains(prefix) && ExtractDocPattern().IsMatch(nameWithoutExt))
                    return true;
            }
        }

        // 4. 文件名包含"关于…的…"句式（公文典型特征）
        if (ContainsDocumentPattern(nameWithoutExt))
            return true;

        // 5. 文件名与文件夹名相同（去扩展名后）
        if (string.Equals(nameWithoutExt, folderName, StringComparison.Ordinal))
            return true;

        // 5. 文件名包含文件夹名（文件夹名带日期前缀时）
        if (folderName.Length > 10 && nameWithoutExt.Contains(folderName.Substring(10), StringComparison.Ordinal))
            return true;

        // 6. 文件名包含文件夹名中去掉日期前缀后的部分
        var folderNameTrimmed = TrimDatePrefix(folderName);
        if (!string.IsNullOrEmpty(folderNameTrimmed) &&
            nameWithoutExt.Contains(folderNameTrimmed, StringComparison.Ordinal))
            return true;

        return false;
    }

    /// <summary>检测文件名是否包含"关于…的…"公文句式</summary>
    private static bool ContainsDocumentPattern(string name)
    {
        return name.Contains("关于") && name.Contains("的");
    }

    /// <summary>去重：同名不同格式的正文文件，优先级 docx > doc > pdf</summary>
    private static void DeduplicateMainDocuments(List<FileInfo> mainDocs)
    {
        if (mainDocs.Count <= 1) return;

        // 按文件名（去扩展名）分组
        var groups = mainDocs
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Name))
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            // 保留优先级最高的，移除其余的
            var best = group.OrderBy(f => GetFormatPriority(f.Extension)).First();
            var toRemove = group.Where(f => f != best).ToList();
            foreach (var r in toRemove)
                mainDocs.Remove(r);
        }
    }

    private static int GetFormatPriority(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".docx" => 0,
            ".doc" => 1,
            ".pdf" => 2,
            _ => 99,
        };
    }

    // ────────────────────── 信息提取 ──────────────────────

    // 核心文号模式：〔年份〕序号号?（序号可以是数字/x/n/×，也可为空如"〔2025〕号"）
    [GeneratedRegex(@"〔\d+〕[0-9xXa-zA-Z×]*号?")]
    private static partial Regex PlainDocNumber();

    // 文件名中匹配文号（用于判定主公文）
    [GeneratedRegex(@"[^〔〕\s（【）】]*〔\d+〕[0-9xXa-zA-Z×]*号?")]
    private static partial Regex ExtractDocPattern();

    /// <summary>
    /// 构造文号：用用户前缀 + 检测到的〔年份〕序号
    /// 不依赖于外层括号，输出始终干净
    /// </summary>
    private string ExtractDocumentNumber(string folderName, string fileName)
    {
        var docNum = TryConstruct(folderName);
        if (!string.IsNullOrEmpty(docNum)) return docNum;

        docNum = TryConstruct(fileName);
        return docNum ?? "";
    }

    /// <summary>尝试在文本中构造文号</summary>
    private string? TryConstruct(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // 找核心模式：〔年份〕序号号?
        var match = PlainDocNumber().Match(text);
        if (!match.Success) return null;

        var core = match.Value;            // 如 "〔2026〕19号"
        var before = text[..match.Index];   // 核心之前的所有文字

        // 1. 优先用已知前缀
        if (_knownPrefixes != null && _knownPrefixes.Count > 0)
        {
            foreach (var prefix in _knownPrefixes)
            {
                if (before.Contains(prefix))
                    return $"{prefix}{core}";
            }
        }

        // 2. 从核心往前找单位前缀（不含任何中文括号的连续文字，最长20字）
        var prefixMatch = Regex.Match(before, @"[^〔〕\s（【）】]+$");
        if (prefixMatch.Success)
        {
            var prefix = prefixMatch.Value;
            if (prefix.Length <= 20 && prefix.Length > 0)
                return $"{prefix}{core}";
        }

        // 3. 实在找不到前缀，只返回核心部分
        return core;
    }

    /// <summary>从文件夹名或文件名中提取日期</summary>
    private static string ExtractDate(string folderName, string fileName)
    {
        // 优先从文件夹名提取
        var date = ExtractDateFromString(folderName);
        if (!string.IsNullOrEmpty(date))
            return date;

        // 备选文件名
        return ExtractDateFromString(fileName);
    }

    /// <summary>从字符串中提取日期（支持多种格式）</summary>
    private static string ExtractDateFromString(string text)
    {
        // 格式1：2026-04-16 或 2026/04/16
        var m1 = DatePattern1().Match(text);
        if (m1.Success) return m1.Value;

        // 格式4：2026.3.2 / 2026.03.20 / 2026.3.20（点号分隔，月日可为1~2位）
        var m4 = DatePattern4().Match(text);
        if (m4.Success)
        {
            var v = m4.Value;
            // 把点号统一转横线
            return v.Replace(".", "-");
        }

        // 格式2：20260416（8位纯数字，在括号外）
        var m2 = DatePattern2().Match(text);
        if (m2.Success)
        {
            var v = m2.Value;
            return $"{v[..4]}-{v[4..6]}-{v[6..8]}";
        }

        // 格式3：2026年4月16日
        var m3 = DatePattern3().Match(text);
        if (m3.Success) return m3.Value.Replace("年", "-").Replace("月", "-").Replace("日", "");

        return "";
    }

    [GeneratedRegex(@"\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b")]
    private static partial Regex DatePattern1();

    [GeneratedRegex(@"(?<!〔)\b\d{8}\b(?!〕)")]
    private static partial Regex DatePattern2();

    [GeneratedRegex(@"\d{4}年\d{1,2}月\d{1,2}日")]
    private static partial Regex DatePattern3();

    /// <summary>2026.3.2 / 2026.03.20 / 2026.3.20（点号分隔）</summary>
    [GeneratedRegex(@"\b\d{4}\.\d{1,2}\.\d{1,2}\b")]
    private static partial Regex DatePattern4();

    /// <summary>提取发文单位（优先用已知前缀，备选从文号前缀提取）</summary>
    private string ExtractIssuingUnit(string folderName, string documentNumber)
    {
        // 1. 已知前缀优先
        if (_knownPrefixes != null && _knownPrefixes.Count > 0)
        {
            foreach (var prefix in _knownPrefixes)
            {
                if (folderName.Contains(prefix))
                    return prefix;
            }
        }

        // 2. 从文号中提取（documentNumber 已是干净格式：前缀〔年份〕序号号）
        var match = UnitPrefixPattern().Match(documentNumber);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return "";
    }

    /// <summary>从文号中提取前缀部分</summary>
    [GeneratedRegex(@"^(.+?)〔\d+〕")]
    private static partial Regex UnitPrefixPattern();

    // ── 辅助方法 ──

    private static string TrimDatePrefix(string folderName)
    {
        // 去掉开头的日期前缀，如 "2026-04-16 xxx" → "xxx"
        var m = DatePrefixPattern().Match(folderName);
        return m.Success ? m.Groups[1].Value.Trim() : folderName;
    }

    [GeneratedRegex(@"^\d{4}[-/]\d{1,2}[-/]\d{1,2}\s+(.+)")]
    private static partial Regex DatePrefixPattern();

    private static string GetRelativePath(string fullPath)
    {
        // 简化实现：返回文件夹名
        return Path.GetFileName(fullPath);
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
        };
    }
}
