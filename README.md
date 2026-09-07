# Excel2GuanLiDe

**Excel → 管立得 / AutoCAD 管网数据自动化工具** 🚰📐

目标：把设计人员每天需要手工录入、整理、定位的 Excel 管网数据，尽量变成“一键识别 → 自动生成节点 → 导出 CAD 数据”。

## 当前版本：V0.1 MVP

已经写入仓库的核心功能：

- Excel `.xlsx/.xlsm` 导入
- 自动识别常见字段：桩号、节点编号、地面高程、管底高程、管径、管材、节点水压等
- 支持 `K0+000`、`K1+235.6`、纯数字米数等桩号
- 桩号统一换算为米并自动排序
- 相同桩号自动合并节点
- 缺失/异常桩号检查
- 自动生成 `N001、N002...` 节点编号
- 导出标准化节点 CSV
- 导出完整标准化数据 CSV
- AutoLISP 读取节点 CSV，在 AutoCAD 中按实际桩号距离生成节点、桩号、高程、管底高程、水压文字
- AutoLISP 按桩号顺序生成连续管线中心线
- GitHub Actions 自动编译 Windows x64 单文件程序

## 文件结构

```text
Excel2GuanLiDe/
├─ src/Excel2GuanLiDe/          # Windows 桌面程序
├─ AutoLISP/
│  ├─ Excel2GuanLiDe.lsp        # 节点/标注生成
│  └─ Excel2GuanLiDe_Pipe.lsp   # 管线中心线生成
├─ examples/                    # 示例数据
├─ docs/                         # 开发文档
└─ .github/workflows/            # 自动编译
```

## 使用方式

### 1. 编译程序

Windows 电脑安装 .NET 8 SDK 后执行：

```powershell
dotnet restore src/Excel2GuanLiDe/Excel2GuanLiDe.csproj
dotnet publish src/Excel2GuanLiDe/Excel2GuanLiDe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

编译后的程序位于 `publish/Excel2GuanLiDe.exe`。

### 2. 导入 Excel

Excel 第一行放表头，例如：

| 节点编号 | 桩号 | 地面高程 | 管底高程 | 管径 | 管材 | 节点水压 |
|---|---|---:|---:|---:|---|---:|
| W1 | K0+000 | 1523.600 | 1522.100 | 400 | HDPE | 28.6 |

程序会自动识别字段，不要求必须使用完全相同的表头。

### 3. 导出节点 CSV

点击“导出节点 CSV”，生成：

`管立得_节点数据.csv`

### 4. AutoCAD 生成节点

在 AutoCAD 2018 中：

1. 输入 `APPLOAD`
2. 加载 `AutoLISP/Excel2GuanLiDe.lsp`
3. 输入 `E2GDNODE`
4. 选择节点 CSV
5. 指定 K0+000 起点
6. 输入出图比例，例如 `2000`
7. 输入文字高度，例如 `3`

程序会根据真实桩号距离计算 CAD 中节点位置。

### 5. 生成管线中心线

加载 `AutoLISP/Excel2GuanLiDe_Pipe.lsp` 后输入：

`E2GDPIPE`

它会按照桩号顺序连接节点生成连续管线中心线。

> 注意：连续连接适合单一路径。存在大量分支、环网或明确的起终点关系时，应使用 Excel 中的“起点/终点”字段建立拓扑关系，后续版本会增加自动拓扑生成。

## 关于“直接导入管立得”

当前版本采用 **Excel → 标准化 CSV → AutoCAD/管立得辅助绘图** 的稳定路线，不虚构管立得的内部专用格式。

下一阶段会加入“管立得专用适配器”：根据用户提供的管立得 2024 实际导出/导入样例，自动生成对应格式，从而实现真正的一键导入。

## 版本路线

- **V0.1** Excel 识别、桩号标准化、节点生成、CSV、AutoLISP ✅
- **V0.2** 节点拓扑、起终点关系、管径/材质自动继承
- **V0.3** 纵断面自动生成、节点水压自动匹配
- **V0.4** 管立得 2024 专用导入适配
- **V1.0** Windows 一键式 Excel → 管立得完整工具

## 目标环境

- Windows 10 / Windows 11
- AutoCAD 2018+
- 管立得 2024
- Excel / WPS 导出的 `.xlsx/.xlsm`

## 免责声明

本工具用于辅助设计数据整理和 CAD 绘图。工程成果仍需由设计人员依据项目资料、设计规范和软件计算结果复核。
