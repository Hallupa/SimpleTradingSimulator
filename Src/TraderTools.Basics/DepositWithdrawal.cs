using System;

namespace TraderTools.Basics
{
    public class DepositWithdrawal
    {
        public DateTime Time { get; set; }

        public string Asset { get; set; }

        public decimal Amount { get; set; }
        public string Id { get; set; }
        public string Description { get; set; }
        public string Broker { get; set; }
        public decimal Commission { get; set; }
        public string CommissionAsset { get; set; }


        public override string ToString()
        {
            return $"{Time} {Broker} {Amount:0.000} {Asset} Commission: {Commission:0.000} {CommissionAsset} {Description}";
        }
    }
}