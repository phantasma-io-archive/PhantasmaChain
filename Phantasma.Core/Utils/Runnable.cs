using System.Threading;

namespace Phantasma.Core
{
    public abstract class Runnable
    {
        private enum State
        {
            Stopped,
            Running,
            Stopping
        }

        private State _state = State.Stopped;

        protected abstract bool Run();

        public void Start(ThreadPriority priority = ThreadPriority.Normal)
        {
            if (_state != State.Stopped)
            {
                return;
            }

            _state = State.Running;

#if BRIDGE_NET
            OnStart();
            do
            {
                if (!Run())
                {
                    break;
                }
            } while (_state == State.Running);
            OnStop();
#else 
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                OnStart();

                do
                {
                    if (!Run())
                    {
                        break;
                    }
                } while (_state == State.Running);

                _state = State.Stopped;
                OnStop();
            }).Start();
#endif
        }

        public bool IsRunning => _state == State.Running;

        public void Stop()
        {
            if (_state != State.Running)
            {
                return;
            }

            _state = State.Stopping;

            while (_state == State.Stopping)
            {
                Thread.Sleep(100);
            }
        }

        protected virtual void OnStart() { }
        protected virtual void OnStop() { }

    }
}
