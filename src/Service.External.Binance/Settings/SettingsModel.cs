using System.Collections.Generic;
using System.Net;
using MyYamlParser;
using SimpleTrading.FeedTcpContext.TcpServer;

namespace Service.External.Binance.Settings
{
    //[YamlAttributesOnly]
    public class SettingsModel
    {
        [YamlProperty("ExternalBinance.SeqServiceUrl")]
        public string SeqServiceUrl { get; set; }

        [YamlProperty("ExternalBinance.ZipkinUrl")]
        public string ZipkinUrl { get; set; }

        [YamlProperty("ExternalBinance.Instruments")]
        public string Instruments { get; set; }

        [YamlProperty("ExternalBinance.RefreshBalanceIntervalSec")]
        public int RefreshBalanceIntervalSec { get; set; }

        [YamlProperty("ExternalBinance.ApiKey")]
        public string BinanceApiKey { get; set; }

        [YamlProperty("ExternalBinance.ApiSecret")]
        public string BinanceApiSecret { get; set; }

        [YamlProperty("ExternalBinance.StInstrumentsMapping")]
        public string StInstrumentsMapping { get; set; }
    }
}
