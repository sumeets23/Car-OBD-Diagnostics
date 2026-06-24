using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarDiagnostics
{
    [Serializable]
    public class OBDParameters
    {
        [Header("Engine & Vehicle Basics")]
        public float EngineRPM;
        public float VehicleSpeed;
        public float EngineCoolantTemp;
        public float IntakeAirTemp;
        public float AmbientAirTemp;
        public float ThrottlePosition;
        public float AcceleratorPedalPosition;
        public float EngineLoad;
        public float FuelLevel;
        public float BatteryVoltage;
        public float OilPressure;

        [Header("Fuel & Air System")]
        public float FuelTrimShort;
        public float FuelTrimLong;
        public float FuelPressure;
        public float MassAirFlow;
        public float ManifoldPressure;
        public float AirFuelRatio;
        public float O2SensorValue;

        [Header("Emission & Diagnostics")]
        public string[] DTCErrorCodes;
        public bool CheckEngineStatus;
        public float CatalystTemperature;
        public string EvapSystemStatus;

        [Header("Driving & Performance")]
        public float TimingAdvance;
        public float FuelConsumption;
        public float DistanceSinceCodesCleared;
        public float EngineRunTime;
        public float EngineTorque;

        [Header("Transmission")]
        public int GearPosition;
        public float TransmissionTemp;

        [Header("Diagnostic Summary")]
        public float OverallHealth = 100f;
        public float EngineHealth = 100f;
        public float CoolingHealth = 100f;
        public float BatteryHealth = 100f;
        public List<string> ActiveAnomalies = new List<string>();
        public List<string> Predictions = new List<string>();
    }
}

