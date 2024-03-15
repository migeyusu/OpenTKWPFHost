using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTkWPFHost.Core
{
    public class SingleThreadScheduler : TaskScheduler, IDisposable
    {
        private readonly Thread _thread;

        private readonly BlockingCollection<Task> _tasks = new BlockingCollection<Task>();

        public SingleThreadScheduler()
        {
            _thread = new Thread(() =>
            {
                foreach (var task in _tasks.GetConsumingEnumerable())
                {
                    TryExecuteTask(task);
                }
            });
            _thread.Start();
        }
        
        protected override void QueueTask(Task task)
        {
            if (_thread.IsAlive)
            {
                _tasks.Add(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks.ToArray();
        }

        public void Dispose()
        {
            _tasks.CompleteAdding();
        }
    }
}