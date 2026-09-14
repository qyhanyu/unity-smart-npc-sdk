using System.Threading.Tasks;
using UnityEngine;

namespace OrcaRouter.SmartNpc.Samples
{
    /// <summary>
    /// 零依赖演示：把本脚本挂到任意 GameObject 上、拖入一个 NpcConfig，
    /// 运行游戏即可在屏幕上和 NPC 聊天（用 OnGUI 画界面，无需预制体/场景）。
    /// </summary>
    public class DemoChatUI : MonoBehaviour
    {
        public NpcConfig config;
        public string npcId = "demo_npc";

        private SmartNpc _npc;
        private string _input = "";
        private string _log = "和 NPC 打个招呼吧！\n";
        private Vector2 _scroll;

        private async void Start()
        {
            gameObject.AddComponent<SmartNpc>();
            _npc = GetComponent<SmartNpc>();
            _npc.config = config;
            _npc.npcId = npcId;
            _npc.OnError += e => _log += $"\n[错误] {e.Message}\n";
            await Task.Yield();
        }

        private async void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            GUILayout.Label(_log);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _input = GUILayout.TextField(_input, GUILayout.ExpandWidth(true));
            GUI.enabled = _npc != null && !string.IsNullOrEmpty(_input);
            if (GUILayout.Button("发送", GUILayout.Width(80)))
            {
                string said = _input;
                _input = "";
                _log += $"\n[玩家] {said}\n";
                string reply = await _npc.TalkAsync(said);
                if (!string.IsNullOrEmpty(reply))
                    _log += $"[NPC] {reply}\n";
                _scroll = new Vector2(0, float.MaxValue);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
