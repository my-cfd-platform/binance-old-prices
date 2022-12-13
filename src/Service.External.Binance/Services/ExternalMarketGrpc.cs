using System;
using System.Linq;
using System.Threading.Tasks;
using Binance;
using Microsoft.Extensions.Logging;
using MyJetWallet.Domain.ExternalMarketApi;
using MyJetWallet.Domain.ExternalMarketApi.Dto;
using MyJetWallet.Domain.ExternalMarketApi.Models;
using MyJetWallet.Sdk.Service;
using Newtonsoft.Json;

namespace Service.External.Binance.Services
{
    public class ExternalMarketGrpc: IExternalMarket
    {
        private readonly MarketAndBalanceCache _cache;
        private readonly BinanceApi _client;
        private readonly BinanceApiUser _user;
        private readonly ILogger<ExternalMarketGrpc> _logger;

        public ExternalMarketGrpc(MarketAndBalanceCache cache, BinanceApi client, BinanceApiUser user, ILogger<ExternalMarketGrpc> logger)
        {
            _cache = cache;
            _client = client;
            _user = user;
            _logger = logger;
        }

        public Task<GetNameResult> GetNameAsync()
        {
            return Task.FromResult(new GetNameResult() {Name = BinanceConst.Name});
        }

        public Task<GetBalancesResponse> GetBalancesAsync()
        {
            var list = _cache.GetBalances();
            return Task.FromResult(new GetBalancesResponse() {Balances = list});
        }

        public Task<GetMarketInfoResponse> GetMarketInfoAsync(MarketRequest request)
        {
            var list = _cache.GetMarkets();
            return Task.FromResult(new GetMarketInfoResponse() { Info = list.FirstOrDefault(e => e.Market == request.Market) });
        }

        public Task<GetMarketInfoListResponse> GetMarketInfoListAsync()
        {
            var list = _cache.GetMarkets();
            return Task.FromResult(new GetMarketInfoListResponse() { Infos = list });
        }

        public async Task<ExchangeTrade> MarketTrade(MarketTradeRequest request)
        {
            using var activity = MyTelemetry.StartActivity("Market trade");

            try
            {
                request.AddToActivityAsJsonTag("request");

                _logger.LogInformation("Request to market trade {requestJson}", JsonConvert.SerializeObject(request));

                var reqId = !string.IsNullOrEmpty(request.ReferenceId)
                    ? request.ReferenceId
                    : Guid.NewGuid().ToString("N");

                reqId.AddToActivityAsTag("client-trade-id");

                var order = await GetOrderByClientId(request.Market, reqId);

                if (order != null)
                {
                    activity?.AddTag("message", "order already exist");

                    _logger.LogInformation("Order already exist {orderJson}", JsonConvert.SerializeObject(order));

                    ExchangeTrade trade = ParseOrder(order);

                    _logger.LogInformation("Result trade {tradeJson}", JsonConvert.SerializeObject(trade));

                    return trade;
                }

                var clientOrder = new MarketOrder(_user)
                {
                    Symbol = request.Market,
                    Side = request.Side == MyJetWallet.Domain.Orders.OrderSide.Buy ? OrderSide.Buy : OrderSide.Sell,
                    Quantity = (decimal) Math.Abs(request.Volume),
                    Id = request.ReferenceId
                };

                clientOrder.AddToActivityAsJsonTag("binance-request");

                _logger.LogInformation("Try to execute market trade {requestJson}", JsonConvert.SerializeObject(clientOrder));

                var result = await _client.PlaceMarginMarketAsync(clientOrder, true);

                result.AddToActivityAsJsonTag("binance-result");
                _logger.LogInformation("Executed market trade {resultJson}", JsonConvert.SerializeObject(result));


                var waitTime = 5000;
                var iteration = 0;

                while (waitTime >= 0)
                {
                    iteration++;

                    order = await GetOrderByClientId(request.Market, reqId);

                    if (order != null)
                    {
                        if (order.status != "FILLED")
                        {
                            _logger.LogError("Market order is not FILLED. Order: {orderJson}", JsonConvert.SerializeObject(order));
                            throw new Exception("Market order is not FILLED");
                        }

                        activity?.AddTag("message", "order is executed");
                        activity?.AddTag("iterations", iteration);


                        _logger.LogInformation("Order from Binance {orderJson}", JsonConvert.SerializeObject(order));

                        ExchangeTrade trade = ParseOrder(order);

                        _logger.LogInformation("Result trade {tradeJson}", JsonConvert.SerializeObject(trade));

                        return trade;
                    }

                    await Task.Delay(500);
                    waitTime -= 500;
                }

                _logger.LogError("Cannot found executed order");
                throw new Exception("Cannot found executed order");
            }
            catch (Exception ex)
            {
                ex.FailActivity();
                throw;
            }
        }

        private ExchangeTrade ParseOrder(MarginTrade order)
        {
            if (order == null)
                throw new Exception("Cannot read null order");

            var dateTimeOffSet = DateTimeOffset.FromUnixTimeMilliseconds(order.updateTime);
            var dateTime = dateTimeOffSet.DateTime;

            var instrument = _cache.GetMarkets().FirstOrDefault(e => e.Market == order.symbol);

            var priceAccuracy = instrument?.PriceAccuracy;

            var price = order.cummulativeQuoteQty / order.executedQty;

            if (priceAccuracy.HasValue)
                price = Math.Round(price, priceAccuracy.Value);

            var trade = new ExchangeTrade()
            {
                ReferenceId = order.clientOrderId,
                Id = order.orderId.ToString(),
                Market = order.symbol,
                Side = order.side == "BUY" ? MyJetWallet.Domain.Orders.OrderSide.Buy : MyJetWallet.Domain.Orders.OrderSide.Sell,
                Volume = order.side == "BUY" ? (double)order.executedQty : (double)(-order.executedQty),
                Source = BinanceConst.Name,
                Timestamp = dateTime,
                Price = (double)price
            };

            return trade;
        }

        private async Task<MarginTrade> GetOrderByClientId(string symbol, string orderClientId)
        {
            using var activity = MyTelemetry.StartActivity("Get order by client id")
                ?.AddTag("client-order-id", orderClientId);

            try
            {
                var order = await _client.GetMarginOrderByClientIdAsync(_user, symbol, orderClientId);
                return order;
            }
            catch (BinanceHttpException ex)
            {
                ex.FailActivity();
                if (ex.ErrorCode == -2013) // Order does not exist
                    return null;

                throw;
            }
            catch(Exception ex)
            {
                ex.FailActivity();
                throw;
            }
        }
    }

    internal static class BinanceConst
    {
        public const string Name = "Binance";
    }
}