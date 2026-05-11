using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SANJET.Core.Constants
{
    public static class ModbusConstants
    {
        public const ushort DisplayAreaControlRelativeAddress = 0; // 展機區：啟動/停止寫入位置
        public const ushort StatusRelativeAddress = 1; // 展機區：設備狀態讀取位置
        public const ushort RunCountRelativeAddress = 10; // 展機區：運轉次數讀寫起始位置

        public const ushort TestAreaControlBaseAddress = 0; // 測試區：設備1~4 啟動/停止位置 0~3
        public const ushort TestAreaStatusBaseAddress = 10; // 測試區：設備1~4 狀態位置 10~13
        public const ushort TestAreaRunCountBaseAddress = 20; // 測試區：設備1~4 次數起始位置
        public const byte RunCountRegisterQuantity = 2; // 運轉次數使用 2 個 16-bit register
    }
}
