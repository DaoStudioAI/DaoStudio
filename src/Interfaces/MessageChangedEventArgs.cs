using System;

namespace DaoStudio.Interfaces
{
    public sealed class MessageChangedEventArgs : EventArgs
    {
        public IMessage Message { get; }
        public MessageChangeType Change { get; }

        public MessageChangedEventArgs(IMessage message, MessageChangeType change)
        {
            Message = message;
            Change = change;
        }
    }
}
