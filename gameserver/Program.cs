#region

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using common;
using log4net;
using log4net.Config;
using gameserver.networking;
using gameserver.realm;
using common.config;
using System.Diagnostics;
using System.Threading.Tasks;
using gameserver.realm.commands.mreyeball;

#endregion

namespace gameserver
{
    internal static class Program
    {
        public static DateTime uptime { get; private set; }
        public static readonly ILog Logger = LogManager.GetLogger("Server");

        private static readonly ManualResetEvent Shutdown = new ManualResetEvent(false);

        public static int Usage { get; private set; }
        public static bool autoRestart { get; private set; }

        public static Stopwatch sw { get; set; }
        public static int delay_ { get; set; }
        public static int delay { get; set; }
        public static ChatManager chat { get; set; }

        private static RealmManager manager;

        public static DateTime WhiteListTurnOff { get; private set; }

        private static void UpdateTitle(bool hasRestart, TimeSpan time, int usage = -1) => Console.Title = $"{Settings.GAMESERVER.TITLE}{(hasRestart ? $" | Restart: {(time == TimeSpan.MinValue ? "0/" : $"{Convert.ToInt32(time.ToString().Split('.')[0].Split(':')[1])}/")}{delay} min" : "")}{(usage == -1 ? "" : $" | Online: {usage}/{Settings.NETWORKING.MAX_CONNECTIONS}")}{(time == TimeSpan.MinValue ? "" : $" (Uptime {time.ToString().Split('.')[0]})")}";

        private static void Main(string[] args)
        {
            Console.Title = "Loading...";

            XmlConfigurator.ConfigureAndWatch(new FileInfo("_gameserver.config"));

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.Name = "Entry";
                
            using (var db = new Database())
            {
                Usage = -1;

                manager = new RealmManager(db);

                autoRestart = Settings.NETWORKING.RESTART.ENABLE_RESTART;

                manager.Initialize();
                manager.Run();

                Server server = new Server(manager);
                PolicyServer policy = new PolicyServer();

                Console.CancelKeyPress += (sender, e) => e.Cancel = true;

                policy.Start();
                server.Start();

                if (autoRestart)
                {
                    delay = Settings.NETWORKING.RESTART.RESTART_DELAY_MINUTES <= 5 ? 6 : Settings.NETWORKING.RESTART.RESTART_DELAY_MINUTES;
                    chat = manager.Chat;
                    uptime = DateTime.Now;
                    sw = Stopwatch.StartNew();
                    delay_ = 0;
                    new Thread(restart).Start();
                    new Thread(uptime_usage).Start();
                }

                UpdateTitle(autoRestart, TimeSpan.MinValue);

                Logger.Info("Server initialized.");

                Console.CancelKeyPress += delegate
                {
                    Shutdown?.Set();
                };

                while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                Logger.Info("Terminating...");
                server?.Stop();
                policy?.Stop();
                manager?.Stop();
                Shutdown?.Dispose();
                Logger.Info("Server terminated.");
                Environment.Exit(0);
            }
        }

        static int ToMiliseconds(int minutes) => minutes * 60 * 1000;

        public static void uptime_usage()
        {
            do
            {
                int ttl = ToMiliseconds(Settings.GAMESERVER.TTL) / 60;
                int delay__ = 0;
                Stopwatch sw_ = Stopwatch.StartNew();
                do
                {
                    delay__ = (int) sw_.ElapsedMilliseconds;

                    if (delay__ >= ttl) {
                        UpdateTitle(autoRestart, sw.Elapsed, Usage);
                        Usage = manager.Clients.Keys.Count;
                        sw_.Restart();
                    }
                } while (true);
            } while (true);
        }

        public async static void ForceShutdown(Exception ex = null)
        {
            Task task = Task.Delay(1000);

            await task;

            Process.Start(Settings.GAMESERVER.FILE);

            Environment.Exit(0);

            if (ex != null)
                Logger.Error(ex);
        }

        public static void restart()
        {
            do
            {
                delay_ = (int) sw.ElapsedMilliseconds;

                if (delay_ >= (ToMiliseconds(delay - 5)))
                {
                    sw.Stop();
                    int i = 5;
                    string message = null;
                    do
                    {
                        message = $"Server will be restarted in {i} minute{(i <= 1 ? "" : "s")}.";
                        Logger.Info(message);
                        try
                        {
                            foreach (Client j in manager.Clients.Values)
                                chat.Tell(j?.Player, MrEyeball_Dictionary.BOT_NAME, ("Hey (PLAYER_NAME), prepare to disconnect." + message).Replace("(PLAYER_NAME)", j?.Player.Name));
                        } catch (Exception ex)
                        {
                            ForceShutdown(ex);
                        }
                        Thread.Sleep(ToMiliseconds(1));
                        i--;
                    } while (i != 0);
                    message = "Server is now offline.";
                    Logger.Warn(message);
                    try
                    {
                        foreach (Client k in manager.Clients.Values)
                            chat.Tell(k?.Player, MrEyeball_Dictionary.BOT_NAME, message);
                    } catch (Exception ex)
                    {
                        ForceShutdown(ex);
                    }
                    Thread.Sleep(2000);
                    try
                    {
                        foreach (Client clients in manager.Clients.Values)
                            clients?.Disconnect();
                    } catch (Exception ex)
                    {
                        ForceShutdown(ex);
                    }
                    Process.Start(Settings.GAMESERVER.FILE);
                    Environment.Exit(0);
                }
            } while (true);
        }

        public static void Stop(Task task = null)
        {
            if (task != null)
                Logger.Fatal(task.Exception);

            Shutdown.Set();
        }
    }
}