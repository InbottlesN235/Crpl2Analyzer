# 致谢与格式来源

本工具不包含任何游戏资源，只是读取玩家自己录制的回放文件；所有格式知识都来自社区公开信息与对文件的实测。

## Creplay 模组

`.crpl2` 由 **Creplay** 模组（作者 **Potato**）录制。容器格式、字段顺序与语义均出自该模组的产物本身，本项目通过实测逆向整理。

- 容器：`CRP2` / `CRP4` + `int32` 版本 + AES-256-CBC(PKCS#7)
- 密钥：`SHA-256("qwerty")`，IV：`SHA-256("potato")[0:16]`

## 社区格式整理

- **SleepingCui/crpl2tools** —— 早期 CRP2 的 `decrpl2.py` / `gencrpl2.py`，验证了密钥派生与 `formatVersion 2` 的字段顺序。
  <https://github.com/SleepingCui/crpl2tools>

## ADOFAI 判定与精准度

- **我要丸冰火《全网最详细冰与火之舞判定系统与精准度系统说明》** —— 基于 ADOFAI Stable 3.3.1 源码整理，给出 `HitMargin` 枚举、三级角度边界（Counted / Perfect / Pure）、X-Acc 权重（1.00 / 0.75 / 0.40 / 0.20）。
  <https://www.bilibili.com/opus/1187804902628261926>
- **ADOFAI v3.4.0 更新公告（XPerfect）** —— 说明 XPerfect 的判定窗口（约 ±16.6666ms / ±12.5°）以及 Perfect 拆分为 `-Perfect / XPerfect / Perfect+`。
  <https://7thbeat.notion.site/ADOFAI-v3-4-0-3d890365a1618083b9fbd66cdeed896f>
  <https://steamcommunity.com/games/977950/announcements/detail/680764929940127756>

## 本项目的实测结论

以下内容是本项目在真实回放上验证得到的，用于补齐公开资料未覆盖的部分：

| 结论 | 证据 |
| --- | --- |
| `formatVersion 3` 的 `keyCodes` 由 `ushort` 改为 `byte`，`keyPresses` 由 `int` 改为 `byte` | 数组长度与取值分布（VK 码 27/50/69/80/187，按下/抬起各 179） |
| v3 在 `hitFlags` 前多出两个事件字节数组 | 顺序解析后无残留字节，两数组在键盘录制下恒为 0 |
| v3 末尾新增 `double[] hitErrorSeconds` | 与角度误差的比值恒等于 `π·BPM/60`（示例文件 BPM=150） |
| 判定槽位 3/4/5 = `完美- / X完美 / 完美+` | `|误差| < 12.5°` 的命中数 144 恰等于索引 4 的计数；23 个越界命中中 22 负 1 正，对应索引 3/5 |
| `hitCurrAngles` 即 `cachedAngle − targetExitAngle` | 逐条相减，最大偏差 1.4e-8 |

若后续版本再次改动格式，欢迎提交 issue 并附一份回放样本。
