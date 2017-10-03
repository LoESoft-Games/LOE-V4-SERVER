using common;
using common.config;
using System.Net.Sockets;

namespace gameserver.networking
{
    internal partial class NetworkHandler
    {
        private void ProcessPolicyFile()
        {
            NetworkStream s = new NetworkStream(skt);
            NWriter wtr = new NWriter(s);
            wtr.WriteNullTerminatedString(Settings.IS_PRODUCTION ? Settings.NETWORKING.INTERNAL.SELECTED_DOMAINS : Settings.NETWORKING.INTERNAL.LOCALHOST_DOMAINS);
            wtr.Write((byte) '\r');
            wtr.Write((byte) '\n');
            parent.Disconnect();
        }
    }
}