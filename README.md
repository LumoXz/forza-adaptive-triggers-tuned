# 极限竞速：地平线5 × PS5 DualSense 自适应扳机（手感调校版）

给 Steam 版《地平线5》的 PS5 DualSense 手柄补上它原生没有的**自适应扳机**：油门有弹簧阻尼、刹车有阻力，手感像真车踏板。读游戏自带遥测（数据输出），直写手柄 HID——**不改游戏文件、不注入、不装驱动**。

> [English version](README.en.md) · **下载成品：[Releases](../../releases/latest)**（免安装，双击即用）

## 为什么做这个 fork

原版 [Jason13201/forza-adaptive-triggers](https://github.com/Jason13201/forza-adaptive-triggers) 把转速、车速、打滑这些高频数据直接算进扳机力度，结果就是**仪表盘指针怎么抖，扳机就怎么抖**，像打枪不像开车。

这个 fork 用六轮实测定居换掉了整套映射逻辑，核心原则一句话：

> **力度只跟你的脚走（弹簧），不跟数据走（噪声）。**

## 手感说明

| 操作 | 扳机反馈 |
|---|---|
| 踩油门 | 右扳机出现阻尼，**踩得越深越硬**（渐进曲线，起步轻、全油门顶） |
| 松油门 | 阻尼立刻消失 |
| 踩刹车 | 左扳机阻尼随刹车深度线性增强 |
| 菜单 / 暂停 | 全部效果自动关闭 |
| 手柄震动 | **完全不碰**——留给 Steam / 游戏自己管，游戏内震动设置照常生效 |

防抖三件套（都是实测定下的参数，见"手感微调"）：**宽迟滞带**（防临界闪烁）＋ **EMA 低通**（滤掉游戏侧换挡/TC 的油门调制）＋ **力度量化**（消除逐帧微调导致的高频"马达追数"）。

## 使用（3 步）

1. **手柄用 USB 线连电脑**（蓝牙下 Windows 会静默忽略扳机指令，这是硬件限制不是 bug）
2. **游戏里开遥测**（只需设置一次）：设置 → HUD 和游戏性 → 拉到最底部 → **数据输出 = 开**，地址 `127.0.0.1`、端口 `5300` 保持默认
3. **先双击 `ForzaAdaptiveTriggers.exe`，再从 Steam 启动游戏**，上路自动生效

关窗口即退出，退出时扳机自动复位。Steam Input 保持默认开启即可，游戏内震动想开就开，互不干扰。

## 安全性

- 全部逻辑就 11 个 C# 文件，**源码即发布物**，不放心可以clone 下来自己 `dotnet publish` 一份
- 程序只做三件事：监听**本机** UDP 5300 收遥测 → 解析 → 向手柄 HID 写扳机报告。**无任何网络外联、无文件落盘、无注册表写入**
- fork 自上游 MIT 项目，上游代码与新增代码均已逐行过审

## 手感微调

改 `Mapping/*.cs` 顶部常量（每个值都有注释说明取值依据），改完重新编译即可：

| 参数 | 默认 | 作用 |
|---|---|---|
| `CurveExponent` | 1.5 | 油门力度曲线：1.0=线性，1.5=渐进（默认），2.0=激进 |
| `AccelOnThreshold` / `Off` | 60 / 10 | 油门触发/释放阈值（0-255），嫌前段空行程长就调低 |
| `ForceFloor` | 45 | 触发瞬间的力度底（越大过渡越"实"） |
| `SmoothingAlpha` | 0.12 | 油门平滑度（越小越柔，越大越"跟手"但越接近噪声） |
| `ForceQuantum` | 12 | 力度台阶大小（治"打枪感"的关键参数） |

## 编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)：

```
dotnet publish ForzaAdaptiveTriggers -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 常见问题

- **没有任何效果？** 确认三件事：USB 连接、遥测已开（`127.0.0.1:5300`）、人在车上（菜单/读盘中不生效）。防火墙若弹窗请允许监听本机 5300
- **想要更多效果（打滑泄力/ABS/路面感）？** 设计原则允许低频开关类效果，但高频量进力度公式是抖动之源，见 [HANDOFF.md](HANDOFF.md)
- **双截棍/DualShock4？** 不支持，仅 DualSense（PS5）

## 许可与来源

MIT（见 [LICENSE](LICENSE)）。Fork 自 [Jason13201/forza-adaptive-triggers](https://github.com/Jason13201/forza-adaptive-triggers)，手感层为本仓库重写，改动动机与决策记录见 [CHANGELOG.md](CHANGELOG.md) 和 [HANDOFF.md](HANDOFF.md)。
