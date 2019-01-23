using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;

namespace Hallupa.Library
{
    public enum ProducerConsumerActionResult
    {
        Success,
        Stop
    }

    public class ProducerConsumer<T>
    {
        private readonly int _consumers;
        private readonly Func<T, ProducerConsumerActionResult> _consumeAction;
        private readonly Queue<T> _queue;
        private readonly object _consumerWait = new object();
        private bool _producerCompleted;
        private List<Task> _consumerTasks;
        private bool _cancel;

        public ProducerConsumer(int consumers, Func<T, ProducerConsumerActionResult> consumeAction)
        {
            _consumers = consumers;
            _consumeAction = consumeAction;
            _queue = new Queue<T>();
        }

        public void Start()
        {
            _consumerTasks = new List<Task>();
            for (var i = 0; i < _consumers; i++)
            {
                _consumerTasks.Add(Task.Run(() =>
                {
                    ConsumeItems();
                }));
            }
        }

        public void Add(T item, bool pulse = true)
        {
            if (_producerCompleted)
            {
                throw new ApplicationException("Producer set to completed");
            }

            lock (_queue)
            {
                _queue.Enqueue(item);

                if (pulse)
                {
                    Monitor.Pulse(_queue);
                }
            }
        }

        public void Add(IEnumerable<T> items)
        {
            if (_producerCompleted)
            {
                throw new ApplicationException("Producer set to completed");
            }

            lock (_queue)
            {
                foreach (var item in items)
                {
                    _queue.Enqueue(item);
                }

                Monitor.PulseAll(_queue);
            }
        }

        public void SetProducerCompleted()
        {
            _producerCompleted = true;
            lock (_queue)
            {
                Monitor.PulseAll(_queue);
            }
        }

        public void Stop()
        {
            _cancel = true;
            lock (_queue)
            {
                Monitor.PulseAll(_queue);
            }
        }

        public void WaitUntilConsumersFinished()
        {
            Task.WaitAll(_consumerTasks.ToArray());
        }

        private void ConsumeItems()
        {
            while (!_cancel)
            {
                T item = default(T);
                var gotItem = false;
                lock (_queue)
                {
                    if (_queue.Count > 0)
                    {
                        item = _queue.Dequeue();
                        gotItem = true;
                    }
                }

                if (!gotItem)
                {
                    if (_producerCompleted)
                    {
                        break;
                    }

                    lock (_queue)
                    {
                        // Check again no further item added to queue
                        if (_queue.Count > 0)
                        {
                            item = _queue.Dequeue();
                            gotItem = true;
                        }

                        if (!gotItem)
                        {
                            Monitor.Wait(_queue);
                        }
                    }
                }

                if (gotItem)
                {
                    var ret = _consumeAction(item);
                    if (ret == ProducerConsumerActionResult.Stop)
                    {
                        Stop();
                    }
                }
            }
        }
    }
}