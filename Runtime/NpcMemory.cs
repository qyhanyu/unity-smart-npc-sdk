using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OrcaRouter.SmartNpc
{
    /// <summary>一条长期记忆。</summary>
    [Serializable]
    public class MemoryEntry
    {
        public string content;      // 记忆内容，如"玩家叫小明"
        public string speaker;      // "player" | "npc"
        public float importance;    // 0~1，越大越重要
        public long timestamp;      // Unix 秒

        public MemoryEntry(string content, string speaker, float importance)
        {
            this.content = content;
            this.speaker = speaker;
            this.importance = Mathf.Clamp01(importance);
            this.timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        }
    }

    [Serializable]
    private class MemoryListWrapper
    {
        public List<MemoryEntry> items = new List<MemoryEntry>();
    }

    /// <summary>
    /// 长期记忆库：按"重要性 × 时间衰减 + 关键词重合度"检索最相关的记忆，
    /// 并以 JSON 持久化到 Application.persistentDataPath。
    /// 简单、零依赖；后续可替换为向量数据库版本。
    /// </summary>
    public class MemoryStore
    {
        public List<MemoryEntry> All => _entries;

        private readonly List<MemoryEntry> _entries = new List<MemoryEntry>();
        private readonly string _savePath;

        /// <param name="npcId">每个 NPC 独立的记忆文件名</param>
        public MemoryStore(string npcId)
        {
            string safeId = string.Concat(npcId.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
            _savePath = Path.Combine(Application.persistentDataPath, $"smartnpc_memory_{safeId}.json");
            Load();
        }

        public void Add(MemoryEntry entry)
        {
            _entries.Add(entry);
            // 防止重复：内容完全相同且 10 分钟内的旧记忆删掉
            _entries.RemoveAll(e =>
                !ReferenceEquals(e, entry) &&
                e.content == entry.content &&
                entry.timestamp - e.timestamp < 600);
            Save();
        }

        /// <summary>检索与 query 最相关的前 topK 条记忆。</summary>
        public List<MemoryEntry> Search(string query, int topK)
        {
            if (_entries.Count == 0 || topK <= 0) return new List<MemoryEntry>();

            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var qWords = SplitWords(query);

            return _entries
                .Select(e => new { e, score = Score(e, qWords, now) })
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => x.e)
                .ToList();
        }

        public void Clear()
        {
            _entries.Clear();
            Save();
        }

        // ---------- 内部 ----------

        private float Score(MemoryEntry e, HashSet<string> qWords, long now)
        {
            // 时间衰减：半衰期 3 天
            double ageDays = Math.Max(0, now - e.timestamp) / 86400.0;
            float recency = (float)Math.Pow(0.5, ageDays / 3.0);

            // 关键词重合度（支持简单中文：整句匹配给基础分，英文单词匹配加权）
            float overlap = 0.1f;
            if (qWords.Count > 0)
            {
                string lower = e.content.ToLowerInvariant();
                overlap += qWords.Count(w => w.Length > 1 && lower.Contains(w)) * 0.3f;
            }

            return e.importance * recency + overlap;
        }

        private static HashSet<string> SplitWords(string text)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (var w in text.ToLowerInvariant().Split(
                new[] { ' ', '，', '。', '！', '？', ',', '.', '!', '?', '、', '：', ':', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries))
                set.Add(w);
            return set;
        }

        private void Save()
        {
            try
            {
                var wrapper = new MemoryListWrapper { items = _entries };
                File.WriteAllText(_savePath, JsonUtility.ToJson(wrapper));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SmartNPC] 记忆保存失败：" + e.Message);
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_savePath)) return;
                var wrapper = JsonUtility.FromJson<MemoryListWrapper>(File.ReadAllText(_savePath));
                if (wrapper?.items != null)
                {
                    _entries.Clear();
                    _entries.AddRange(wrapper.items);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SmartNPC] 记忆加载失败：" + e.Message);
            }
        }
    }
}
