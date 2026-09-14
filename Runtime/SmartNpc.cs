using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OrcaRouter.SmartNpc
{
    /// <summary>
    /// 挂在 NPC 游戏对象上的主组件。用法：
    ///   string reply = await GetComponent&lt;SmartNpc&gt;().TalkAsync("你好");
    /// </summary>
    public class SmartNpc : MonoBehaviour
    {
        [Tooltip("NPC 配置资产，Create → Smart NPC → NPC Config")]
        public NpcConfig config;

        [Tooltip("不同 NPC 用不同 id，记忆按 id 分开存储")]
        public string npcId = "npc_01";

        [Tooltip("也支持运行时直接用代码设置 Key，优先级高于 config")]
        public string overrideApiKey = "";

        public event Action<string> OnReply;          // 收到回复时触发（主线程）
        public event Action<Exception> OnError;       // 出错时触发

        public MemoryStore Memory => _memory;
        public List<ChatMessage> DialogueHistory => _history;

        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private MemoryStore _memory;
        private OrcaRouterClient _client;

        private void Awake()
        {
            _memory = new MemoryStore(npcId);
            _client = new OrcaRouterClient
            {
                BaseUrl = config != null ? config.baseUrl : "https://api.orcarouter.ai/v1",
                ApiKey = !string.IsNullOrEmpty(overrideApiKey) ? overrideApiKey : (config != null ? config.apiKey : ""),
                Model = config != null ? config.model : "orcarouter/auto",
            };
        }

        /// <summary>
        /// 玩家对 NPC 说一句话，返回 NPC 的回复。
        /// 必须在主线程 await。
        /// </summary>
        public async Task<string> TalkAsync(string playerInput)
        {
            if (config == null)
                throw new InvalidOperationException("SmartNpc.config 未设置，请先创建一个 NPC Config 资产并拖入。");
            if (string.IsNullOrWhiteSpace(playerInput))
                return "";

            var messages = BuildPrompt(playerInput);

            string reply;
            try
            {
                reply = await _client.ChatAsync(messages, config.temperature, config.maxTokens);
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                Debug.LogWarning("[SmartNPC] 请求失败：" + e.Message);
                return "";
            }

            // 更新短期对话历史
            _history.Add(ChatMessage.User(playerInput));
            _history.Add(ChatMessage.Assistant(reply));
            int keep = Mathf.Max(2, config.contextTurns * 2);
            while (_history.Count > keep)
                _history.RemoveAt(0);

            // 更新长期记忆
            Remember("玩家说：" + playerInput, "player", GuessImportance(playerInput));
            Remember("NPC回复：" + reply, "npc", 0.4f);
            if (config.autoExtractMemory)
                _ = ExtractMemoryAsync(playerInput, reply); // 异步提炼，不阻塞对话

            OnReply?.Invoke(reply);
            return reply;
        }

        public void ResetConversation()
        {
            _history.Clear();
        }

        public void ForgetAll()
        {
            _history.Clear();
            _memory.Clear();
        }

        // ---------- 提示词组装 ----------

        private List<ChatMessage> BuildPrompt(string playerInput)
        {
            var sb = new StringBuilder();
            sb.AppendLine(config.persona);
            sb.AppendLine();
            sb.AppendLine("你记得关于这位玩家的以下事情（长期记忆）：");

            var memories = _memory.Search(playerInput, config.memoryTopK);
            if (memories.Count > 0)
            {
                foreach (var m in memories)
                    sb.AppendLine("- " + m.content);
            }
            else
            {
                sb.AppendLine("（暂无）");
            }

            sb.AppendLine();
            sb.AppendLine("请自然地聊天。如果记忆与当前话题相关，可以主动提起；不要机械地列举记忆。");

            var messages = new List<ChatMessage> { ChatMessage.System(sb.ToString()) };
            messages.AddRange(_history); // 最近对话作为多轮上下文
            messages.Add(ChatMessage.User(playerInput));
            return messages;
        }

        // ---------- 记忆写入 ----------

        private void Remember(string content, string speaker, float importance)
        {
            _memory.Add(new MemoryEntry(content, speaker, importance));
        }

        /// <summary>用关键词启发式粗判重要性；开启 autoExtractMemory 后还会由模型精修。</summary>
        private static float GuessImportance(string text)
        {
            float score = 0.3f;
            string[] highSignal =
            {
                "我叫", "我的名字", "记住", "喜欢", "讨厌", "是", "职业", "来自",
                "明天", "以后", "下次", "约定", "答应", "秘密", "密码"
            };
            foreach (var kw in highSignal)
                if (text.Contains(kw)) { score += 0.15f; }
            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// 让模型从本轮对话中提炼值得长期记住的事实。
        /// 输出约定为单行：MEMORY: 内容 | importance(0~1)，或 NONE。
        /// </summary>
        private async Task ExtractMemoryAsync(string playerInput, string reply)
        {
            try
            {
                var prompt = new List<ChatMessage>
                {
                    ChatMessage.System(
                        "你是游戏NPC的记忆管家。从对话中提炼值得长期记住的事实（玩家身份、偏好、约定、重要事件）。\n" +
                        "只输出一行：MEMORY: <内容> | <importance 0~1>；没有值得记的就输出 NONE。"),
                    ChatMessage.User($"玩家：{playerInput}\nNPC：{reply}")
                };

                string result = await _client.ChatAsync(prompt, 0.2f, 60);
                if (string.IsNullOrEmpty(result) || result.Contains("NONE")) return;

                // 解析 "MEMORY: xxx | 0.8"
                const string prefix = "MEMORY:";
                int start = result.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0) return;
                string line = result.Substring(start + prefix.Length).Trim();
                string content = line;
                float importance = 0.7f;
                int bar = line.LastIndexOf('|');
                if (bar > 0)
                {
                    content = line.Substring(0, bar).Trim();
                    if (float.TryParse(line.Substring(bar + 1).Trim(), out float v))
                        importance = Mathf.Clamp01(v);
                }
                if (content.Length > 0)
                    _memory.Add(new MemoryEntry(content, "extracted", importance));
            }
            catch (Exception e)
            {
                // 记忆提炼失败不影响对话
                Debug.LogWarning("[SmartNPC] 记忆提炼失败：" + e.Message);
            }
        }
    }
}
