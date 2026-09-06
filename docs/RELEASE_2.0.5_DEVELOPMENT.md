# Stardew Gallery 2.0.5 — Current-State Accuracy

## 目标与边界

本版只修正当前状态判定：已知“未约会/未婚”按空集合处理，不再误报未知；每条事件按自身地点判断天气；事件候选定义继续缓存，但 current variant、角色、配偶归属和目录每次打开时刷新。

不改 UI、条件语法、解锁与回放、历史、存储格式或 2.0.4 的安全边界与生命周期。

## 操作流程与实现

- 打开角色事件页时先读取一次与地点无关的共享状态，其中天气保持未知；随后按每条事件的 `LocationName` 解析地点，并覆盖该地点的天气 ID 与降雨状态。地点无法解析时天气保持未知。
- 当前玩家没有约会对象或配偶时分别记录为空集合；只有状态确实无法取得时才使用 `null`。
- `GalleryCatalogCache` 仅缓存读取并解析后的事件候选定义。每次 `Get` 都用候选重新构建 `ResolvedEventIndex`、执行安全 precondition probe、选择 current variant，再扫描角色并构建目录；不会重复读取未失效的 Event 资产，也不会按 tick 扫描。

## 模块职责、配置与存储

- `RuntimeStateReader` 读取共享状态与地点天气；`CurrentStateSnapshot` 提供纯数据覆盖。
- `GalleryCharacterMenu` 为每条事件选用目标地点状态；`ResolvedEventCandidateCache` 只保留稳定的候选定义，动态 variant 选择留在每次 `Get`。
- 无配置、存档、数据库、玩家文本或 UI 坐标变化；UI 原型不适用。

## 兼容风险

- Mod 地点在菜单打开时尚未注册或无法解析，其天气条件仍显示未知，不猜测当前地点天气。
- 目录每次打开都会重新执行安全 probe、选择 current variant、扫描角色并构建，成本高于旧缓存命中，但不在逐帧路径；Event 资产读取与候选解析仍只做一次。
- 多人模式未实测。

## 验收

- 自动检查覆盖关系空集合、真实未知、否定条件、地点天气覆盖、weather 判定，以及生产候选缓存的单次加载、variant 刷新与失效后重载。
- 运行 Checks、PersistenceChecks、Release build 与基线 diff whitespace check。
- 实机 R25-1～R25-6 与 replay smoke：关系、目标地点天气、动态 projection、重复打开及无内容失效时的 current variant 刷新；均标记待实测。
