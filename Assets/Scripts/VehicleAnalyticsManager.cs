using UnityEngine;

namespace CarDiagnostics
{
    public class VehicleAnalyticsManager : MonoBehaviour
    {
        [SerializeField] private OBDSimulator simulator;
        [SerializeField] private DashboardManager dashboard;
    }
}
