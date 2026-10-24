# SOrtus MVP（0.1.0）验收记录

> 验收对象：`版本迭代规划.md` 第三章 MVP 验收标准 A1–A16
> 记录日期：2026-10-24
> 代码基线：`src/SOrtus`（WPF · net10.0-windows）、`src/SOrtus.Core`、`tests/SOrtus.UnitTests`

---

## 一、验证方式

| 手段 | 命令 | 结果 |
| --- | --- | --- |
| 编译（Debug / Release） | `dotnet build -c Debug` / `-c Release` | 0 警告 0 错误 |
| 单元 / 集成测试 | `dotnet test` | 23 / 23 通过 |
| 冒烟自检 | `SOrtus.exe --smoke` | 全部自定义控件与视图模板实例化成功 |
| 界面截图 | `SOrtus.exe --shot <目录>` | 5 张核心界面渲染成功（`resources/screenshots/latest`） |
| 真机启动 | `SOrtus.exe` | 启动无异常，首次运行自动建库 |

---

## 二、A1–A16 逐条结论

| 项 | 结论 | 依据 |
| --- | --- | --- |
| A1 编译零错误 | 通过 | Debug / Release 均 0 警告 0 错误 |
| A2 主窗口与导航框架、无启动崩溃 | 通过（已修复阻断缺陷） | 启动崩溃根因见 §3.3；修复后 `--shot` 渲染出主窗口、导航与内容区 |
| A3 四层职责清晰 | 通过 | `ViewModels` / `Services` / `Repositories` / `Core`（契约 + 模型）分层，code-behind 仅保留窗口级 UI 行为 |
| A4 首次运行建库与迁移机制 | 通过 | `MigrationRunner` 嵌入脚本 + `SchemaVersion` 记账；重启二次执行幂等（有用例覆盖） |
| A5 向导完成编程立项 | 通过 | `Wizard_Create_SavesAllCollectedFields` 覆盖名字/技术栈/简介/类型/文件夹/工期 |
| A6 技术栈预设 + 自定义、可多选 | 通过（本次补齐） | 新增 12 个预设 chips，点选即入栈；自定义输入支持逗号/分号/顿号批量添加；chip 可删除 |
| A7 列表展示 / 筛选 / 搜索 | 通过 | 类型、状态双筛选 + 关键字搜索（名称/简介/技术栈），空态与无结果态分开 |
| A8 详情页可编辑后置字段 | 通过（本次补齐状态编辑） | 开发规划/约束/目标/验收标准/进度 + 状态下拉；保存即时反映 |
| A9 进度设置并持久化 | 通过 | `ProgressPercent` 落库，范围 0–100 钳制；`Update_PersistsPostCreationFields_AndProgress` |
| A10 重启后数据完整 | 通过 | `FullLoop_CreateFromWizard_EditInDetail_SurvivesRestart` 用全新仓储实例读同一库验证 |
| A11 创建→查看→编辑→列表→重启无阻断 | 通过 | 同上端到端用例 + 删除用例 |
| A12 未保存变更提示 | 通过（本次补齐） | 返回、侧边栏导航、关闭窗口三处统一走「保存 / 不保存 / 取消」三选对话框 |
| A13 样式字典结构 + App.xaml 双入口 | 有条件通过 | 字典结构完整（Colors/Sizes/Brushes/Animations/Blur/Icons/Controls.*）；**App.xaml 采用平铺合并**，未收敛为 `Styles.xaml` 单一入口，原因见 §3.9 |
| A14 BlurPanel 四附加属性 + 页面无内联 BlurEffect | 通过 | 页面 XAML 无任何 `<BlurEffect>`；仅 `Blur.xaml` 定义效果资源 |
| A15 模糊只作用背景、随尺寸跟随 | 通过（本次修复取样方式） | 卡片改为按自身位置 1:1 采样窗口背景层（`PART_Backdrop`），随 `LayoutUpdated` / `SizeChanged` 实时对齐 |
| A16 模糊性能与全局联动 | 通过（本次修复联动） | `SOrCard` 缺省半径绑定 `GlobalBlurRadius`；未自定义强度时取 `BlurRadiusNormal` 档位；模糊层启用 `BitmapCache`；无逐帧改半径写法 |

**通过项：15 / 16；有条件通过：1（A13）**

---

## 三、本次修复的阻断/缺陷清单

### 3.1 SQLite 空值参数导致立项必崩（P0）
`AddWithValue("$scheduleStart", null)` 被 `Microsoft.Data.Sqlite` 视为「未赋值」，执行时抛
`InvalidOperationException: Value must be set.`。任何未填工期的立项都会失败。
**修复**：可空值统一转 `DBNull.Value`，并补空值往返用例。

### 3.2 AsyncRelayCommand 永不执行（P0）
`CanExecute` 内用 `Interlocked.Exchange(ref _isRunning, 1)` 做「检查并占用」，而 WPF 会高频查询
`CanExecute`，导致真正 `Execute` 时判定「已在运行」直接返回——创建、保存、删除、返回等异步命令点了没反应。
**修复**：`CanExecute` 改为只读判断，新增可等待的 `ExecuteAsync`；补回归用例。

### 3.3 启动即崩溃：Color 当 Brush 用（P0）
`Brushes.xaml` 的背景装饰把 `ColorAccentA/B/C`（`Color`）直接赋给 `Ellipse.Fill`（`Brush`），
窗口首次布局抛 `'#FF6366F1' 不是属性 Fill 的有效值`。冒烟自检未覆盖 Window 渲染，故此前未暴露。
**修复**：改用 `AccentABrush / AccentBBrush / AccentCBrush`，随主题联动。

### 3.4 全局模糊强度不联动（A16）
`SOrCard.BlurRadius` 默认硬编码 `12`，直接覆盖了 `BlurPanel` 的全局令牌，设置页调节与 `Sizes.xaml` 改值均无效。
**修复**：缺省改为 `double.NaN`（跟随 `GlobalBlurRadius`，`DynamicResource` 绑定）；`ThemeService` 在用户未自定义时取 `BlurRadiusNormal` 档位作为默认。

### 3.5 磨砂卡片取样错位（A15）
模糊层原先用「拉伸后的壁纸画刷」，卡片内看到的是被放大的缩略背景，颜色斑块被放大后压过正文。
**修复**：`MainWindow.xaml` 拆出背景层 `PART_Backdrop`，`BlurPanel` 以 `VisualBrush` 按控件实际位置 1:1 采样该层，再叠近白覆盖层，正文对比度达标。

### 3.6 详情页无法修改状态
状态只能由列表筛选看到却无法修改（新建永远停在「规划中」）。
**修复**：详情页新增状态下拉（规划中/进行中/已暂停/已完成/已归档）。

### 3.7 未保存更改会静默丢失（A12）
仅「返回」按钮有提示；侧边栏导航与关窗会直接丢弃编辑。
**修复**：新增 `UnsavedChangesDialogViewModel`（保存 / 不保存 / 取消），导航与 `OnClosing` 统一拦截。

### 3.8 技术栈录入不完整（A6）
缺预设多选 chips，且已添加的 chip 无法删除（`RemoveTechStackCommand` 未接到 UI）。
**修复**：预设 chips（选中态高亮）+ chip 内「✕」删除 + 批量输入。

### 3.9 样式字典入口未收敛（A13 遗留）
WPF 的 `StaticResource` 在解析时**不会跨 `MergedDictionaries` 的兄弟字典查找**，若把叶子字典收进
`Styles.xaml` 再由 `App.xaml` 单一引入，`Controls.Dialog` → `Controls.Card`、`Blur` → `Sizes`、
`ViewTemplates` → `Controls.Dialog` 三处引用会在加载期抛错。当前保留平铺合并（顺序即依赖顺序），
并在 `App.xaml` 注释中记录原因。后续如需收敛，应把这些跨字典引用改为 `DynamicResource` 或将相关资源合并入同一文件。

### 3.10 数据库文件名与历史结构
- 文件名 `soctus.db` 拼写错误 → 改为 `sortus.db`，并一次性把旧文件改名继承（不丢数据）。
- 早期迭代遗留的库结构与当前模型不兼容（`Project + ProgrammingProject` 双表、`SchemaVersion` 为 `Id/Version TEXT`），
  会导致启动后读写异常。**修复**：迁移前做兼容性守卫，检测到旧结构即备份为 `sortus.db.legacy-<时间戳>.bak` 后重建。

### 3.11 文案占位符未格式化
`Wizard_SelectedType` 的值含 `{0}` 却按普通文本渲染，界面出现「已选类型：{0}」。**修复**：改为纯标签，类型名单独展示。

---

## 四、截图

`resources/screenshots/latest/`：

| 文件 | 内容 |
| --- | --- |
| `01-list-empty.png` | 主窗口 + 侧边导航 + 列表空状态 |
| `02-wizard-step1.png` | 立项向导 · 选择类型 |
| `03-wizard-step2.png` | 立项向导 · 填写信息（含技术栈预设 chips 与可删除胶囊） |
| `04-detail.png` | 立项详情（状态编辑、过程字段、进度） |
| `05-settings.png` | 设置（主题 / 模糊强度 / 语言） |

重新生成：`SOrtus.exe --shot "resources\screenshots\latest"`（离屏渲染，不触碰真实数据库）。

---

## 五、遗留与下版本建议

1. **A13 样式入口收敛**：见 §3.9，需先处理跨字典 `StaticResource`。
2. **模糊取样性能**：每张卡片一个 `VisualBrush` + `BlurEffect`，卡片数量多时可考虑共享一次离屏模糊。
3. **工期字段**：详情页目前只读展示工期，编辑仍在向导；建议 0.2.0 一并开放。
4. **删除确认文案**：标题与正文复用同一条文案，可拆成两条资源键。
5. **1.1 起的功能**：剪辑进度/素材、待办完成状态与提醒尚未实现（按版本规划在 0.2.0 / 0.3.0）。
