using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using TraderTools.Basics;

namespace TraderTools.TradingTrainer.Services
{
    public class CandlesService
    {
        public CandlesService()
        {
            CandlesDirectory = Path.Combine(Path.GetDirectoryName(typeof(CandlesService).Assembly.Location), "Candles");
        }

        public string CandlesDirectory { get; }

        public List<Candle> GetCandles(string market, Timeframe timeframe)
        {
            var path = Path.Combine(CandlesDirectory, $"FXCM_{market}_{timeframe}.dat");
            var data = Decompress(File.ReadAllBytes(path));

            int structSize = Marshal.SizeOf(typeof(SimpleCandle));
            var ret = new SimpleCandle[data.Length / structSize]; // Array of structs we want to push the bytes into
            var handle2 = GCHandle.Alloc(ret, GCHandleType.Pinned);// get handle to that array
            Marshal.Copy(data, 0, handle2.AddrOfPinnedObject(), data.Length);// do the copy
            handle2.Free();// cleanup the handle

            return ret.Select(c => new Candle(c)).ToList();
        }

        public static byte[] Decompress(byte[] data)
        {
            byte[] decompressedBytes;

            using (var compressedStream = new MemoryStream(data))
            using (var decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                using (var decompressedStream = new MemoryStream())
                {
                    decompressorStream.CopyTo(decompressedStream);

                    decompressedBytes = decompressedStream.ToArray();
                }
            }

            return decompressedBytes;
        }
    }
}