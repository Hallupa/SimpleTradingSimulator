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