using Ava.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Ava.Logic.OfficialDocument;

/// <summary>
/// 模版读取器：读取目标文件（docx/xlsx）的表头，通过别名字典匹配标准列名
/// </summary>
public class TemplateReader
{
    /// <summary>
    /// 读取模版文件，返回列映射结果
    /// </summary>
    public TemplateReadResult Read(string filePath)
    {
        var result = new TemplateReadResult
        {
            SourcePath = filePath,
            SourceType = GetFileType(filePath),
        };

        if (!File.Exists(filePath))
        {
            result.ErrorMessage = "文件不存在";
            return result;
        }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".xlsx":
                    ReadExcelTemplate(filePath, result);
                    break;
                case ".docx":
                    ReadWordTemplate(filePath, result);
                    break;
                default:
                    result.ErrorMessage = $"不支持的文件格式：{ext}，仅支持 .docx 和 .xlsx";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"读取模版失败：{ex.Message}";
        }

        return result;
    }

    // ────────────────────── xlsx 读取（OpenXml SDK） ──────────────────────

    private void ReadExcelTemplate(string filePath, TemplateReadResult result)
    {
        using var doc = SpreadsheetDocument.Open(filePath, false);
        var wbPart = doc.WorkbookPart!;
        var sheet = wbPart.Workbook.Descendants<Sheet>().First();
        var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
        var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>()!;
        var sstPart = wbPart.SharedStringTablePart;

        // 找表头行
        var allRows = sheetData.Elements<Row>().ToList();
        var headerRow = FindExcelHeaderRow(allRows, sstPart);
        if (headerRow == null)
        {
            result.ErrorMessage = "未找到有效的表头行（至少匹配两个字段）";
            return;
        }

        result.HeaderRowNumber = (int)(headerRow.RowIndex?.Value ?? 1);

        // 读取所有表头（顺序计数，与写入时的遍历位置一致）
        int col = 0;
        foreach (var cell in headerRow.Elements<Cell>())
        {
            col++;
            string text = GetExcelCellText(cell, sstPart).Trim();
            if (!string.IsNullOrEmpty(text))
                result.RawHeaders[col] = text;
        }

        result.MatchHeaders();
    }

    private static Row? FindExcelHeaderRow(List<Row> rows, SharedStringTablePart? sstPart)
    {
        // 优先匹配别名（至少 2 个字段命中）
        foreach (var row in rows)
        {
            int matchCount = 0;
            foreach (var cell in row.Elements<Cell>())
            {
                var text = GetExcelCellText(cell, sstPart).Trim();
                if (Services_OfficialDocument.MatchField(text) != null)
                {
                    matchCount++;
                    if (matchCount >= 2) return row;
                }
            }
        }

        // 回退取第一行有数据的行
        foreach (var row in rows)
        {
            bool hasText = row.Elements<Cell>().Any(c =>
            {
                var t = GetExcelCellText(c, sstPart).Trim();
                return !string.IsNullOrEmpty(t);
            });
            if (hasText) return row;
        }

        return null;
    }

    /// <summary>从 Excel 单元格读取文本（支持 SharedString 和 inline）</summary>
    private static string GetExcelCellText(Cell cell, SharedStringTablePart? sstPart)
    {
        if (cell.CellValue == null) return "";

        if (cell.DataType != null && cell.DataType == CellValues.SharedString && sstPart != null)
        {
            if (int.TryParse(cell.CellValue.Text, out int idx))
            {
                var item = sstPart.SharedStringTable.ElementAtOrDefault(idx);
                return item?.InnerText ?? "";
            }
        }

        return cell.CellValue.Text ?? "";
    }

    // ────── docx 读取 ───────

    private void ReadWordTemplate(string filePath, TemplateReadResult result)
    {
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            result.ErrorMessage = "文档内容为空";
            return;
        }

        var tables = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().ToList();
        if (tables.Count == 0)
        {
            result.ErrorMessage = "文档中没有表格";
            return;
        }

        // 遍历所有表格，找第一个有表头的
        foreach (var table in tables)
        {
            var rows = table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>().ToList();
            if (rows.Count == 0) continue;

            var headerRow = FindWordHeaderRow(rows);
            if (headerRow == null) continue;

            int colIndex = 0;
            foreach (var cell in headerRow.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
            {
                var text = GetCellText(cell);
                if (!string.IsNullOrEmpty(text))
                {
                    result.RawHeaders[colIndex] = text;
                }

                // 处理合并列
                var gridSpan = cell.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.GridSpan>();
                int span = (gridSpan?.Val != null && int.TryParse(gridSpan.Val, out int s)) ? s : 1;
                colIndex += span;
            }

            if (result.RawHeaders.Count > 0)
            {
                result.MatchHeaders();
                return;
            }
        }

        if (result.RawHeaders.Count == 0)
        {
            result.ErrorMessage = "未在表格中找到有效的表头行";
        }
    }

    private DocumentFormat.OpenXml.Wordprocessing.TableRow? FindWordHeaderRow(
        List<DocumentFormat.OpenXml.Wordprocessing.TableRow> rows)
    {
        // 优先匹配别名（至少 2 个字段命中）
        foreach (var row in rows)
        {
            int matchCount = 0;
            foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
            {
                var text = GetCellText(cell);
                if (Services_OfficialDocument.MatchField(text) != null)
                {
                    matchCount++;
                    if (matchCount >= 2)
                        return row;
                }
            }
        }

        // 回退取第一行
        return rows.Count > 0 ? rows[0] : null;
    }

    private static string GetCellText(DocumentFormat.OpenXml.Wordprocessing.TableCell cell)
    {
        var paragraphs = cell.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
        var texts = paragraphs
            .SelectMany(p => p.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
            .SelectMany(r => r.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
            .Select(t => t.Text);
        return string.Concat(texts).Trim();
    }

    private static string GetFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => "Excel 工作簿",
            ".docx" => "Word 文档",
            _ => "未知格式",
        };
    }

    /// <summary>Excel 引用如 "C5" 转列号 3</summary>
    private static int CellReferenceToCol(string reference)
    {
        int col = 0;
        foreach (char c in reference)
        {
            if (c >= 'A' && c <= 'Z')
                col = col * 26 + (c - 'A' + 1);
            else
                break;
        }
        return col;
    }
}

/// <summary>
/// 模板读取结果
/// </summary>
public class TemplateReadResult
{
    public string SourcePath { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>表头行号（用于写入时定位）</summary>
    public int HeaderRowNumber { get; set; } = 1;

    /// <summary>列映射：列号 → 标准名</summary>
    public Dictionary<int, string> ColumnMapping { get; set; } = new();

    /// <summary>原始表头列表（按列号顺序）</summary>
    public Dictionary<int, string> RawHeaders { get; set; } = new();

    /// <summary>未匹配的列名列表</summary>
    public List<string> UnmatchedHeaders { get; set; } = new();

    /// <summary>匹配到的标准名列表</summary>
    public List<string> MatchedFields { get; set; } = new();

    /// <summary>自动判断的模式：发文/收文/通用</summary>
    public string Mode { get; set; } = "通用";

    /// <summary>自动判断模式</summary>
    public void DetectMode()
    {
        var matched = MatchedFields;

        bool hasFaWen = matched.Any(f => f is "FaWenDate" or "FaWenUnit");
        bool hasShouWen = matched.Any(f => f is "ShouWenDate" or "LaiWenUnit");

        Mode = (hasFaWen, hasShouWen) switch
        {
            (true, false) => "发文",
            (false, true) => "收文",
            (true, true) => "混合",
            (false, false) => "通用",
        };
    }

    /// <summary>匹配表头</summary>
    public void MatchHeaders()
    {
        foreach (var kv in RawHeaders)
        {
            var standardName = Services_OfficialDocument.MatchField(kv.Value);
            if (standardName != null)
            {
                ColumnMapping[kv.Key] = standardName;
                if (!MatchedFields.Contains(standardName))
                    MatchedFields.Add(standardName);
            }
            else
            {
                UnmatchedHeaders.Add(kv.Value);
            }
        }

        DetectMode();
    }
}
