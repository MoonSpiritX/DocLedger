using Ava.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Ava.Logic.OfficialDocument;

/// <summary>
/// 台账写入器：将结构化数据写入目标文件（docx/xlsx），保持原始格式
/// 使用 OpenXml SDK，兼容 Native AOT 编译
/// </summary>
public class LedgerWriter
{
    public WriteResult Write(string templatePath, string outputPath, List<OfficialDocumentInfo> data,
        Dictionary<int, string> columnMapping, bool appendMode = false)
    {
        var result = new WriteResult();

        if (!File.Exists(templatePath))
        {
            result.ErrorMessage = "模板文件不存在";
            return result;
        }

        if (data.Count == 0)
        {
            result.ErrorMessage = "没有可写入的数据";
            return result;
        }

        try
        {
            var sorted = data
                .OrderBy(d => d.SortDate)
                .ThenBy(d => d.DocumentNumber)
                .ToList();

            var ext = Path.GetExtension(templatePath).ToLowerInvariant();
            switch (ext)
            {
                case ".xlsx":
                    WriteExcel(templatePath, outputPath, sorted, columnMapping, appendMode);
                    break;
                case ".docx":
                    WriteWord(templatePath, outputPath, sorted, columnMapping, appendMode);
                    break;
                default:
                    result.ErrorMessage = $"不支持的文件格式：{ext}";
                    return result;
            }

            result.IsSuccess = true;
            result.OutputPath = outputPath;
            result.RowCount = sorted.Count;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"写入失败：{ex.Message}";
        }

        return result;
    }

    // ════ xlsx 写入 ═════

    private void WriteExcel(string templatePath, string outputPath, List<OfficialDocumentInfo> data,
        Dictionary<int, string> columnMapping, bool appendMode)
    {
        File.Copy(templatePath, outputPath, true);
        using var doc = SpreadsheetDocument.Open(outputPath, true);
        var wbPart = doc.WorkbookPart!;
        var sheet = wbPart.Workbook.Descendants<Sheet>().First();
        var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
        var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>()!;
        var sstPart = wbPart.SharedStringTablePart;

        // 确保自动换行和居中对齐样式
        EnsureExcelCellStyles(wbPart, out uint wrapStyleIdx, out uint centerStyleIdx);

        var allRows = sheetData.Elements<Row>().ToList();
        var headerRow = FindExcelHeaderRow(allRows, sstPart)
            ?? throw new InvalidOperationException("未找到有效的表头行");
        uint headerRowIdx = headerRow.RowIndex?.Value ?? 1;
        int headerColCount = headerRow.Elements<Cell>().Count();

        // 定位已有数据行
        var existingDataRows = new List<Row>();
        foreach (var row in allRows)
        {
            if ((row.RowIndex?.Value ?? 0) > headerRowIdx && row.Elements<Cell>().Count() == headerColCount)
                existingDataRows.Add(row);
        }

        // 取模板行（已有数据行优先，否则用表头行），用于保留原始单元格格式（边框/字体/背景色等）
        Row templateRow = existingDataRows.Count > 0
            ? existingDataRows[0]
            : headerRow;

        // 追加模式逻辑
        if (!appendMode)
        {
            foreach (var row in existingDataRows)
                row.Remove();
        }
        uint startIdx = headerRowIdx + 1;
        if (appendMode && existingDataRows.Count > 0)
            startIdx = existingDataRows[^1].RowIndex!.Value + 1;
        Row insertAnchor = (appendMode && existingDataRows.Count > 0)
            ? existingDataRows[^1]
            : headerRow;

        // 从模板行克隆并填充数据（保留单元格原始格式）
        Row? lastInserted = null;
        for (int i = 0; i < data.Count; i++)
        {
            uint newIdx = startIdx + (uint)i;
            // 克隆模板整行（含全部 Cell），保留边框/字体/背景色/对齐等原始格式
            var newRow = (Row)templateRow.CloneNode(true);
            newRow.RowIndex = newIdx;

            // 按列号索引克隆行中的单元格
            var rowCellsByCol = new Dictionary<int, Cell>();
            foreach (var tc in newRow.Elements<Cell>())
            {
                int cn = CellReferenceToCol(tc.CellReference!);
                rowCellsByCol[cn] = tc;
            }

            // 遍历表头列，逐列填充数据
            int pos = 0;
            foreach (var hCell in headerRow.Elements<Cell>())
            {
                pos++;
                int colNum = CellReferenceToCol(hCell.CellReference!);
                string fieldName = columnMapping.TryGetValue(pos, out var fn) ? fn : "";
                string val = !string.IsNullOrEmpty(fieldName)
                    ? GetFieldValue(data[i], fieldName, i + 1) : "";

                if (rowCellsByCol.TryGetValue(colNum, out var cell))
                {
                    // ✅ 复用模板单元格（边框/字体/背景色/对齐等格式已通过 CloneNode 保留）
                    cell.CellReference = $"{GetColumnLetter(colNum)}{newIdx}";
                    cell.RemoveAllChildren();
                    cell.CellValue = null;
                    cell.DataType = null;

                    if (!string.IsNullOrEmpty(val))
                    {
                        cell.DataType = CellValues.InlineString;
                        cell.AppendChild(new InlineString(new Text
                        {
                            Text = val,
                            Space = SpaceProcessingModeValues.Preserve
                        }));
                    }
                }
                else
                {
                    // ⚠️ 模板行缺失此列时新建单元格（仅设基本对齐样式，Excel 打开时会自动排序）
                    var newCell = new Cell
                    {
                        CellReference = $"{GetColumnLetter(colNum)}{newIdx}",
                        StyleIndex = (pos == 1 || pos == 2 || pos == 3) ? centerStyleIdx : wrapStyleIdx,
                    };
                    if (!string.IsNullOrEmpty(val))
                    {
                        newCell.DataType = CellValues.InlineString;
                        newCell.AppendChild(new InlineString(new Text
                        {
                            Text = val,
                            Space = SpaceProcessingModeValues.Preserve
                        }));
                    }
                    newRow.AppendChild(newCell);
                }
            }

            if (i == 0)
                sheetData.InsertAfter(newRow, insertAnchor);
            else if (lastInserted != null)
                sheetData.InsertAfter(newRow, lastInserted);
            lastInserted = newRow;
        }

        wsPart.Worksheet.Save();
    }

    /// <summary>确保 Stylesheet 中包含自动换行和居中对齐的 CellFormat，返回样式索引</summary>
    private static void EnsureExcelCellStyles(WorkbookPart wbPart,
        out uint wrapStyleIdx, out uint centerWrapStyleIdx)
    {
        var stylesPart = wbPart.GetPartsOfType<WorkbookStylesPart>().FirstOrDefault();
        if (stylesPart == null)
        {
            stylesPart = wbPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = new Stylesheet();
        }

        var ss = stylesPart.Stylesheet!;
        if (ss.CellFormats == null)
            ss.CellFormats = new CellFormats();

        // index 0：默认格式（保留模板原有）
        if (ss.CellFormats.Count?.Value is null or 0)
            ss.CellFormats.AppendChild(new CellFormat());

        // 格式 1：自动换行 + 垂直居中
        var wrapCf = new CellFormat
        {
            Alignment = new Alignment
            {
                WrapText = true,
                Vertical = VerticalAlignmentValues.Center
            },
            ApplyAlignment = true
        };
        ss.CellFormats.AppendChild(wrapCf);
        wrapStyleIdx = (uint)(ss.CellFormats.Count! - 1);

        // 格式 2：自动换行 + 水平居中 + 垂直居中
        var centerCf = new CellFormat
        {
            Alignment = new Alignment
            {
                WrapText = true,
                Horizontal = HorizontalAlignmentValues.Center,
                Vertical = VerticalAlignmentValues.Center
            },
            ApplyAlignment = true
        };
        ss.CellFormats.AppendChild(centerCf);
        centerWrapStyleIdx = (uint)(ss.CellFormats.Count! - 1);

        ss.Save();
    }

    private static string GetColumnLetter(int col)
    {
        string result = "";
        while (col > 0)
        {
            col--;
            result = (char)('A' + col % 26) + result;
            col /= 26;
        }
        return result;
    }

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

    private static Row? FindExcelHeaderRow(List<Row> rows, SharedStringTablePart? sstPart)
    {
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
        return rows.Count > 0 ? rows[0] : null;
    }

    // ═══════ docx 写入 ═════════

    private void WriteWord(string templatePath, string outputPath, List<OfficialDocumentInfo> data,
        Dictionary<int, string> columnMapping, bool appendMode)
    {
        File.Copy(templatePath, outputPath, true);

        using var document = WordprocessingDocument.Open(outputPath, true);
        var mainPart = document.MainDocumentPart!;
        var body = mainPart.Document!.Body!;

        var tables = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>().ToList();
        if (tables.Count == 0)
            throw new InvalidOperationException("文档中没有表格");

        int nextNumId = 1;
        foreach (var table in tables)
        {
            if (ProcessWordTable(table, mainPart, data, columnMapping, ref nextNumId, appendMode))
                break;
        }

        mainPart.Document.Save();
    }

    private bool ProcessWordTable(DocumentFormat.OpenXml.Wordprocessing.Table table,
        DocumentFormat.OpenXml.Packaging.MainDocumentPart mainPart,
        List<OfficialDocumentInfo> data, Dictionary<int, string> columnMapping,
        ref int nextNumId, bool appendMode)
    {
        var rows = table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>().ToList();
        if (rows.Count == 0) return false;

        var headerRow = FindWordHeaderRow(rows);
        if (headerRow == null) return false;

        int headerIndex = rows.IndexOf(headerRow);
        var dataRows = new List<DocumentFormat.OpenXml.Wordprocessing.TableRow>();
        int headerColCount = GetRowColCount(headerRow);

        for (int i = headerIndex + 1; i < rows.Count; i++)
        {
            if (GetRowColCount(rows[i]) == headerColCount)
                dataRows.Add(rows[i]);
            else
                break;
        }

        DocumentFormat.OpenXml.Wordprocessing.TableRow? templateRow = dataRows.Count > 0
            ? (DocumentFormat.OpenXml.Wordprocessing.TableRow)dataRows[0].CloneNode(true)
            : (DocumentFormat.OpenXml.Wordprocessing.TableRow)rows[headerIndex].CloneNode(true);

        // 追加模式逻辑 
        if (!appendMode)
        {
            foreach (var row in dataRows)
                row.Remove();
        }
        DocumentFormat.OpenXml.Wordprocessing.TableRow? lastInserted = null;
        for (int i = 0; i < data.Count; i++)
        {
            var newRow = (DocumentFormat.OpenXml.Wordprocessing.TableRow)templateRow.CloneNode(true);

            foreach (var cell in newRow.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
            {
                foreach (var para in cell.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    foreach (var run in para.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
                    {
                        foreach (var text in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                            text.Text = "";
                    }
                }
            }

            var newCells = newRow.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>().ToList();

            // 按单元格顺序遍历（与 Excel 写入逻辑一致），通过位置索引映射
            for (int ci = 0; ci < newCells.Count; ci++)
            {
                if (!columnMapping.TryGetValue(ci, out var fieldName))
                    continue;

                string val = GetFieldValue(data[i], fieldName, i + 1);
                var cell = newCells[ci];
                var para = cell.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
                if (para == null)
                {
                    para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                    cell.AppendChild(para);
                }

                var run = para.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Run>();
                if (run == null)
                {
                    run = new DocumentFormat.OpenXml.Wordprocessing.Run();
                    para.AppendChild(run);
                }

                var text = run.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.Text>();
                if (text == null)
                {
                    text = new DocumentFormat.OpenXml.Wordprocessing.Text();
                    run.AppendChild(text);
                }

                text.Text = val;
            }

            if (i == 0)
            {
                // 追加模式：插在最后一条旧数据之后；覆盖模式：插在表头之后
                var anchor = (appendMode && dataRows.Count > 0) ? dataRows[^1] : headerRow;
                table.InsertAfter(newRow, anchor);
            }
            else if (lastInserted != null)
                table.InsertAfter(newRow, lastInserted);

            lastInserted = newRow;
        }

        return true;
    }

    private DocumentFormat.OpenXml.Wordprocessing.TableRow? FindWordHeaderRow(
        List<DocumentFormat.OpenXml.Wordprocessing.TableRow> rows)
    {
        foreach (var row in rows)
        {
            int matchCount = 0;
            foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
            {
                var text = GetWordCellText(cell);
                if (Services_OfficialDocument.MatchField(text) != null)
                {
                    matchCount++;
                    if (matchCount >= 2) return row;
                }
            }
        }
        return rows.Count > 0 ? rows[0] : null;
    }

    private static int GetRowColCount(DocumentFormat.OpenXml.Wordprocessing.TableRow row)
    {
        int total = 0;
        foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
        {
            var gridSpan = cell.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.GridSpan>();
            int span = (gridSpan?.Val != null && int.TryParse(gridSpan.Val, out int s)) ? s : 1;
            total += span;
        }
        return total;
    }

    private static string GetWordCellText(DocumentFormat.OpenXml.Wordprocessing.TableCell cell)
    {
        var paragraphs = cell.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
        return string.Concat(paragraphs
            .SelectMany(p => p.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
            .SelectMany(r => r.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
            .Select(t => t.Text));
    }

    // ══════════ 字段取值 ══════

    public static string GetFieldValue(OfficialDocumentInfo doc, string fieldName, int rowIndex = 0)
    {
        return fieldName switch
        {
            "FaWenDate" or "ShouWenDate" => doc.DocumentDate,
            "FaWenUnit" or "LaiWenUnit" => doc.IssuingUnit,
            "FileNumber" => StripOuterBrackets(doc.DocumentNumber),
            "FileName" => doc.MainDocument,
            "Attachments" => string.Join("；", doc.Attachments),
            "FileSize" => doc.FileSize,
            "Location" => doc.FolderPath,
            "XuHao" => rowIndex.ToString(),
            _ => "",
        };
    }

    public static string StripOuterBrackets(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2) return text;
        char first = text[0];
        char last = text[^1];
        if ((first == '【' && last == '】') ||
            (first == '（' && last == '）') ||
            (first == '(' && last == ')') ||
            (first == '{' && last == '}') ||
            (first == '｛' && last == '｝'))
        {
            return text[1..^1].Trim();
        }
        return text;
    }
}

public class WriteResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputPath { get; set; }
    public int RowCount { get; set; }
    public int ExistingRowCount { get; set; }
}
