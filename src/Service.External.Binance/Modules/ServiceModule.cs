using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Binance;
using Service.External.Binance.Services;
using SimpleTrading.FeedTcpContext.TcpServer;

namespace Service.External.Binance.Modules
{
    public class ServiceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var api = new BinanceApi();
            var user = new BinanceApiUser(Program.Settings.BinanceApiKey, Program.Settings.BinanceApiSecret);

            builder.RegisterInstance(api).AsSelf().SingleInstance();
            builder.RegisterInstance(user).AsSelf().SingleInstance();

            builder
                .RegisterType<MarketAndBalanceCache>()
                .AsSelf()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();

            builder
                .RegisterType<OrderBookCacheManager>()
                .AsSelf()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}