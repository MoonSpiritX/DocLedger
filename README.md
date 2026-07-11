# 文件台账工具 Ava

> 一个轻量的公文台账辅助工具，自动扫描文件夹结构，识别文号、发文单位、附件信息，填充到 Excel/Word 模板。

## 功能

- 扫描源文件夹（每个公文一个文件夹），自动识别公文正文、附件和杂音文件
- 从文件夹名/文件名中提取文号（支持自定义前缀）
- 自动提取发文日期、发文单位
- 附件按文件名排序，与主公文关联
- 排序输出：按日期升序排列
- 支持 .xlsx 和 .docx 模板
- **自动扫描**：添加文件夹或切换模板后自动识别，无需手动触发
- **增量添加**：支持多批文件夹连续粘贴/拖放，自动去重
- **追加/覆盖模式**：追加模式保留模板既有数据行，覆盖模式清空重写
- Native AOT 编译，单 exe 发布，无需安装 .NET 运行时

## 截图

> （选用中）

## 使用流程

```
1. 选择目标台账文件（.xlsx 或 .docx 模板）→ 自动读取表头映射
2. 添加源公文文件夹（粘贴/拖放/浏览多选，支持多批增量添加）→ 自动扫描
3. 配置文号前缀（可选，提高识别准确率）
4. 检查识别结果（公文名、附件数）
5. 点击「开始生成」导出填充后的文件
```

> 已取消手动的「扫描文件」按钮，添加文件夹或切换模板后自动触发扫描，
> 扫描完成后「开始生成」按钮自动点亮。

## 技术栈

| 组件 | 说明 |
|------|------|
| **框架** | .NET 10 + Avalonia UI 12 |
| **架构** | MVVM（CommunityToolkit.Mvvm 源生成器） |
| **文档** | DocumentFormat.OpenXml（直接操作 xlsx/docx） |
| **测试** | xUnit（61 个用例，覆盖核心逻辑） |
| **发布** | Native AOT（单文件原生 exe，~56MB） |

## 构建与发布

```bash
# 开发运行
dotnet run

# 发布单文件（需要 .NET 运行时）
dotnet publish -c Release -p:PublishSingleFile=true

# 发布 Native AOT（独立 exe，不依赖运行时）
dotnet publish -c Release -aot

# 指定平台
dotnet publish -c Release -aot -r win-x64
dotnet publish -c Release -aot -r linux-x64
dotnet publish -c Release -aot -r osx-arm64
```

## 运行测试

项目包含 61 个单元测试，覆盖文件分类、文号提取、日期识别、字段取值、别名匹配等核心逻辑。

```bash
# 在项目根目录下运行（推荐）
dotnet test

# 或在测试项目目录下运行
cd 文件台账工具Ava.Tests
dotnet test
```

也可在 IDE（VS / Rider）的测试资源管理器中可视化运行。

## 项目结构

```
Logic_OfficialDocument/
├── FileScanner.cs              # 文件扫描与智能分类
├── MappingPanel.axaml / .cs    # 主界面 + 粘贴/拖放/Enter 事件
├── MappingPanelViewModel.cs    # 主界面 ViewModel（自动扫描逻辑）
├── TemplateReader.cs           # 模板读取与列映射
├── LedgerWriter.cs             # Excel/Word 数据写入
├── OfficialDocumentInfo.cs     # 数据结构模型
├── Converters.cs               # 值转换器
└── Services_OfficialDocument.cs
    (Services/)                 # 字段别名字典

文件台账工具Ava.Tests/          # 单元测试项目
├── FileScannerTests.cs         # 文件分类/文号/日期提取测试（28 个用例）
├── LedgerWriterTests.cs        # 字段取值/去括号测试（20 个用例）
└── Services_OfficialDocumentTests.cs  # 别名匹配测试（13 个用例）
```

## 关于 AOT

项目已移除对 ClosedXML 的依赖，使用 DocumentFormat.OpenXml 直接操作 Excel，
满足 Native AOT 编译要求。发布产物是一个不依赖 .NET 运行时的独立可执行文件。

## 许可证

[MIT](LICENSE)

## 作者

**斜月 (MoonSpirit)** — 374532719@qq.com
