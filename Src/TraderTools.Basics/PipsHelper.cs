using System;
using TraderTools.Basics.Extensions;

namespace TraderTools.Basics
{
    public static class PipsHelper
    {
        /// <summary>
        /// For most currency pairs, the 'pip' location is the fourth decimal place. In this example, if the GBP/USD moved from 1.42279 to 1.42289 you would have gained or lost one pip
        /// http://help.fxcm.com/markets/Trading/Education/Trading-Basics/32856512/How-to-calculate-PIP-value.htm
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        public static decimal GetPriceInPips(decimal price, string market)
        {
            var pipInDecimals = GetOnePipInDecimals(market);

            return price / pipInDecimals;
        }

        public static decimal GetPriceFromPips(decimal pips, string market)
        {
            var pipInDecimals = GetOnePipInDecimals(market);

            return pips * pipInDecimals;
        }

        public static decimal ConvertLotSizeToGBPPerPip(
            decimal lotSize, string market, DateTime date,
            IBrokersCandlesService candleService, IBroker broker, bool updateCandles)
        {
            var marketPairForCalculation = string.Empty;

            if (market.Contains("/")) marketPairForCalculation = $"GBP/{market.Split('/')[1]}";
            if (marketPairForCalculation == "GBP/EUR") marketPairForCalculation = "EUR/GBP";
            if (marketPairForCalculation == "GBP/GBP") marketPairForCalculation = "GBP";
            var multiplier = 1.0M;

            if (string.IsNullOrEmpty(marketPairForCalculation))
            {
                multiplier = 0.1M;
                switch (market)
                {
                    case "AUS200":
                        marketPairForCalculation = "GBP/AUD";
                        break;
                    case "GER30":
                        marketPairForCalculation = "EUR/GBP";
                        break;
                    case "NAS100":
                        multiplier = 0.1M;
                        marketPairForCalculation = "GBP/USD";
                        break;
                    case "NGAS":
                        multiplier = 100M;
                        marketPairForCalculation = "GBP/USD";
                        break;
                    case "Bund":
                        multiplier = 0.1M;
                        marketPairForCalculation = "EUR/GBP";
                        break;
                    case "USOil":
                        marketPairForCalculation = "GBP/USD";
                        multiplier = 10M;
                        break;
                    case "SPX500":
                        marketPairForCalculation = "GBP/USD";
                        multiplier = 1M;
                        break;
                    case "Copper":
                        marketPairForCalculation = "GBP/USD";
                        multiplier = 100M;
                        break;
                    case "US30":
                        marketPairForCalculation = "GBP/USD";
                        break;
                    case "UK100":
                        marketPairForCalculation = "GBP";
                        break;
                    case "FRA40":
                        marketPairForCalculation = "EUR/GBP";
                        break;
                }
            }

            if (string.IsNullOrEmpty(marketPairForCalculation))
            {
                throw new ApplicationException($"Unable to convert lot size to GBP/pip for {market}");
            }

            var price = 1M;
            if (marketPairForCalculation != "GBP")
            {
                var gbpCandle = marketPairForCalculation != "GBP/MXN" && marketPairForCalculation != "GBP/SEK"
                    ? candleService.GetFirstCandleThatClosesBeforeDateTime(marketPairForCalculation, broker, Timeframe.D1, date, updateCandles)
                    : null;

                if (gbpCandle != null)
                {
                    price = (decimal)gbpCandle.Open;
                    if (!marketPairForCalculation.StartsWith("GBP")) price = 1 / price;
                }
                else
                {
                    // Try to get $ candle and convert to £
                    var usdCandle = candleService.GetFirstCandleThatClosesBeforeDateTime(
                        $"USD/{market.Split('/')[1]}", broker, Timeframe.D1, date, updateCandles);
                    var gbpUSDCandle = candleService.GetFirstCandleThatClosesBeforeDateTime(
                        "GBP/USD", broker, Timeframe.D1, date, updateCandles);
                    price = ((decimal)usdCandle.Open) / ((decimal)gbpUSDCandle.Open);
                }
            }

            var factor = GetOnePipInDecimals(market);
            var pricePerPip = lotSize / price * factor * multiplier;
            return pricePerPip;
        }

        public static decimal GetOnePipInDecimals(string market)
        {
            if (market == "AUS200" || market == "NAS100" || market == "UK100" || market == "GER30" ||
                    market == "CHN50" || market == "FRA40" || market == "US30")
            {
                return 1M;
            }

            if (market == "Bund")
            {
                return 0.01M;
            }

            if (market == "Copper")
            {
                return 0.001M;
            }

            if (market == "Copper" || market == "NGAS")
            {
                return 0.001M;
            }

            if (market == "USOil")
            {
                return 0.01M;
            }

            if (market == "SPX500")
            {
                return 0.1M;
            }

            if (market == "XAU/USD")
            {
                return 1 / 100M;
            }

            // JPY markets only ever end with JPY
            if (market.EndsWith("JPY"))
            {
                return 1 / 100M;
            }

            return 1 / 10000M;
        }
    }
}