using System;
using System.Collections.Generic;
using System.Threading;

namespace Phantasma.Utils
{
    public abstract class Runnable
    {
        private enum State {
        Stopped,
        Running,
        Stopping
        }

        private State _state;

        private Thread _thread;

        protected abstract bool Run();

        public void Start() {
            if (_state != State.Stopped)
            {
                return;
            }

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                _state = State.Running;
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
        }

        public void Stop() {
            if (_state != State.Running) {
                return;
            }

            _state = State.Stopping;

            while (_state == State.Stopping) {
                Thread.Sleep(100);
            }
        }

        protected virtual void OnStart() { }
        protected virtual void OnStop() { }

    }
}
