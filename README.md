# Unity 智能 NPC SDK

几行代码让你的 Unity 游戏 NPC **会聊天、有长期记忆**。

## 特性

- 🎭 **人设驱动**：用一段文本定义 NPC 性格、世界观、说话风格
- 🧠 **长期记忆**：NPC 记得玩家说过的话、偏好、约定，跨会话持久化到本地
- 🔍 **记忆检索**：按「重要性 × 时间衰减 + 关键词相关度」取最相关的记忆注入提示词
- 🤖 **自动记忆提炼**：每轮对话后由模型自动提炼值得记住的事实（可关闭）
- 🔄 **模型可切换**：改配置即可换模型，默认通过 OpenAI 兼容网关接入大模型
- 📦 **UPM 包**：通过 Git URL 一键安装，Samples 自带零依赖聊天 Demo
- 🔌 **零依赖**：不引用任何第三方库，UnityWebRequest + JsonUtility 实现

## 快速开始

### 1. 安装

Unity 2021.3 及以上。打开 **Window → Package Manager → + → Add package from git URL**：

```
https://github.com/qyhanyu/unity-smart-npc-sdk.git
```

（也可以在 `Packages/manifest.json` 里加 `"com.orcarouter.smartnpc": "https://github.com/qyhanyu/unity-smart-npc-sdk.git"`）

### 2. 配置

1. 准备一个 OpenAI 兼容的大模型 API Key（任意兼容 `chat/completions` 的网关均可）。还没有的话可以[通过这个链接注册](https://www.orcarouter.ai/ref/ref_3b7f96e0eb1f87ef642)
2. 在 Project 窗口右键 → **Create → Smart NPC → NPC Config**，创建配置资产
3. 填入 `Api Key`，按需修改人设（Persona）和模型名

默认网关地址为 `https://api.orcarouter.ai/v1`，如需换其他兼容网关，改 `Base Url` 即可。

### 3. 开聊

把 `SmartNpc` 组件挂到 NPC 的游戏对象上，拖入刚才的配置资产：

```csharp
using UnityEngine;
using OrcaRouter.SmartNpc;

public class PlayerTalk : MonoBehaviour
{
    public SmartNpc npc;

    async void Start()
    {
        string reply = await npc.TalkAsync("你好，你叫什么名字？");
        Debug.Log(reply);
    }
}
```

在 Package Manager 中导入 **Samples → Demo**，挂 `DemoChatUI` 运行即可开箱聊天。

## 工作原理

```
玩家输入
   │
   ▼
┌─────────────────────────────┐
│ 提示词组装                    │
│  system = 人设 + 检索到的长期记忆 │
│  + 最近 N 轮对话               │
└─────────────┬───────────────┘
              ▼
   POST {base_url}/chat/completions
              │
              ▼
   NPC 回复 → 写入短期历史 + 长期记忆
              │
              ▼
   (可选) 模型提炼本轮值得记住的事实 → 写入长期记忆
```

## 目录结构

```
Runtime/
├── OrcaRouterClient.cs   # HTTP 客户端：OpenAI 兼容 chat/completions
├── NpcConfig.cs          # NPC 配置（ScriptableObject）
├── NpcMemory.cs          # 长期记忆：存储 / 评分检索 / JSON 持久化
└── SmartNpc.cs           # 主组件：提示词组装 + 对话 + 记忆更新
Samples~/Demo/
└── DemoChatUI.cs         # 零依赖聊天 Demo（OnGUI）
```

## 常用 API

| 方法 | 说明 |
|---|---|
| `await npc.TalkAsync("...")` | 对话，返回 NPC 回复 |
| `npc.Memory.Search("关键词", 5)` | 手动检索记忆 |
| `npc.Memory.All` | 查看全部记忆 |
| `npc.ForgetAll()` | 清空该 NPC 的所有记忆 |
| `npc.ResetConversation()` | 只清空短期对话上下文 |
| `npc.OnReply / OnError` | 回复 / 错误事件 |

## ⚠️ 安全提示

**不要把你的 API Key 提交到公开仓库。** Key 一旦泄露，任何人都能用你的额度。

- 本 SDK 的设计是：**每个使用者填入自己的 Key**
- `.gitignore` 已排除 `*Config.asset`，防止本地配置被误提交

## 许可

MIT License。详见 [LICENSE](LICENSE)。
