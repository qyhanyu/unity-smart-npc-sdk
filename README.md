# Unity Smart NPC SDK（OrcaRouter）

几行代码让你的 Unity 游戏 NPC **会聊天、有长期记忆**。后端通过 [OrcaRouter](https://www.orcarouter.ai)（OpenAI 兼容的智能模型路由网关）接入 200+ 大模型，一个 API Key 即可切换 GPT、Claude、Gemini、DeepSeek、Qwen、Kimi 等任意模型，无需改代码。

## 特性

- 🎭 **人设驱动**：用一段文本定义 NPC 性格、世界观、说话风格
- 🧠 **长期记忆**：NPC 记得玩家说过的话、偏好、约定，跨会话持久化到本地
- 🔍 **记忆检索**：按「重要性 × 时间衰减 + 关键词相关度」取最相关的记忆注入提示词
- 🤖 **自动记忆提炼**：每轮对话后由模型自动提炼值得记住的事实（可关闭）
- 🔄 **模型一键切换**：改配置里的模型名即可换模型，支持 `orcarouter/auto` 自动路由
- 📦 **UPM 包**：通过 Git URL 一键安装，Samples 自带零依赖聊天 Demo

## 快速开始

### 1. 安装

Unity 2021.3 及以上。打开 **Window → Package Manager → + → Add package from git URL**：

```
https://github.com/qyhanyu/unity-smart-npc-sdk.git
```

（也可以在 `Packages/manifest.json` 里加 `"com.orcarouter.smartnpc": "https://github.com/qyhanyu/unity-smart-npc-sdk.git"`）

### 2. 配置

1. 去 [OrcaRouter 控制台](https://www.orcarouter.ai) 注册并创建一个 API Key
2. 在 Project 窗口右键 → **Create → Smart NPC → NPC Config**，创建配置资产
3. 填入 `Api Key`，按需修改人设（Persona）和模型（如 `openai/gpt-4o`、`deepseek/deepseek-chat` 或 `orcarouter/auto`）

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
   POST https://api.orcarouter.ai/v1/chat/completions
              │
              ▼
   NPC 回复 → 写入短期历史 + 长期记忆
              │
              ▼
   (可选) 模型提炼本轮值得记住的事实 → 写入长期记忆
```

## 常用 API

| 方法 | 说明 |
|---|---|
| `await npc.TalkAsync("...")` | 对话，返回 NPC 回复 |
| `npc.Memory.Search("关键词", 5)` | 手动检索记忆 |
| `npc.ForgetAll()` | 清空该 NPC 的所有记忆 |
| `npc.ResetConversation()` | 只清空短期对话上下文 |
| `npc.OnReply / OnError` | 回复 / 错误事件 |

## ⚠️ 安全提示（重要）

**不要把你的 OrcaRouter API Key 提交到公开仓库。** Key 一旦泄露，任何人都能用你的额度。

- 本 SDK 的设计是：**每个使用者填入自己的 Key**（SDK 只是代码，不附带任何 Key）
- 如果你想让"别人直接用你的模型额度"，正确做法是自建一个后端代理转发请求，而不是在客户端代码里放 Key
- 推荐在仓库的 `.gitignore` 中忽略 `*Config.asset` 之类的本地配置文件，README 里只放占位符

## 模型选择

在 NpcConfig 的 `Model` 字段填：

| 值 | 说明 |
|---|---|
| `orcarouter/auto` | 自动路由，按成本/质量自适应选模型（默认） |
| `openai/gpt-4o` | 指定厂商模型，格式 `vendor/model` |
| `deepseek/deepseek-chat` | 低成本中文场景推荐 |

完整模型清单见 [OrcaRouter 定价页](https://www.orcarouter.ai/api/pricing)。

## 许可

MIT License。详见 [LICENSE](LICENSE)。
