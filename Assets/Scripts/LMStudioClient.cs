using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CarDiagnostics
{
    public class LMStudioClient : MonoBehaviour
    {
        [SerializeField] private string apiUrl = "http://localhost:1234/v1/chat/completions";
        [SerializeField] private string modelName = "local-model";
        [SerializeField] private float timeoutSeconds = 30f;

        [Serializable]
        private class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class ChatRequest
        {
            public string model;
            public ChatMessage[] messages;
            public float temperature = 0.2f;
        }

        [Serializable]
        private class ChatResponse
        {
            public Choice[] choices;
        }

        [Serializable]
        private class Choice
        {
            public ChatMessage message;
        }

        public void GetAnalysis(OBDParameters data, Action<string> onComplete)
        {
            string prompt = BuildPromptFromDecodedData(data);
            StartCoroutine(SendPrompt(prompt, onComplete));
        }

        public void GetAnalysis(string customPrompt, Action<string> onComplete)
        {
            StartCoroutine(SendPrompt(customPrompt, onComplete));
        }

        private string BuildPromptFromDecodedData(OBDParameters data)
        {
            if (data == null)
            {
                return "Analyze the car telemetry and explain the likely status.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Analyze the decoded OBD data and give a concise automotive diagnosis.");
            builder.AppendLine("Include likely issue, severity, and immediate action.");
            builder.AppendLine($"RPM: {data.EngineRPM:F0}");
            builder.AppendLine($"Speed: {data.VehicleSpeed:F1} km/h");
            builder.AppendLine($"Coolant Temp: {data.EngineCoolantTemp:F1} C");
            builder.AppendLine($"Battery: {data.BatteryVoltage:F2} V");
            builder.AppendLine($"Oil Pressure: {data.OilPressure:F1} kPa");
            builder.AppendLine($"Fuel Pressure: {data.FuelPressure:F1} kPa");
            builder.AppendLine($"Throttle: {data.ThrottlePosition:F1}%");
            builder.AppendLine($"Load: {data.EngineLoad:F1}%");
            builder.AppendLine($"DTCs: {(data.DTCErrorCodes != null && data.DTCErrorCodes.Length > 0 ? string.Join(", ", data.DTCErrorCodes) : "none")}");
            return builder.ToString();
        }

        private IEnumerator SendPrompt(string prompt, Action<string> onComplete)
        {
            var requestBody = new ChatRequest
            {
                model = modelName,
                messages = new[]
                {
                    new ChatMessage
                    {
                        role = "system",
                        content = "You are a precise automotive diagnostics assistant. Keep the response short, direct, and practical."
                    },
                    new ChatMessage { role = "user", content = prompt }
                }
            };

            string json = JsonUtility.ToJson(requestBody);
            using (var request = new UnityWebRequest(apiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.CeilToInt(timeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke($"Local diagnostics request failed: {request.error}. Check that LM Studio is running on port 1234.");
                    yield break;
                }

                var response = JsonUtility.FromJson<ChatResponse>(request.downloadHandler.text);
                if (response != null && response.choices != null && response.choices.Length > 0 && response.choices[0].message != null)
                {
                    onComplete?.Invoke(response.choices[0].message.content);
                }
                else
                {
                    onComplete?.Invoke("The local diagnostics service returned an empty response.");
                }
            }
        }
    }
}
