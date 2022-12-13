using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using MyJetWallet.Domain.ExternalMarketApi;
using MyJetWallet.Domain.ExternalMarketApi.Dto;

namespace Service.External.Binance.Services
{
    public class OrderBookSourceGrpc : IOrderBookSource
    {
        private readonly OrderBookCacheManager _manager;
        private readonly MarketAndBalanceCache _marketService;

        public OrderBookSourceGrpc(OrderBookCacheManager manager, MarketAndBalanceCache marketService)
        {
            _manager = manager;
            _marketService = marketService;
        }

        public Task<GetNameResult> GetNameAsync()
        {
            return Task.FromResult(new GetNameResult() { Name = BinanceConst.Name });
        }

        public Task<GetSymbolResponse> GetSymbolsAsync()
        {
            var list = _marketService.GetMarkets().Select(e => e.Market).ToList();
            return Task.FromResult(new GetSymbolResponse() {Symbols = list});
        }

        public Task<HasSymbolResponse> HasSymbolAsync(MarketRequest request)
        {
            var result = _marketService.GetMarkets().Any(e => e.Market == request.Market);
            return Task.FromResult(new HasSymbolResponse() {Result = result});
        }

        public Task<GetOrderBookResponse> GetOrderBookAsync(MarketRequest request)
        {
            var resp = _manager.GetOrderBookAsync(request);

            return Task.FromResult(resp);
        }
    }
}