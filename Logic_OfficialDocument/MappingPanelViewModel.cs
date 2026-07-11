using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ava.Services;
using 文件台账工具Ava.ViewModels;

namespace Ava.Logic.OfficialDocument;

/// <summary>
/// 源文件夹列表项
/// </summary>
public partial class SourceFolderItem : ViewModelBase
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _folderName = "";
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _documentName = "";
    [ObservableProperty] private string _attachmentCount = "";
    [ObservableProperty] private bool _hasScanned;
}

/// <summary>
/// 文号前缀配置项
/// </summary>
public partial class DocNumberPrefixItem : ViewModelBase
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _prefix = "";
    [ObservableProperty] private string _unitName = "";
}

/// <summary>
/// 映射面板的 ViewModel
/// </summary>
public partial class MappingPanelViewModel : ViewModelBase
{
    // ── 可观察属性 ──

    [ObservableProperty] private string _targetFilePath = "";

    partial void OnTargetFilePathChanged(string value)
    {
        var clean = CleanPath(value);
        if (clean != value)
        {
            _targetFilePath = clean;
            OnPropertyChanged(nameof(TargetFilePath));
        }
        TargetDisplayName = !string.IsNullOrEmpty(clean) && File.Exists(clean)
            ? Path.GetFileName(clean) : "";
        // 目标文件变更 → 模板映射过期
        _templateDirty = true;
        _templateResult = null;
        HasResult = false;
        ScanStatus = "";
        OnPropertyChanged(nameof(CanFill));
        _ = TryAutoScanAsync();
    }

    [ObservableProperty] private int _folderCount;
    [ObservableProperty] private int _documentCount;
    [ObservableProperty] private int _numberCount;
    [ObservableProperty] private int _unmatchedColumnCount;
    [ObservableProperty] private string _scanStatus = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _modeText = "通用";
    [ObservableProperty] private string _targetDisplayName = "";
    [ObservableProperty] private bool _appendMode = true;
    [ObservableProperty] private string _toastMessage = "";

    /// <summary>按钮是否可点击：自动扫描完成后且未在扫描中</summary>
    public bool CanFill => HasResult && !IsScanning;

    partial void OnFolderCountChanged(int value) => OnPropertyChanged(nameof(CanFill));
    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CanFill));
    partial void OnHasResultChanged(bool value) => OnPropertyChanged(nameof(CanFill));

    // ── 文号前缀列表（表格形式） ──

    public ObservableCollection<DocNumberPrefixItem> DocNumberPrefixes { get; } = new();

    private static string PrefixesFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AvaLedger", "prefixes.dat");

    public MappingPanelViewModel()
    {
        LoadPrefixes();
    }

    /// <summary>添加一行空的前缀配置</summary>
    [RelayCommand]
    private void AddPrefixRow()
    {
        var item = new DocNumberPrefixItem();
        SubscribePrefixItem(item);
        DocNumberPrefixes.Add(item);
        RefreshPrefixIndexes();
    }

    private void SubscribePrefixItem(DocNumberPrefixItem item)
    {
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(DocNumberPrefixItem.Prefix) or nameof(DocNumberPrefixItem.UnitName))
                SavePrefixes();
        };
    }

    /// <summary>删除指定前缀行</summary>
    [RelayCommand]
    private void RemovePrefixRow(DocNumberPrefixItem? item)
    {
        if (item == null) return;
        DocNumberPrefixes.Remove(item);
        RefreshPrefixIndexes();
        SavePrefixes();
    }

    private void RefreshPrefixIndexes()
    {
        for (int i = 0; i < DocNumberPrefixes.Count; i++)
            DocNumberPrefixes[i].Index = i + 1;
    }

    private void LoadPrefixes()
    {
        try
        {
            var path = PrefixesFilePath;
            // 兼容旧版：如果 .dat 不存在但 .json 存在，自动迁移
            if (!File.Exists(path))
            {
                var oldPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AvaLedger", "prefixes.json");
                if (File.Exists(oldPath))
                {
                    var oldLines = File.ReadAllLines(oldPath);
                    var newLines = oldLines
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .Select(l => $"{l}\t{l}");
                    File.WriteAllLines(path, newLines);
                    File.Delete(oldPath);
                }
            }

            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    var prefix = parts.Length > 0 ? parts[0].Trim() : "";
                    var unit = parts.Length > 1 ? parts[1].Trim() : prefix;
                    if (prefix.Length > 0)
                    {
                        var item = new DocNumberPrefixItem
                        {
                            Prefix = prefix,
                            UnitName = unit,
                        };
                        SubscribePrefixItem(item);
                        DocNumberPrefixes.Add(item);
                    }
                }
                RefreshPrefixIndexes();
                if (DocNumberPrefixes.Count > 0)
                    ScanStatus = $"已加载 {DocNumberPrefixes.Count} 个前缀配置";
            }
        }
        catch (Exception ex)
        {
            ScanStatus = $"前缀加载失败: {ex.Message}";
        }
    }

    /// <summary>保存前缀配置</summary>
    [RelayCommand]
    private void SavePrefixConfig()
    {
        SavePrefixes();
        ScanStatus = $"已保存 {DocNumberPrefixes.Count} 个前缀配置";
    }

    private void SavePrefixes()
    {
        try
        {
            var dir = Path.GetDirectoryName(PrefixesFilePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var lines = DocNumberPrefixes
                .Where(i => !string.IsNullOrEmpty(i.Prefix))
                .Select(i => $"{i.Prefix}\t{i.UnitName}");
            File.WriteAllLines(PrefixesFilePath, lines);
        }
        catch (Exception ex)
        {
            ScanStatus = $"前缀保存失败: {ex.Message}";
        }
    }

    public ObservableCollection<SourceFolderItem> SourceFolderItems { get; } = new();

    // ── 内部状态 ──
    private TemplateReadResult? _templateResult;
    private List<OfficialDocumentInfo> _scanResults = new();
    private bool _foldersDirty = true;
    private bool _templateDirty = true;

    // ── 命令 ──

    [RelayCommand] private async Task BrowseTarget()
    {
        var window = GetMainWindow();
        if (window == null) return;
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(window);
        if (topLevel?.StorageProvider == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "选择目标台账文件", AllowMultiple = false,
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Excel / Word 文件")
            { Patterns = new[] { "*.xlsx", "*.docx" } } },
        });
        if (files.Count > 0) { TargetFilePath = files[0].Path.LocalPath; TargetDisplayName = Path.GetFileName(TargetFilePath); }
    }

    [RelayCommand] private async Task BrowseFolder()
    {
        var window = GetMainWindow();
        if (window == null) return;
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(window);
        if (topLevel?.StorageProvider == null) return;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new()
        { Title = "选择源文件夹", AllowMultiple = true });
        foreach (var folder in folders)
        { var path = folder.Path?.LocalPath; if (!string.IsNullOrEmpty(path)) AddFolderPath(path); }
        TriggerScan();
    }

    [RelayCommand] private void RemoveFolder(string? path)
    {
        if (path == null) return;
        var item = SourceFolderItems.FirstOrDefault(i => i.FolderPath == path);
        if (item != null) SourceFolderItems.Remove(item);
        RefreshIndexes();
        _foldersDirty = true;
        _scanResults.Clear();
        HasResult = false;
        OnPropertyChanged(nameof(CanFill));
        _ = TryAutoScanAsync();
    }

    [RelayCommand] private void ClearList()
    {
        SourceFolderItems.Clear(); HasResult = false; ScanStatus = "";
        FolderCount = 0;
        _foldersDirty = true;
        _scanResults.Clear();
        _templateResult = null;
        _templateDirty = true;
        OnPropertyChanged(nameof(CanFill));
    }

    [RelayCommand] private void RemoveTargetFile()
    {
        TargetFilePath = ""; TargetDisplayName = ""; HasResult = false;
    }

    /// <summary>自动读取模板表头映射（模板文件变更时触发）</summary>
    private async Task<bool> AutoReadTemplate()
    {
        if (!_templateDirty) return true;
        if (string.IsNullOrEmpty(TargetFilePath) || !File.Exists(TargetFilePath))
        {
            ScanStatus = "请先选择有效的目标台账文件";
            return false;
        }

        IsScanning = true;
        ScanStatus = "正在读取模板...";

        TemplateReadResult? result = null;
        string? error = null;
        await Task.Run(() =>
        {
            try
            {
                var reader = new TemplateReader();
                result = reader.Read(TargetFilePath);
                if (!result.IsSuccess) error = result.ErrorMessage;
            }
            catch (Exception ex) { error = $"模板读取失败：{ex.Message}"; }
        });

        if (error != null) { ScanStatus = error; IsScanning = false; return false; }

        _templateResult = result;
        _templateDirty = false;

        if (_foldersDirty)
        {
            // 文件夹也脏 → 等文件夹扫完后一起更新 UI（不释放 IsScanning，防并发）
            return true;
        }

        // 只更新模板相关的 UI
        UnmatchedColumnCount = _templateResult?.UnmatchedHeaders.Count ?? 0;
        ModeText = _templateResult?.Mode ?? "通用";
        HasResult = _scanResults.Count > 0;
        IsScanning = false;
        return true;
    }

    /// <summary>自动扫描文件夹（文件夹列表变更时触发）</summary>
    private async Task<bool> AutoScanFolders()
    {
        if (!_foldersDirty) return true;
        if (SourceFolderItems.Count == 0)
        {
            _foldersDirty = false;
            return true;
        }

        IsScanning = true;
        ScanStatus = "正在扫描文件夹...";
        var folderPaths = SourceFolderItems.Select(i => i.FolderPath).ToList();
        List<OfficialDocumentInfo>? scanResults = null;
        string? error = null;

        await Task.Run(() =>
        {
            try
            {
                var prefixes = DocNumberPrefixes.Where(p => !string.IsNullOrEmpty(p.Prefix))
                    .Select(p => p.Prefix).ToList();
                var scanner = new FileScanner();
                scanResults = scanner.ScanFolders(folderPaths, prefixes);
            }
            catch (Exception ex) { error = $"扫描出错：{ex.Message}"; }
        });

        if (error != null) { ScanStatus = error; IsScanning = false; return false; }

        _scanResults = scanResults ?? new();

        // 用用户配置的单位名覆盖自动提取的单位
        foreach (var r in _scanResults)
        {
            var match = DocNumberPrefixes.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Prefix) && r.DocumentNumber.StartsWith(p.Prefix));
            if (match != null && !string.IsNullOrEmpty(match.UnitName))
                r.IssuingUnit = match.UnitName;
        }

        // 用扫描结果更新现有文件列表项的显示属性（绝不触碰用户维护的列表结构）
        foreach (var item in SourceFolderItems)
        {
            var r = _scanResults.FirstOrDefault(s => s.FolderPath == item.FolderPath);
            if (r != null)
            {
                item.DocumentName = r.MainDocument;
                item.AttachmentCount = $"{r.Attachments.Count} 个";
                item.HasScanned = true;
            }
        }
        RefreshIndexes();

        DocumentCount = _scanResults.Count;
        NumberCount = _scanResults.Count(r => !string.IsNullOrEmpty(r.DocumentNumber));
        _foldersDirty = false;

        UnmatchedColumnCount = _templateResult?.UnmatchedHeaders.Count ?? 0;
        ModeText = _templateResult?.Mode ?? "通用";
        HasResult = true;
        IsScanning = false;
        ScanStatus = $"{_scanResults.Count} 个有效文件";
        return true;
    }

    /// <summary>当文件夹列表和目标文件都已就绪时自动触发扫描</summary>
    private async Task TryAutoScanAsync()
    {
        // 并发保护：正在扫描时跳过
        if (IsScanning) return;
        // 两个前提缺一不可
        if (SourceFolderItems.Count == 0) return;
        if (string.IsNullOrEmpty(TargetFilePath) || !File.Exists(TargetFilePath)) return;
        if (!_foldersDirty && !_templateDirty) return;

        IsScanning = true;
        try
        {
            await EnsureScanned();
        }
        finally
        {
            IsScanning = false;
        }

        // 扫描期间又有新变更（如用户在扫描中追加了文件夹）→ 再扫一次
        if ((_foldersDirty || _templateDirty)
            && SourceFolderItems.Count > 0
            && !string.IsNullOrEmpty(TargetFilePath)
            && File.Exists(TargetFilePath))
        {
            await TryAutoScanAsync();
        }
    }

    /// <summary>一次性完成模板读取和文件夹扫描，返回是否全部成功</summary>
    private async Task<bool> EnsureScanned()
    {
        // 依次扫描（先模板后文件夹，两者异步无依赖，但顺序执行更清晰）
        if (_templateDirty)
        {
            if (!await AutoReadTemplate()) return false;
        }
        if (_foldersDirty)
        {
            if (!await AutoScanFolders()) return false;
        }
        // 全部就绪
        HasResult = _scanResults.Count > 0 && _templateResult != null;
        if (HasResult)
            ScanStatus = $"{_scanResults.Count} 个有效文件 | {_templateResult?.Mode ?? "通用"}模式";
        return HasResult;
    }

    [RelayCommand]
    private async Task Fill()
    {
        // ── 自动补扫（防呆）：确保模板和文件夹都已扫描 ──
        if (!await EnsureScanned()) return;

        var dir = Path.GetDirectoryName(TargetFilePath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(TargetFilePath);
        var ext = Path.GetExtension(TargetFilePath);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(dir, $"{name}_{ts}{ext}");
        var writer = new LedgerWriter();

        IsScanning = true;
        ScanStatus = "正在生成台账...";

        var result = await Task.Run(() => writer.Write(TargetFilePath, outputPath, _scanResults, _templateResult!.ColumnMapping, AppendMode));

        // 导出完成后重置模板状态，保证下次导出时重新读取映射
        _templateDirty = true;
        _templateResult = null;
        HasResult = false;
        IsScanning = false;

        ScanStatus = result.IsSuccess
            ? $"填充完成！输出文件：{result.OutputPath}（新增 {result.RowCount} 行）"
            : $"填充失败：{result.ErrorMessage}";

        if (result.IsSuccess)
        {
            var fileName = Path.GetFileName(result.OutputPath);
            ToastMessage = $"已生成 {fileName}，与目标文件同目录。";
            _ = ClearToastDelayedAsync();
        }

        // 导出后自动重新扫描，使按钮恢复可用状态
        TriggerScan();
    }

    private async Task ClearToastDelayedAsync()
    {
        await Task.Delay(3000);
        ToastMessage = "";
    }

    // ── 辅助方法 ──

    public void AddFolderPath(string path)
    {
        if (SourceFolderItems.Any(i => i.FolderPath == path)) return;
        SourceFolderItems.Add(new SourceFolderItem { FolderPath = path, FolderName = new DirectoryInfo(path).Name });
        RefreshIndexes();
        _foldersDirty = true;
        HasResult = false;
        OnPropertyChanged(nameof(CanFill));
    }

    /// <summary>供外部批量添加后统一触发扫描</summary>
    public void TriggerScan() => _ = TryAutoScanAsync();

    public void SetTargetFilePath(string path)
    {
        if (File.Exists(path)) { TargetFilePath = path; TargetDisplayName = Path.GetFileName(path); }
    }

    public static string CleanPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return path.Trim().Trim('"', '\'', ' ', '\t', '\n', '\r');
    }

    private void RefreshIndexes()
    {
        for (int i = 0; i < SourceFolderItems.Count; i++)
            SourceFolderItems[i].Index = i + 1;
        FolderCount = SourceFolderItems.Count;
    }

    private static Avalonia.Controls.Window? GetMainWindow()
    {
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
