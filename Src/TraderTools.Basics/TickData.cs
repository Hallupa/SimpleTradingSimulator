using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TraderTools.Basics
{
    /// <summary>
    /// 24 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TickData
    {
        public DateTime Datetime
        {
            get => new DateTime(DateTimeTicks);
            set => DateTimeTicks = value.Ticks;
        }

        public long DateTimeTicks { get; set; }
        public float Open { get; set; } // 8 bytes
        public float Close { get; set; } // 8 bytes
        public float High { get; set; } // 8 bytes
        public float Low { get; set; } // 8 bytes

        public static byte[] GetBytesFast(TickData tickData)
        {
            int size = Marshal.SizeOf(typeof(TickData));// * array.Length;
            var bytes = new byte[size];
            GCHandle gcHandle = GCHandle.Alloc(tickData, GCHandleType.Pinned);
            var ptr = gcHandle.AddrOfPinnedObject();
            Marshal.Copy(ptr, bytes, 0, size);
            gcHandle.Free();

            return bytes;
        }

        public static TickData GetTickData(Stream stream)
        {
            int size = Marshal.SizeOf(typeof(TickData));
            var bytes = new byte[size];
            stream.Read(bytes, 0, size);
            GCHandle gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            
            // Get a pointer to the byteSerializedData array.
            IntPtr pbyteSerializedData = gch.AddrOfPinnedObject();
            var ret = (TickData)Marshal.PtrToStructure(pbyteSerializedData, typeof(TickData));
            // Free the GCHandle.
            gch.Free();

            return ret;
        }         
    }
}