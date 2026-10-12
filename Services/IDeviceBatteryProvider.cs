using BatteryIndicator.Models;

namespace BatteryIndicator.Services;

public interface IDeviceBatteryProvider
{
    string ProviderName { get; }
    Task<List<BatteryDeviceInfo>> PollDevicesAsync();
}
