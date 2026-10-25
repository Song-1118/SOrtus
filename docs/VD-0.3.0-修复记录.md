# VD-0.3.0 修复记录（VD-060~080 + 0.1.0 遗留 6 项）

> 对应缺陷单：《SOrtus 视觉缺陷修复单-0.3.0》（基线 0.3.0 / HEAD `b0d948c` + 工作区）
> 本记录范围：VD-060~080（21 项）+ 0.1.0 遗留未闭环 6 项（VD-003 / 022 / 053 / 054 / 056 / 057），合计 27 项。
> 不在范围：VD-081~105、VD-107~120（缺陷单其余条目，留待下一轮）。
> 验证基线：`dotnet build` 0 警告 0 错误、`--smoke` PASS、单元测试 117 项全绿、`--shot` 13 张实机截图（`outputs/vd-0.3.0/`）。

## 一、P0 阻断（2 项）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-060 | 已修复 | `MainWindow.xaml`（`WindowChrome`：`CaptionHeight=44` / `ResizeBorderThickness=6` / `GlassFrameThickness=0` / `UseAeroCaptionButtons=False`；顶栏品牌标记 + 三键）、`MainWindow.xaml.cs`（`WindowCommand`、`WM_GETMINMAXINFO` 钳工作区、`PlayThemeTransition`）、`Controls.Button.xaml`（`CaptionButtonStyle` / `CaptionCloseButtonStyle`）、`Icons.xaml`（`IconMinimize` / `IconMaximize` / `IconRestore`） | `08/10`：顶栏含三键、无系统标题栏；`12-maximized.png` 实测 1280×760＝工作区（不含 40px 任务栏），外距归 0 |
| VD-061 | 已修复 | `Brushes.xaml`（全部主题画刷 `x:Shared="False"`）、`ThemeService.cs`（换字典后遍历可视树 `InvalidateProperty` 重挂资源引用）、`BlurPanel.cs`（覆盖层 `SetResourceReference`，`x:Shared` 关联） | `08-settings.png` 与「浅→深」往返后的 `10-settings-after-theme-switch-dark.png` **MD5 完全一致**（`a750d47661f2`）；`09` 为同一页面切到浅色后的实拍，无深色残留 |

## 二、0.1.0 遗留未闭环（6 项）

| 旧编号 | 并入 | 结论 | 落点 |
| --- | --- | --- | --- |
| VD-003 | VD-076 | 已修复 | `Sizes.xaml`（`AppFontFamily`）、`Controls.Base.xaml`（TextBlock 隐式样式）、`MainWindow.xaml`（根视觉 `TextElement.FontFamily`） |
| VD-022 / 053 / 054 | VD-106 | 已修复 | `ViewTemplates.xaml`（GIT 工具条：`IconCloudUpload` / `IconLink` / `IconRefresh`，顺序 推送 > 添加远端 > 刷新）、`Icons.xaml`、`Strings.resx`（`Git_Push` → 「推送」） |
| VD-056 | VD-087 | 已修复 | `ProjectListViewModel.cs`（`ClearFiltersCommand` / `NoResultsText`）、`ViewTemplates.xaml`（工具栏按 `IsEmpty` 隐藏、空结果给匹配数与清除按钮）、`Strings.resx`（`List_Subtitle` 只留统计 + 新增 4 条） |
| VD-057 | VD-120 | 已修复 | `ViewTemplates.xaml`（三处页面标题）、`Strings.resx`（`List_Title` / `Wizard_TitleStepType` / `Wizard_TitleStepInfo` / `Settings_Title`） |

## 三、全局动效（VD-062~068）

| 编号 | 结论 | 落点 |
| --- | --- | --- |
| VD-062 | 已修复 | `Animations.xaml`（`MicroIn` / `MicroOut` / `PressIn` / `PressOut` / `FocusRingIn` / `FocusRingOut` / `FadeIn` / `SlideUp` / `PopIn` / `OverlayIn` + 4 个 OnLoad/触发样式）；`Controls.Base/Button/Input/Display.xaml` 各自合并动画字典（`StaticResource` 只在字典自身作用域可见） |
| VD-063 | 已修复 | 页面：`ViewTemplates.xaml` 的 `PageCardStyle`（BasedOn 隐式 SOrCard）；版块：`SectionFadeInStyle`；弹窗：`MainWindow.xaml`（遮罩 `IsDialogOpen` 淡入 + `PopInStoryboard`）、`MainViewModel.cs`（`IsDialogClosing` + 140ms 后卸载） |
| VD-064 | 已修复 | 同上，一键删除模板内联 Storyboard（120ms 进 / 160ms 出 `CubicEaseOut`） |
| VD-065 | 已修复 | `Controls.Button.xaml`（`PART_Root` 加 `ScaleTransform` + `RenderTransformOrigin`，`PressIn/Out` 驱动 0.98） |
| VD-066 | 已修复 | `Controls.Button / Input / Base / Display.xaml`（焦点环改独立 `PART_FocusRing` + `FocusRingIn/Out`） |
| VD-067 | 已修复 | `MainWindow.xaml.cs` `PlayThemeTransition`（120ms → 0.86、300ms → 1）+ `App.xaml.cs` 只在主题变化时调用 |
| VD-068 | 已修复 | `Controls.Input.xaml`（下拉 `PopupAnimation=None` + `OverlayInOnLoadStyle`、箭头 `RotateTransform` 旋转、日历 `Placement=Bottom`）、`SOrDatePicker.cs`（`Popup.Opened` 给内置 Calendar 加同样的入场动画） |

## 四、全局排版与视觉（VD-069~077）

| 编号 | 结论 | 落点 |
| --- | --- | --- |
| VD-069 | 已修复 | `Sizes.xaml`（`FontSizeCardTitle` / `CardTitleBarWidth` / `CardTitleGap`）、`Controls.Card.xaml`（`CardTitleBorderStyle` / `CardTitleTextStyle`）、`ViewTemplates.xaml`（14 处卡片标题） |
| VD-070 | 已修复 | `Sizes.xaml`（`MultilineMinHeight` / `MultilineMaxHeight`）、`Controls.Input.xaml`（`AcceptsReturn` 触发器）、`ViewTemplates.xaml`（删除 11 处硬编码高度（56/70/72/80）） |
| VD-071 | 已修复 | `MarkdownRenderer.cs`（正文 14 / 行高 24、列表 22、表格 13 / 20）、`Sizes.xaml`（`FontSizeDocBody` 等令牌留档） |
| VD-072 | 已修复 | `Light.xaml`（`ColorScrollThumb #8C949F` / Hover `#6B7280`）、`Dark.xaml`（`#6E7681` / `#9AA3AF`） |
| VD-073 | 已修复 | `Light.xaml` / `Dark.xaml`（`ColorInputSurface` / `ColorBorderStrong`）、`Brushes.xaml`（`InputSurfaceBrush` / `BorderStrongBrush`）、`Controls.Input.xaml`（输入框 / 下拉 / 日期三处） |
| VD-074 | 已修复 | `Light.xaml` / `Dark.xaml`（`ColorTagNeutralBorder`）、`Brushes.xaml`（`TagNeutralBorderBrush`）、`Controls.Display.xaml`（`SOrTag` 描边，语义色调不描边） |
| VD-075 | 已修复 | `Colors.xaml`（移除共享 `ColorDanger/Success`，避免重复键永远取旧值）、`Light.xaml` / `Dark.xaml`（各一套语义色）、`Controls.Button.xaml`（Danger 变体改软危险） |
| VD-077 | 已修复 | `Light.xaml` / `Dark.xaml`（`ColorSurfaceAlt`）、`Brushes.xaml`（`SurfaceAltBrush`）、`Controls.Card.xaml`（`GroupCardStyle`）、`ViewTemplates.xaml`（13 处分组卡） |
| VD-076 | 已修复 | 见「二、遗留 6 项」VD-003 |

## 五、主窗口（VD-078~080）

| 编号 | 结论 | 落点 |
| --- | --- | --- |
| VD-078 | 已修复 | `MainWindow.xaml`（`MinWidth=1040` / `MinHeight=680`） |
| VD-079 | 已修复 | `MainViewModel.cs`（`_listViewModel` 复用 + `Initialize` 落缓存）、`ProjectListViewModel.cs`（`ScrollOffset`）、`Controls/ScrollMemory.cs`（双向绑定还原）、`ViewTemplates.xaml`（列表 `ScrollViewer` 绑定） |
| VD-080 | 已修复 | `MainWindow.xaml`（提示条改浮层、居中、`-8px` 落下淡入、不占布局）、`App.xaml.cs`（启动检查 3s → 5s） |

## 六、验证

1. `dotnet build src/SOrtus/SOrtus.csproj -c Debug`：0 警告 0 错误。
2. `SOrtus.exe --smoke`：PASS（9 个自定义控件 + 9 个视图模板 + 主题/语言切换）。
3. `dotnet test`：117 项通过（新增 2 项：`List_ClearFilters_ResetsConditionsAndReloads`、`Main_ReturningToList_KeepsSameListViewModel`）。
4. `SOrtus.exe --shot`：13 张（`outputs/vd-0.3.0/`），其中
   - `00-before-08-settings.png` 为改动前同一页面对照（可见旧版分组卡与字段标签几乎无层级差）；
   - `08` / `10` 字节级一致 → VD-061 回归；
   - `12-maximized.png` 1280×760 → VD-060 工作区钳制与外距归零。
5. 颜色令牌按 WCAG 2.1 公式实测（对比度写在各令牌注释里）：输入框边界 3.22:1 / 3.30:1、滚动条滑块 3.06:1 / 3.30:1、危险色文字 6.57:1 / 7.82:1。

## 七、已知未验证 / 遗留

- **交互行为无法离屏验证**：顶栏拖动、双击最大化、Aero Snap、边缘拖拽改尺寸、右键系统菜单依赖真实窗口交互，本次仅以 `WindowChrome` 的标准组合 + 最大化几何实测覆盖，需人工点一遍。
- **换肤过渡（VD-067）** 是 300ms 的瞬时视觉，静态截图无法留证，仅代码走查 + 期间无残留（`08`≡`10`）。
- **页面出场动画**：本版只做了入场（旧内容不淡出直接替换），未引入「旧内容淡出 90ms」的双层快照机制。
- 缺陷单其余条目（VD-081~105、VD-107~120）未处理；其中 VD-081（弹窗焦点陷阱）、VD-083（配置类弹窗禁止点遮罩关闭）、VD-109（模糊滑块性能）建议列入下一批。
