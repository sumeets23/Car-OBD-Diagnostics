using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CarDiagnostics
{
    [Serializable]
    public class OBDRawFrame
    {
        public int SequenceId;
        public string TimestampUtc;
        public string ScenarioName;
        public bool MalfunctionIndicatorLamp;

        public int EngineRpmA;
        public int EngineRpmB;
        public int VehicleSpeedA;
        public int EngineCoolantTempA;
        public int IntakeAirTempA;
        public int ThrottlePositionA;
        public int AcceleratorPedalPositionA;
        public int EngineLoadA;
        public int FuelLevelA;
        public int BatteryVoltageA;
        public int BatteryVoltageB;
        public int OilPressureA;
        public int FuelTrimShortA;
        public int FuelTrimLongA;
        public int FuelPressureA;
        public int MassAirFlowA;
        public int MassAirFlowB;
        public int ManifoldPressureA;
        public int AirFuelRatioA;
        public int AirFuelRatioB;
        public int O2SensorValueA;
        public int EngineRunTimeA;
        public int EngineRunTimeB;
        public string[] DtcCodes = Array.Empty<string>();
        public string RawNotes;
    }

    public static class OBDDecoder
    {
                public static OBDParameters Decode(OBDRawFrame raw)
        {
            if (raw == null)
            {
                return new OBDParameters
                {
                    DTCErrorCodes = Array.Empty<string>(),
                    ActiveAnomalies = new List<string>(),
                    Predictions = new List<string>()
                };
            }
            var decoded = new OBDParameters
            {
                EngineRPM = Mathf.Max(0f, ((raw.EngineRpmA * 256f) + raw.EngineRpmB) / 4f),
                VehicleSpeed = Mathf.Max(0f, raw.VehicleSpeedA),
                EngineCoolantTemp = raw.EngineCoolantTempA - 40f,
                IntakeAirTemp = raw.IntakeAirTempA - 40f,
                AmbientAirTemp = raw.IntakeAirTempA - 40f,
                ThrottlePosition = Mathf.Clamp(raw.ThrottlePositionA * 100f / 255f, 0f, 100f),
                AcceleratorPedalPosition = Mathf.Clamp(raw.AcceleratorPedalPositionA * 100f / 255f, 0f, 100f),
                EngineLoad = Mathf.Clamp(raw.EngineLoadA * 100f / 255f, 0f, 100f),
                FuelLevel = Mathf.Clamp(raw.FuelLevelA * 100f / 255f, 0f, 100f),
                BatteryVoltage = ((raw.BatteryVoltageA * 256f) + raw.BatteryVoltageB) / 1000f,
                OilPressure = raw.OilPressureA * 4f,
                FuelTrimShort = DecodeSignedPercent(raw.FuelTrimShortA),
                FuelTrimLong = DecodeSignedPercent(raw.FuelTrimLongA),
                FuelPressure = raw.FuelPressureA * 3f,
                MassAirFlow = ((raw.MassAirFlowA * 256f) + raw.MassAirFlowB) / 100f,
                ManifoldPressure = raw.ManifoldPressureA,
                AirFuelRatio = ((raw.AirFuelRatioA * 256f) + raw.AirFuelRatioB) / 100f,
                O2SensorValue = raw.O2SensorValueA / 255f,
                EngineRunTime = ((raw.EngineRunTimeA * 256f) + raw.EngineRunTimeB) / 4f,
                DTCErrorCodes = raw.DtcCodes ?? Array.Empty<string>(),
                CheckEngineStatus = raw.MalfunctionIndicatorLamp,
                EvapSystemStatus = raw.MalfunctionIndicatorLamp ? "Fault detected" : "Normal",
                GearPosition = CalculateGearPosition(Mathf.Max(0f, raw.VehicleSpeedA))
            };

            decoded.CatalystTemperature = decoded.EngineCoolantTemp + 25f;
            decoded.TimingAdvance = Mathf.Lerp(8f, 24f, decoded.EngineLoad / 100f);
            decoded.EngineTorque = Mathf.Clamp(decoded.EngineLoad * 5.2f, 0f, 650f);
            decoded.DistanceSinceCodesCleared = Mathf.Max(0f, raw.SequenceId * 0.15f);
            decoded.TransmissionTemp = Mathf.Lerp(72f, 110f, decoded.VehicleSpeed / 160f);
            decoded.FuelConsumption = Mathf.Lerp(4.5f, 14.5f, decoded.EngineLoad / 100f);
            decoded.OverallHealth = 100f;
            decoded.EngineHealth = 100f;
            decoded.CoolingHealth = 100f;
            decoded.BatteryHealth = 100f;
            decoded.ActiveAnomalies = new List<string>();
            decoded.Predictions = new List<string>();

            return decoded;
        }

        public static OBDRawFrame Encode(OBDParameters decoded, string scenarioName, string[] dtcCodes, bool milOn, int sequenceId, string rawNotes = null)
        {
            var raw = new OBDRawFrame
            {
                SequenceId = sequenceId,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                ScenarioName = scenarioName,
                MalfunctionIndicatorLamp = milOn,
                DtcCodes = dtcCodes ?? Array.Empty<string>(),
                RawNotes = rawNotes ?? string.Empty
            };

            int rpmRaw = Mathf.RoundToInt(Mathf.Clamp(decoded.EngineRPM, 0f, 16383.75f) * 4f);
            raw.EngineRpmA = (rpmRaw >> 8) & 0xFF;
            raw.EngineRpmB = rpmRaw & 0xFF;
            raw.VehicleSpeedA = Mathf.Clamp(Mathf.RoundToInt(decoded.VehicleSpeed), 0, 255);
            raw.EngineCoolantTempA = Mathf.Clamp(Mathf.RoundToInt(decoded.EngineCoolantTemp + 40f), 0, 255);
            raw.IntakeAirTempA = Mathf.Clamp(Mathf.RoundToInt(decoded.IntakeAirTemp + 40f), 0, 255);
            raw.ThrottlePositionA = Mathf.Clamp(Mathf.RoundToInt(decoded.ThrottlePosition * 255f / 100f), 0, 255);
            raw.AcceleratorPedalPositionA = Mathf.Clamp(Mathf.RoundToInt(decoded.AcceleratorPedalPosition * 255f / 100f), 0, 255);
            raw.EngineLoadA = Mathf.Clamp(Mathf.RoundToInt(decoded.EngineLoad * 255f / 100f), 0, 255);
            raw.FuelLevelA = Mathf.Clamp(Mathf.RoundToInt(decoded.FuelLevel * 255f / 100f), 0, 255);

            int batteryRaw = Mathf.Clamp(Mathf.RoundToInt(decoded.BatteryVoltage * 1000f), 0, 65535);
            raw.BatteryVoltageA = (batteryRaw >> 8) & 0xFF;
            raw.BatteryVoltageB = batteryRaw & 0xFF;

            raw.OilPressureA = Mathf.Clamp(Mathf.RoundToInt(decoded.OilPressure / 4f), 0, 255);
            raw.FuelTrimShortA = EncodeSignedPercent(decoded.FuelTrimShort);
            raw.FuelTrimLongA = EncodeSignedPercent(decoded.FuelTrimLong);
            raw.FuelPressureA = Mathf.Clamp(Mathf.RoundToInt(decoded.FuelPressure / 3f), 0, 255);

            int mafRaw = Mathf.Clamp(Mathf.RoundToInt(decoded.MassAirFlow * 100f), 0, 65535);
            raw.MassAirFlowA = (mafRaw >> 8) & 0xFF;
            raw.MassAirFlowB = mafRaw & 0xFF;

            raw.ManifoldPressureA = Mathf.Clamp(Mathf.RoundToInt(decoded.ManifoldPressure), 0, 255);

            int afrRaw = Mathf.Clamp(Mathf.RoundToInt(decoded.AirFuelRatio * 100f), 0, 65535);
            raw.AirFuelRatioA = (afrRaw >> 8) & 0xFF;
            raw.AirFuelRatioB = afrRaw & 0xFF;

            raw.O2SensorValueA = Mathf.Clamp(Mathf.RoundToInt(decoded.O2SensorValue * 255f), 0, 255);

            int runtimeRaw = Mathf.Clamp(Mathf.RoundToInt(decoded.EngineRunTime * 4f), 0, 65535);
            raw.EngineRunTimeA = (runtimeRaw >> 8) & 0xFF;
            raw.EngineRunTimeB = runtimeRaw & 0xFF;

            return raw;
        }

        public static string FormatRawFrame(OBDRawFrame raw)
        {
            if (raw == null)
            {
                return "No raw OBD frame available.";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Sequence: {raw.SequenceId}");
            builder.AppendLine($"Timestamp UTC: {raw.TimestampUtc}");
            builder.AppendLine($"Scenario: {raw.ScenarioName}");
            builder.AppendLine($"MIL: {(raw.MalfunctionIndicatorLamp ? "ON" : "OFF")}");
            builder.AppendLine($"RPM bytes: {raw.EngineRpmA}/{raw.EngineRpmB}");
            builder.AppendLine($"Speed byte: {raw.VehicleSpeedA}");
            builder.AppendLine($"Coolant byte: {raw.EngineCoolantTempA}");
            builder.AppendLine($"Throttle byte: {raw.ThrottlePositionA}");
            builder.AppendLine($"Load byte: {raw.EngineLoadA}");
            builder.AppendLine($"Fuel byte: {raw.FuelLevelA}");
            builder.AppendLine($"Battery bytes: {raw.BatteryVoltageA}/{raw.BatteryVoltageB}");
            builder.AppendLine($"Oil pressure byte: {raw.OilPressureA}");
            builder.AppendLine($"Fuel pressure byte: {raw.FuelPressureA}");
            builder.AppendLine($"MAF bytes: {raw.MassAirFlowA}/{raw.MassAirFlowB}");
            builder.AppendLine($"AFR bytes: {raw.AirFuelRatioA}/{raw.AirFuelRatioB}");
            builder.AppendLine($"DTCs: {(raw.DtcCodes != null && raw.DtcCodes.Length > 0 ? string.Join(", ", raw.DtcCodes) : "none")}");

            if (!string.IsNullOrWhiteSpace(raw.RawNotes))
            {
                builder.AppendLine($"Note: {raw.RawNotes}");
            }

            return builder.ToString().TrimEnd();
        }

        public static string FormatDecodedFrame(OBDParameters data)
        {
            if (data == null)
            {
                return "No decoded OBD data available.";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"RPM: {data.EngineRPM:F0}");
            builder.AppendLine($"Speed: {data.VehicleSpeed:F1} km/h");
            builder.AppendLine($"Coolant: {data.EngineCoolantTemp:F1} C");
            builder.AppendLine($"Intake Air: {data.IntakeAirTemp:F1} C");
            builder.AppendLine($"Throttle: {data.ThrottlePosition:F1}%");
            builder.AppendLine($"Load: {data.EngineLoad:F1}%");
            builder.AppendLine($"Fuel: {data.FuelLevel:F1}%");
            builder.AppendLine($"Battery: {data.BatteryVoltage:F2} V");
            builder.AppendLine($"Oil Pressure: {data.OilPressure:F1} kPa");
            builder.AppendLine($"Fuel Pressure: {data.FuelPressure:F1} kPa");
            builder.AppendLine($"MAF: {data.MassAirFlow:F2} g/s");
            builder.AppendLine($"A/F Ratio: {data.AirFuelRatio:F2}");
            builder.AppendLine($"MIL: {(data.CheckEngineStatus ? "ON" : "OFF")}");
            builder.AppendLine($"DTCs: {(data.DTCErrorCodes != null && data.DTCErrorCodes.Length > 0 ? string.Join(", ", data.DTCErrorCodes) : "none")}");
            return builder.ToString().TrimEnd();
        }

        public static string BuildDiagnosticPrompt(OBDParameters data, OBDRawFrame raw, string scenarioName)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("You are an automotive diagnostics assistant connected to a Unity dashboard.");
            prompt.AppendLine("Review the decoded OBD data and the raw frame below, then answer in a concise, practical way.");
            prompt.AppendLine("Return:");
            prompt.AppendLine("1. Likely issue.");
            prompt.AppendLine("2. Severity.");
            prompt.AppendLine("3. Evidence from telemetry.");
            prompt.AppendLine("4. Recommended next step.");
            prompt.AppendLine();
            prompt.AppendLine($"Scenario: {scenarioName}");
            prompt.AppendLine("Decoded data:");
            prompt.AppendLine(FormatDecodedFrame(data));
            prompt.AppendLine();
            prompt.AppendLine("Raw frame:");
            prompt.AppendLine(FormatRawFrame(raw));
            return prompt.ToString();
        }

        private static float DecodeSignedPercent(int rawValue)
        {
            return rawValue > 127 ? rawValue - 256f : rawValue;
        }

        private static int EncodeSignedPercent(float value)
        {
            int clamped = Mathf.Clamp(Mathf.RoundToInt(value), -128, 127);
            return clamped < 0 ? 256 + clamped : clamped;
        }

        private static int CalculateGearPosition(float speedKmh)
        {
            if (speedKmh < 5f) return 1;
            if (speedKmh < 25f) return 2;
            if (speedKmh < 45f) return 3;
            if (speedKmh < 70f) return 4;
            if (speedKmh < 100f) return 5;
            return 6;
        }
    }
}

