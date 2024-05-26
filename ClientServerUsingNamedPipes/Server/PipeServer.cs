using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ClientServerUsingNamedPipes.Interfaces;
using ClientServerUsingNamedPipes.Utilities;

namespace ClientServerUsingNamedPipes.Server
{
    public class PipeServer : ICommunicationServer
    {
        #region private fields

        private readonly string _pipeName;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly IDictionary<string, ICommunicationServer> _servers; // ConcurrentDictionary is thread safe
        private int MaxNumberOfServerInstances { get; } = 10;
        public bool IsConnected { get; } = false;

        #endregion

        #region c'tor

        public PipeServer(string name, int maxServers) : this(name) {
            MaxNumberOfServerInstances = maxServers;
        } 

        public PipeServer(string name) {
            _pipeName = name;
            _synchronizationContext = AsyncOperationManager.SynchronizationContext;
            _servers = new ConcurrentDictionary<string, ICommunicationServer>();
        } 

        public PipeServer(int maxServers) : this() {
            MaxNumberOfServerInstances = maxServers;
        } 

        public PipeServer()
        {
            _pipeName = Guid.NewGuid().ToString();
            _synchronizationContext = AsyncOperationManager.SynchronizationContext;
            _servers = new ConcurrentDictionary<string, ICommunicationServer>();
        }

        #endregion

        #region events

        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;
        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnectedEvent;

        #endregion

        #region ICommunicationServer implementation

        public string ServerId
        {
            get { return _pipeName; }
        }

        public void Start()
        {
        }

        public void Stop()
        {
            foreach (var server in _servers.Values)
            {
                try
                {
                    UnregisterFromServerEvents(server);
                    server.Stop();
                }
                catch (Exception)
                {
                    Logger.Error("Failed to stop server");
                }
            }

            _servers.Clear();
        }

        public Task<TaskResult> SendMessage(string message) {
            var taskCompletionSource = new TaskCompletionSource<TaskResult>();
            var success = true;
            foreach (var server in _servers.Values) {
                if (server.IsConnected) {
                    try {
                        var result = server.SendMessage(message);
                        if (success) success = result.Result.IsSuccess;
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        throw;
                    }
                }
            }

            taskCompletionSource.SetResult(new TaskResult {IsSuccess = true});
            return taskCompletionSource.Task;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Starts a new NamedPipeServerStream that waits for connection
        /// </summary>
        public void StartNamedPipeServer()
        {
            var server = new InternalPipeServer(_pipeName, MaxNumberOfServerInstances);
            _servers[server.Id] = server;

            server.ClientConnectedEvent += ClientConnectedHandler;
            server.ClientDisconnectedEvent += ClientDisconnectedHandler;
            server.MessageReceivedEvent += MessageReceivedHandler;

            server.Start();
        }

        /// <summary>
        /// Stops the server that belongs to the given id
        /// </summary>
        /// <param name="id"></param>
        private void StopNamedPipeServer(string id)
        {
            UnregisterFromServerEvents(_servers[id]);
            _servers[id].Stop();
            _servers.Remove(id);
        }

        /// <summary>
        /// Unregisters from the given server's events
        /// </summary>
        /// <param name="server"></param>
        private void UnregisterFromServerEvents(ICommunicationServer server)
        {
            server.ClientConnectedEvent -= ClientConnectedHandler;
            server.ClientDisconnectedEvent -= ClientDisconnectedHandler;
            server.MessageReceivedEvent -= MessageReceivedHandler;
        }

        /// <summary>
        /// Fires MessageReceivedEvent in the current thread
        /// </summary>
        /// <param name="eventArgs"></param>
        private void OnMessageReceived(MessageReceivedEventArgs eventArgs)
        {
            _synchronizationContext.Post(e => MessageReceivedEvent.SafeInvoke(this, (MessageReceivedEventArgs) e),
                eventArgs);
        }

        /// <summary>
        /// Fires ClientConnectedEvent in the current thread
        /// </summary>
        /// <param name="eventArgs"></param>
        private void OnClientConnected(ClientConnectedEventArgs eventArgs)
        {
            _synchronizationContext.Post(e => ClientConnectedEvent.SafeInvoke(this, (ClientConnectedEventArgs) e),
                eventArgs);
        }

        /// <summary>
        /// Fires ClientDisconnectedEvent in the current thread
        /// </summary>
        /// <param name="eventArgs"></param>
        private void OnClientDisconnected(ClientDisconnectedEventArgs eventArgs)
        {
            _synchronizationContext.Post(
                e => ClientDisconnectedEvent.SafeInvoke(this, (ClientDisconnectedEventArgs) e), eventArgs);
        }

        /// <summary>
        /// Handles a client connection. Fires the relevant event and prepares for new connection.
        /// </summary>
        private void ClientConnectedHandler(object sender, ClientConnectedEventArgs eventArgs)
        {
            OnClientConnected(eventArgs);
        }

        /// <summary>
        /// Hanldes a client disconnection. Fires the relevant event ans removes its server from the pool
        /// </summary>
        private void ClientDisconnectedHandler(object sender, ClientDisconnectedEventArgs eventArgs)
        {
            OnClientDisconnected(eventArgs);

            StopNamedPipeServer(eventArgs.ClientId);
        }

        /// <summary>
        /// Handles a message that is received from the client. Fires the relevant event.
        /// </summary>
        private void MessageReceivedHandler(object sender, MessageReceivedEventArgs eventArgs)
        {
            OnMessageReceived(eventArgs);
        }

        #endregion
    }
}
