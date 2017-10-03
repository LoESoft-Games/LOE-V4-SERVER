using gameserver.networking.error;
using gameserver.networking.outgoing;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using FAILURE = gameserver.networking.outgoing.FAILURE;

namespace gameserver.networking
{
    public partial class Client
    {
        public Task task = Task.Delay(250);

        public string[] time => DateTime.Now.ToString().Split(' ');

        public void _(string accId, RECONNECT msg)
        {
            string response = $"[{time[1]}] [{nameof(Client)}] Reconnect\t->\tplayer id {accId} to {msg.Name}";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(response);
            Console.ResetColor();
        }

        public void _(string accId, Socket skt)
        {
            string response = $"[{time[1]}] [{nameof(Client)}] Disconnect\t->\tplayer id {accId} to {skt.RemoteEndPoint.ToString().Split(':')[0]}";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(response);
            Console.ResetColor();
        }

        public async void Reconnect(RECONNECT msg)
        {
            if (this == null)
                return;

            if (Account == null)
            {
                string[] labels = new string[] { "{CLIENT_NAME}" };
                string[] arguments = new string[] { Account.Name };
                
                SendMessage(new FAILURE
                {
                    ErrorId = Type.JSON_DIALOG,
                    ErrorDescription =
                        JSONErrorIDHandler.
                            FormatedJSONError(
                                errorID: ErrorIDs.LOST_CONNECTION,
                                labels: labels,
                                arguments: arguments
                            )
                });

                await task;

                Disconnect();
                return;
            }

            _(Account.AccountId, msg);

            Save();

            await task;

            SendMessage(msg);
        }

        public void Save()
        {
            try
            {
                Player?.SaveToCharacter();

                if (Character != null)
                    Manager.Database.SaveCharacter(Account, Character, false);
                if (Account != null)
                    Manager.Database.ReleaseLock(Account);
            }
            catch (Exception ex)
            {
                Program.Logger.Error($"[{nameof(Client)}] Save exception:\n{ex}");
            }
        }
        
        private async void Disconnect(Client client)
        {
            if (client == null)
                return;

            Save();

            await task;

            Manager.Disconnect(this);
        }

        public async void Disconnect()
        {
            try
            {
                Save();

                await task;

                if (State == ProtocolState.Disconnected)
                    return;

                if (Socket == null)
                    return;

                if (Account == null)
                    return;

                _(Account.AccountId, Socket);
                
                State = ProtocolState.Disconnected;

                if (Account != null)
                    Disconnect(this);

                Socket?.Close();
            }
            catch (Exception e)
            {
                Program.Logger.Error($"[{nameof(Client)}] Disconnect exception:\n{e}");
            }
        }
    }
}