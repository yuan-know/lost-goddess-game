# 字体调试场景设计（FontDebug）

日期：2026-09-10
状态：已与用户确认，实施中

## 1. 目的

为《失落的女神》（古风 + 神秘题材 2D 解谜）选型 UI/标题字体。在一个可滚动的调试场景中，把所有候选字体用**同一段游戏文案、同一字号**渲染出来，并且每款字体同时走 **老 UGUI `Text`** 与 **TextMeshPro（TMP 动态 SDF）** 两套渲染，便于：

1. 人工挑选字体（用户看截图/在编辑器里 Play 后报编号）；
2. 顺带对比 UGUI 与 TMP 的实际观感，为"是否迁移 TMP"提供依据。

场景为纯调试用途，不加入 Build Settings，不进正式包。定标后删除 `Assets/_Project/Fonts/Debug/` 与本场景即可。

## 2. 现状

- Unity 2022.3 LTS；`com.unity.textmeshpro 3.0.9`、`com.unity.ugui 1.0.0` 已装。
- TMP 从未使用：项目内无 TMP 字体资产、脚本无 `TMPro` 引用；现有 15 个脚本使用老 `UnityEngine.UI.Text`。
- `Assets/_Project/Fonts/` 存在但为空。
- 现有调试场景传统：`Scenes/Sandbox.unity`、`Scenes/AIAnimSandbox.unity`。

## 3. 候选字体清单

均为免费可商用字体，每款只取 Regular/常规一个字重以控制体积。分类：

- **A 宋/明体**
  1. 思源宋体 Noto Serif SC（OFL，GitHub 自动）
  2. 站酷仓耳渔阳体（站酷免费商用，**手动下载**）
  3. 源样明体 GenYoMin（OFL，GitHub 自动，取 TTC）
- **B 楷体**
  4. 霞鹜文楷 LXGW WenKai（OFL，GitHub 自动）
  5. 江西拙楷（免费商用，**手动下载**）
  6. 演示悠然小楷（免费商用，**手动下载**）
  7. 演示春风楷（免费商用，**手动下载**）
  8. 演示秋鸿楷（免费商用，**手动下载**）
- **C 毛笔/手写**
  9. 沐瑶软笔手写体（免费商用，**手动下载**）
  10. 站酷小薇 LOGO 体（站酷免费商用，Google Fonts 自动）
  11. 清松手寫體（免费，**手动下载**）
  12. 马善政毛笔楷书（OFL，Google Fonts 自动，额外候选）
  13. 刘建剪影手写体（OFL，Google Fonts 自动，额外候选）
- **D 现代标题**
  14. 得意黑 Smiley Sans（OFL，GitHub 自动）
  15. 站酷庆科黄油体（站酷免费商用，Google Fonts 自动）
  16. 阿里巴巴普惠体 3.0（免费商用，**手动下载**，OSS 403 需浏览器）
  17. 站酷酷黑（站酷免费商用，**手动下载**）

> 说明：初版菜单为 14 款，C 类增加 2 款 Google Fonts 可直链的毛笔/手写字体（12、13），总计 17 款；手动下载若用户懒得补，场景仍可正常生成，缺几款显示几款。

## 4. 产物与目录

```
Assets/_Project/
├─ Fonts/
│  └─ Debug/                       # 候选字体原文件（ttf/otf/ttc）
│     ├─ TMP_Generated/            # 自动生成的 TMP 动态 SDF 资产（勿手改）
│     └─ _下载清单.md               # 每款：文件名/分类/授权/URL/自动 or 手动/勾选框
├─ Scenes/
│  └─ FontDebug.unity              # 生成出来的调试场景
└─ Scripts/
   ├─ Editor/
   │  └─ FontDebugSceneBuilder.cs  # 菜单 Tools/字体调试/生成字体调试场景
   └─ Utils/
      └─ FontDebugLiveText.cs      # 运行时输入框联动
tools/font_debug/
└─ download_fonts.py               # 走 7897 代理下载可直链字体
```

## 5. 场景布局（参考分辨率 1920×1080）

- Canvas：Screen Space - Overlay，CanvasScaler `ScaleWithScreenSize`，1920×1080，match=0.5。
- **顶栏（固定，高 ~110）**
  - 标题："字体调试 · UGUI / TMP 双列对照"（用 Arial/自带字体显示，避免干扰候选观感）
  - UGUI `InputField`（占位提示："输入任意文字实时替换全部样张，清空恢复默认"）
- **滚动区（ScrollRect，剩余区域）**
  - VerticalLayoutGroup + ContentSizeFitter，14~17 张卡片竖排，卡片间距 12。
  - 每张卡片（宽 ~1760，浅色底图 + 边框）：
    - 卡片头（自带字体，22 号）：`编号. 中文字体名（文件名）` + 分类标签 + 授权标签
    - 行标签"UGUI"/"TMP"（24 号，灰色，自带字体）
    - 三档样张（每档独立组件，富文本关闭；UGUI 行 3 个 `Text`，TMP 行 3 个 `TextMeshProUGUI`）：
      - 44 号：`失落的女神`
      - 28 号：`古老的神殿在千盏灯熄灭之后，沉入了永夜。`
      - 20 号：`女神 失落 谜题 日记 一二三四五六七八九十 0123456789 ABCabc ，。！？：""「」……—·`

三档不同字号在同一个 Text 组件内无法实现 → 实际每档各一个组件：每款字体共 2 行 × 3 档 = 6 个样张组件。

## 6. TMP 动态资产策略

- 每款字体在 `Fonts/Debug/TMP_Generated/` 生成一个 `TMP_<字体名>.asset`：
  - `FontEngine.LoadFontFace(font)` 后 `CreateFontAsset()`（默认即 Dynamic，atlas 512×512、SDFAA）；
  - 保存前把 atlas 宽度提到 1024 以减少运行时扩容；
  - 源字体用 `font` 字段直接引用同目录 `Font`（不拷贝字体名），移动文件夹后重跑生成即可。
- 已存在同名资产则复用，不重复生成。
- TTC（源样明体）取 face index 0。
- 生成失败时在 Console 明确报字体名，该卡片 TMP 行显示"TMP 资产生成失败，见 Console"，UGUI 行不受影响。

## 7. 编辑器生成脚本（FontDebugSceneBuilder）

菜单：`Tools/字体调试/生成字体调试场景`。

流程：
1. `AssetDatabase.FindAssets("t:Font", new[]{"Assets/_Project/Fonts/Debug"})` 后按路径过滤为**仅根目录直放**文件（排除 `TMP_Generated` 等子目录），按文件名排序；
2. 缺失的 TMP 动态资产生成/补齐；
3. NewEmptyScene → 搭 Canvas/EventSystem/顶栏/ScrollView；
4. 每款字体生成一张卡片（头部 + 6 个样张），TMP 样张挂对应动态资产；
5. 挂 `FontDebugLiveText`，注入全部样张组件与默认文案；
6. 保存为 `Assets/_Project/Scenes/FontDebug.unity`。

幂等：重跑覆盖场景文件、复用已有 TMP 资产。字体数为 0 时 `EditorUtility.DisplayDialog` 警告但仍生成场景，卡片区放提示文字。

## 8. 运行时联动（FontDebugLiveText）

- 监听输入框 `onValueChanged`：
  - 非空：6 个样张组件全部替换为输入文本（保持各自字号）；
  - 空：恢复三档默认文案。
- 用 `[Serializable]` 条目数组在 Inspector 持有全部引用（由生成脚本注入），不做运行时查找。
- 纯展示：无按钮、无点击、无业务逻辑、不写任何 GameFlag/存档。

## 9. 下载脚本与清单

- `tools/font_debug/download_fonts.py`：读内置清单，`HTTPS_PROXY=http://127.0.0.1:7897`，自动下载 9 款可直链字体：
  - GitHub releases：霞鹜文楷 Regular ttf、得意黑 ttf（zip 解包）、思源宋体 SC subset otf（zip 解包取 SC Regular）、源样明体 TTC（zip 解包）；
  - Google Fonts 仓库 raw：站酷小薇、庆科黄油、马善政、刘建剪影。
  - 已存在文件跳过（按大小校验）。
- `_下载清单.md`：全部 17 款的表格（分类/文件名/授权/URL/自动✅手动⬜），手动 8 款给猫啃网/站酷/阿里官网页面链接；用户把文件**直放**在 `Fonts/Debug/` 根目录；生成脚本只扫描根目录（不含子目录），`TMP_Generated/` 与 zip 解包残留目录因此天然被排除。

## 10. 异常与边界

- 字体不齐：生成照常，几款显示几款；Console 输出缺失清单。
- 同名去重：按文件名（不含扩展名）。
- 一个字体文件含多 face（.ttc）：UGUI 与 TMP 均用 face 0。
- Unity 编辑器需在脚本写入后获得焦点触发编译；通过 `EditorApplication.delayCall` 无法跨域执行菜单，故生成动作由用户/桌面自动化在编译完成后触发一次菜单点击。

## 11. 实施顺序

1. spec 文档提交；
2. 下载清单 md + python 脚本，运行自动下载；
3. 两个 C# 脚本；
4. 等 Unity 编译，触发生成菜单；
5. 打开 FontDebug 场景、Play、截图给用户挑选；
6. 用户报编号 → 后续另起任务做正式字体替换（不在本 spec 范围）。

## 12. 非目标（YAGNI）

- 不做加粗/斜体/描边效果对比（先选字形，效果后调）；
- 不做点击选中、复制名称、一键应用全局字体；
- 不做字号/行距调节面板；
- 不接入正式游戏 UI，不改现有 15 个脚本；
- 不引入 Windows 系统字体（华文行楷等），授权不允许打包。
