# VD-0.3.0 修复记录（VD-060~120 + 0.1.0 遗留 6 项）

> 对应缺陷单：《SOrtus 视觉缺陷修复单-0.3.0》（基线 0.3.0 / HEAD `b0d948c` + 工作区）
> 本记录范围：**VD-060~120（61 项）** + 0.1.0 遗留未闭环 6 项（VD-003 / 022 / 053 / 054 / 056 / 057），合计 67 项，另含 3 项用户反馈（焦点框开关 / 更新开关 / 顶栏图标同源）。
> 验证基线：`dotnet build` 0 警告 0 错误、`--smoke` PASS、单元测试 127 项全绿、`--shot` 20 张实机截图 + 3 条运行时自检（`outputs/vd-0.3.0/` 与 `outputs/vd-0.3.0-third/`）。

## 一、P0 阻断（VD-060 / VD-061）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-060 | 已修复 | `MainWindow.xaml`（`WindowChrome`：`CaptionHeight=44` / `ResizeBorderThickness=6` / `GlassFrameThickness=0` / `UseAeroCaptionButtons=False`；顶栏品牌标记 + 三键）、`MainWindow.xaml.cs`（`WindowCommand`、`WM_GETMINMAXINFO` 钳工作区、`PlayThemeTransition`）、`Controls.Button.xaml`（`CaptionButtonStyle` / `CaptionCloseButtonStyle`）、`Icons.xaml` | 顶栏含三键、无系统标题栏；`12-maximized.png` 实测 1280×760 ＝ 工作区（不含 40px 任务栏），外距归 0；真实窗口 Win32 读数 `WS_CAPTION=NO`、客户区 ＝ 窗口矩形 |
| VD-061 | 已修复 | `Brushes.xaml`（主题画刷全部 `x:Shared="False"`）、`ThemeService.cs`（换字典后遍历可视树 `InvalidateProperty`）、`BlurPanel.cs`（覆盖层 `SetResourceReference`） | `08-settings.png` 与「浅→深」往返后的 `10-…dark.png` **MD5 相同**（`a750d47661f2`） |

## 二、0.1.0 遗留未闭环（6 项 → 并入 VD-076 / 106 / 087 / 120）

| 旧编号 | 并入 | 结论 | 落点 |
| --- | --- | --- | --- |
| VD-003 | VD-076 | 已修复 | `Sizes.xaml`（`AppFontFamily`）、`Controls.Base.xaml`、`MainWindow.xaml`（根视觉下发） |
| VD-022 / 053 / 054 | VD-106 | 已修复 | `ViewTemplates.xaml`（GIT 工具条：`IconCloudUpload` / `IconLink` / `IconRefresh`）、`Icons.xaml`、`Strings.resx`（`Git_Push` →「推送」） |
| VD-056 | VD-087 | 已修复 | `ProjectListViewModel.cs`、`ViewTemplates.xaml`（工具栏按 `IsEmpty` 隐藏、空结果给匹配数与清除按钮） |
| VD-057 | VD-120 | 已修复 | `ViewTemplates.xaml`（三处页面标题）、`Strings.resx` |

## 三、全局动效与视觉（VD-062~077）

| 编号 | 结论 | 落点 |
| --- | --- | --- |
| VD-062 | 已修复 | `Animations.xaml`（10 组 Storyboard + 4 个触发样式）；`Controls.Base/Button/Input/Display.xaml` 与 `ViewTemplates.xaml` 各自合并动画字典 |
| VD-063 | 已修复 | 页面 `PageCardStyle`、版块 `SectionFadeInStyle`、弹窗 `MainWindow.xaml` + `MainViewModel.IsDialogClosing`（140ms 淡出后卸载） |
| VD-064 / 065 / 066 | 已修复 | 微交互 120/160ms；按压 0.98 缩放 90/140ms；焦点环 100ms 进、0ms 出 |
| VD-067 | 已修复 | `MainWindow.PlayThemeTransition`（120ms → 0.86、300ms → 1），仅在主题真变时播放 |
| VD-068 | 已修复 | `Controls.Input.xaml`（浮层 8px + 150ms、箭头 `RotateTransform`、日历 `Placement=Bottom`）、`SOrDatePicker.cs` |
| VD-069~077 | 已修复 | 卡片标题档 + 左竖条（`Controls.Card.xaml`）；多行输入 64/180 自适应；文档 14/24；滚动条 3.06:1；输入框边界 3.22:1 / 3.30:1；标签描边；危险/成功语义收口；卡内分组卡降一阶（`SurfaceAltBrush`） |
| VD-078~080 | 已修复 | 最小尺寸 1040×680；列表实例复用 + `ScrollMemory`；更新提示改浮层并延后到 5s |

## 四、主窗口（VD-081~083）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-081 | 已修复 | `MainWindow.xaml`（`PART_DialogHost` + `KeyboardNavigation.*Navigation=Cycle`）、`MainWindow.xaml.cs`（`MoveFocusIntoDialog` / `RestoreFocusAfterDialog`）、`Controls/FormField.cs`（`FindFirstField`）、`ViewTemplates.xaml`（确认 / 未保存 / 配置远端三处主按钮 `IsDefault=True`；取消不用 `IsCancel`——Esc 已由窗口 `KeyBinding` 统一走取消路径，两者并存会重复执行一次取消） | `--shot` 自检：`[FOCUS] 07-document-dialog dialogFocused=True element=SOrButton` |
| VD-082 | 已修复 | `MainViewModel.cs`（`RecentProjects` / `RefreshRecentAsync` / `VersionLine`）、`MainWindow.xaml`（侧边栏「最近立项」+ 页脚版本行） | `01b-list-with-items.png` 侧边栏列出 3 条 |
| VD-083 | 已修复 | `IOverlayDialogViewModel.AllowScrimClose`（默认 true）、`AddRemoteDialogViewModel`（false + `HasInput` + `ConfirmEsc`）、`MainViewModel.ScrimCloseCommand`、`MainWindow.xaml`（遮罩绑独立命令） | 单击遮罩无效；第一次 Esc 出提示「再按一次 Esc 放弃已填写的内容」 |

## 五、立项列表（VD-084~090）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-084 | 已修复 | `SOrProjectListItem.cs`（`OnPreviewMouseLeftButtonDown` + 按钮命中放行）、`Controls.Display.xaml`（去掉双击绑定、整行 ToolTip）、`Strings.resx`（`List_OpenHint` →「单击打开详情」） | `01b-list-with-items.png`；单击行为由代码路径覆盖 |
| VD-085 | 已修复 | `SOrTextBox.IsClearable / ClearCommand`、`Controls.Input.xaml`（`PART_Clear` + 三条触发器）、`ProjectListViewModel.ClearSearchCommand` | `List_ClearSearchCommand_ClearsKeyword`；搜索框有内容时出现 × |
| VD-086 | 已修复 | `ProjectListViewModel.cs`（`SearchDebounceMs=250`、`_loadVersion`、`CancellationTokenSource`、`MarkTotalDirty`） | `List_SearchDebounce_CollapsesRapidKeystrokes`（三次击键 → 一次查询） |
| VD-088 | 已修复 | `Controls.Display.xaml`（进度条右侧 12px 百分比） | `01b-list-with-items.png`：65% / 40% / 10% |
| VD-089 | 已修复 | 上一批 `List_Subtitle` 只留统计；本轮复核全站仅设置页出现「数据留在本地」 | 截图 |
| VD-090 | 已修复 | `Converters.cs`（同 `List_UpdatedAt` / 跨年 `List_UpdatedAtCrossYear`） | `UpdatedAtConverter_ShowsYearOnlyWhenCrossYear`；`01b`：`更新于 2025-10-25` |

## 六、立项向导（VD-091~098）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-091 | 已修复 | `ProjectWizardViewModel.cs`（一次查完全部必填、`FocusRequested`、`ErrorMessage` 汇总）、`IFieldFocusSource`、`Controls/FormField.cs`（`Anchor` / `RevealAndFocus` / 抖动）、`MainWindow.xaml.cs`（`OnFieldFocusRequested`）、`SOrTextBox / SOrComboBox / SOrDatePicker.IsInvalid` | `--shot` 自检：`[FOCUS] fieldAnchor=Name element=SOrTextBox`；`05b-wizard-validation-error.png` 显示红框 + 两条字段错误 + 页脚「还有 2 项必填未完成」 |
| VD-092 | 已修复 | `ProjectWizardViewModel`（`IsDirty` / `ConfirmLeaveAsync` / `CancelCommand`）、`MainViewModel.ConfirmLeaveCurrentAsync`（纳入向导）、`ViewTemplates.xaml` 页脚「取消」 | `Wizard_ConfirmLeave_OnlyAsksWhenDirty` |
| VD-093 | 已修复 | `SOrButton` 新增 `Card` 变体（`Controls.Button.xaml` + `SOrButton.cs`）、模板 `PART_ActiveRing` / `PART_Check` + `CardActiveIn/OutStoryboard`（160ms 淡入，Setter 保底）、`ViewTemplates.xaml`（去掉 `MinHeight=340`、内容顶部对齐） | `02-wizard-step1.png` |
| VD-094 | 已修复 | `ViewTemplates.xaml`（预设圆点 +「已选 N 项」）、`ProjectWizardViewModel.SelectedTechStackCountText` | `03-wizard-step2.png` |
| VD-095 | 已修复 | `ViewTemplates.xaml`（等分两列 +「至」）、`Strings.resx`（`Wizard_DateTo`） | `03b-wizard-step2-bottom.png` |
| VD-096 | 已修复 | `ViewTemplates.xaml`（`IconPlus` +「添加」）、`Strings.resx`（`Wizard_TechStackAdd`） | `03-wizard-step2.png` |
| VD-097 | 已修复 | `Strings.resx`（`Wizard_ClipFeat2/3`、`Wizard_TodoFeat3`、`Wizard_TypeClipDesc`、`Wizard_TypeTodoDesc`）、`ViewTemplates.xaml`（第 2 步提示按类型分支） | 文案与详情页字段一一对应 |
| VD-098 | 已修复 | `ViewTemplates.xaml`（标题右侧步进器）+ `Strings.resx`（`Wizard_Step2Hint` / `Wizard_Step2HintNoStack`） | `02` / `03` 两张截图的右上角 |

## 七、立项详情（VD-099~100）

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-099 | 已修复 | `Controls/ScrollReset.cs`（附加属性绑定 `ActiveSection`）、`ViewTemplates.xaml`（详情 `ScrollViewer`） | `04b-detail-basic-bottom.png` → 切版块 → `04c-detail-dev-after-scrolled-basic.png`（页面回到顶部） |
| VD-100 | 已修复 | `SOrButton` 新增 `Tab` 变体（`PART_TabIndicator` + `TabIndicatorIn/OutStoryboard`）、`ViewTemplates.xaml`（三个 tab）、`Strings.resx`（`Detail_Basic` →「项目字段」） | `04-detail-basic.png` / `04c-…png` |

## 八、第三批（VD-101~120 + 3 项用户反馈）

**立项详情**

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-101 | 已修复 | `ViewTemplates.xaml`（危险操作卡片，标题 + 说明 + 软危险按钮）、`Strings.resx`（`Detail_DangerZoneTitle/Hint`） | `05a-detail-dev-bottom.png` |
| VD-102 | 已修复 | `ProjectDetailViewModel.cs`（`SaveButtonText` / `_isJustSaved` / `_savedFlashTimer`）、`ViewTemplates.xaml` | 保存后按钮短暂「已保存」 |
| VD-103 | 已修复 | `Strings.resx`（`Detail_CardDevPlan`） | `05a` 卡片标题不再是空竖条 |
| VD-104 / 105 | 已修复 | `ViewTemplates.xaml`（文档卡 `×` + ToolTip） | 文档卡区域截图 |
| VD-113 | 已修复 | `DocumentDialogViewModel.cs`（`DropDuplicateLeadingTitle`） | `07-document-dialog.png`（标题只出现一次） |
| VD-114 | 已修复 | `ViewTemplates.xaml`（文档弹窗右上关闭） | 同上 |
| VD-116 | 已修复 | `ProjectDetailViewModel` / `ProjectWizardViewModel`（`ErrorDetail` + `AppLog`，加载/导入/删除分路径 try-catch）、`Services/AppLog.cs` | 横幅一句人话 + 详情可展开 |
| VD-117 | 已修复 | `SOrButton.IsDestructive` + `Controls.Button.xaml` 隐式样式 Trigger、`ConfirmDialogViewModel.IsDestructive`、`IDialogService.ConfirmDestructiveAsync` | `13-destructive-confirm.png` |
| VD-118 | 已修复 | `ViewTemplates.xaml`（确认 / 未保存弹窗「取消」统一 `Secondary`） | `13-destructive-confirm.png` |

**GIT 管理**

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-107 | 已修复 | `GitViewModel.cs`（`TimelinePageSize=10` / `TimelineWindow=50` / `_allCommits` / `ShowMoreCommitsCommand` / `TimelineSummaryText`）、`ViewTemplates.xaml`、`Strings.resx`（`Git_TimelineCount(Window)` / `Git_TimelineMore`） | `06b-detail-git-more.png`、单测 `Git_Timeline_*` |

**设置页**

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-108 | 已修复 | `SOrToggleSwitch.cs` + `Controls.Input.xaml`（开关样式）+ `Animations.xaml`（`ToggleOn/OffStoryboard`）、`ViewTemplates.xaml` | `08b-settings-bottom.png` |
| VD-109 | 已修复 | `IThemeService`（`PreviewBlurStrength` / `CommitBlurStrength`）、`ThemeService.cs`、`BlurPanel.cs`（拖动摘缓存 + 220ms 挂回） | 拖动手感（交互验证） |
| VD-110 | 已修复 | `MainViewModel.RefreshLocalizedText` → `OnCultureChanged` 转发 | 切英文后无半中半英 |
| VD-111 | 已修复 | `ViewTemplates.xaml`（「手动检查」标签）、`Strings.resx`（`Settings_CheckNowLabel`） | `08b-settings-bottom.png` |
| VD-112 | 已修复 | `ViewTemplates.xaml`（打开文件夹 / 复制路径）、`SettingsViewModel.CopyDatabasePathCommand`、`Strings.resx` | `08b-settings-bottom.png` |

**更新流程**

| 编号 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| VD-115 | 已修复 | `UpdateViewModel.cs`（`LaterCommand` → Snooze）、`UpdateState.cs`（`SnoozedVersion` / `SnoozedUntilUtc` / `SnoozeDuration`）、`UpdateService.cs`（snooze 过滤）、`ViewTemplates.xaml` | `11-update-toast.png` |

**用户反馈**

| 项 | 结论 | 落点 | 验证 |
| --- | --- | --- | --- |
| 焦点框开关（默认开） | 已实现 | `Services/FocusRingSettings.cs`（应用级把 `FocusRingBrush` 覆盖为透明）、`AppSettings.ShowFocusRing`、`SettingsViewModel.ShowFocusRing`、`App.xaml.cs`（启动时应用）、`ViewTemplates.xaml`（外观分组开关 + 状态文案） | `08-settings.png` / `08b` |
| 更新按钮改开关 | 已实现 | 同 VD-108 | `08b-settings-bottom.png` |
| 顶栏 logo 与图标同源 | 已实现 | `MainWindow.xaml`（`Image` + pack URI 引用 `Resources/icon/app.ico`）、`Resources/icon/app.ico` 与 `EXE/assets/app.ico` 替换为用户提供的 `favicon_multi.ico`、`outputs/brand/mark.json`（`iconSourceOverride`）、`outputs/brand/gen_app_ico.py`（守卫） | 顶栏图标 = 任务栏图标；`08`/`09`/`10` 三主题下均可见 |

## 九、验证

### 9.1 第一批 / 第二批（VD-060~100）

1. `dotnet build src/SOrtus/SOrtus.csproj -c Debug`：0 警告 0 错误。
2. `SOrtus.exe --smoke`：PASS（9 控件 + 9 视图模板 + 主题/语言切换）。
3. `dotnet test`：123 项通过（第一批新增 8 项）。
4. `SOrtus.exe --shot`：17 张（`outputs/vd-0.3.0/`），含三条运行时自检输出：
   - `[FOCUS] 07-document-dialog dialogFocused=True`（VD-081）
   - `[FOCUS] 05b-wizard-validation-error fieldAnchor=Name element=SOrTextBox`（VD-091）
   - `08` ≡ `10` 字节级一致（VD-061 回归）
5. 颜色令牌按 WCAG 2.1 公式实测（对比度写在各令牌注释里）：输入框边界 3.22:1 / 3.30:1、滚动条滑块 3.06:1 / 3.30:1、危险色文字 6.57:1 / 7.82:1。

### 9.2 第三批（VD-101~120 + 3 项用户反馈）

1. `dotnet build`：0 警告 0 错误。
2. `dotnet test`：**127 项通过**（新增 4 项：GIT 分页三态、不足 10 条不出按钮、破坏性确认标记、焦点框开关无 `Application` 实例不抛）。
3. `SOrtus.exe --smoke`：PASS。
4. `SOrtus.exe --shot`：**20 张**（`outputs/vd-0.3.0-third/`），新增四张针对性证据：
   - `05a-detail-dev-bottom.png`：危险操作卡片（标题 + 说明 + 软危险按钮），确认删除已离开页脚。
   - `06b-detail-git-more.png`：时间线汇总「共 4 条提交 · 最近 MM-dd HH:mm」，无残留键名。
   - `13-destructive-confirm.png`：破坏性确认弹窗，主按钮实心危险底、默认焦点在「取消」。
   - `08b-settings-bottom.png`：设置页下半段，确认「显示焦点框」在**外观**分组、更新为开关、「手动检查」「复制路径」标签齐全。
5. 主题回归改为走设置页下拉真实路径：`09`（浅 `#F7F8FA`）/ `10`（深 `#1E2126`）像素级取样符合各自主题，下拉框文字与页面配色一致。
6. `build/check_locale_keys.py`：中英各 284 键、XAML 与 C# 引用全部命中（`OK`）。
7. 图标链路：`src/SOrtus/Resources/icon/app.ico` 与 `EXE/assets/app.ico` MD5 相同（`57bfdbaf…`），7 档 16~256 / 32bpp；顶栏 `Image` 直接引用该 ico（pack URI），不再有第二份手绘几何。

## 十、已知未验证 / 遗留

- **真实指针交互无法离屏验证**：顶栏拖动、双击最大化、Aero Snap、边缘拖拽改尺寸、列表单击打开、tab 切换的 200ms 指示条动画。已用等价证据覆盖：Win32 窗口样式 `WS_CAPTION=NO` 且客户区 ＝ 窗口矩形（顶栏）、`04c` 截图（切版块回顶）、`--shot` 焦点自检。
- **tab 指示条**不是跨 tab 移动的共享指示块，而是每个 tab 自身的 2px 线从中间展开（200ms）。达到「选中项有明确下划线」的目标，未实现「随选中项滑动」。
- **VD-083 的 Esc 二次确认**用「再按一次」而不是二次确认框：确认框会替换覆盖层里的当前弹窗，关闭它等于把输入一起丢掉。
- **焦点框开关**只经过「资源覆盖生效」的静态验证（`FocusRingBrush` 被替换为透明、控件模板全部用 `DynamicResource` 取该画刷）；真实键盘 Tab 巡览下的视觉消失需在交互环境确认。
- **安装包未重新打包**：本轮只替换了仓库内两处 ico 源并重新构建。已安装的 0.3.0 不会自动换图标，需按 `EXE/Publish-Release.ps1` 重新 publish + iscc 出包后才生效（改 ico 必须重打包，这是 Inno Setup 的嵌入时机决定的）。
