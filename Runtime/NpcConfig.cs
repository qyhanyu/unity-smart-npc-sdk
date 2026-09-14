using UnityEngine;

namespace OrcaRouter.SmartNpc
{
    /// <summary>
    /// NPC 配置（ScriptableObject）。在 Project 窗口右键
    /// Create → Smart NPC → NPC Config 即可创建。
    /// API Key 建议只填在本地，不要把带 Key 的配置文件提交到公开仓库。
    /// </summary>
    [CreateAssetMenu(fileName = "NpcConfig", menuName = "Smart NPC/NPC Config", order = 0)]
    public class NpcConfig : ScriptableObject
    {
        [Header("OrcaRouter 连接")]
        [Tooltip("OpenAI 兼容网关地址，一般不用改")]
        public string baseUrl = "https://api.orcarouter.ai/v1";

        [Tooltip("你的 OrcaRouter API Key，从 https://www.orcarouter.ai 控制台获取")]
        public string apiKey = "";

        [Tooltip("模型名，如 openai/gpt-4o、anthropic/claude-sonnet、deepseek/deepseek-chat，或 orcarouter/auto 自动路由")]
        public string model = "orcarouter/auto";

        [Header("NPC 人设")]
        [TextArea(3, 10)]
        public string persona = "你是一位生活在中世纪村庄里的铁匠，性格豪爽、健谈。";

        [Header("记忆")]
        [Tooltip("每次对话注入提示词的记忆条数")]
        public int memoryTopK = 5;

        [Tooltip("注入提示词的最近对话轮数")]
        public int contextTurns = 6;

        [Tooltip("是否在每轮对话后调用模型自动提炼长期记忆（更聪明，但每轮多一次请求）")]
        public bool autoExtractMemory = true;

        [Header("生成参数")]
        [Range(0f, 2f)] public float temperature = 0.8f;
        public int maxTokens = 512;
    }
}
