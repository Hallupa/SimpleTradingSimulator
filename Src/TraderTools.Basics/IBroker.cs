using System;
using System.Collections.Generic;

namespace TraderTools.Basics
{
    public enum ConnectStatus
    {
        None,
        Connecting,
        Connected,
        Disconnected

    }

    public enum BrokerKind
    {
        SpreadBet,
        Trade
    }

    public interface IBrokerAccount
    {
        List<TradeDetails> Trades { get; set; }

        decimal GetBalance(DateTime? dateTimeUtc = null);

        List<DepositWithdrawal> DepositsWithdrawals { get; set; }
    }

    public interface IBroker
    {
        string Name { get; }
        bool UpdateAccount(IBrokerAccount account);
        bool UpdateCandles(List<ICandle> candles, string market, Timeframe timeframe, DateTime start);
        void Connect();
        ConnectStatus Status { get; set; }
        BrokerKind Kind { get; }
        List<TickData> GetTickData(IBroker broker, string market, DateTime utcStart, DateTime utcEnd);
    }
}