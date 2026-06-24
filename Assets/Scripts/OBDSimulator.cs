using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarDiagnostics
{
    public enum DiagnosticScenario
    {
        Normal = 0,
        EngineMisfire,
        Overheating,
        LowBattery,
        LowOilPressure,
        TransmissionSlip,
        FuelDeliveryIssue,
        SensorFault
    }

    public class OBDSimulator : MonoBehaviour
    {
        public OBDParameters currentData = new OBDParameters();

        [Header("Simulation Settings")]
        public bool isEngineRunning = true;
        public float updateInterval = 0.25f;

        [Header("Active Scenario (read-only)")]
        [SerializeField] private string activeScenarioName = "Normal";
        [SerializeField] private int sequenceId;

        [Header("Latest Raw Frame")]
        [SerializeField] private OBDRawFrame latestRawFrame = new OBDRawFrame();

        private float timer;
        private float noiseOffset;
        private float scenarioBlend;
        private float faultTimer;
        private DiagnosticScenario currentScenario = DiagnosticScenario.Normal;

        private readonly Dictionary<DiagnosticScenario, string[]> scenarioDtcMap = new Dictionary<DiagnosticScenario, string[]>
        {
            { DiagnosticScenario.Normal, Array.Empty<string>() },
            { DiagnosticScenario.EngineMisfire, new[] { "P0300", "P0301" } },
            { DiagnosticScenario.Overheating, new[] { "P0217", "P0118" } },
            { DiagnosticScenario.LowBattery, new[] { "P0562", "P0615" } },
            { DiagnosticScenario.LowOilPressure, new[] { "P0524", "P0011" } },
            { DiagnosticScenario.TransmissionSlip, new[] { "P0730", "P0700" } },
            { DiagnosticScenario.FuelDeliveryIssue, new[] { "P0171", "P0087" } },
            { DiagnosticScenario.SensorFault, new[] { "P0101", "P0113" } }
        };

        private void Start()
        {
            noiseOffset = UnityEngine.Random.Range(0f, 1000f);
            currentData = new OBDParameters
            {
                FuelLevel = 72f,
                BatteryVoltage = 12.6f,
                OilPressure = 260f,
                CheckEngineStatus = false,
                DTCErrorCodes = Array.Empty<string>(),
                GearPosition = 1,
                ActiveAnomalies = new List<string>(),
                Predictions = new List<string>()
            };

            latestRawFrame = OBDDecoder.Encode(currentData, activeScenarioName, Array.Empty<string>(), false, 0, "Boot sample");
        }

        private void Update()
        {
            if (!isEngineRunning)
            {
                return;
            }

            if (currentScenario == DiagnosticScenario.Normal)
            {
                scenarioBlend = Mathf.MoveTowards(scenarioBlend, 0f, Time.deltaTime * 0.8f);
                faultTimer = 0f;
            }
            else
            {
                scenarioBlend = Mathf.MoveTowards(scenarioBlend, 1f, Time.deltaTime * 0.5f);
                faultTimer += Time.deltaTime;
            }

            timer += Time.deltaTime;
            if (timer >= updateInterval)
            {
                SampleAndDecode();
                timer = 0f;
            }
        }

        public void ActivateScenario(DiagnosticScenario scenario)
        {
            currentScenario = scenario;
            activeScenarioName = scenario.ToString();
            scenarioBlend = 0f;
            faultTimer = 0f;

            currentData.CheckEngineStatus = scenario != DiagnosticScenario.Normal;
            currentData.DTCErrorCodes = scenarioDtcMap.TryGetValue(scenario, out var dtcCodes)
                ? dtcCodes
                : Array.Empty<string>();
        }

        public DiagnosticScenario GetCurrentScenario()
        {
            return currentScenario;
        }

        public string GetActiveScenarioName()
        {
            return activeScenarioName;
        }

        public OBDRawFrame GetLatestRawFrame()
        {
            return latestRawFrame;
        }

        public string GetLatestRawSummary()
        {
            return OBDDecoder.FormatRawFrame(latestRawFrame);
        }

        private void SampleAndDecode()
        {
            var target = BuildTargetSnapshot();
            var dtcs = scenarioDtcMap.TryGetValue(currentScenario, out var scenarioCodes)
                ? scenarioCodes
                : Array.Empty<string>();

            latestRawFrame = OBDDecoder.Encode(target, activeScenarioName, dtcs, currentScenario != DiagnosticScenario.Normal, ++sequenceId);
            currentData = OBDDecoder.Decode(latestRawFrame);
            ApplyBasicDiagnostics(currentData);
        }

        private OBDParameters BuildTargetSnapshot()
        {
            float t = Time.time + noiseOffset;

            float normalRpm = 850f + Mathf.PerlinNoise(t * 0.35f, 0.1f) * 3200f;
            float normalSpeed = Mathf.Clamp((normalRpm - 800f) / 30f, 0f, 125f);
            float normalTemp = 84f + Mathf.Sin(t * 0.18f) * 3.5f;
            float normalBattery = 13.7f + Mathf.PerlinNoise(t * 0.15f, 50.1f) * 0.4f;
            float normalThrottle = Mathf.PerlinNoise(t * 0.7f, 20.2f) * 55f;
            float normalLoad = Mathf.Clamp(normalThrottle * 1.2f, 8f, 92f);
            float normalFuel = Mathf.Clamp(currentData.FuelLevel - Time.deltaTime * 0.02f, 0f, 100f);
            float normalOil = 240f + Mathf.PerlinNoise(t * 0.2f, 60.2f) * 40f;
            float normalFuelPressure = 40f + Mathf.PerlinNoise(t * 0.45f, 40.4f) * 6f;
            float normalMaf = Mathf.Max(2f, (normalRpm / 1000f) * 4.8f);
            float normalAfr = 14.6f + (Mathf.PerlinNoise(t * 1.7f, 10.5f) - 0.5f) * 0.2f;

            float rpm = normalRpm;
            float speed = normalSpeed;
            float temp = normalTemp;
            float battery = normalBattery;
            float throttle = normalThrottle;
            float load = normalLoad;
            float fuel = normalFuel;
            float oil = normalOil;
            float fuelPressure = normalFuelPressure;
            float maf = normalMaf;
            float afr = normalAfr;
            string notes = "Virtual OBD feed";

            switch (currentScenario)
            {
                case DiagnosticScenario.EngineMisfire:
                    rpm = Mathf.Lerp(normalRpm, 1200f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 18f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 88f, scenarioBlend);
                    throttle = Mathf.Lerp(normalThrottle, 18f, scenarioBlend);
                    fuelPressure = Mathf.Lerp(normalFuelPressure, 16f, scenarioBlend);
                    maf = Mathf.Lerp(normalMaf, 3.2f, scenarioBlend);
                    afr = Mathf.Lerp(normalAfr, 15.8f, scenarioBlend);
                    notes = "Misfire pattern with unstable idle";
                    break;
                case DiagnosticScenario.Overheating:
                    rpm = Mathf.Lerp(normalRpm, 2500f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 55f, scenarioBlend);
                    temp = Mathf.Lerp(normalTemp, 122f + faultTimer * 0.4f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 82f, scenarioBlend);
                    throttle = Mathf.Lerp(normalThrottle, 52f, scenarioBlend);
                    oil = Mathf.Lerp(normalOil, 190f, scenarioBlend);
                    afr = Mathf.Lerp(normalAfr, 14.2f, scenarioBlend);
                    notes = "Cooling system stress and rising coolant temperature";
                    break;
                case DiagnosticScenario.LowBattery:
                    rpm = Mathf.Lerp(normalRpm, 900f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 0f, scenarioBlend);
                    battery = Mathf.Lerp(normalBattery, 10.8f - faultTimer * 0.03f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 22f, scenarioBlend);
                    throttle = Mathf.Lerp(normalThrottle, 6f, scenarioBlend);
                    notes = "Electrical load issue and falling supply voltage";
                    break;
                case DiagnosticScenario.LowOilPressure:
                    rpm = Mathf.Lerp(normalRpm, 1600f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 28f, scenarioBlend);
                    oil = Mathf.Lerp(normalOil, 54f - faultTimer * 0.5f, scenarioBlend);
                    fuelPressure = Mathf.Lerp(normalFuelPressure, 18f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 70f, scenarioBlend);
                    notes = "Oil pressure loss under load";
                    break;
                case DiagnosticScenario.TransmissionSlip:
                    rpm = Mathf.Lerp(normalRpm, 5200f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 32f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 76f, scenarioBlend);
                    throttle = Mathf.Lerp(normalThrottle, 78f, scenarioBlend);
                    notes = "High RPM with limited road speed";
                    break;
                case DiagnosticScenario.FuelDeliveryIssue:
                    rpm = Mathf.Lerp(normalRpm, 1300f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 22f, scenarioBlend);
                    load = Mathf.Lerp(normalLoad, 84f, scenarioBlend);
                    fuelPressure = Mathf.Lerp(normalFuelPressure, 14f, scenarioBlend);
                    maf = Mathf.Lerp(normalMaf, 2.4f, scenarioBlend);
                    afr = Mathf.Lerp(normalAfr, 16.3f, scenarioBlend);
                    notes = "Lean condition and fuel starvation";
                    break;
                case DiagnosticScenario.SensorFault:
                    rpm = Mathf.Lerp(normalRpm, 1800f, scenarioBlend);
                    speed = Mathf.Lerp(normalSpeed, 40f, scenarioBlend);
                    temp = Mathf.Lerp(normalTemp, 96f, scenarioBlend);
                    battery = Mathf.Lerp(normalBattery, 13.1f, scenarioBlend);
                    afr = Mathf.Lerp(normalAfr, 13.8f, scenarioBlend);
                    notes = "Sensor drift or stale data pattern";
                    break;
            }

            if (currentScenario == DiagnosticScenario.EngineMisfire && scenarioBlend > 0.5f)
            {
                rpm += Mathf.Sin(t * 28f) * 420f * scenarioBlend;
            }

            var target = new OBDParameters
            {
                EngineRPM = Mathf.Max(0f, rpm),
                VehicleSpeed = Mathf.Max(0f, speed),
                EngineCoolantTemp = temp,
                IntakeAirTemp = 27f + Mathf.PerlinNoise(t * 0.2f, 10.3f) * 9f,
                AmbientAirTemp = 23f,
                ThrottlePosition = Mathf.Clamp(throttle, 0f, 100f),
                AcceleratorPedalPosition = Mathf.Clamp(throttle + 2f, 0f, 100f),
                EngineLoad = Mathf.Clamp(load, 0f, 100f),
                FuelLevel = fuel,
                BatteryVoltage = Mathf.Max(8f, battery),
                OilPressure = Mathf.Max(0f, oil),
                FuelTrimShort = currentScenario == DiagnosticScenario.FuelDeliveryIssue ? 14f : 1.5f,
                FuelTrimLong = currentScenario == DiagnosticScenario.FuelDeliveryIssue ? 11f : 0.5f,
                FuelPressure = Mathf.Max(0f, fuelPressure),
                MassAirFlow = Mathf.Max(0f, maf),
                ManifoldPressure = currentScenario == DiagnosticScenario.TransmissionSlip ? 78f : 42f,
                AirFuelRatio = Mathf.Max(0f, afr),
                O2SensorValue = currentScenario == DiagnosticScenario.SensorFault ? 0.15f : 0.72f,
                EngineRunTime = currentData.EngineRunTime + updateInterval,
                FuelConsumption = Mathf.Lerp(6.2f, 15.4f, load / 100f),
                EngineTorque = Mathf.Lerp(80f, 420f, load / 100f),
                CheckEngineStatus = currentScenario != DiagnosticScenario.Normal,
                DTCErrorCodes = currentData.DTCErrorCodes ?? Array.Empty<string>(),
                EvapSystemStatus = currentScenario == DiagnosticScenario.Normal ? "Normal" : "Under review",
                TimingAdvance = Mathf.Lerp(8f, 26f, load / 100f),
                DistanceSinceCodesCleared = currentData.DistanceSinceCodesCleared + (speed * updateInterval / 3600f),
                CatalystTemperature = temp + 24f,
                GearPosition = EstimateGear(speed),
                TransmissionTemp = Mathf.Lerp(72f, 108f, speed / 140f)
            };

            target.ActiveAnomalies = new List<string>();
            target.Predictions = new List<string>();
            if (!string.IsNullOrWhiteSpace(notes))
            {
                target.Predictions.Add(notes);
            }

            return target;
        }

        private static void ApplyBasicDiagnostics(OBDParameters data)
        {
            data.ActiveAnomalies ??= new List<string>();
            data.Predictions ??= new List<string>();
            data.ActiveAnomalies.Clear();

            float engineHealth = 100f;
            float coolingHealth = 100f;
            float batteryHealth = 100f;

            if (data.EngineCoolantTemp > 102f)
            {
                data.ActiveAnomalies.Add("High coolant temperature");
                coolingHealth -= (data.EngineCoolantTemp - 102f) * 2.25f;
            }

            if (data.BatteryVoltage < 12.0f)
            {
                data.ActiveAnomalies.Add("Battery voltage drop");
                batteryHealth -= (12.0f - data.BatteryVoltage) * 22f;
            }

            if (data.OilPressure < 90f)
            {
                data.ActiveAnomalies.Add("Oil pressure low");
                engineHealth -= (90f - data.OilPressure) * 0.55f;
            }

            if (data.EngineRPM > 4500f && data.VehicleSpeed < 45f)
            {
                data.ActiveAnomalies.Add("High RPM / low speed mismatch");
                engineHealth -= 12f;
            }

            if (data.FuelPressure < 22f)
            {
                data.ActiveAnomalies.Add("Fuel pressure low");
                engineHealth -= 8f;
            }

            if (data.DTCErrorCodes != null && data.DTCErrorCodes.Length > 0)
            {
                data.ActiveAnomalies.Add("Stored diagnostic trouble codes");
            }

            data.EngineHealth = Mathf.Clamp(engineHealth, 0f, 100f);
            data.CoolingHealth = Mathf.Clamp(coolingHealth, 0f, 100f);
            data.BatteryHealth = Mathf.Clamp(batteryHealth, 0f, 100f);
            data.OverallHealth = (data.EngineHealth + data.CoolingHealth + data.BatteryHealth) / 3f;

            data.Predictions.Clear();
            if (data.EngineCoolantTemp > 110f)
            {
                data.Predictions.Add("Coolant temperature may keep rising.");
            }
            if (data.BatteryVoltage < 11.5f)
            {
                data.Predictions.Add("Electrical stability is deteriorating.");
            }
            if (data.OilPressure < 70f)
            {
                data.Predictions.Add("Engine wear risk is increasing.");
            }
        }

        private static int EstimateGear(float speedKmh)
        {
            if (speedKmh < 5f) return 1;
            if (speedKmh < 25f) return 2;
            if (speedKmh < 45f) return 3;
            if (speedKmh < 70f) return 4;
            if (speedKmh < 95f) return 5;
            return 6;
        }
    }
}
