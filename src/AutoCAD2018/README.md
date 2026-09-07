# Excel2GuanLiDe AutoCAD 2018 插件

这是一个真正加载到 AutoCAD 2018 的 .NET 插件。

## 已实现

- `EXCEL2GLD`：选择 `.xlsx/.xlsm/.csv`，自动识别桩号、节点、高程、管底高程、管径、管材、节点水压。
- 桩号支持 `K0+000`、`K1+235.6` 和纯数字米数。
- 自动按桩号排序，并合并重复桩号。
- 自动生成节点圆、桩号、地面高程、节点编号。
- 桩号和高程文字默认旋转 90°。
- 按真实桩号距离 / 出图比例计算 CAD 坐标。
- `GLDPIPE`：按桩号顺序生成管线中心线。

## AutoCAD 2018 安装

1. 使用 Visual Studio 或 `dotnet build` 编译。
2. 在 AutoCAD 2018 输入 `NETLOAD`。
3. 选择生成的 `Excel2GuanLiDe.dll`。
4. 输入 `EXCEL2GLD` 开始导入。
5. 如 AutoCAD 提示安全加载限制，把插件所在文件夹加入 `TRUSTEDPATHS`。

AutoCAD 2018 的 Managed .NET SDK 对应 .NET Framework 4.6，本项目按该目标编译。插件依赖 AutoCAD 的 AcCoreMgd、AcDbMgd、AcMgd，不把 AutoCAD DLL 打包到输出目录。

## 推荐 Excel 表头

| 节点编号 | 桩号 | 地面高程 | 管底高程 | 管径 | 管材 | 节点水压 |
|---|---|---:|---:|---:|---|---:|
| W1 | K0+000 | 1523.600 | 1522.100 | 400 | HDPE | 28.6 |
| W2 | K0+125.5 | 1522.900 | 1521.400 | 400 | HDPE | 27.9 |

## 重要说明

当前版本是 AutoCAD/管立得环境中的通用自动化插件，不虚构管立得 2024 的私有数据格式。若管立得运行在 AutoCAD 2018 上，可先通过 `NETLOAD` 加载并使用上述命令；真正的“直接写入管立得内部数据库/专用格式”需要管立得官方 SDK 或一份真实导出文件作为适配依据。
