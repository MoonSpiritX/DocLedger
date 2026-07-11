namespace Ava.Logic.OfficialDocument;

/// <summary>
/// 扫描结果：每个文件夹输出一条结构化数据（台账的一行）
/// </summary>
public class OfficialDocumentInfo
{
    /// <summary>文件夹路径（相对源文件夹）</summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>公文正文文件名</summary>
    public string MainDocument { get; set; } = string.Empty;

    /// <summary>文号（如 "银河系〔3026〕1号"）</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>公文日期（从文件夹名/文件名提取）</summary>
    public string DocumentDate { get; set; } = string.Empty;

    /// <summary>发文单位（从文号前缀提取）</summary>
    public string IssuingUnit { get; set; } = string.Empty;

    /// <summary>附件列表（按文件名排序）</summary>
    public List<string> Attachments { get; set; } = new();

    /// <summary>杂音文件列表（仅记录供参考，不入台账）</summary>
    public List<string> NoiseFiles { get; set; } = new();

    /// <summary>文件大小</summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// 用于排序的日期（首次访问时解析，空值用最大日期兜底排最后）
    /// </summary>
    public DateTime SortDate
    {
        get
        {
            if (_sortDate.HasValue) return _sortDate.Value;

            if (!string.IsNullOrEmpty(DocumentDate))
            {
                // 支持格式：2026-04-16、2026年4月16日、2026/04/16
                var d = DocumentDate
                    .Replace("年", "-").Replace("月", "-").Replace("日", "")
                    .Replace("/", "-");

                if (DateTime.TryParse(d, out var dt))
                {
                    _sortDate = dt;
                    return dt;
                }
            }

            // 无日期排到最后
            _sortDate = DateTime.MaxValue;
            return DateTime.MaxValue;
        }
    }
    private DateTime? _sortDate;
}
