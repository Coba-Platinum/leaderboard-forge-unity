using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using CobaPatinum.LeaderboardForge;

namespace CobaPlatinum.LeaderboardForge
{
    public class LeaderboardForgeBehaviour : MonoBehaviour
    {
        private static class ResponseParser
        {
            public static LeaderboardEntry[] FromJson(string json)
            {
                string wrap = "{\"items\":" + json + "}";
                Wrapper wrapper = JsonUtility.FromJson<Wrapper>(wrap);
                return wrapper.items;
            }
            [Serializable] private class Wrapper { public LeaderboardEntry[] items; }
        }

        public void SendGetRequest(string url, Action<LeaderboardEntry[]> callback, Action<string> errorCallback)
        {
            StartCoroutine(GetRoutine(url, callback, errorCallback));
        }

        private IEnumerator GetRoutine(string url, Action<LeaderboardEntry[]> callback, Action<string> errorCallback)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                    callback?.Invoke(ResponseParser.FromJson(webRequest.downloadHandler.text));
                else
                    errorCallback?.Invoke($"{webRequest.error} - {webRequest.downloadHandler.text}");
            }
        }

        public void SendPostRequest(string url, string jsonPayload, Action<bool> callback, Action<string> errorCallback)
        {
            StartCoroutine(PostRoutine(url, jsonPayload, callback, errorCallback));
        }

        private IEnumerator PostRoutine(string url, string jsonPayload, Action<bool> callback, Action<string> errorCallback)
        {
            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                    callback?.Invoke(true);
                else
                {
                    errorCallback?.Invoke($"{webRequest.error} - {webRequest.downloadHandler.text}");
                    callback?.Invoke(false);
                }
            }
        }
    }
}
