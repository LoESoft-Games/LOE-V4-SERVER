#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using log4net;
using System.Threading.Tasks;

#endregion

namespace gameserver.realm
{
    public class LogicTicker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LogicTicker));
        
        private readonly ManualResetEvent _mre;
        private readonly RealmManager _manager;
        private readonly ConcurrentQueue<Action<RealmTime>>[] _pendings;
                
        public readonly int MsPT;
        public readonly int TPS;

        internal int _ttl = 200; // in miliseconds
        internal int _cqa = 5; // max number of queue actions for task
        
        private Task _worldTask;
        private RealmTime _worldTime;
                
        public LogicTicker(RealmManager manager)
        {
            _manager = manager;
            MsPT = 1000 / manager.TPS;
            _mre = new ManualResetEvent(false);
            _worldTime = new RealmTime();

            _pendings = new ConcurrentQueue<Action<RealmTime>>[_cqa];
            for (int i = 0; i < _cqa; i++)
                _pendings[i] = new ConcurrentQueue<Action<RealmTime>>();
        }
        
        public void TickLoop()
        {
            log.Info("Logic loop started.");

            int loopTime = 0;
            RealmTime t = new RealmTime();
            Stopwatch watch = Stopwatch.StartNew();
            do
            {
                t.TotalElapsedMs = watch.ElapsedMilliseconds;
                t.TickDelta = loopTime / MsPT;
                t.TickCount += t.TickDelta;
                t.ElapsedMsDelta = t.TickDelta * MsPT;

                if (t.TickDelta > 3)
                    log.Warn($"LAGGED! | ticks:{t.TickDelta} ms: {loopTime} tps: {(t.TickCount / (t.TotalElapsedMs / 1000.0)):n2}");
                if (_manager.Terminating)
                    break;

                DoLogic(t);

                var logicTime = (int)(watch.ElapsedMilliseconds - t.TotalElapsedMs);
                _mre.WaitOne(Math.Max(0, MsPT - logicTime));
                loopTime += (int)(watch.ElapsedMilliseconds - t.TotalElapsedMs) - t.ElapsedMsDelta;
            } while (true);

            log.Info("Logic loop stopped.");
        }
        
        private void DoLogic(RealmTime t)
        {
            var clients = _manager.Clients.Values;
            
            foreach (var i in _pendings)
            {
                Action<RealmTime> callback;
                while (i.TryDequeue(out callback))
                    try
                    {
                        callback(t);
                    }
                    catch (Exception e)
                    {
                        log.Error(e);
                    }
            }
            _manager.InterServer.Tick(t.ElapsedMsDelta);

            TickWorlds1(t);

            foreach (var client in clients)
                if (client.Player != null && client.Player.Owner != null)
                    client?.Player.Flush();
        }

        void TickWorlds1(RealmTime t)
        {
            _worldTime.TickDelta += t.TickDelta;
            
            try
            {
                foreach (var w in _manager.Worlds.Values.Distinct())
                    w?.Tick(t);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            
            if (_worldTask == null || _worldTask.IsCompleted)
            {
                t.TickDelta = _worldTime.TickDelta;
                t.ElapsedMsDelta = t.TickDelta * MsPT;

                if (t.ElapsedMsDelta < _ttl) 
                    return;

                _worldTime.TickDelta = 0;
                _worldTask = Task.Factory.StartNew(() =>
                {
                    foreach (var i in _manager.Worlds.Values.Distinct())
                        i?.Tick(t);
                }).ContinueWith(e => log.Error(e.Exception.InnerException.ToString()), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public void AddPendingAction(Action<RealmTime> callback, PendingPriority priority = PendingPriority.Normal) => _pendings[(int)priority].Enqueue(callback);
    }
}