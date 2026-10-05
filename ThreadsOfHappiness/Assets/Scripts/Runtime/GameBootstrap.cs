using UnityEngine;

namespace Toh.Runtime
{
    /// <summary>
    /// 自举入口：场景保持空白，运行时动态创建相机 / Canvas / EventSystem / 游戏导演。
    /// 用户只需用 Unity Hub 打开工程，任意场景点 Play 即可游玩。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // 相机
            if (Object.FindObjectOfType<Camera>() == null)
            {
                var camGo = new GameObject("Main Camera");
                var cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                cam.orthographicSize = 5.4f;
                cam.backgroundColor = new Color(0.93f, 0.91f, 0.87f);
                camGo.transform.position = new Vector3(0, 0, -10);
            }

            // EventSystem
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 游戏导演
            if (Object.FindObjectOfType<GameDirector>() == null)
            {
                var go = new GameObject("GameDirector");
                go.AddComponent<GameDirector>();
            }
        }
    }
}
