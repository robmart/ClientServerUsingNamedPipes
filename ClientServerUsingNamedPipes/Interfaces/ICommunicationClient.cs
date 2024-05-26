using System;
using System.Threading.Tasks;
using ClientServerUsingNamedPipes.Utilities;

namespace ClientServerUsingNamedPipes.Interfaces
{
    public interface ICommunicationClient : ICommunication
    {
        /// <summary>
        /// This event is fired when the client connects to a server
        /// </summary>
        event EventHandler<ConnectedToServerEventArgs> ConnectedToServerEvent;
    }

    public class ConnectedToServerEventArgs : EventArgs
    {
    }
}
