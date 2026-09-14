using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace OrcaRouter.SmartNpc
{
    /// <summary>一条对话消息，对应 OpenAI messages 格式。</summary>
    [Serializable]
    public class ChatMessage
    {
        public string role;     // "system" | "user" | "assistant"
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }

        public static ChatMessage System(string content) => new ChatMessage("system", content);
        public static ChatMessage User(string content) => new ChatMessage("user", content);
        public static ChatMessage Assistant(string content) => new ChatMessage("assistant", content);
    }

    /// <summary>OrcaRouter 网络请求异常。</summary>
    public class OrcaRouterException : Exception
    {
        public long StatusCode;

        public OrcaRouterException(string message, long statusCode = 0) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// OrcaRouter 客户端。OrcaRouter 是 OpenAI 兼容网关，
    /// 只需改 base_url 即可路由到 200+ 模型。
    /// 文档：https://docs.orcarouter.ai
    /// </summary>
    public class OrcaRouterClient
    {
        public string BaseUrl = "https://api.orcarouter.ai/v1";
        public string ApiKey = "";
        public string Model = "orcarouter/auto"; // "vendor/model" 或 "orcarouter/auto" 自动路由
        public int TimeoutSeconds = 60;

        [Header("可选：归因标识（会显示在 OrcaRouter 控制台流量视图）")]
        public string AppUrl = "";   // HTTP-Referer，如 https://github.com/qyhanyu/unity-smart-npc-sdk
        public string AppName = "";  // X-Title，如 Unity Smart NPC SDK

        // ---------- 请求 / 响应 DTO（对齐 OpenAI chat.completions） ----------

        [Serializable]
        private class MsgDto
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class RequestDto
        {
            public string model;
            public List<MsgDto> messages = new List<MsgDto>();
            public float temperature;
            public int max_tokens;
        }

        [Serializable]
        private class ResponseDto
        {
            public ChoiceDto[] choices;
        }

        [Serializable]
        private class ChoiceDto
        {
            public MsgDto message;
        }

        // ---------- 公共 API ----------

        /// <summary>
        /// 发送一轮多轮对话，返回 assistant 的回复文本。
        /// 必须在主线程调用（内部使用 UnityWebRequest）。
        /// </summary>
        public async Task<string> ChatAsync(List<ChatMessage> messages,
            float temperature = 0.8f, int maxTokens = 512)
        {
            if (string.IsNullOrEmpty(ApiKey))
                throw new OrcaRouterException("ApiKey 未设置。请在 NpcConfig 或代码中填入你的 OrcaRouter API Key。");

            var dto = new RequestDto { model = Model, temperature = temperature, max_tokens = maxTokens };
            foreach (var m in messages)
                dto.messages.Add(new MsgDto { role = m.role, content = m.content });

            string bodyJson = JsonUtility.ToJson(dto);

            using var req = new UnityWebRequest(BaseUrl.TrimEnd('/') + "/chat/completions", UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + ApiKey);
            if (!string.IsNullOrEmpty(AppUrl)) req.SetRequestHeader("HTTP-Referer", AppUrl);
            if (!string.IsNullOrEmpty(AppName)) req.SetRequestHeader("X-Title", AppName);
            req.timeout = TimeoutSeconds;

            var tcs = new TaskCompletionSource<string>();
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonUtility.FromJson<ResponseDto>(req.downloadHandler.text);
                        string text = resp?.choices != null && resp.choices.Length > 0 && resp.choices[0].message != null
                            ? resp.choices[0].message.content
                            : "";
                        tcs.TrySetResult(text ?? "");
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(new OrcaRouterException("解析响应失败：" + e.Message));
                    }
                }
                else
                {
                    long code = req.responseCode;
                    string detail = req.downloadHandler != null ? req.downloadHandler.text : "";
                    tcs.TrySetException(new OrcaRouterException(
                        $"请求失败 HTTP {code}：{req.error} {detail}", code));
                }
            };

            return await tcs.Task;
        }
    }
}
