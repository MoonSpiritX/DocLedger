namespace Ava.Services;

/// <summary>
/// 公文台账业务字典：标准名 → 别名列表
/// 发文和收文台账共用同一套字典，通过实际匹配到的列名自动区分模式
/// </summary>
public static class Services_OfficialDocument
{
    /// <summary>
    /// 获取所有标准名到别名列表的映射
    /// </summary>
    public static readonly Dictionary<string, string[]> Aliases = new()
    {
        // ── 发文相关 ──
        ["FaWenDate"] = new[] { "发文日期", "发文时间", "审批时间" },
        ["FaWenUnit"] = new[] { "发文单位", "发文机关", "发文部门" },

        // ── 收文相关 ──
        ["ShouWenDate"] = new[] { "收文日期", "收文时间", "来文日期" },
        ["LaiWenUnit"] = new[] { "来文机关", "来文单位", "发文单位", "发往单位", "发文单位", "发文机关", "往来单位", "往来机关" },

        // ── 通用 ──
        ["FileNumber"] = new[] { "文件编号", "文号", "发文字号", "编号" },
        ["FileName"] = new[] { "文件名称", "文件名", "标题", "文件标题", "公文名称", "公文标题" },
        ["QianShouRen"] = new[] { "签收人", "接收人" },
        ["QianShouDate"] = new[] { "签收日期", "接收日期", "发文日期", "签发日期", "签收时间", "接收时间", "发文时间", "签发时间" },
        ["Attachments"] = new[] { "附件", "附件信息", "附件清单", "附件列表" },
        ["FileSize"] = new[] { "文件大小", "大小" },
        ["FileType"] = new[] { "文件类型", "格式", "文件格式", "扩展名" },
        ["Location"] = new[] { "所在位置", "文件位置", "目录", "路径", "文件夹" },
        ["XuHao"] = new[] { "序号", "编号", "顺序号", "行号" },
    };

    /// <summary>
    /// 根据列名文本匹配标准名（忽略空格和全半角差异）
    /// </summary>
    public static string? MatchField(string headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return null;

        // 归一化：去空格、全角转半角
        var normalized = headerText
            .Replace(" ", "")
            .Replace("　", "")
            .Replace("：", ":")
            .Replace("（", "(")
            .Replace("）", ")")
            .Replace("\"", "\"");

        foreach (var kv in Aliases)
        {
            foreach (var alias in kv.Value)
            {
                // 别名也做同样的归一化处理
                var aliasNormalized = alias
                    .Replace(" ", "")
                    .Replace("　", "")
                    .Replace("：", ":")
                    .Replace("（", "(")
                    .Replace("）", ")");

                if (normalized.Contains(aliasNormalized) || aliasNormalized.Contains(normalized))
                    return kv.Key;
            }
        }

        return null;
    }
}
