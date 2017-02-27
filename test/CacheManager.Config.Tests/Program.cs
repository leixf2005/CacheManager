using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.Redis;
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
            int iterations = 10;
            var cach = new BaseCacheManager<int>(
                new Core.ConfigurationBuilder()
                    .WithMaxRetries(5)
                    .WithRetryTimeout(5000)
                    .WithRedisConfiguration("redis", "localhost,allowAdmin=true")
                    .WithJsonSerializer()
                    .WithRedisCacheHandle("redis")
                    .Build());

            var counter = 0;

            var redisHandle = cach.CacheHandles.OfType<Redis.RedisCacheHandle<int>>().First();

            while (true)
            {
                try
                {
                    cach.Clear();
                    var swatch = Stopwatch.StartNew();
                    var iters = 500000;
                    long innerTimeTaken = 0;
                    var tasks = new List<Task<RedisCacheHandle<int>.SetResult>>();
                    var item = new CacheItem<int>("key", 1);
                    for (var i = 0; i < iters; i++)
                    {
                        var innerSwatch = Stopwatch.StartNew();
                        tasks.Add(redisHandle.SetAsyncPerf(item, StackExchange.Redis.When.Always, true)
                            //.ContinueWith((t)=>
                            //{
                            //    var el = innerSwatch.ElapsedTicks;
                            //    Interlocked.Add(ref innerTimeTaken, el);
                            //    return t.Result;
                            //})
                            );
                        //redisHandle.AddAsync("key" + i, i.ToString());
                    }

                    var added = redisHandle.SetAsyncPerf(item, StackExchange.Redis.When.Always, true).Result;
                    Task.WhenAll(tasks.ToArray());
                    var re = redisHandle.Get("key3");
                    var elapsed = swatch.ElapsedMilliseconds;

                    long failed = 0, a = 0, b = 0, c = 0, d = 0;
                    foreach(var t in tasks)
                    {
                        failed += t.Result.Success ? 0 : 1;
                        a += t.Result.StageA;
                        b += t.Result.StageB;
                        c = t.Result.StageC;
                        d = t.Result.StageD;
                    }
                    //d = (d - c);
                    c = c - b;
                    b = b - a;

                    Console.WriteLine($"Inner time taken: {innerTimeTaken / (double)TimeSpan.TicksPerMillisecond:N0}ms. per iter {innerTimeTaken / (double)TimeSpan.TicksPerMillisecond / iters:N0}");
                    Console.WriteLine($"failed:{failed}, a: {a / (double)TimeSpan.TicksPerMillisecond:N0}, b: {b / (double)TimeSpan.TicksPerMillisecond:N0}, c: {c / (double)TimeSpan.TicksPerMillisecond:N0}, total: {d / (double)TimeSpan.TicksPerMillisecond:N0}.");
                    Console.WriteLine($"Elapsed {elapsed} added {counter} {re} {(iters / (double)elapsed) * 1000:N0}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            Console.ReadKey();
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