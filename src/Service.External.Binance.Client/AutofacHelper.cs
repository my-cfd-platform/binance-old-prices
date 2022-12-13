using Autofac;
using MyJetWallet.Domain.ExternalMarketApi;

// ReSharper disable UnusedMember.Global

namespace Service.External.Binance.Client
{
    public static class AutofacHelper
    {
        public static void RegisterExternalBinanceClient(this ContainerBuilder builder, string grpcServiceUrl)
        {
            var factory = new ExternalBinanceClientFactory(grpcServiceUrl);

            builder.RegisterInstance(factory.GetOrderBookSource()).As<IOrderBookSource>().SingleInstance();
            builder.RegisterInstance(factory.GetExternalMarket()).As<IExternalMarket>().SingleInstance();
        }
    }
}
