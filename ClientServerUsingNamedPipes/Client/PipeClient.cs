using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using ClientServerUsingNamedPipes.Interfaces;
using ClientServerUsingNamedPipes.Utilities;

namespace ClientServerUsingNamedPipes.Client
{
    public class PipeClient : ICommunicationClient
    {
        #region private fields

        private readonly NamedPipeClientStream _pipeClient;
        private const int BufferSize = 2048;

        private class Info
        {
            public readonly byte[] Buffer;
            public readonly StringBuilder StringBuilder;

            public Info()
            {
                Buffer = new byte[BufferSize];
                StringBuilder = new StringBuilder();
            }
        }

        #endregion

        #region c'tor

        public PipeClient(string serverId)
        {
            _pipeClient = new NamedPipeClientStream(".", serverId, PipeDirection.InOut, PipeOptions.Asynchronous);
        }

        #endregion

        #region events

        public event EventHandler<ConnectedToServerEventArgs> ConnectedToServerEvent;
        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

        #endregion

        #region ICommunicationClient implementation

        /// <summary>
        /// Starts the client. Connects to the server.
        /// </summary>
        public void Start()
        {
            const int tryConnectTimeout = 5*60*1000; // 5 minutes
            _pipeClient.Connect(tryConnectTimeout);
            if (_pipeClient.IsConnected) {
                _pipeClient.ReadMode = PipeTransmissionMode.Message;
                BeginRead(new Info());
                ConnectedToServerEvent.SafeInvoke(this, new ConnectedToServerEventArgs());
            }
        }

        /// <summary>
        /// Stops the client. Waits for pipe drain, closes and disposes it.
        /// </summary>
        public void Stop()
        {
            try {
                _pipeClient.WaitForPipeDrain();
            } catch (IOException e) {
                _pipeClient.Close();
                _pipeClient.Dispose();
            }
            finally
            {
                _pipeClient.Close();
                _pipeClient.Dispose();
            }
        }

        public Task<TaskResult> SendMessage(string message)
        {
            var taskCompletionSource = new TaskCompletionSource<TaskResult>();

            if (_pipeClient.IsConnected)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                _pipeClient.BeginWrite(buffer, 0, buffer.Length, asyncResult =>
                {
                    try
                    {
                        taskCompletionSource.SetResult(EndWriteCallBack(asyncResult));
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.SetException(ex);
                    }

                }, null);
            }
            else
            {
                Logger.Error("Cannot send message, pipe is not connected");
                throw new IOException("pipe is not connected");
            }

            return taskCompletionSource.Task;
        }

        #endregion


        #region private methods

        /// <summary>
        /// This method begins an asynchronous read operation.
        /// </summary>
        private void BeginRead(Info info)
        {
            try
            {
                _pipeClient.BeginRead(info.Buffer, 0, BufferSize, EndReadCallBack, info);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// This callback is called when the BeginRead operation is completed.
        /// We can arrive here whether the connection is valid or not
        /// </summary>
        private void EndReadCallBack(IAsyncResult result)
        {
            var readBytes = _pipeClient.EndRead(result);
            if (readBytes > 0)
            {
                var info = (Info) result.AsyncState;

                // Get the read bytes and append them
                info.StringBuilder.Append(Encoding.UTF8.GetString(info.Buffer, 0, readBytes));

                if (!_pipeClient.IsMessageComplete) // Message is not complete, continue reading
                {
                    BeginRead(info);
                }
                else // Message is completed
                {
                    // Finalize the received string and fire MessageReceivedEvent
                    var message = info.StringBuilder.ToString().TrimEnd('\0');

                    OnMessageReceived(message);

                    // Begin a new reading operation
                    BeginRead(new Info());
                }
            }
        }

        /// <summary>
        /// This callback is called when the BeginWrite operation is completed.
        /// It can be called whether the connection is valid or not.
        /// </summary>
        /// <param name="asyncResult"></param>
        private TaskResult EndWriteCallBack(IAsyncResult asyncResult)
        {
            _pipeClient.EndWrite(asyncResult);
            _pipeClient.Flush();

            return new TaskResult {IsSuccess = true};
        }

        /// <summary>
        /// This method fires MessageReceivedEvent with the given message
        /// </summary>
        private void OnMessageReceived(string message)
        {
            if (MessageReceivedEvent != null)
            {
                MessageReceivedEvent(this,
                    new MessageReceivedEventArgs
                    {
                        Message = message
                    });
            }
        }

        #endregion
    }
}
