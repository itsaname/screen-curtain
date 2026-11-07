using System;

namespace ScreenCurtain
{
    internal enum GuardState
    {
        Off,
        Starting,
        Active,
        Degraded,
        Stopping
    }

    internal sealed class GuardStatusEventArgs : EventArgs
    {
        public GuardState State;
        public string Message;

        public GuardStatusEventArgs(GuardState state, string message)
        {
            State = state;
            Message = message;
        }
    }
}
