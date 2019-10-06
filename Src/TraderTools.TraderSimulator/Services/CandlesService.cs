using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Core.Services;

namespace TraderTools.TradingSimulator.Services
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
            var path = GetCandlesPath(market, timeframe);
            var data = Decompress(File.ReadAllBytes(path));

            return BrokersCandlesService.BytesToCandles(data).ToList();
        }

        public void ConvertCandles(IBrokersCandlesService candlesService, IBroker broker, List<string> markets)
        {
            var candlesDirectory = Path.Combine(Path.GetDirectoryName(typeof(MainWindowViewModel).Assembly.Location), "Candles");
            if (!Directory.Exists(candlesDirectory)) Directory.CreateDirectory(candlesDirectory);

            foreach (var market in markets)
            {
                if (File.Exists(GetCandlesPath(market, Timeframe.H2))
                    && File.Exists(GetCandlesPath(market, Timeframe.H4))
                    && File.Exists(GetCandlesPath(market, Timeframe.D1))
                    && File.Exists(GetCandlesPath(market, Timeframe.M5))) continue;

                var allM5Candles = candlesService.GetCandles(broker, market, Timeframe.M5, false, cacheData: false);
                var allH2Candles = candlesService.GetCandles(broker, market, Timeframe.H2, false, cacheData: false);
                var allH4Candles = candlesService.GetCandles(broker, market, Timeframe.H4, false, cacheData: false);
                var allD1Candles = candlesService.GetCandles(broker, market, Timeframe.D1, false, cacheData: false);

                if (allD1Candles.Count == 0 || allH2Candles.Count == 0 || allH4Candles.Count == 0 || allM5Candles.Count == 0) continue;

                var bytes = Compress(BrokersCandlesService.CandlesToBytes(allM5Candles));
                var path = Path.Combine(candlesDirectory, $"{market.Replace("/", string.Empty)}_M5.dat");
                File.WriteAllBytes(path, bytes);

                bytes = Compress(BrokersCandlesService.CandlesToBytes(allH2Candles));
                path = Path.Combine(candlesDirectory, $"{market.Replace("/", string.Empty)}_H2.dat");
                File.WriteAllBytes(path, bytes);

                bytes = Compress(BrokersCandlesService.CandlesToBytes(allH4Candles));
                path = Path.Combine(candlesDirectory, $"{market.Replace("/", string.Empty)}_H4.dat");
                File.WriteAllBytes(path, bytes);

                bytes = Compress(BrokersCandlesService.CandlesToBytes(allD1Candles));
                path = Path.Combine(candlesDirectory, $"{market.Replace("/", string.Empty)}_D1.dat");
                File.WriteAllBytes(path, bytes);

                GC.Collect();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        public static byte[] Compress(byte[] data)
        {
            using (var dataToCompress = new MemoryStream(data))
            using (var compressed = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(compressed, CompressionMode.Compress))
                {
                    compressionStream.Write(data, 0, data.Length);
                }

                return compressed.ToArray();
            }
        }

        public string GetCandlesPath(string market, Timeframe timeframe)
        {
            return Path.Combine(CandlesDirectory, $"{market.Replace("/", "")}_{timeframe}.dat");
        }
    }
}