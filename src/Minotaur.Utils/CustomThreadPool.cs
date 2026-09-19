using System.Collections.Concurrent;

using static Minotaur.Utils.Logging;

namespace Minotaur.Utils;

public class CustomThreadPool : IDisposable
{
	private class WorkerThread
	{
		public readonly CustomThreadPool Pool;
		public readonly Thread T;
		public readonly BlockingCollection<(string name, ThreadPriority prio, Action task)> Jobs = new (1);
		public bool Employed { get; private set; } = false;
		public string CurrentJob { get; private set; }

		public WorkerThread (CustomThreadPool pool)
		{
			Pool = pool;
			T = new Thread (Run) {
				Name = "Worker starting up"
			};
			T.Start ();
		}

		private void Run ()
		{
			T.Name = $"T{T.ManagedThreadId:000}";

			foreach (var tup in Jobs.GetConsumingEnumerable ()) {
				Employed = true;
				CurrentJob = tup.name;
				//T.Name = $"T{T.ManagedThreadId:000}: {tup.name}";
				T.Priority = tup.prio;

				tup.task ();
				//T.Name = "Unemployed";
				Employed = false;
			}
		}
	}

	private readonly List<WorkerThread> _Workers = new ();

	public CustomThreadPool ()
	{
	}

	public void AddTask (string name, ThreadPriority prio, Action job)
	{
		lock (_Workers) {
			WorkerThread GetWorker ()
			{
				for (int i = 0; i < _Workers.Count; i++) {
					var w = _Workers [i];
					if (w.Employed) {
						continue;
					}
					return w;
				}
				var add = new WorkerThread (this);
				_Workers.Add (add);
				return add;
			}

			while (true) {
				WorkerThread w = GetWorker ();
				var ok = w.Jobs.TryAdd ((name, prio, job));
				if (ok) {
					break;
				}
			}
		}
	}

	public void Dispose ()
	{
		LogN ("Disposing custom thread pool");
		lock (_Workers) {
			for (int i = 0; i < _Workers.Count; i++) {
				var w = _Workers [i];
				w.Jobs.CompleteAdding ();
			}
			_Workers.Clear ();
		}
	}
}
