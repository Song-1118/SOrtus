# 更新日志（CHANGELOG）

本文件遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 格式；版本号遵循 SemVer（0.x 阶段 minor 可含不兼容变更）。版本号单一来源为 `build/version.props`，与程序集版本、安装包版本、关于页版本保持一致。

覆盖范围：自首个提交 `a5b574a`（2026-10-24 08:37）至标签 `v0.1.2`（提交 `6337db5`）；`0.2.0` 起直接按版本条目记录。

---

## [0.3.0] - 2026-10-25

本版闭合《SOrtus 视觉缺陷修复单-0.3.0》**VD-060~120（61 项）** 与 **0.1.0 遗留 6 项**（VD-003 / 022 / 053 / 054 / 056 / 057，按单内合并关系并入 VD-076 / 106 / 087 / 120 一并修复），合计 67 项，另含 3 项用户反馈（焦点框开关、更新开关、顶栏图标同源）。逐项落点见 `docs/VD-0.3.0-修复记录.md`。

### 修复（P0 阻断）

- **VD-061 深色模式分组卡片白底黑字**：根因是主题画刷以**单实例**资源存在，被 WPF 冻结后内部的 `Color="{DynamicResource …}"` 就地固化，运行时整本换主题后画刷不再跟随。修法：`Brushes.xaml` 全部主题画刷改 `x:Shared="False"`；`ThemeService` 换字典后遍历可视树对承载画刷的依赖属性逐个 `InvalidateProperty` 重新求值；`BlurPanel` 覆盖层改用 `SetResourceReference`，不再在建层时把画刷实例拍进 `Fill`。回归证据：`08-settings.png` 与「浅色→深色」往返后的 `10-settings-after-theme-switch-dark.png` **字节级一致**。
- **VD-060 窗口三键并入应用顶栏**：`WindowStyle=None` + `WindowChrome`（`CaptionHeight=44`、`ResizeBorderThickness=6`、`UseAeroCaptionButtons=False`），拖动 / 双击最大化 / Aero Snap / 右键系统菜单交由系统处理；顶栏自绘品牌标记、标题与 46×32 三键（关闭键悬停浅红）。最大化尺寸由 `WM_GETMINMAXINFO` 钳到显示器工作区，同时外距 22 收为 0。

### 修复（遗留 6 项 · 全站）

- **VD-076（旧 VD-003）字体栈**：`Segoe UI, Microsoft YaHei, PingFang SC, sans-serif` → `Microsoft YaHei UI, Microsoft YaHei, Segoe UI, PingFang SC`（中文优先、去无效 `sans-serif` 兜底），收敛为 `AppFontFamily` 令牌，在根视觉下发，控件与正文同源。
- **VD-106（旧 VD-022 / 053 / 054）GIT 工具条**：「刷新 / 添加远端地址 / 推送至云端」三枚纯文字按钮改为图标 + 短标签；刷新不再借用放大镜（新增 `IconRefresh`）、添加远端用 `IconLink`、推送用 `IconCloudUpload` 并缩短为「推送」；主次按 推送 > 添加远端 > 刷新 排列，刷新降为图标按钮靠最右。
- **VD-087（旧 VD-056）空列表与空结果**：副标题只留统计（`共 N 个立项`），不再与空状态文案重复定位句；一个立项都没有时整条搜索 / 筛选工具栏隐藏，空状态承接全部引导；筛选后无结果给出「共 N 个被过滤」与「清除筛选」按钮。
- **VD-120（旧 VD-057）页面标题不再与导航同名**：列表页「立项」→「全部立项」；向导标题带步骤「新立项 · 1/2 选择类型 / 2/2 填写信息」；设置页「设置」→「设置 · 外观与数据」。

### 修复（VD-062~068 全局动效）

- **VD-062** `Animations.xaml` 的 3 组 Storyboard + 3 个 OnLoad 样式全部接线（此前 0 引用）；各控件字典显式合并动画字典，Storyboard 以 `PART_Hover / PART_Press / PART_FocusRing / PART_Root` 命名层为目标。
- **VD-063** 页面入场 220ms 淡入 + 8px 上移；详情版块切换 160ms 淡入；弹窗入场 240ms 弹入（遮罩 180ms 淡入）、关闭时 140ms 淡出后再卸载。
- **VD-064** 微交互节奏统一为 120ms 进 / 160ms 出 `CubicEaseOut`，删除模板内联的两套私有节奏。
- **VD-065** 按压反馈增加 0.98 缩放（90ms 进 / 140ms 出）。
- **VD-066** 焦点环 100ms 淡入、离开立即消失。
- **VD-067** 换肤过渡：根视觉 120ms 压到 0.86、180ms 回 1；仅在主题真正变化时播放（拖模糊滑块不闪）。
- **VD-068** 下拉浮层与日历浮层统一「8px 上移 + 150ms 淡入」，下拉箭头改 `RotateTransform` 旋转；日历从「永远向上」改为统一向下。

### 修复（VD-069~077 全局排版与视觉）

- **VD-069** 新增 `FontSizeCardTitle(15)` 档 + 卡片标题左竖条（形状语言：标题=竖条 / tab=下划线 / tag=描边胶囊），卡片标题与字段标签拉开层级。
- **VD-070** 多行输入统一 `MinHeight 64 / MaxHeight 180` 自动增长，删除 11 处 56/70/72/80 硬编码高度。
- **VD-071** 文档阅读区正文 13→14px、行高 21→24，列表项 22，表格单元 12→13px/行高 20。
- **VD-072** 滚动条滑块对比度 1.34:1 → 3.06:1（浅 `#8C949F` / 深 `#6E7681`），悬停 4.83 / 5.95。
- **VD-073** 输入框改为「低一阶填充 + ≥3:1 边界」（浅 `#F9FAFB` + `#88909D`＝3.22:1；深 `#1B1E23` + `#6E7681`＝3.30:1）。
- **VD-074** 中性 / 强调类标签加 1px 描边（浅 `#A9B1BD` / 深 `#5A6270`），胶囊形状可辨；语义标签保留纯色相。
- **VD-075** 危险与成功语义各收口一套（浅 `#B42318` / `#067647`，深 `#FDA29B` / `#75E0A7`），按钮改「浅底 + 深字 + 同色描边」软危险样式，实心高饱和红退出界面。
- **VD-077** 卡内分组卡改用 `SurfaceAlt`（浅 `#F7F8FA` / 深 `#1E2126`），卡片套卡片有明暗差。

### 修复（VD-078~080 主窗口）

- **VD-078** 主窗口 `MinWidth=1040 / MinHeight=680`。
- **VD-079** 列表视图模型在会话内复用同一实例，返回列表保留搜索词 / 类型 / 状态筛选；`ScrollMemory` 附加属性把纵向偏移双向绑到视图模型，返回后还原滚动位置。
- **VD-080** 更新提示条改为浮层（居中浮在内容上方、不占一行布局），进入时从 -8px 落下并淡入；启动检查从 3 秒延后到 5 秒。

### 修复（VD-081~100 第二批）

**主窗口**

- **VD-081** 对话框焦点：打开时把焦点搬进弹窗（优先第一个输入框，纯确认弹窗落到主按钮），关闭后归还触发它的控件；确认 / 未保存 / 配置远端三处主按钮设 `IsDefault`（Enter 即主操作，取消不走 `IsCancel` 以免与窗口 Esc 键位重复执行）；覆盖层容器设 `TabNavigation / DirectionalNavigation / ControlTabNavigation = Cycle` 形成焦点陷阱，Tab 不再「穿过」遮罩跑到背后的导航上。`--shot` 自检输出 `dialogFocused=True`。
- **VD-082** 侧边栏：导航下方补「最近立项」（更新时间最新 4 条，点击直达详情），标语改为页脚版本行 `v0.3.0 · 数据留在本机`；「数据留在本地」承诺保留在设置页。
- **VD-083** 配置远端弹窗不再响应点击遮罩关闭；Esc 对已有输入的内容改为「第一次拦下并提示、第二次才放弃」（不弹二次确认框——确认框会替换掉覆盖层里的当前弹窗，关掉它等于连输入一起丢）。

**立项列表**

- **VD-084** 卡片单击即打开（`SOrProjectListItem` 在 Preview 阶段命中；命中的是行内按钮时放行给按钮）；双击绑定已移除，手型光标不再是空承诺。`List_OpenHint` 改为「单击打开详情」并作为整行 ToolTip 渲染（此前写了从未显示）。
- **VD-085** 搜索框有内容时出现清除 ×（`SOrTextBox.IsClearable`，多行输入不显示）。
- **VD-086** 搜索改为 250ms 防抖 + 过期结果丢弃（版本号 + CancellationToken）；`TotalCount` 不再每次搜索都全表读一遍：无筛选时直接用结果数，有筛选时走缓存，只有新建 / 删除后（`MarkTotalDirty`）才重取。
- **VD-088** 列表项进度条右侧补 12px 百分比读数（65%），不再只能靠 72px 细条的长度猜。
- **VD-089** 副标题与营销语解耦（上一批已改），本轮复核「数据留在本机」全站只出现在设置页一次。
- **VD-090** 更新时间跨年显示完整日期（`更新于 2025-10-25`），同年仍是 `更新于 10-24`。

**立项向导**

- **VD-091** 校验失败不再静默 return：一次查完全部必填项、页脚给「还有 N 项必填未完成」、第一个错误字段 `BringIntoView` + 聚焦 + 2px 抖动 + 边框转语义危险色。`--shot` 自检输出 `fieldAnchor=Name`。
- **VD-092** 页脚补「取消」；新增 `IsDirty`（任一字段有输入）与离开守卫，点侧边栏 / 关闭窗口前先问「放弃已填写的内容？」。
- **VD-093** 类型卡新增 `Card` 变体：未选中 = 低一阶底色 + ≥3:1 明确边框，选中 = 2px 主色环 + 右上角对勾 + 底色提亮（160ms 淡入，Setter 保底）；去掉 `MinHeight=340`，卡片按内容自适应，不再「内容占上面 60%、下面空 40%」。
- **VD-094** 技术栈预设加选中圆点，已选区上方补「已选 N 项」小标题，两段式不再各说各话。
- **VD-095** 两个日期框改等分铺满 + 「至」连字符，右缘与同列其它输入框对齐。
- **VD-096** 自定义技术栈的「+」改为「图标 + 添加」的次要按钮。
- **VD-097** 剪辑 / 待办特性文案与详情页真实字段对齐（原「拍摄与剪辑双进度」「素材清单」「到点提醒」均未实现），类型卡描述同步校正；第 2 步提示按类型分支，不再对剪辑说「技术栈必填」。
- **VD-098** 标题右侧加固定步进器（①选择立项类型 —— ②填写立项信息），当前步实心高亮；两步都保留自己的说明文字。

**立项详情**

- **VD-099** 切换版块时滚动位置归零（`ScrollReset` 附加属性绑定 `ActiveSection`），不再从上一个版块的中段开始；`--shot` 用「滚到底再切版块」的序列截图取证。
- **VD-100** 版块切换改用 `Tab` 变体：未选中次要色、选中主色 + 底部 2px 指示条（200ms 展开；按 tab 自身从中间向两侧展开，不是跨 tab 移动的共享指示块）；「基础信息」卡片标题改名「项目字段」，消除与 tab 同名。

### 修复（VD-101~120 第三批 + 3 项用户反馈）

**立项详情**

- **VD-101** 删除动作从页脚搬进独立「危险操作」卡片：标题 + 「删除后立项及其全部文档将从本地数据库移除，无法撤销」说明 + 软危险按钮，与「保存」物理隔开，不再紧贴主操作。
- **VD-102** 保存按钮文案随状态变化（`保存` / `保存中…` / `已保存`），保存成功后按钮短暂显示「已保存」再回落，用户不必靠肉眼看数据判断是否落地。
- **VD-103** 开发管理卡片标题补齐（「版本迭代规划」等），此前键名不存在于资源字典，卡片只剩左侧竖条没有标题。
- **VD-104** 文档卡片删除改为 `×` 图标按钮并带 ToolTip，不再占一整颗文字按钮的宽度。
- **VD-105** 文档卡片「查看 / 替换 / 删除」补 ToolTip，避免三个同名按钮在不同文档行里无法区分作用对象。
- **VD-113** 文档弹窗正文首块若与标题同名（一级标题重复），渲染时摘掉那一块：同一句话不再在 60px 内出现两遍；只动渲染结果，不改原文、不影响待办索引。
- **VD-114** 文档弹窗补右上关闭按钮，此前只能点遮罩或按 Esc。
- **VD-116** 错误提示分层：横幅给一句人话（「导入失败，文件可能已被占用或格式不受支持」），原始异常放进可展开的「详情」区；加载 / 导入 / 删除三条路径分别兜住异常，不再静默失败。
- **VD-117** 破坏性确认：新增 `SOrButton.IsDestructive`（实心危险底 + 反色文字，与页面内软危险按钮拉开重量），标题左侧加危险图标，且**不设** `IsDefault`——默认焦点落在「取消」，Enter 不会误触删除。
- **VD-118** 确认弹窗的「取消」统一为 `Secondary`（有边），不再一处 `Secondary` 一处 `Ghost`。

**GIT 管理**

- **VD-107** 提交时间线分页：默认 10 条，底部「查看更早的 N 条」每点一次续显 10 条，取满 50 条窗口后按钮消失；末尾给出汇总（「共 N 条提交 · 最近 MM-dd HH:mm」/「已显示最近 N 条提交」），抓满窗口时不再把窗口当总数冒充全量。

**设置页**

- **VD-108** 「是否启用自动检查更新」由按钮改为开关：按钮表示动作、开关表示状态，与同页的下拉框、滑块语义统一；开关右侧保留「已开启 / 已关闭」文字作为补充。
- **VD-109** 模糊强度滑块拆成 `PreviewBlurStrength`（拖动中，只改渲染、不落盘）与 `CommitBlurStrength`（松手提交，写设置并持久化）；`BlurPanel` 拖动期间摘掉 `BitmapCache`、停手 220ms 再挂回，滑块不再拖一下卡一下。
- **VD-110** 语言切换后视图模型里拼好的文案同步刷新（`MainViewModel` 统一转发给当前页与常驻复用的列表页），不再出现「Settings + 共 3 个立项」的半中半英。
- **VD-111** 「立即检查」左侧补齐「手动检查」标签，此前左列 160px 全空、按钮孤零零悬在值列。
- **VD-112** 数据库位置可一键「打开所在文件夹」与「复制路径」，复制成功给出「路径已复制到剪贴板」。

**更新流程**

- **VD-115** 更新弹窗按钮收敛为「稍后 / 立即更新」两颗，「稍后」不再直接关窗而是推迟到下一版本或 24 小时后（`SnoozedVersion` / `SnoozedUntilUtc`），避免每次启动都被同一个版本反复打扰。

**用户反馈 3 项**

- **焦点框开关**：设置 · 外观新增「显示焦点框」，默认开。实现为资源级开关——全站控件模板的焦点层统一取 `FocusRingBrush`，关闭时在应用级把该画刷覆盖为透明，一次关掉按钮 / 输入框 / 下拉框 / 日期框 / 滑块 / 列表项 / 导航项的焦点环，不需要在十几处模板里各写触发器；输入框描边与光标保留，键盘焦点仍可辨。启动时按持久化设置应用（窗口创建前生效，不闪一帧）。
- **更新开关**：同 VD-108，按钮改为开关。
- **顶栏 logo 与图标同源**：顶栏此前是手绘的近似母题（「窗框 + 光点」），与应用图标不是同一份，于是「换了图标但界面没变」。现在顶栏直接 `Image` 引用 `pack://application:,,,/Resources/icon/app.ico`——与 csproj `ApplicationIcon`、安装器 `SetupIconFile` 同一文件；用户提供的新 `app.ico`（7 档 16~256，32bpp）已全局替换 `src/SOrtus/Resources/icon/app.ico` 与 `EXE/assets/app.ico`（MD5 一致）。`gen_app_ico.py` 增加 `iconSourceOverride` 守卫：`mark.json` 声明该字段后脚本拒绝再按旧母题覆盖 ico。

### 动效规范落地（《SOrtus 动效设计规范-0.3.0》M-01~M-57）

- **接线层**：新增 `Controls/Motion.cs`——统一注入 `RenderTransform`（规范 §7「Storyboard 写死变换路径时元素没有对应变换就静默不动」）、统一播放入口、统一「减少动效」降级（读 `SystemParameters.ClientAreaAnimation` + `SPI_GETMINANIMATE`）、统一动画计数。页面只声明意图（`Motion.PageEnter` / `StaggerIndex` / `RefreshPulse` / `PlayOnLoad`）。
- **令牌层**：`Animations.xaml` 补 `Std / Enter / Exit / Back` 四种缓动与六档时长用法注释；规范 §4 的 57 条逐条落地，其中 **32 条新接线、13 条沿用既有且已合规、2 条本版无场景、10 条如实记为未做**（逐条原因见 `docs/动效落地记录-0.3.0.md`）。
- **新增关键动效**：页面「先出后进」（旧页 90ms 淡出 → 新页 220/280ms 入场，且列表→详情 +12、详情→列表 −8 方向相反）、弹窗遮罩 180ms 先行 + 面板 240ms Back 弹入、列表项 40ms 错开入场（最多 8 项）、GIT 时间线 30ms 错开、设置页分组卡首次进入错开、危险确认按钮 120ms 颜色过渡（不是闪红）、保存回执 180ms 淡入 → 停 2.5s → 240ms 淡出、进度条 220ms → 400ms、主题切换 180 + 60ms 停留 + 180 下沉、语言切换 100 + 140 淡出淡入。
- **断言层**：新增 `build/check_animation_wiring.py`——要求 `Animations.xaml` 每个 `x:Key` 至少被引用一次（VD-062 的原始事故就是「3 组 Storyboard 全仓 0 引用」）。当前 **48 个动画资源全部接线**；未接线且无场景的资源已删除，不留「做了没接」的空转。
- **修掉三个真实缺陷**：① 页面内联 `<Style TargetType="local:SOr*">` 顶掉隐式样式导致控件无模板（确认弹窗主按钮整颗消失）——统一改为在控件字典里定义 keyed 样式并让页面字典合并；② 弹窗退场动画未走完就开下一个弹窗会被 `DialogService` 吞掉（截图 07 空白）——截图顺序已调整；③ **启动即崩溃**——设置页语言下拉在选中项为 null 时把 `settings.json` 写成 `"Culture": null`，下次启动 `new CultureInfo(null)` 抛异常，应用完全起不来；`AppSettings.Culture` setter、`LocalizationService` 与 `SettingsViewModel` 三处兜底，并让 `AppSettings.Load` 读到历史脏数据时自我修复（已补单测）。

### 用户反馈批次（暗黑边框 / 切换动画 / 加载动画 / 滑块 / 开关 / 图标居中 / 滚轮 / 非线性缓动）

- **暗黑主题输入框边框不可见**：实机取样发现输入框虽有 1px 边框，但深色下 `#6E7681` 对 `#1B1E23` 填充只有 3.73:1，主观上「与背景同色」。修法是**只改颜色不改粗细**（用户明确不要加粗）：深色 `ColorBorderStrong` → `#9AA4B2`（约 6.1:1），浅色 → `#98A1AE`；同时把 `SOrTextBox` 模板的边框引用由 `TemplateBinding BorderBrush` 改为直接取 `BorderStrongBrush`（前者实测未画出），日期框边框由弱 `BorderBrush` 升到强 `BorderStrongBrush`。
- **界面切换动画**：新增 `SectionSwitchInStyle`（淡入 + 8px 上移，180ms Enter），替换原先只做透明度变化的 `SectionFadeInStyle`（后者已无引用，删除以免「做了没接」）。接入四处：左侧菜单切换（页面级 `Motion.PageEnter`）、项目详情三个版块 tab、向导第 1/2 步、进入项目（列表→详情 +12px 上浮、返回 −8px 下落）。
- **GIT 加载动画**：新增 `SOrSpinner`（旋转弧线）+「正在读取仓库…」提示，绑定 `GitViewModel.IsBusy`。这是全站唯一允许的循环动画（规范负面清单第 7 条）。
- **滑块点击改为动画滑到点击处**：`SOrSlider` 覆写鼠标按下，命中拖块本体仍走默认拖动，命中轨道则接管——计算目标值后**只动画拖块的视觉偏移**（240ms CubicEaseOut），动画结束才写回 `Value`。这样绑定实时预览的场景（高斯模糊强度）不会逐帧改值、不会逐帧重建模糊缓存（规范负面清单第 1 条）。
- **开关高度收窄**：轨道 24 → 20px，滑块圆点 18 → 14px，开启位移 20 → 24px（动画与 Setter 保底同步）。
- **纯图标按钮图标居中**：按钮模板此前是「图标列 Auto + 内容列 *」，只有图标时图标贴在左侧；现改为内容为空时图标跨两列，整颗按钮水平居中。
- **文档预览滚轮灵敏度**：新增 `MouseWheelScroll` 附加属性，文档阅读区每格滚轮 6 行（系统默认 3），按正文行高 24px 换算像素并接管事件，避免系统再滚一次造成双倍位移。
- **全局非线性缓动**：补齐 8 处仍为线性插值的动画——类型卡淡出、tab 指示条收起、开关轨道色、占位符淡入淡出、加载指示器退出、进度条 400ms 填充（改 Exit 缓动）。规范令牌 `Std / Enter / Exit / Back` 已在 `Animations.xaml` 统一提供；仅加载指示的匀速旋转与 0ms 的焦点环失焦保持原语义。

### 用户反馈批次七（模糊=0 时悬停底色残留在背景里 · 顺带修 Theme=null 启动崩溃）

- **现象**（用户 2026-10-26）：高斯模糊强度为 0（无模糊）时，鼠标快速划过左侧菜单，某一项的悬停底色会残留在背景里，直到下一次划过才刷新；模糊 > 0 时不出现。用户目测确认「改前几轮必现、改后两轮不再出现」。
- **根因**：磨砂层在半径 0 时仍然挂着 `BlurEffect(Radius=0)` + `BitmapCache`。没有可模糊的内容，缓存把这块区域变成一张不再重绘的静态位图——「效果 + 位图缓存」的组合让脏区不刷新，悬停底色就被留在缓存里。半径 > 0 时效果每帧重算，把脏区盖住了，所以只有 0 才明显。
- **修复**：`BlurPanel` 在半径 ≤ 0.01 时**不挂 Effect、不挂 CacheMode**（纯色块没有可模糊内容，缓存毫无收益）；「拖动停手后恢复缓存」与「换肤清缓存」两处都加了「只有真的挂着模糊效果才恢复」的守卫；半径从 0 拉回 > 0 时重新创建 Effect。
- **回归护栏**：新增 `--hover-test`——真实光标高速扫过窗口内全部按钮后连续采样 1.6s，逐按钮统计「指针离开后悬停层仍存活的时长」，>600ms 判残留；模糊 0 与 12 两档各跑一遍。此前用「单点取色 / 前后对比」都漏判（残留恒定时前后都带着它，差值为 0），改时间线后才可靠。
- **顺带修 P0**：`settings.json` 里 `"Theme": null` 会让启动时的 `ApplyTheme(null)` 去加载 `resources/themes/.xaml`，抛 IOException，整个应用起不来。`AppSettings.Theme` 改为带兜底 setter、`Load()` 见到 `"Theme": null` 自愈写回、`ThemeService.ApplyTheme/SetTheme` 双保险，单测 `AppSettings_NullTheme_FallsBackToDefault` 守住。

### 用户反馈批次六（筛选器「全部」改为真实选项，默认选中）

- **改法（用户指定）**：不再把 `null` 本身当下拉项，也不再用 Placeholder 表示「无选中」。新增 `FilterOption` 包装（`Value` 为 null 即「全部」），`TypeOptions` / `StatusOptions` 的第一项就是「全部类型 / 全部状态」，构造时默认选中——「全部」与「编程开发 / 剪辑 / 待办」走**同一条选中链路**，从根上消除「选了非全部就选不回去」。
- `HasFilter` 改判 `FilterOption.Value is not null`；`ClearFilters` 复位到第一项（不再是 null）；查询层仍收到 `null`（= 不筛选），服务层与 SQL 条件零改动。
- 新增 `TypeFilterItemTemplate` / `StatusFilterItemTemplate`（绑定 `FilterOption.Value`，复用原转换器，null → 全部类型 / 全部状态）；详情页编辑状态下拉继续用原 `StatusItemTemplate`，两者互不影响。
- **实测证据**：`--combo-test` 覆盖「默认选中全部 → 切到编程开发 → 真实点击点回第一行」全链路——默认 `SelectedItem=All`、框内文字「全部类型」、占位符 `Collapsed`；切到编程开发后文字「编程开发」、占位符仍 `Collapsed`（无重叠）；点回第一行 `SelectedItem=All` → PASS。
- 单测新增 `List_Filters_DefaultToAllOption_AndAllIsSelectableBack`：默认 All → 查询层收到 null；选 Clip → 收到 Clip；选回 All → 回到 null。

### 用户反馈批次五（下拉框选中后占位符与选中内容重叠）

- **根因（实测复现）**：`--combo-test` 验证——选中枚举项（Programming）后，模板里 `SelectedItem==null` 的 `DataTrigger` **不退出**，`PART_Placeholder`（占位符「全部类型」）保持 `Visible`，与 `PART_ContentSite` 的选中内容**同时显示在框里**，正是用户报的「选择后选择的内容和原始内容全部留在框里造成重叠」。此前几轮修的都是浮层位置（间距 12px）与打开期间隐藏，未命中这个真因。
- **修复**：占位符可见性改由 `SOrComboBox` 代码控制——`SelectedItemProperty.OverrideMetadata` 监听选中变化 + `OnApplyTemplate` 初始同步，有选中项即 `Collapsed`、无选中项即 `Visible`；删除模板里不可靠的 `DataTrigger`，避免双源竞争。实测选中后 `占位符可见=Collapsed` 通过。
- 新增 `--combo-test` 自测（选中态 + 占位符可见性），守住该回归。

### 用户反馈批次四（导航悬停灰 / 下拉动画恢复 / 打开下拉收起旧文字）

- **左侧菜单栏悬停颜色**：原来导航复用全站 `HoverOverlayBrush`——深色主题下它是**白色 8% 叠加（悬停变亮）**，与「悬停变灰、移走恢复」的预期不符；该画刷全站共用不能直接改。新增 `NavHoverBrush`（深色 `#262A2F` / 浅色 `#EDEFF2`，均比各自的选中态弱一档，避免「悬停和选中看起来一样」），经模板触发器只对 Nav 变体替换覆盖层背景（`Style.Triggers` 的 Setter 不能设 TargetName，必须放 `ControlTemplate.Triggers`）；恢复由 `IsMouseOver=False` 保底 Setter 保证，与动画终值无关。
- **下拉动画恢复**（上轮按「改没了」删错，用户实际要保留）：恢复 `OverlayInStoryboard`（淡入 + 8px 上移 150ms）与 Popup 上的 `Motion.PlayOnShow` 引用。
- **打开下拉时收起触发器旧文字**（用户意见「菜单栏切换是删除原数据」）：浮层打开期间把 `PART_ContentSite`（原选中项「编程开发」）置为 Hidden——否则它与浮层第一行「全部类型」在视觉上叠在一起；用 Hidden 而非 Collapsed 保留布局空间，关闭后文字位置不跳。

### 用户反馈批次三（滑块回跳回归 / 下拉动画移除 / 按钮居中 / hover 残留）

- **滑块「点击到 A 又缩回去」**：上一轮为消除「动画持有 Value」把填充行为改成 `FillBehavior.Stop`——它的语义是动画一结束属性**立即弹回动画前旧值**，Completed 回调要到下一帧才写回目标值，用户看到的就是「进度条到 A 又缩回去、再点一次才过来」。改为默认 HoldEnd 保持到结束，Completed 里**同一帧内**先 `BeginAnimation(prop, null)` 摘掉动画再 `SetCurrentValue` 固化终值——两步之间没有渲染帧，既不留持有也不回跳。
- **下拉框动画移除**（用户要求）：删除 `OverlayInStoryboard` 及其在 Popup 上的引用，下拉框现在直接出现不做淡入。
- **刷新 / 标签删除按钮不居中**：根因是图标间距 `Margin` 写在 `<Path>` **元素本地属性**上——本地属性优先级高于 Style Trigger，Trigger 里的「清零」永远失效，纯图标按钮一直带着 8px 右间距；且 TemplateBinding 传 `HorizontalContentAlignment` 到容器实测不生效（回落 Stretch 导致内容贴左）。修复：容器 `StackPanel` 硬编码 `Center`（图标+文字整体居中），图标间距改由 `SOrButton` 代码按「有无文字」设置（`ContentProperty.OverrideMetadata` + `OnApplyTemplate`）。实测 GIT 刷新配置与标签删除配置**偏差均为 0**。
- **hover 背景残留**（鼠标滑过留下悬停底色）：动画（HoldEnd）在元素复用 / 状态快速翻转时可能把覆盖层 Opacity 遗留在 1。新增 `IsMouseOver=False` 的**保底 Setter**（PART_Hover / PART_Press 归零），与动画状态无关恒定兜底。
- 筛选下拉遮挡复验：类型 / 状态两个下拉实测浮层均在触发器下方 12px、不遮挡（`--shot` 内置断言两个都过）。

### 用户反馈批次二（P0 崩溃 + 下拉遮挡 + 滑块 + 搜索 + 五项优化）

**P0 崩溃（任何界面点击设置直接崩）**——根因是「Style Setter 的 `TranslateTransform` 被 WPF 冻结共享」：
- `SectionSwitchInStyle` / `OverlayInOnLoadStyle` 在 Setter 里放 `TranslateTransform Y=8`，WPF 会把该实例**冻结并共享给所有应用该样式的元素**；第一个元素动画成功后对象进入只读态，第二个元素再对它做动画即抛 `InvalidOperationException: 无法对 TranslateTransform 进行动画处理`——表现为「第一次进向导/设置正常，再次进入直接崩溃」。已迁移到 `Motion.PlayOnShow`（代码为每个元素注入独立 `TransformGroup`）并删除两个会冻结的样式；`SOrToggleSwitch` 同样改为「每次替换新变换实例」，且切换动画不再走 `Storyboard.Begin(this)`（模板 NameScope 未就绪时会抛「找不到 PART_Thumb」）。
- **入场动画初始态一律归一为 Y=0**：动画只提供 From→To 的位移，Setter 不再预留偏移，避免「动画没播」时残留 8px。

**筛选下拉遮挡触发器 / 选不回「全部」**：`Popup` 缺省 `PlacementTarget` 会 fallback 到模板内 28px 高的 ToggleButton，浮层整体上偏压住触发器，第一行「全部类型」被遮住点不到。修复：显式 `PlacementTarget` 指向 ComboBox 本体 + `VerticalOffset=12`（8px PopupRoot 固有边距补偿 + 4px 视觉间距）。`--shot` 内置回归断言（浮层不遮挡）。

**滑块拖动数值与圆点脱钩**：实测当前构建滑块本身正常（Track 原生管理圆点位置）；按「强绑定」建议加固——轨道点击动画改 `FillBehavior.Stop` + 结束时 `SetCurrentValue` 固化终值，消除 HoldEnd 期间动画持有 Value 的窗口期。

**搜索功能无效**：`SOrTextBox.Text` 自定义 DP 元数据缺少 `BindsTwoWayByDefault`——系统 TextBox.Text 自带该标志所以页面省略 `Mode=TwoWay` 也是双向，自定义 DP 不写就是 OneWay：用户打字只传到外壳，视图模型的 `SearchText` 永远收不到。已补上该标志，`--input-test` 新增「省略 Mode 回传 VM」用例守住（`回传正常=True`）。

**五项优化**：① 按钮内容居中——`HorizontalContentAlignment` 默认 Left→Center；纯图标按钮跨两列时清掉 `IconGap` 边距，实测偏差=0。② 向导工期「至」改用两行 Grid 与日期框同行居中（原为硬编码 `Margin=20`）。③ 日期框 Calendar 月/年视图接入 `CalendarButtonStyle`（原来是系统白底 + 系统蓝高亮）；顺带删除会 OOM 的自定义 `CalendarItem` 模板死代码。④ 弹窗光晕收敛——模糊半径 20→10、覆盖层 0.9→0.96。⑤ 遮罩点击不可关闭的弹窗时弹窗左右抖动 260ms（原来静默 return，像卡死）。

### 输入框不可输入——「改动画改坏了输入框」的根因与修复（f0d98fc 缺陷）

- **根因（git 考古定位到具体提交）**：`f0d98fc`（动效规范 M-21）给占位符淡入淡出加 `EnterActions/ExitActions` 时，**漏写了 `Storyboard.TargetName`**。模板触发器里没有 TargetName 的动画，作用对象是**模板宿主——整个输入框外壳自己**。于是：空框未聚焦时触发淡入 → 外壳 Opacity 被锁到 0.85；一点击（失焦/聚焦态翻转）→ 触发淡出 → **外壳 Opacity 被动画到 0**，整块输入框「闪一下就黑/消失」，光标也在 Opacity=0 的元素里看不见。有内容的框 `Text==""` 条件不成立、动画从不播放，所以一直正常——与用户观察完全一致。
- **修复一（根因）**：`PlaceholderInStoryboard` / `PlaceholderOutStoryboard` 补 `Storyboard.TargetName="PART_Placeholder"`，动画只作用于占位符本身。
- **审计（同类坑全局排查）**：扫描全仓在模板/Style 触发器里引用的动画资源，共 12 处「无 TargetName」——其中 11 处宿主语义正确（更新提示条、遮罩、弹窗面板、保存按钮、未保存徽标本就该动宿主），仅占位符这 1 处错误。
- **修复二（结构加固）**：`SOrTextBox` 由「继承 TextBox + 自定义模板」改为「外壳 ContentControl + **内嵌系统原生 TextBox**」。此前自定义模板下 TextBox 内部编辑区（`TextBoxView`）的输入焦点接不上（实测 `editorFocused=False`），且用系统色绘制导致「点击发黑」；现在光标定位、输入、选中、IME、撤销全部走 WPF 原生实现，外壳只负责边框/占位符/前置图标/清除按钮/校验态外观。对外 API 与页面绑定不变。
- **修复三（重构中发现的第二个真 bug）**：占位符触发器原为「空文本 **且** 未聚焦」的 MultiTrigger，重构时拆成两个独立 DataTrigger，导致聚焦后「文本为空」那条仍命中、淡入动画把占位符锁在 0.85（占位符文字盖在光标位置上）。合成 `MultiDataTrigger`（`MultiTrigger.Condition` 不支持 ElementName，`MultiDataTrigger.Condition` 的 Binding 支持）。
- **验证（确定性）**：新增 `--input-test` 端到端自测——空框 / 有内容框 / 原生 TextBox 对照组三项全部 `pass=True`、`focusWithin=True`、`opacity=1.00`、聚焦后占位符淡出到 `0.00`；发布版产物复测同样通过。`--shot` 内置输入框自检（4/4 `opacity=1.00 innerFocused=True`）。
- **测试方法上的两个坑（已修正并留注释）**：① 此沙箱会话会拦截 `SendInput`/`keybd_event` 键盘注入（原生 TextBox 对照组同样收不到字符），键盘链路改用「焦点归属 + 外壳 Opacity + 内层写入双向绑定」三重验证，真实键盘输入请以本机实测为准；② `SendInput` 版实现的 Win32 常量写错（KEYDOWN 用了 `0x0002` 实际是 KEYUP），曾造成「三个对象全失败」的假阴性。

### 修复（本轮自查发现的回归）

- **确认弹窗主按钮整颗消失**：模板里用内联 `<Style TargetType="local:SOrButton">` 覆盖 `Variant`，但控件模板只存在于隐式样式（无 `generic.xaml`），内联样式顶掉隐式样式后按钮无 `Template` → 不可见。改为控件属性 `IsDestructive` + 隐式样式内 `Trigger`，不再在模板里写样式。
- **7 个界面文本键在 resx 中不存在**：`Detail_DangerZoneTitle` / `Detail_DangerZoneHint` / `Detail_CardDevPlan` / `Detail_UndoRemove` / `Settings_CheckNowLabel` / `Settings_CopyPath` / `Git_TimelineCount(Window)`。`{DynamicResource 缺键}` 不报错、渲染成空白（界面上表现为「只有竖条没有标题」「孤立开关」）；C# 侧 `GetString` 缺键则把键名直接显示给用户（GIT 底部曾出现 `Git_TimelineCount`）。已全部补齐中英两本 resx。
- **`BlurPanel` 主题缓存**：`BitmapCache` 缓存的是上一套主题的渲染结果，换肤后不清会看到卡片底色停留在旧主题。新增 `BlurPanel.InvalidateCaches()`，正式启动与截图模式都在 `ThemeChanged` 时调用。
- **测试桩与冒烟桩补齐新接口成员**：`SmokeDialogService` / `AlwaysDiscardDialog` / `StubDialogService` 实现 `ConfirmDestructiveAsync`，`StubThemeService` 实现 `PreviewBlurStrength` / `CommitBlurStrength`；此前 `dotnet test --no-build` 会误判为通过。

### 变更

- 版本号 `0.2.0` → `0.3.0`（`build/version.props`，程序集 / 文件 / 包版本三处同步）。
- 新增单元测试 8 项：列表「清除筛选」复位、主视图模型返回列表复用实例（后者先暴露了 `Initialize()` 绕过缓存实例的真实缺陷）、向导离开守卫、校验失败聚焦锚点、搜索防抖只查一次、跨年日期格式、清除搜索、配置远端 Esc 两次确认。
- 第三批再增 4 项单测：GIT 时间线「首次 10 条 / 每次 +10 / 封顶停用按钮」、不足 10 条不出「查看更早」、破坏性确认标记、焦点框开关无 `Application` 实例时不抛。累计 127 项。
- 新增 `build/check_locale_keys.py`：校验 XAML `{DynamicResource}` 与 C# `GetString` 引用的文本键是否都在 resx、中英两本键集合是否相同。缺键在 WPF 里不会报错、只会渲染成空白或键名，靠截图肉眼发现代价太高；本批 12 处缺键即由此脚本定位。
- `--shot` 截图模式扩展为 20 张：新增「开发管理滚到底（危险区）」「GIT 续显一页」「破坏性确认弹窗」「设置页下半段」，并把换肤回归改为走设置页下拉的真实路径（`SettingsViewModel.CurrentTheme`），截图里的主题名与页面配色不再自相矛盾。全部归档于 `outputs/vd-0.3.0-third/`。
- 应用图标全局替换为用户提供的 `favicon_multi.ico`（7 档、32bpp）；顶栏品牌标记从手绘几何改为直接引用同一枚 ico。
- `--shot` 截图模式扩展为 17 张并加入三条运行时自检：换肤往返（08 ≡ 10）、弹窗焦点归属（`[FOCUS] dialogFocused=True`）、校验失败聚焦锚点（`[FOCUS] fieldAnchor=Name`）；新增「有数据列表」「滚到底再切版块」「校验失败态」三张对照图，全部归档于 `outputs/vd-0.3.0/`。

---

## [0.2.0] - 2026-10-25

### 修复

- 焦点体系：新增 `ColorFocusRing` / `FocusRingBrush` 中间灰令牌（Dark `#8E98A8` / Light `#6B7280`），修复旧焦点环使用 PrimaryBrush、在深色主题下与主按钮底色同色不可见的问题；输入类控件收敛为单一焦点信号（聚焦仅边框换色），移除「边框染 Primary + 外扩环」双信号。
- 排版重排：内容区宽度上限 760 → 1120（超宽才停、左对齐不居中）；设置页改为「外观 / 语言 / 数据」三分组卡 + 行式字段；立项向导第 2 步拆为「基本信息 / 归属与工期」两卡；详情页按项目类型派生版块（编程 = 基础 / 开发 / GIT，剪辑与待办 = 基本信息 + 规划），GIT 版块卡片化。
- 顺手关闭缺陷：VD-009 保存 / 删除就近反馈（页脚状态行约 2 秒自动消失）；VD-012 数据库位置展示真实路径并支持打开所在文件夹；VD-013 语言提示改为单一资源句（不再外露版本计划）；VD-014 剪辑 / 待办详情页移除分号串技术栈编辑器；VD-017 单槽位文档覆盖前二次确认；VD-019 子类型下拉按项目类型联动。

### 新增

- GitHub Issue 模板（bug_report / feature_request / typo / config）。
- 自动更新：启动 3 秒后检查 GitHub Release（24 小时节流 + ETag 缓存），发现更高版本时显示非阻塞提示条；用户确认后下载安装包并做 SHA256 校验，安装前备份数据库与设置，再由安装器静默升级并自动拉起新版。设置页新增「软件更新」分组（自动检查开关 + 立即检查），关于页将来复用同一命令。
- 新增 `SOrtus.Core/Updates`：`SemanticVersion`（SemVer 比较，确保 `0.1.10` 大于 `0.1.9`）与 `GitHubReleaseParser`（Release JSON 解析、隐藏 SHA256 标记提取），配套 23 个单元测试。
- 单实例守护：命名互斥量，避免静默升级时安装器重启与 `[Run]` 条目各拉起一次造成双开。

### 变更

- **安装位置改为按用户**：`SOrtus.iss` 使用 `PrivilegesRequired=lowest`，并把 `DefaultDirName` 显式写为 `{localappdata}\Programs\SOrtus`（不用 `{autopf}`，避免其随安装模式在 Program Files 与会话目录之间切换），安装与后续静默升级全程不弹 UAC。0.2.0 是首个采用用户级安装的版本；从 0.1.2（默认装入 `Program Files`）手动升级时需先卸载旧版，否则两套程序会同时存在。
- `Settings_AboutLicense` 文案由「无网络请求 · 数据只留在本机」改为「数据留在本地」，与自动更新的联网行为保持一致。
- `build/build.ps1` 修正为引用 `SOrtus.slnx`（原引用的 `SOrtus.sln` 不存在）；`build/package.ps1` 改为从 `build/version.props` 读取版本号、去掉会导致失败的 `--no-build`，并明确它只产便携 zip、不产安装包。
- README 中英文版重写：更正「无网络请求」表述，补充安装方式、自动更新流程、联网说明与发版约定。

> 本版合并两批改动：`083dbd7`（界面缺陷第五轮修复，20 文件 +1760/-682，位于 `v0.1.2` 之后）与自动更新功能（含从 `publish-sc` 旧版本升级的兼容处理）。

---

## [0.1.2] - 2026-10-24

编程详情页 V2：三分版块 + 文档导入渲染 + Git 时间线与推送。标签 `v0.1.2`，提交 `6337db5`（61 文件 +6305/-248）。

### 新增

- 编程项目详情页改为「基础信息 / 开发管理 / GIT 管理」三分版块；仅编程类型走新版块，剪辑 / 待办保留原单栏，避免未实现类型被强套语义。
- 项目文档管理：PRD 与版本迭代规划单槽位（重复导入覆盖，覆盖前有确认）+ 其他文档列表；支持 md / markdown / txt / docx / doc 导入；弹窗内渲染 Markdown，待办复选框勾选即时回写原文并落库。
- Git 管理版块：分支与提交时间线、远端地址配置、推送；未配置远端时禁用推送并引导填写地址。
- 数据层 v0002 迁移：新增 `Project.Version` 与 `ProjectDocument` 表，`(ProjectId, Category)` 建部分唯一索引兜住「PRD / 迭代规划各一份」；连接字符串开启 `ForeignKeys=true`，外键级联删除生效。
- 测试新增 30 例（Git 解析与真实仓库集成、docx 抽取与导入、文档仓储回写），累计 91 例。

### 变更

- 滚动条 / 进度条 / 滑块轨道端点改为圆角矩形（新增 `CornerRadiusBar` / `CornerRadiusThumb` 令牌，半径小于短边一半，不再呈胶囊状）。
- 磨砂弹窗遮罩铺满：内边距由容器 `Padding` 改为内层 `Margin`，修复弹窗四周从边缘透出背景内容的问题。
- 深色主题补充 CheckBox / ToolTip 全局主题化模板，并调整 Surface / 边框 / 覆盖层层次对比，修复深色下的突兀白块。
- 设置页删除「高斯模糊强度」档位下拉，仅保留滑块 + 当前强度摘要。
- Markdown 待办复选框缩小至 14px（勾 10px），高度对齐正文行高。

### 修复

- `BlurPanel.InsertLayer` 在 Decorator / ContentControl 分支未先断开原逻辑父级，导致任何磨砂弹窗首次加载即抛 `ChangeLogicalParent`；已改为先置空再挂载。

---

## [0.1.1] - 2026-10-24

验收遗留问题首批处置。标签 `v0.1.1`，提交 `deaa653` + `32fe807`。

### 修复

- SO-001（P0）样式体系：新增 `Resources/Styles/Styles.xaml` 作为唯一样式总入口，`App.xaml` 收敛为「Theme.xaml + Styles.xaml」两个入口，验收条款 A13 达标；跨字典 `StaticResource` 改由各字典自行声明 `MergedDictionaries` 依赖（Blur → Sizes、Dialog → Card、ViewTemplates → Dialog）。
- SO-002（P1）立项向导：创建校验按类型分支（编程 = 技术栈 / 剪辑 = 剪辑类型 / 待办 = 完成时间），修复剪辑、待办「可选却建不出」的死路。
- SO-003（P1）设置页：模糊档位动态补「自定义（N）」项，滑块拖到非档位值不再显示空白。
- SO-005（P2）侧边栏标语改用资源键 `App_Tagline`，不再硬编码中文。
- SO-006（P2）立项列表状态筛选补齐「已暂停 / 已归档」。
- SO-007（P2）关联文件夹改用 `OpenFolderDialog`，支持选中空文件夹。
- SO-013（P2）删除确认的标题与正文拆为两条资源，正文带立项名称。
- SO-016（P3）待办「完成时间」补必填校验。
- 深色主题背景装饰色改为主题令牌，修复切换深色后出现的大片白斑。

### 新增

- 回归用例 +3：剪辑项目创建、待办必填校验、类型切换字段重置。

### 变更

- 版本号 0.1.0 → 0.1.1（`build/version.props`，AssemblyVersion / FileVersion 0.1.1.0）。

---

## [0.1.0] - 2026-10-24（首个可运行版本 · MVP）

打通编程立项闭环。提交 `a5b574a` / `ce456a4` / `2531bf2` / `df5fef5`。本版未打 Git 标签（标签制度自 v0.1.1 起建立），四次提交的 `build/version.props` 均为 0.1.0。

### 新增

- 三层架构骨架：View / ViewModel / Service / Repository。
- SQLite 本地数据库与迁移机制；设置页「数据位置」展示具体数据库文件路径。
- 编程立项全流程：向导创建 → 立项列表 → 详情 → 进度管理。
- 磨砂玻璃视觉：`BlurPanel` 以 `VisualBrush` 按控件实际位置 1:1 采样背景层，避免拉伸放大产生巨型色块。
- 浅色 / 深色双主题（两主题键一一对应）、中英双语资源、构建与版本号管理（`build/version.props` 单一来源）。

### 修复

- 4 项 P0 阻断缺陷：可空工期参数未转 `DBNull` 导致创建 / 更新抛异常；`AsyncRelayCommand` 的 `CanExecute` 占用运行标志导致异步命令全部失效；主题装饰把 `Color` 赋给 `Ellipse.Fill` 致主窗口首次布局即崩溃；等（共 11 项）。
- 第一轮 UI 走查 8 条 + 2 个隐藏缺陷：侧边栏与内容区严格等高、按钮图标与文字对齐、背景改纯色、全站去除蓝紫渐变改近黑中性主色、真圆角（`BlurPanel` 增加 `CornerRadius` + 几何裁剪）、输入框文本贴左、深底按钮文字消失（隐式样式写死 `Foreground`）、文本被全局 `TextTrimming` 静默截断。
- 第二轮交互 7 条 + 3 个 Bug：全局禁用键盘聚焦视觉、悬停高亮整卡、背景加极淡网格使模糊效果可感知、滑块重做 6px 单条、向导第 2 步按类型分支、日期控件改向上弹出且点外部关闭；修复选日期崩溃（`CalendarItem` 套错样式 + 自定义模板测量 OOM，回归原生内核）、剪辑 / 待办误入编程向导、技术栈回车不添加。

---

*维护约定：每次发版时在顶部追加新版本小节；编号类缺陷（SO-xxx / VD-xxx）在对应版本小节登记，详情见 `Sortus遗留问题.md` 与 `outputs/SOrtus视觉缺陷修复单`。*
