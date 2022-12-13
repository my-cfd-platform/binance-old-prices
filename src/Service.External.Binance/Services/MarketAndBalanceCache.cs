using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Binance;
using Grpc.Core.Logging;
using Microsoft.Extensions.Logging;
using MyJetWallet.Domain.ExternalMarketApi.Models;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.Service.Tools;

namespace Service.External.Binance.Services
{
    public class MarketAndBalanceCache: IStartable, IDisposable
    {
        private readonly BinanceApi _client;
        private readonly IBinanceApiUser _user;
        private readonly ILogger<MarketAndBalanceCache> _logger;

        private List<ExchangeMarketInfo> _markets = new List<ExchangeMarketInfo>();
        private List<string> _assets = new List<string>();
        private Dictionary<string, ExchangeBalance> _balances = new Dictionary<string, ExchangeBalance>();

        private readonly MyTaskTimer _timer;

        public MarketAndBalanceCache(ILogger<MarketAndBalanceCache> logger)
        {
            _logger = logger;

            _client = new BinanceApi();
            _user = new BinanceApiUser(Program.Settings.BinanceApiKey, Program.Settings.BinanceApiSecret);

            _timer = new MyTaskTimer(nameof(MarketAndBalanceCache), TimeSpan.FromSeconds(1), logger, DoTimer);
        }

        private async Task DoTimer()
        {
            _timer.ChangeInterval(TimeSpan.FromSeconds(Program.Settings.RefreshBalanceIntervalSec));

            using var activity = MyTelemetry.StartActivity("Refresh balance data");
            try
            {
                await RefreshData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on refresh balance");
                ex.FailActivity();
            }
        }

        public async Task RefreshData()
        {
            _logger.LogInformation("Balance and market update");

            await UpdateMarkets();

            var balances = await GetMarginAccountBalances();

            var dict = new Dictionary<string, ExchangeBalance>();

            foreach (var balance in balances)
            {
                using var activityBalance = MyTelemetry.StartActivity($"Update balance {balance.Asset}")?.AddTag("asset", balance.Asset);
                
                try
                {
                    var free = await _client.GetMaxBorrowAsync(_user, balance.Asset);

                    var item = new ExchangeBalance()
                    {
                        Symbol = balance.Asset,
                        Balance = balance.Free,
                        Free = (decimal) free
                    };

                    dict[item.Symbol] = item;
                }
                catch (Exception ex)
                {
                    ex.FailActivity();
                    _logger.LogError(ex, "Canoot update borrow balance");
                }
            }

            _balances = dict;
        }

        private async Task<List<MarginAccountBalance>> GetMarginAccountBalances()
        {
            try
            {
                var balances = await _client.GetMarginBalancesAsync(_user);
                balances = balances.Where(e => _assets.Contains(e.Asset)).ToList();
                return balances;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot get account balance");
                throw;
            }
        }

        private async Task UpdateMarkets()
        {
            try
            {
                if (_markets == null || !_markets.Any())
                {
                    using var activityMarket = MyTelemetry.StartActivity("Fetch market data");

                    var pairsSettings = Program.Settings.Instruments.Split(';').ToList();

                    await Symbol.UpdateCacheAsync(_client);

                    var marginPairs = await _client.GetMarginPairsAsync(_user);
                    marginPairs = marginPairs.Where(e => pairsSettings.Contains(e.Symbol)).ToList();
                    var symbols = Symbol.Cache.GetAll().Where(e => pairsSettings.Contains(e.ToString())).ToList();

                    var list = new List<ExchangeMarketInfo>();
                    var assets = new Dictionary<string, string>();

                    foreach (var marginPair in marginPairs)
                    {
                        var symbol = Symbol.Cache.Get(marginPair.Symbol);
                        if (symbol == null)
                            continue;

                        var prm = symbol.Quantity.Increment.ToString(CultureInfo.InvariantCulture).Split('.');
                        var volumeAccuracy = prm.Length == 2 ? prm[1].Length : 0;

                        prm = symbol.Price.Increment.ToString(CultureInfo.InvariantCulture).Split('.');
                        var priceAccuracy = prm.Length == 2 ? prm[1].Length : 0;

                        var item = new ExchangeMarketInfo()
                        {
                            Market = marginPair.Symbol,
                            BaseAsset = marginPair.Base,
                            QuoteAsset = marginPair.Quote,
                            MinVolume = (double) symbol.Quantity.Minimum,
                            PriceAccuracy = priceAccuracy,
                            VolumeAccuracy = volumeAccuracy
                        };

                        list.Add(item);
                        assets[item.BaseAsset] = item.BaseAsset;
                        assets[item.QuoteAsset] = item.QuoteAsset;
                    }

                    _markets = list;
                    _assets = assets.Keys.ToList();
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Cannot update markets");
            }
        }


        public void Start()
        {
            _timer.Start();
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        public List<ExchangeMarketInfo> GetMarkets()
        {
            return _markets;
        }

        public List<ExchangeBalance> GetBalances()
        {
            return _balances.Values.ToList();
        }
    }
}