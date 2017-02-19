using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CacheManager.Core;
#if !NETCOREAPP
using Enyim.Caching;
using Enyim.Caching.Configuration;
#endif
using Microsoft.Extensions.Logging;

namespace CacheManager.Config.Tests
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var c = new BaseCacheManager<string>(
                new Core.ConfigurationBuilder()
                    .WithRedisConfiguration("redis", "localhost,allowAdmin=true")
                    .WithJsonSerializer()
                    .WithRedisCacheHandle("redis")
                    .Build());

            c.Clear();
            var counter = 0;
            var swatch = Stopwatch.StartNew();
            for (var i = 0; i < 500000; i++)
            {
                c.AddAsync("key" + i, i.ToString())
                    .ContinueWith((t) =>
                    {
                        if (t.Result)
                        {
                            Interlocked.Increment(ref counter);
                        }
                        else
                        {
                            Console.WriteLine("Not added key" + i);
                        }
                    });
            }

            var added = c.AddAsync("key3", "value3").Result;
            var elapsed = swatch.ElapsedMilliseconds;

            int iterations = 10;
            Console.WriteLine($"Elapsed {elapsed} added {counter}.");
            return;
            try
            {
                var builder = new Core.ConfigurationBuilder("myCache");
                builder.WithMicrosoftLogging(f =>
                {
                    f.AddConsole(LogLevel.Warning);
                    f.AddDebug(LogLevel.Debug);
                });

                builder.WithRetryTimeout(1000);
                builder.WithMaxRetries(10);
                builder.WithDictionaryHandle()
                    .WithExpiration(ExpirationMode.Absolute, TimeSpan.FromSeconds(20))
                    .DisableStatistics();

                builder.WithRedisCacheHandle("redis", true)
                    .WithExpiration(ExpirationMode.Sliding, TimeSpan.FromSeconds(60))
                    .DisableStatistics();

                builder.WithRedisBackplane("redis");

                builder.WithRedisConfiguration("redis", config =>
                {
                    config
                        .WithAllowAdmin()
                        .WithDatabase(0)
                        .WithConnectionTimeout(5000)
                        .WithEndpoint("127.0.0.1", 6379);
                });

                //builder.WithGzJsonSerializer();
                builder.WithBondCompactBinarySerializer();

#if !NETCOREAPP
                //var memcachedCfg = new MemcachedClientConfiguration();
                //memcachedCfg.AddServer("localhost", 11211);
                //builder.WithMemcachedCacheHandle(memcachedCfg);
#endif

                var cacheA = new BaseCacheManager<string>(builder.Build());
                cacheA.Clear();

                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        Tests.PutAndMultiGetTest(cacheA);
                    }
                    catch (AggregateException ex)
                    {
                        ex.Handle((e) =>
                        {
                            Console.WriteLine(e);
                            return true;
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: " + e.Message + "\n" + e.StackTrace);
                        Thread.Sleep(1000);
                    }

                    Console.WriteLine("---------------------------------------------------------");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("We are done...");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.ReadKey();
        }
    }
}