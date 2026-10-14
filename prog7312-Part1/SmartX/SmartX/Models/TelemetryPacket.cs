using System;

namespace SmartX.Models
{
    // Generic packet that carries a single sensor reading along with its metadata
    public struct TelemetryPacket<T> where T : struct
    {
        public string SensorId { get; set; }
        public T Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }

        // Timestamp is set automatically to the current UTC time on creation
        public TelemetryPacket(string sensorId, T value, string unit)
        {
            SensorId = sensorId;
            Value = value;
            Unit = unit;
            Timestamp = DateTime.UtcNow;
        }
    }

    // Wraps a wattage reading and adds arithmetic/comparison support for power values
    public struct PowerMeterValue
    {
        public double Wattage { get; set; }

        public PowerMeterValue(double wattage)
        {
            Wattage = wattage;
        }

        // Adds two power readings together
        public static PowerMeterValue operator +(PowerMeterValue a, PowerMeterValue b)
        {
            return new PowerMeterValue(a.Wattage + b.Wattage);
        }

        // Subtracts one reading from another, never going below zero
        public static PowerMeterValue operator -(PowerMeterValue a, PowerMeterValue b)
        {
            return new PowerMeterValue(Math.Max(0, a.Wattage - b.Wattage));
        }

        // Allows comparing a PowerMeterValue directly against a raw double threshold
        public static bool operator >(PowerMeterValue a, double threshold) => a.Wattage > threshold;
        public static bool operator <(PowerMeterValue a, double threshold) => a.Wattage < threshold;

        // Formats the value with two decimals and appends the watt unit
        public override string ToString() => $"{Wattage:F2} W";
    }
}