# Ava 文件台账工具 — 项目技术笔记

.NET 10 + Avalonia 12.0.5 + CommunityToolkit.Mvvm + Native AOT

---

## 一、项目概况

- **框架**：.NET 10，Avalonia 12.0.5（Avalonia.Themes.Fluent）
- **模式**：MVVM（CommunityToolkit.Mvvm 源代码生成器）
- **目标**：Native AOT 发布（`PublishAot=true`）
- **数据格式**：OpenXml SDK 读写 `.docx` / `.xlsx`

### 关键 NuGet 包

| 包 | 用途 |
|---|---|
| Avalonia 12.0.5 | UI 框架 |
| Avalonia.Themes.Fluent | Fluent 主题 |
| CommunityToolkit.Mvvm 8.4.1 | MVVM + 源代码生成 |
| DocumentFormat.OpenXml 3.3.0 | Word/Excel 读写 |

---

## 二、文件结构

```
文件台账工具Ava/
├── App.axaml / App.axaml.cs     — 应用入口 + 全局主题/样式 + 转换器注册
├── Program.cs                   — 启动入口 + 单实例控制
├── Views/
│   ├── MainWindow.axaml / .cs   — 主窗口（Acrylic + 渐变背景）
└── ViewModels/
    └── ViewModelBase.cs         — ObservableObject 基类
├── Logic_OfficialDocument/      — 核心业务
│   ├── MappingPanel.axaml / .cs — 主界面 + 粘贴/拖放/Enter 事件
│   ├── MappingPanelViewModel.cs — 主 ViewModel + 自动扫描 + 命令
│   ├── FileScanner.cs           — 文件智能分类扫描
│   ├── LedgerWriter.cs          — Excel/Word 写入
│   ├── TemplateReader.cs        — 模板表头识别
│   ├── OfficialDocumentInfo.cs  — 数据结构
│   └── Converters.cs            — 值转换器
└── Services/
    └── Services_OfficialDocument.cs — 字段别名字典
```

---

## 三、UI 现代化改造

### 3.1 自定义主题色板

**文件**：`App.axaml`

**思路**：不用 `SystemControl...` 系列系统动态资源，改用 ThemeDictionaries 定义暗色/亮色两套独立资源。

```
Application.Resources
  └── ResourceDictionary.ThemeDictionaries
        ├── ResourceDictionary x:Key="Dark"     ← 暗色资源
        └── ResourceDictionary x:Key="Light"    ← 亮色资源
  └── 转换器（全局共享）
```

**关键资源分类**：

| 类别 | 资源名示例 | 说明 |
|---|---|---|
| 窗口背景 | `WindowBackground` | LinearGradientBrush 渐变 |
| 卡片 | `CardBackground` / `CardBorder` | 半透明白色 |
| 输入框 | `InputBackground` / `InputBorder` | 半透明输入区 |
| 文本 | `TextPrimary` / `TextSecondary` / `TextAccent` | 三级文本色 |
| 按钮 | `ButtonPrimaryBg` / `ButtonSecondaryBg` | 主次按钮填充 |
| 列表 | `ListRowBorder` / `ListRowHover` | 行分隔/悬停 |

**注意**：`DynamicResource` 在 `Application.Styles` 中可以解析到 `Application.Resources.ThemeDictionaries` 中的资源——但仅限于常规资源引用。对于 FluentTheme 模板内部的 VisualState 资源，需要特定资源键名（见 3.4）。

### 3.2 全局样式类

在 `Application.Styles` 中定义，用 `Classes="..."` 引用。

| 样式选择器 | 用途 | 关键属性 |
|---|---|---|
| `Border.Card` | 所有卡片容器 | 半透明背景 + 12px 圆角 + 0.5px 边框 |
| `Border.CardHeader` | 列表表头 | 稍深的半透明底 |
| `Button.Primary` | 生成按钮 | 蓝色填充 + hover 加深 |
| `Button.Secondary` | 浏览/清空按钮 | 半透明轮廓 + hover 加深 |
| `Button.Danger` | 删除按钮 | 透明 + hover 变红 |
| `TextBlock.SectionTitle` | 区域标题 | 13px + accent 色 |
| `TextBlock.Hint` | 辅助提示 | 11px + 三级文本色 |
| `TextBlock.ColumnHeader` | 列表表头文字 | 12px + 二级文本色 |

**TextBox 全局样式**：
```xml
<Style Selector="TextBox">
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="CornerRadius" Value="8"/>
  <Setter Property="Padding" Value="10,7"/>
  <!-- 聚焦/悬停边框通过 FluentTheme 资源键控制，见 3.4 -->
</Style>
```

**坑**：默认 0.5px 边框 + 聚焦 1.5px 会导致控件高度变化，下方布局抖动。解法：统一用 1px，聚焦只换颜色不换厚度。

### 3.3 Acrylic（毛玻璃）效果

**文件**：`MainWindow.axaml`

**正确配置**：
```xml
<Window TransparencyLevelHint="AcrylicBlur"
        Background="Transparent">
  <Panel>
    <!-- 半透明覆盖层提供深蓝渐变底色 -->
    <Border Background="{DynamicResource WindowBackground}" Opacity="0.55"/>
    <doc:MappingPanel/>
  </Panel>
</Window>
```

**关键点**：
1. `Background="Transparent"` **必须设置**，否则 OS 不启用 Acrylic 渲染路径
2. 覆盖层 `Opacity` 控制透明感——0.78 几乎不透明，0.55 透出约一半桌面
3. `LinearGradientBrush` 的 alpha 通道在窗口级不生效，必须通过 `<Border Opacity="...">` 控制
4. `AcrylicBlur` 依赖 Windows 11 合成器，Win10 降级为普通透明

**最终效果**：桌面墙纸 → Acrylic 模糊 → 55% 深蓝渐变覆盖层 → 11~63% 半透卡片 → 控件。

### 3.4 TextBox 悬停/聚焦边框色

**坑的根源**：FluentTheme 的 TextBox 控件模板内部有一个 `Border#PART_BorderElement`，VisualState 切换时**直接操作这个内部 Border**，不走 `TextBox.BorderBrush` 属性。

**错误尝试**（都无效）：
- ❌ `Style Selector="TextBox:pointerover"` set `BorderBrush` — 设的是外层，模板不理
- ❌ 代码 `tb.BorderBrush = ...` — 同样设了外层
- ❌ 嵌套样式 `^:pointerover` — 跟顶层样式效果一样

**正确解法**：覆盖 FluentTheme 模板使用的内部资源键：

```xml
<!-- 暗色示例 -->
<SolidColorBrush x:Key="TextControlBorderBrush" Color="#40FFFFFF"/>
<SolidColorBrush x:Key="TextControlBorderBrushPointerOver" Color="#884A9AF5"/>
<SolidColorBrush x:Key="TextControlBorderBrushFocused" Color="#CC4A9AF5"/>
<Thickness x:Key="TextControlBorderThemeThicknessFocused">1</Thickness>
```

这三个资源键在 `Avalonia.Themes.Fluent/Controls/TextBox.xaml` 中定义，分别是默认/悬停/聚焦态的模板内部 BorderBrush。

---

## 四、剪贴板粘贴文件/文件夹

**文件**：`MappingPanel.axaml.cs`

### 问题

两个 TextBox（目标文件 / 源文件夹）粘贴文件/文件夹不生效。

### 根因分析（两次修复）

**第一次**：只读取 `DataFormat.Text`，但 Ctrl+C 复制文件时剪贴板是 `DataFormat.File` 格式。

```csharp
// ❌ 旧代码：只认文本格式
var formats = await cb.GetDataFormatsAsync();
if (!formats.Contains(DataFormat.Text)) return null;
```

**第二次**：手动 `TryGetValueAsync + 类型转换` 在 Windows 上返回类型不匹配。

```csharp
// ❌ 旧代码：数据类型转换可能失败
var data = await cb.TryGetValueAsync(DataFormat.File);
if (data is IReadOnlyList<IStorageItem> items) ...
```

### 最终方案

直接用 Avalonia 官方扩展方法：

```csharp
// ✅ 文本：复制路径字符串时
var text = await cb.TryGetTextAsync();

// ✅ 文件：Ctrl+C 复制文件/文件夹时
var items = await cb.TryGetFilesAsync();  // 返回 IStorageItem[]?
```

优先级：先试文本（复制路径字符串），失败则回退到文件格式（Ctrl+C 复制文件）。

### 拖放 vs 粘贴

拖放功能一直正常，因为它走的是 `DragEventArgs.DataTransfer` 路径，而粘贴走的是 `IClipboard` 路径——两套不同的 API。

---

## 五、Excel 列偏移 Bug

**文件**：`LedgerWriter.cs`

### 问题

Excel 导出时附件文件名写到了公文名称列，Word 导出正常。

### 根因

在 `WriteExcel` 中，原来用 `cellPos++` 逐个计数来映射列位置：

```csharp
int cellPos = 0;
foreach (var cell in newRow.Elements<Cell>())
{
    cellPos++;
    if (columnMapping.TryGetValue(cellPos, out var fieldName)) ...
}
```

OpenXml 格式中，如果模板的数据行有**空列**（没有值），该列的 `<Cell>` 元素可能不写入 XML。迭代时位置计数偏移，导致数据写到了错误的列。

### 修复

用单元格自身的 `CellReference` 解析真实列号代替位置计数：

```csharp
foreach (var cell in newRow.Elements<Cell>())
{
    int colNum = CellReferenceToCol(cell.CellReference!);
    if (columnMapping.TryGetValue(colNum, out var fieldName)) ...
}
```

Word 写入没问题，因为它直接用 `kv.Key` 作为索引访问 `newCells[colIndex]`，不受空列影响。

---

## 六、Excel 导出格式丢失 Bug（2026-07-10 修复）

**文件**：`LedgerWriter.cs`

### 问题 1：行高估算失效（已过时——2026-07-11 已移除）

> **历史**：原 `GetCellTextValue()` 先判断 `cell.CellValue != null`，但 InlineString 的文本存储在 `InlineString.Text.Text` 中，`CellValue` 为 null，导致行高估算全为最低值 28。
>
> **2026-07-11 彻底移除**：手动估算行高的方案已被清理。现不设 `CustomHeight`，完全交给 Excel auto-fit，由 Excel 根据列宽+内容自动计算精确行高。

### 问题 2：模板格式完全丢失

原来用 `new Cell { ... }` 新建每一个单元格，只设置了换行/居中对齐。模板原有的**边框、字体、字号、背景填充色**全部丢失。

### 修复（2026-07-10 基干 + 2026-07-11 演进）

1. **读取文本读取**：先判断 `cell.DataType == CellValues.InlineString` 从 `InlineString.Text.Text` 读取，再回退到 `cell.CellValue.Text`（该逻辑后来随行高移除而删除）
2. **写入逻辑重构**：
   - 从已有数据行（存在时）或表头行（备选）用 `CloneNode(true)` 克隆一行作为模板
   - 每行数据从此模板行克隆，保留全部原始格式
   - 按列号匹配表头，更新 CellReference 和文本内容
   - 新增 `Space = SpaceProcessingModeValues.Preserve` 防止空格被压缩
3. 新增 `using DocumentFormat.OpenXml;` 引用

---

## 六-B、Excel 行高：手动估算 → Excel Auto-Fit（2026-07-11）

**文件**：`LedgerWriter.cs`

### 背景

原方案手动估算行高：
1. `EstimateColumnCharWidth` 用**表头文本长度 × 4** 估算每列能容纳的字符数
2. `SetExcelRowHeight` 用 `text.Length / colChars` 估算行数，设 `row.Height = 行数 × 15`，`CustomHeight = true`

### 问题

- 汉字未加倍计宽（`text.Length` 中汉字≈1，实际≈2），导致实际文本折行后被截断
- 列宽只从表头推算，与真实内容长度脱节（如附件列内容远超表头"附件列表"4 个字的长度）

### 改动

- 移除 `SetExcelRowHeight` 方法、`GetCellTextValue` 辅助方法、`EstimateColumnCharWidth` 方法、`columnCharWidths` 估算块
- 新数据行不再设 `CustomHeight`，Excel 打开时自动根据列宽+内容完整计算行高

### 副作用

- 行高精准显示全部内容，但无额外呼吸空间（Excel auto-fit 默认只留少许边距）
- 如果将来自定义列宽，行高会自适应调整，无需修改代码

---

## 七、Word 导出列偏移 Bug（2026-07-10 修复）

**文件**：`LedgerWriter.cs` - `ProcessWordTable()`

### 问题

Word 导出时数据比对应的列靠右了一列。

### 根因

原写法遍历 `columnMapping` 字典，用 `kv.Key` 直接索引 `newCells[kv.Key]`：

```csharp
foreach (var kv in columnMapping)
{
    int colIndex = kv.Key;
    var cell = newCells[colIndex];
```

当模板中某些列的表头文本未被别名匹配到时，`columnMapping` 会缺少对应的键，导致键值不连续（如 {1, 2, 3} 而非 {0, 1, 2, 3}）。此时直接以 key 作为 cell 索引会错位。

### 修复

改为按单元格实际顺序遍历，用位置 `ci` 查找映射：

```csharp
for (int ci = 0; ci < newCells.Count; ci++)
{
    if (!columnMapping.TryGetValue(ci, out var fieldName))
        continue;
    // 填充 cell
}
```

与 Excel 写入逻辑一致（Excel 也按 `pos++` 位置序查找 mapping），避免键值不连续时的索引错位。

---

## 八、交互重构：自动扫描 + 增量添加（2026-07-10 实现）

### 背景

原设计有「扫描文件」按钮，用户需手动点击。但切换文件格式时 `ColumnMapping` 索引体系不同（Excel=1-based, Word=0-based），`_templateResult` 未随模板切换失效时会产生列偏移。

### 改造

**移除手动「扫描文件」按钮**，改为自动触发：

```
用户添加文件夹 ──→ _foldersDirty = true ──→ TryAutoScanAsync()
用户切换目标文件 ──→ _templateDirty = true ──→ TryAutoScanAsync()
                                                      ↓
                                              EnsureScanned()
                                               ├── AutoReadTemplate()
                                               └── AutoScanFolders()
                                                      ↓
                                              HasResult = true
                                              按钮自动点亮
```

### 脏标记机制

两个独立脏标记，各自触发各自更新：

| 标记 | 触发时机 | 执行方法 |
|------|---------|----------|
| `_foldersDirty` | 添加/删除文件夹 | `AutoScanFolders()` |
| `_templateDirty` | 切换目标文件、导出完成 | `AutoReadTemplate()` |

`EnsureScanned()` 按序处理两个标记，先模板后文件夹。

### 并发保护

`TryAutoScanAsync` 用 `IsScanning` 做互斥锁。扫描期间追加操作虽被跳过，但扫描完成后会检测脏标记是否仍然存在，若是则递归重扫一次。

### 文件夹列表管理

**用户独享控制权**——程序绝不增删改列表结构。扫描结果通过更新现有项的显示属性（`DocumentName`、`AttachmentCount`、`HasScanned`）来反映，列表完整性由用户操作（粘贴/回车/拖放/浏览/删除）独占。

### 按钮状态

```
CanFill = HasResult && !IsScanning
```
开始生成按钮仅在「自动扫描完成」且「未在扫描/生成中」时启用。导出完成后自动重置并触发新一轮扫描。

### 增量添加

- 粘贴路径文本 → 立即解析并逐条添加（去重），随后统一触发扫描
- 按 Enter → 同粘贴逻辑
- 拖放文件夹 → 同上
- 浏览多选 → 同上

---

## 九、单实例运行

**文件**：`Program.cs`

```csharp
private const string MutexName = "AvaLedger_SingleInstance_7E4B2A1C";

[STAThread]
public static void Main(string[] args)
{
    using var mutex = new Mutex(true, MutexName, out var createdNew);
    if (!createdNew) return;  // 已有实例，直接退出
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```

`using var mutex` 确保程序退出时自动释放互斥体。

---

## 十、追加模式（保留旧数据）

**文件**：`LedgerWriter.cs`、`MappingPanelViewModel.cs`、`MappingPanel.axaml`

### 需求

默认启用「追加模式」——生成新文件时**保留模版中已有的数据行**，新扫描的公文追加在后面。取消勾选则跟原来一样（清除旧数据后重写）。无论哪种模式，原始模版文件本身不做任何改动，始终基于模版拷贝生成新文件。

### UI 布局

```
文号前缀配置  输入前缀和单位...  已加载 3 个前缀配置  [+ 添加行]
...
文件夹列表（每行带删除按钮）
...
[☑ 追加模式]    文件夹:2 公文:5...   [开始生成]
```

### ViewModel

新增 `AppendMode` 属性，绑定到 CheckBox。

### LedgerWriter

`Write()` 方法新增 `appendMode` 参数，传入 `WriteExcel` / `WriteWord`：

- **追加模式**：找到表头下方已有数据行 → **不删除** → 在最后一行之后插入新数据
- **覆盖模式**：跟原来一样，删除所有旧数据行后重写

```csharp
// Excel 追加示例
if (!appendMode) {
    foreach (var row in existingDataRows) row.Remove();
}
uint startIdx = (appendMode && existingDataRows.Count > 0)
    ? existingDataRows[^1].RowIndex!.Value + 1   // 追加：从最后一行之后
    : headerRowIdx + 1;                            // 覆盖：从表头下一行
```

---

## 十一、窗口设置

### 屏幕居中

```xml
<Window WindowStartupLocation="CenterScreen"/>
```

### 自适应高度

```xml
<Window SizeToContent="Height" Width="860"/>
```

### 防止文本选中

点击窗口空白处时让 TextBox 失去焦点——根 Grid 设置 `Background="Transparent"` + `PointerPressed` 事件，让 Grid 自身获取焦点。

### 窗口拖拽移动

在失焦的同一事件中追加 `Window.BeginMoveDrag(e)`：

```csharp
private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (e.Source is TextBox or Button) return;  // 交互控件不触发
    // 清焦点
    if (sender is Control c) { c.Focusable = true; c.Focus(...); }
    // 启动拖拽
    var window = TopLevel.GetTopLevel(this) as Window;
    window?.BeginMoveDrag(e);
}
```

在非交互区域（卡片背景、空白区、提示文字等）按下左键可拖动整个窗口，单击不动则不触发拖动。

---

## 十二、数据流概览

```
用户操作
  │
  ├── 添加源文件夹 → 粘贴 / 拖放 / BrowseFolder / Enter
  │     └── AddFolderPath() → _foldersDirty = true → TryAutoScanAsync()
  │
  ├── 选择目标文件 → BrowseTarget / 粘贴 / 拖放
  │     └── TargetFilePath → _templateDirty = true → TryAutoScanAsync()
  │
  ├── 配置文号前缀 → 前缀表格（持久化到 %LOCALAPPDATA%/AvaLedger/prefixes.dat）
  │
  ├── 自动扫描 → TryAutoScanAsync()
  │     ├── EnsureScanned()
  │     │   ├── AutoReadTemplate()    → TemplateReader.Read()
  │     │   └── AutoScanFolders()     → FileScanner.ScanFolders()
  │     └── HasResult = true → 按钮点亮
  │
  └── 导出 → 点击「开始生成」
        └── EnsureScanned() (防呆补扫)
        └── LedgerWriter.Write()
              ├── WriteExcel()        OpenXml SDK
              └── WriteWord()         OpenXml SDK
        └── TriggerScan() (导出后自动重扫，复位按钮状态)
```

---

## 十三、附：Avalonia 样式优先级

```
Application.Styles（我们的自定义样式）
    ↑ 高于
FluentTheme（主题默认样式）
    ↑ 高于
控件默认样式
```

**特例**：FluentTheme 控件模板内部的 `VisualState` 在事件处理后应用，会覆盖大多数样式设置。解决方案是覆盖模板内部引用的资源键（如 3.4 所述）。

---

## 十四、单元测试

**项目位置**：`F:\【Program】\文件台账工具Ava.Tests\`（与主项目同级，因 .NET SDK 的 ProjectReference 问题，不能放在主项目目录内）

**框架**：xUnit + Microsoft.NET.Test.Sdk

**运行方式**：
```bash
dotnet test                    # 在主项目目录
dotnet test                    # 在测试项目目录
```

**技巧**：在 VS/Rider 的测试资源管理器中可以单个/批量运行，失败用例双击跳转到代码行。

### 14.1 FileScannerTests.cs（28 个用例）

通过创建临时目录和文件来模拟真实的文件系统场景，用完自动清理。

| 类别 | 用例数 | 覆盖内容 |
|------|--------|---------|
| 文件分类 | 9 | Docx/Pdf 正文识别、附件、杂音排除、同名去重（docx>doc>pdf）、"关于…的…"句式、附件图片 vs 杂音图片、空文件夹 |
| 文号提取 | 5 | 完整文号、已知前缀、无前缀核心、无文号、无序号（"〔2026〕号"） |
| 日期提取 | 5 | 横线 `2026-04-16`、点号 `2026.3.2`、中文 `2026年4月16日`、8位数字 `20260416`、无日期 |
| 整体流程 | 2 | 容器多子公文、前缀匹配单位名覆盖 |

### 14.2 LedgerWriterTests.cs（20 个用例）

纯逻辑测试，不依赖文件系统。

- **GetFieldValue**（12 个）：逐一验证每个字段名的返回值，含空附件、未知字段、文号去括号等边界
- **StripOuterBrackets**（8 个）：中文方括号/圆括号、英文圆括号、花括号，以及空串/单字符/左右不匹配等边界

### 14.3 Services_OfficialDocumentTests.cs（13 个用例）

测试别名匹配的完整性和健壮性。

- 所有标准别名均能正确匹配对应标准名
- 空格/全角空格归一化、空/null 输入、未知列名、部分匹配等边界场景

