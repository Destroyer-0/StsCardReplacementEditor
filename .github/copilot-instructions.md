# TomAndJerryCardImage — 项目规则

## 只读参考目录
- `e:\STSMods\TouhouAncients\Sts2-TouhouAncients`（及其下所有内容）**仅供参考，严禁修改、创建、删除、移动其中任何文件**。
- 需要参考其 csproj / 脚本 / 本地化写法时只读，不得回写。

## 项目定位
- 杀戮尖塔 2 的「卡面替换」Mod，只替换卡图，不影响玩法（`affects_gameplay: false`）。
- 不依赖 BaseLib / RitsuLib，仅引用 `sts2.dll` + `0Harmony.dll`。
- **不要主动执行 `导出pck.bat` / 导出 pck**，只在用户明确要求时才导出。

## 卡图约定
- 图片按卡池分目录：`images/{卡池小写}/{卡牌id小写}.png`，卡池名取自 `CardPoolModel.Title`（`ironclad` / `silent` / `defect` / `necrobinder` / `regent` / `fool` / `colorless` / `curse` / `status` / `token` / `event` / `quest`）。
- 文件名以无下划线为主（`ashenstrike.png`），代码两种命名（`ashen_strike` / `ashenstrike`）都兼容。
- 尺寸沿用原版卡面 500×381。
- 新增或替换 png 后必须经 Godot 导入（生成 `.import`），否则不会被打进 pck。
- 卡图工具 `CardArtTool.exe` 直接放项目根目录，由 `export_presets.cfg` 的 `exclude_filter` 排除，不建子文件夹。
- 工具源码在 `Tools/CardArtTool/`（带 `.gdignore`），发布方式：`dotnet publish -p:PublishProfile=win-x64 -o Tools/publish-tmp` 再把 exe 复制到根目录（直接 `-o` 到根目录会触发 WPF 的 MC1000 错误）。
