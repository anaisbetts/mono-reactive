using System;
using System.Collections.Generic;
using System.Threading;
using System.Reactive.Disposables;

namespace System.Reactive.Concurrency
{
	public sealed class ThreadPoolScheduler : IScheduler
	{
		internal ThreadPoolScheduler ()
		{
		}
		
		public DateTimeOffset Now {
			get { return Scheduler.Now; }
		}
		
		public IDisposable Schedule<TState> (TState state, Func<IScheduler, TState, IDisposable> action)
		{
			return Schedule (state, Scheduler.Now, action);
		}
		
		public IDisposable Schedule<TState> (TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
		{
			bool cancel = false;
			var dis = new CompositeDisposable ();
			dis.Add (Disposable.Create (() => cancel = true));
			ThreadPool.QueueUserWorkItem (s => {
				Thread.Sleep (Scheduler.Normalize (dueTime - Now));
				if (!cancel)
					dis.Add (action (this, state));
			});
			return dis;
		}
		
		public IDisposable Schedule<TState> (TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
		{
			return Schedule<TState> (state, Now + Scheduler.Normalize (dueTime), action);
		}
	}
}
