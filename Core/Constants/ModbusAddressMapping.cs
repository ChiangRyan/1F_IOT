namespace SANJET.Core.Constants
{
    public sealed record ModbusAddressMap(
        ushort ControlAddress,
        ushort StatusAddress,
        ushort RunCountAddress,
        byte RunCountRegisterQuantity = 2,
        byte FunctionCode = 3);

    public static class ModbusAddressMapping
    {
        public const string DisplayAreaName = "展機區";
        public const string TestAreaName = "測試區";
        public const int DefaultDeviceIndex = 1;
        public const int MaxTestAreaDeviceIndex = 4;

        public static ModbusAddressMap GetMap(string? area, int deviceIndex)
        {
            var normalizedIndex = NormalizeDeviceIndex(deviceIndex);

            if (IsTestArea(area))
            {
                var offset = (ushort)(normalizedIndex - 1);
                return new ModbusAddressMap(
                    ControlAddress: (ushort)(ModbusConstants.TestAreaControlBaseAddress + offset),
                    StatusAddress: (ushort)(ModbusConstants.TestAreaStatusBaseAddress + offset),
                    RunCountAddress: (ushort)(ModbusConstants.TestAreaRunCountBaseAddress + (offset * ModbusConstants.RunCountRegisterQuantity)));
            }

            return new ModbusAddressMap(
                ControlAddress: ModbusConstants.DisplayAreaControlRelativeAddress,
                StatusAddress: ModbusConstants.StatusRelativeAddress,
                RunCountAddress: ModbusConstants.RunCountRelativeAddress);
        }

        public static bool IsTestArea(string? area)
        {
            return string.Equals(area, TestAreaName, System.StringComparison.OrdinalIgnoreCase);
        }

        public static int NormalizeDeviceIndex(int deviceIndex)
        {
            return deviceIndex < DefaultDeviceIndex ? DefaultDeviceIndex : deviceIndex;
        }
    }
}
