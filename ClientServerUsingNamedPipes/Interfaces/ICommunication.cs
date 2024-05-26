using System;
using System.Threading.Tasks;
using ClientServerUsingNamedPipes.Utilities;

namespace ClientServerUsingNamedPipes.Interfaces
{
    public interface ICommunication
    {
        /// <summary>
        /// Starts the communication channel
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the communication channel
        /// </summary>
        void Stop();
        
        /// <summary>
        /// This method sends the given message asynchronously over the communication channel
        /// </summary>
        /// <param name="message"></param>
        /// <returns>A task of TaskResult</returns>
        Task<TaskResult> SendMessage(string message);

        /// <summary>
        /// This event is fired when a message is received 
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
