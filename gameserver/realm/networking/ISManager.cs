#region

using common;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

#endregion

namespace gameserver.realm
{
    public class ISManager : InterServerChannel, IDisposable
    {
        private ILog log = LogManager.GetLogger(nameof(ISManager));

        public const string NETWORK = "network";
        public const string CHAT = "chat";
        public const string CONTROL = "control";   //maybe later...

        private enum NetworkCode
        {
            JOIN,
            PING,
            QUIT
        }

        private struct NetworkMsg
        {
            public NetworkCode Code;
            public string Type;
        }

        private RealmManager Manager;

        public ISManager(RealmManager manager) : base(manager.Database, manager.InstanceId)
        {
            log.Info($"Server's Id is {manager.InstanceId}");
            Manager = manager;

            AddHandler<NetworkMsg>(NETWORK, HandleNetwork);

            Publish(NETWORK, new NetworkMsg()
            {
                Code = NetworkCode.JOIN,
                Type = "World Server"
            });
        }

        private ConcurrentDictionary<string, int> availableInstance = new ConcurrentDictionary<string, int>();

        private long remaining = 2000;
        
        public void Run()
        {
            _tmr.Elapsed += (sender, e) => Tick(PingPeriod);
            _tmr.Start();
        }
        
        private readonly Timer _tmr = new Timer(PingPeriod);
        private readonly object _dicLock = new object();
        private readonly Dictionary<string, int> _lastUpdateTime = new Dictionary<string, int>();
        private const int PingPeriod = 2000;
        private const int ServerTimeout = 30000;
        private long _lastPing;

        public void Tick(int elapsedMs)
        {
            using (TimedLock.Lock(_dicLock))
            {
                _lastPing += elapsedMs;
                
                foreach (var s in _lastUpdateTime.Keys.ToArray())
                    _lastUpdateTime[s] += elapsedMs;
                
                if (_lastPing < PingPeriod)
                    return;

                _lastPing = 0;

                Publish(NETWORK, new NetworkMsg() {
                    Code = NetworkCode.PING,
                    Type = "World Server"
                });

                foreach (var i in availableInstance.Keys)
                {
                    if (availableInstance.ContainsKey(i) && --availableInstance[i] == 0)
                    {
                        int val;
                        availableInstance.TryRemove(i, out val);
                        log.Info($"Server {i} timed out");
                    }
                }
            }
        }

        public void Dispose()
        {
            Publish(NETWORK, new NetworkMsg() { Code = NetworkCode.QUIT });
        }

        private void HandleNetwork(object sender, InterServerEventArgs<NetworkMsg> e)
        {
            switch (e.Content.Code)
            {
                case NetworkCode.JOIN:
                    if (availableInstance.TryAdd(e.InstanceId, 5))
                    {
                        log.Info($"Server {e.InstanceId} ({e.Content.Type}) joined the network");
                        Publish(NETWORK, new NetworkMsg()   //for the new instances
                        {
                            Code = NetworkCode.JOIN,
                            Type = "World Server"
                        });
                    }
                    else
                        availableInstance[e.InstanceId] = 5;
                    break;
                case NetworkCode.PING:
                    if (!availableInstance.ContainsKey(e.InstanceId))
                        log.Info($"Server {e.InstanceId} re-joined the network");
                    availableInstance[e.InstanceId] = 5;
                    break;
                case NetworkCode.QUIT:
                    int dummy;
                    availableInstance.TryRemove(e.InstanceId, out dummy);
                    log.Info($"Server {e.InstanceId} quited the network");
                    break;
            }
        }
    }
}