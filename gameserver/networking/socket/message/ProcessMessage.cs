using gameserver.networking.incoming;
using gameserver.realm.entity.player;
using System;
using System.Collections.Generic;

namespace gameserver.networking
{
    public partial class Client
    {
        public ProtocolState State { get; internal set; }
        public Player Player { get; internal set; }

        internal void ProcessMessage(Message msg)
        {
            try
            {
                //log.Logger.Log(typeof (Client), Level.Verbose, $"Handling packet '{msg}'...", null);
                if (msg.ID == (MessageID) 255) return;
                IMessage handler;
                if (!MessageHandler.Handlers.TryGetValue(msg.ID, out handler));
                    //log.Warn($"Unhandled packet '{msg.ID}'.");
                else
                    handler.Handle(this, (IncomingMessage) msg);
            }
            catch (Exception e)
            {
                //log.Error($"Error when handling packet '{msg}'...", e);
                Disconnect();
            }
        }        

        public bool IsReady()
        {
            if (State == ProtocolState.Disconnected)
                return false;
            return State != ProtocolState.Ready || (Player != null && (Player == null || Player.Owner != null));
        }

        public void SendMessage(Message msg)
        {
            handler?.IncomingMessage(msg);
        }

        public void SendMessage(IEnumerable<Message> msgs)
        {
            handler?.IncomingMessage(msgs);
        }
    }
}