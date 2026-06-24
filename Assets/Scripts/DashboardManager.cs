using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace CarDiagnostics
{
    public class DashboardManager : MonoBehaviour
    {
        [Header("References")]
        public OBDSimulator simulator;
        [FormerlySerializedAs("aiClient")]
        public LMStudioClient diagnosticsClient;
        public UIDocument uiDocument;

        [Header("Activity Log")]
        [SerializeField] private float logSampleInterval = 0.5f;
        [SerializeField] private int maxLogEntries = 40;

        private readonly List<string> activityLog = new List<string>();
        private readonly List<ScenarioOption> scenarios = new List<ScenarioOption>
        {
            new ScenarioOption("Normal Drive", DiagnosticScenario.Normal, "Balanced telemetry with no active fault."),
            new ScenarioOption("Engine Misfire", DiagnosticScenario.EngineMisfire, "RPM surges, lean fuel trims, and unstable idle."),
            new ScenarioOption("Overheating", DiagnosticScenario.Overheating, "Coolant temperature rises above safe range."),
            new ScenarioOption("Low Battery", DiagnosticScenario.LowBattery, "Voltage drops and electrical stability degrades."),
            new ScenarioOption("Low Oil Pressure", DiagnosticScenario.LowOilPressure, "Oil pressure falls under load."),
            new ScenarioOption("Transmission Slip", DiagnosticScenario.TransmissionSlip, "RPM is high while speed stays low."),
            new ScenarioOption("Fuel Delivery", DiagnosticScenario.FuelDeliveryIssue, "Lean condition with low rail pressure."),
            new ScenarioOption("Sensor Fault", DiagnosticScenario.SensorFault, "Telemetry looks stale or inconsistent.")
        };

        private VisualElement diagnosticsPanel;
        private VisualElement scenariosPanel;
        private VisualElement activityPanel;
        private Button diagnosticsTabButton;
        private Button scenariosTabButton;
        private Button activityTabButton;
        private Label topSummaryLabel;
        private Label modelSummaryLabel;
        private Label diagnosticsResultLabel;
        private Label decodedRpmLabel;
        private Label decodedSpeedLabel;
        private Label decodedTempLabel;
        private Label decodedBatteryLabel;
        private Label decodedOilLabel;
        private Label decodedFuelPressureLabel;
        private Label decodedThrottleLabel;
        private Label decodedDtcLabel;
        private Label scenarioDescriptionLabel;
        private VisualElement scenarioButtonContainer;
        private ScrollView activityLogScroll;
        private TextField diagnosticsPromptField;
        private Button sendSummaryPromptButton;
        private Button sendButton;
        private float logTimer;
        private int lastLoggedSequence = -1;
        private bool analysisInFlight;

        private struct ScenarioOption
        {
            public readonly string Label;
            public readonly DiagnosticScenario Scenario;
            public readonly string Description;

            public ScenarioOption(string label, DiagnosticScenario scenario, string description)
            {
                Label = label;
                Scenario = scenario;
                Description = description;
            }
        }

        private void Start()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (simulator == null)
            {
                simulator = FindObjectOfType<OBDSimulator>();
            }

            if (diagnosticsClient == null)
            {
                diagnosticsClient = FindObjectOfType<LMStudioClient>();
            }

            BindUi();
            BuildScenarioButtons();
            SetTab("diagnostics");
            ApplyScenarioPreview(scenarios[0]);
            AppendLog("Activity log ready.");
        }

        private void BindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogWarning("DashboardManager: UIDocument or rootVisualElement is missing.");
                return;
            }

            var root = uiDocument.rootVisualElement;
            diagnosticsPanel = root.Q<VisualElement>("DiagnosticsPanel");
            scenariosPanel = root.Q<VisualElement>("ScenariosPanel");
            activityPanel = root.Q<VisualElement>("ActivityPanel");

            diagnosticsTabButton = root.Q<Button>("DiagnosticsTabButton");
            scenariosTabButton = root.Q<Button>("ScenariosTabButton");
            activityTabButton = root.Q<Button>("ActivityTabButton");

            topSummaryLabel = root.Q<Label>("TopSummaryValue");
            modelSummaryLabel = root.Q<Label>("ModelSummaryValue");
            diagnosticsResultLabel = root.Q<Label>("DiagnosticsResultValue");
            decodedRpmLabel = root.Q<Label>("DecodedRpmValue");
            decodedSpeedLabel = root.Q<Label>("DecodedSpeedValue");
            decodedTempLabel = root.Q<Label>("DecodedTempValue");
            decodedBatteryLabel = root.Q<Label>("DecodedBatteryValue");
            decodedOilLabel = root.Q<Label>("DecodedOilValue");
            decodedFuelPressureLabel = root.Q<Label>("DecodedFuelPressureValue");
            decodedThrottleLabel = root.Q<Label>("DecodedThrottleValue");
            decodedDtcLabel = root.Q<Label>("DecodedDtcValue");
            scenarioDescriptionLabel = root.Q<Label>("ScenarioDescriptionValue");
            scenarioButtonContainer = root.Q<VisualElement>("ScenarioButtonContainer");
            activityLogScroll = root.Q<ScrollView>("ActivityLogScroll");
            diagnosticsPromptField = root.Q<TextField>("DiagnosticsPromptField");
            sendSummaryPromptButton = root.Q<Button>("SendSummaryPromptButton");
            sendButton = root.Q<Button>("SendButton");

            if (diagnosticsTabButton != null) diagnosticsTabButton.clicked += () => SetTab("diagnostics");
            if (scenariosTabButton != null) scenariosTabButton.clicked += () => SetTab("scenarios");
            if (activityTabButton != null) activityTabButton.clicked += () => SetTab("activity");
            if (sendSummaryPromptButton != null) sendSummaryPromptButton.clicked += PrepareDiagnosticsPrompt;
            if (sendButton != null) sendButton.clicked += SendPromptToModel;
        }

        private void BuildScenarioButtons()
        {
            if (scenarioButtonContainer == null)
            {
                return;
            }

            scenarioButtonContainer.Clear();
            foreach (var scenario in scenarios)
            {
                var option = scenario;
                var button = new Button { text = option.Label };
                button.AddToClassList("scenario-button");
                button.clicked += () => ActivateScenario(option);
                scenarioButtonContainer.Add(button);
            }
        }

        private void Update()
        {
            if (simulator == null || uiDocument == null || uiDocument.rootVisualElement == null)
            {
                return;
            }

            var raw = simulator.GetLatestRawFrame();
            var data = simulator.currentData;

            UpdateDecodedFields(data);
            UpdateSummaryFields(data);
            UpdateModelSummary(data);
            HandleRawLogging(raw);
        }

        private void UpdateDecodedFields(OBDParameters data)
        {
            if (decodedRpmLabel != null) decodedRpmLabel.text = $"{data.EngineRPM:F0} rpm";
            if (decodedSpeedLabel != null) decodedSpeedLabel.text = $"{data.VehicleSpeed:F1} km/h";
            if (decodedTempLabel != null) decodedTempLabel.text = $"{data.EngineCoolantTemp:F1} C";
            if (decodedBatteryLabel != null) decodedBatteryLabel.text = $"{data.BatteryVoltage:F2} V";
            if (decodedOilLabel != null) decodedOilLabel.text = $"{data.OilPressure:F1} kPa";
            if (decodedFuelPressureLabel != null) decodedFuelPressureLabel.text = $"{data.FuelPressure:F1} kPa";
            if (decodedThrottleLabel != null) decodedThrottleLabel.text = $"{data.ThrottlePosition:F1}%";
            if (decodedDtcLabel != null)
            {
                decodedDtcLabel.text = data.DTCErrorCodes != null && data.DTCErrorCodes.Length > 0
                    ? string.Join(", ", data.DTCErrorCodes)
                    : "None";
            }
        }

        private void UpdateSummaryFields(OBDParameters data)
        {
            string summary = OBDDecoder.FormatDecodedFrame(data);
            if (topSummaryLabel != null)
            {
                topSummaryLabel.text = summary;
            }
        }

        private void UpdateModelSummary(OBDParameters data)
        {
            if (modelSummaryLabel == null || data == null)
            {
                return;
            }

            var statuses = new List<string>
            {
                $"RPM: {GetStatusText(data.EngineRPM >= 900f && data.EngineRPM <= 3200f, data.EngineRPM > 4200f)}",
                $"Coolant: {GetStatusText(data.EngineCoolantTemp < 98f, data.EngineCoolantTemp > 108f)}",
                $"Battery: {GetStatusText(data.BatteryVoltage >= 12.2f, data.BatteryVoltage < 11.7f)}",
                $"Oil Pressure: {GetStatusText(data.OilPressure >= 120f, data.OilPressure < 90f)}",
                $"Fuel Pressure: {GetStatusText(data.FuelPressure >= 28f, data.FuelPressure < 20f)}",
                $"Throttle: {GetStatusText(data.ThrottlePosition < 70f, data.ThrottlePosition > 90f)}",
                $"DTCs: {(data.DTCErrorCodes != null && data.DTCErrorCodes.Length > 0 ? "Fault codes present" : "No fault codes")}"
            };

            modelSummaryLabel.text = string.Join("\n", statuses);
        }

        private void HandleRawLogging(OBDRawFrame raw)
        {
            if (raw == null || raw.SequenceId == lastLoggedSequence)
            {
                return;
            }

            logTimer += Time.deltaTime;
            if (logTimer < logSampleInterval)
            {
                return;
            }

            logTimer = 0f;
            lastLoggedSequence = raw.SequenceId;
            AppendLog($"[{raw.TimestampUtc}] {raw.ScenarioName} | RPM {Mathf.RoundToInt(simulator.currentData.EngineRPM)} | Speed {simulator.currentData.VehicleSpeed:F1} km/h");
        }

        private void PrepareDiagnosticsPrompt()
        {
            if (simulator == null || diagnosticsPromptField == null)
            {
                return;
            }

            var raw = simulator.GetLatestRawFrame();
            var data = simulator.currentData;
            diagnosticsPromptField.value = OBDDecoder.BuildDiagnosticPrompt(data, raw, simulator.GetActiveScenarioName());
        }

        private void SendPromptToModel()
        {
            if (analysisInFlight || simulator == null)
            {
                return;
            }

            if (diagnosticsClient == null)
            {
                if (diagnosticsResultLabel != null)
                {
                    diagnosticsResultLabel.text = "Local diagnostics client is not configured.";
                }
                return;
            }

            analysisInFlight = true;

            var raw = simulator.GetLatestRawFrame();
            var data = simulator.currentData;
            string customPrompt = diagnosticsPromptField != null ? diagnosticsPromptField.value.Trim() : string.Empty;
            string prompt = string.IsNullOrWhiteSpace(customPrompt)
                ? OBDDecoder.BuildDiagnosticPrompt(data, raw, simulator.GetActiveScenarioName())
                : customPrompt;

            diagnosticsClient.GetAnalysis(prompt, result =>
            {
                analysisInFlight = false;
                if (diagnosticsResultLabel != null)
                {
                    diagnosticsResultLabel.text = result;
                }
            });
        }

        private void ApplyScenarioPreview(ScenarioOption option)
        {
            if (scenarioDescriptionLabel != null)
            {
                scenarioDescriptionLabel.text = option.Description;
            }
        }

        private void ActivateScenario(ScenarioOption option)
        {
            if (simulator == null)
            {
                return;
            }

            simulator.ActivateScenario(option.Scenario);
            ApplyScenarioPreview(option);
            AppendLog($"Scenario changed to {option.Label}");
        }

        private void SetTab(string tabName)
        {
            TogglePanel(diagnosticsPanel, tabName == "diagnostics");
            TogglePanel(scenariosPanel, tabName == "scenarios");
            TogglePanel(activityPanel, tabName == "activity");

            ToggleButton(diagnosticsTabButton, tabName == "diagnostics");
            ToggleButton(scenariosTabButton, tabName == "scenarios");
            ToggleButton(activityTabButton, tabName == "activity");
        }

        private static void TogglePanel(VisualElement panel, bool visible)
        {
            if (panel == null)
            {
                return;
            }

            panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ToggleButton(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList("tab-button--active", active);
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            activityLog.Insert(0, message);
            while (activityLog.Count > maxLogEntries)
            {
                activityLog.RemoveAt(activityLog.Count - 1);
            }

            RefreshActivityLog();
        }

        private void RefreshActivityLog()
        {
            if (activityLogScroll == null)
            {
                return;
            }

            activityLogScroll.Clear();
            for (int i = 0; i < activityLog.Count; i++)
            {
                var entry = new Label(activityLog[i]);
                entry.AddToClassList("log-entry");
                activityLogScroll.Add(entry);
            }
        }

        private static string GetStatusText(bool normalCondition, bool criticalCondition)
        {
            if (criticalCondition)
            {
                return "Critical";
            }

            return normalCondition ? "Normal" : "Watch";
        }
    }
}
