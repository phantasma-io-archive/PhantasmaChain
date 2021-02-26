using System.Threading;

namespace Phantasma.Core
{
    public abstract class Runnable
    {
        public enum State
        {
            Stopped,
            Running,
            Stopping
        }

        private State _state = State.Stopped;

        public State CurrentState => _state;

        public bool Running => CurrentState == State.Running;
        
        protected abstract bool Run();

        public void StartInThread(ThreadPriority priority = ThreadPriority.Normal)
        {
            if (_state != State.Stopped)
            {
                return;
            }

            _state = State.Running;

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                OnStart();

                while (_state == State.Running)
                {
                    if (!Run())
                    {
                        break;
                    }
                }

                _state = State.Stopped;
                OnStop();
            }).Start();
        }

        public void Start()
        {
            if (_state != State.Stopped)
            {
                return;
            }

            _state = State.Running;

            OnStart();
            while (_state == State.Running)
            {
                if (!Run())
                {
                    break;
                }
            }
            OnStop();
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
