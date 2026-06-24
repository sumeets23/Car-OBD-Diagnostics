using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarDiagnostics.Editor
{
    public class CarDiagnosticSetupTool : EditorWindow
    {
        [MenuItem("Tools/Car Diagnostic/Force Setup Scene")]
        public static void SetupScene()
        {
            Debug.Log("--- STARTING FORCE SETUP ---");

            foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (canvas.gameObject.name.Contains("Diagnostic"))
                {
                    Debug.Log("Removing old legacy canvas: " + canvas.gameObject.name);
                    Undo.DestroyObjectImmediate(canvas.gameObject);
                }
            }

            GameObject managerObj = GameObject.Find("DiagnosticManager") ?? new GameObject("DiagnosticManager");

            var simulator = managerObj.GetComponent<OBDSimulator>() ?? managerObj.AddComponent<OBDSimulator>();
            var diagnosticsClient = managerObj.GetComponent<LMStudioClient>() ?? managerObj.AddComponent<LMStudioClient>();
            var dashboard = managerObj.GetComponent<DashboardManager>() ?? managerObj.AddComponent<DashboardManager>();

            GameObject uiObj = GameObject.Find("DiagnosticUI") ?? new GameObject("DiagnosticUI");
            var uiDoc = uiObj.GetComponent<UIDocument>() ?? uiObj.AddComponent<UIDocument>();

            string panelSettingsPath = "Assets/UI/DashboardPanelSettings.asset";
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
            }

            panelSettings.targetDisplay = 0;
            panelSettings.sortingOrder = 999;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.clearColor = false;
            panelSettings.clearDepthStencil = false;

            uiDoc.panelSettings = panelSettings;
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/DashboardLayout.uxml");

            dashboard.uiDocument = uiDoc;
            dashboard.simulator = simulator;
            dashboard.diagnosticsClient = diagnosticsClient;

            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
            if (listeners.Length == 0 && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            else if (listeners.Length > 1)
            {
                for (int i = 1; i < listeners.Length; i++)
                {
                    listeners[i].enabled = false;
                }
            }

            Selection.activeGameObject = managerObj;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("--- SETUP COMPLETE. The dashboard now uses UI Toolkit tabs and decoded diagnostics telemetry. ---");
        }
    }
}
