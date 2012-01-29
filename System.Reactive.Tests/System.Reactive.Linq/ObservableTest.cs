using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using NUnit.Framework;

namespace System.Reactive.Linq.Tests
{
	[TestFixture]
	public class ObservableTest
	{
		// first, test some basic functionality used by every other tests.
		[Test]
		public void ToEnumerable ()
		{
			var e = Observable.Range (0, 3).ToEnumerable ();
			var ee = e.GetEnumerator ();
			Assert.IsTrue (ee.MoveNext (), "#1");
			Assert.AreEqual (0, ee.Current, "#2");
			Assert.IsTrue (ee.MoveNext (), "#3");
			Assert.AreEqual (1, ee.Current, "#4");
			Assert.IsTrue (ee.MoveNext (), "#5");
			Assert.AreEqual (2, ee.Current, "#6");
			Assert.IsFalse (ee.MoveNext (), "#7");
		}

		[Test]
		public void ToEnumerableWithInterval ()
		{
			var obs = Observable.Interval (TimeSpan.FromMilliseconds (100)).Take (5);
			obs.Subscribe (v => {}); // should not affect ToEnumerable() startup.
			int i = 0;
			Thread.Sleep (200);
			DateTime start = DateTime.Now;
			var e = obs.ToEnumerable ();
			foreach (var v in e)
				i ++;
			Assert.AreEqual (5, i, "#1");
			Assert.IsTrue (DateTime.Now - start > TimeSpan.FromMilliseconds (500), "#2"); // if it enumerates in incorrect time, it will result in false.
		}
		
		[Test]
		public void Materialize ()
		{
			var expected = new NotificationKind [] {
				NotificationKind.OnNext,
				NotificationKind.OnNext,
				NotificationKind.OnError };
			var source = Observable.Range (0, 2).Concat (Observable.Throw<int> (new Exception ("failure")));
			var l = new List<NotificationKind> ();
			bool done = false;
			var dis = source.Materialize ().Subscribe (v => l.Add (v.Kind), () => done = true); // test that Materialize() yields OnCompleted event after yielding OnError.
			Assert.IsTrue (SpinWait.SpinUntil (() => done, TimeSpan.FromSeconds (1)), "#1");
			Assert.AreEqual (expected, l.ToArray (), "#3");
		}

		public class MyObservable<T> : IObservable<T>
		{
			public IDisposable Subscribe (IObserver<T> observer)
			{
				throw new NotImplementedException ();
			}
		}

		[Test]
		[ExpectedException (typeof (NotImplementedException))]
		public void ErrorFlow ()
		{
			// throw error on main
			new MyObservable<int> ().Timestamp ().Subscribe (v => {});
			Assert.Fail ("should not reach here");
		}

		// tests for individual method follow...

		[Test]
		public void Aggregate ()
		{
			int i = 0, j = 0, k = 0;
			var source = Observable.Range (1, 4).Aggregate ((v1, v2) => v1 + v2);
			source.Subscribe (v => { i += v; k++; }, () => j++);
			Assert.IsTrue (SpinWait.SpinUntil (() => j != 0, 1000), "#1");
			Assert.AreEqual (10, i, "#2");
			Assert.AreEqual (1, k, "#3");
		}
		
		[Test]
		[ExpectedException (typeof (InvalidOperationException))]
		public void AggregateEmpty ()
		{
			Observable.Empty<int> ().Aggregate ((v1, v2) => v1 + v2).Subscribe (TextWriter.Null.WriteLine);
		}
		
		[Test]
		public void AggregateWithSeed ()
		{
			int i = 0, j = 0, k = 0;
			var source = Observable.Range (1, 4).Aggregate (5, (v1, v2) => v1 + v2);
			source.Subscribe (v => { i += v; k++; }, () => j++);
			Assert.IsTrue (SpinWait.SpinUntil (() => j != 0, 1000), "#1");
			Assert.AreEqual (15, i, "#2");
			Assert.AreEqual (1, k, "#3");
		}
		
		[Test]
		// Note that this overload does not result in error.
		public void AggregateEmptyWithSeed ()
		{
			int i = 0, j = 0, k = 0;
			var source = Observable.Empty<int> ().Aggregate (5, (v1, v2) => v1 + v2).Do (v => k++);
			source.Subscribe (v => i += v, () => j++);
			Assert.IsTrue (SpinWait.SpinUntil (() => j != 0, 1000), "#1");
			Assert.AreEqual (5, i, "#2");
			Assert.AreEqual (1, k, "#3");
		}
		
		[Test]
		public void All ()
		{
			Assert.IsFalse (Observable.Empty<int> ().All (v => true).ToEnumerable ().First (), "#1");
			Assert.IsTrue (Observable.Return<int> (1).All (v => true).ToEnumerable ().First (), "#2");
			Assert.IsFalse (Observable.Range (1, 3).All (v => v % 2 == 1).ToEnumerable ().First (), "#3");
		}
		
		[Test]
		public void Amb ()
		{
			var s1 = Observable.Range (1, 3).Delay (TimeSpan.FromMilliseconds (500));
			var s2 = Observable.Range (4, 3);
			var e = s1.Amb (s2).ToEnumerable ().ToArray ();
			Assert.AreEqual (new int [] {4, 5, 6}, e, "#1");
		}
		
		[Test]
		public void AndThenWhen ()
		{
			var s1 = Observable.Range (1, 3);
			var s2 = Observable.Range (4, 4); // extra element is ignored.
			var e = Observable.When<int> (s1.And (s2).Then ((v1, v2) => v1 + v2)).ToEnumerable ().ToArray ();
			Assert.AreEqual (new int [] {5, 7, 9}, e, "#1");
		}
		
		[Test]
		public void Any ()
		{
			Assert.IsFalse (Observable.Empty<int> ().Any (v => true).ToEnumerable ().First (), "#1");
			Assert.IsTrue (Observable.Return<int> (1).Any (v => true).ToEnumerable ().First (), "#2");
			Assert.IsTrue (Observable.Range (1, 3).Any (v => v % 2 == 0).ToEnumerable ().First (), "#3");
		}
		
		[Test]
		public void BufferTimeAndCount ()
		{
			// This emites events as: 0 <0ms> 1 <100ms> 2 ... 5 <500ms>
			var o1 = Observable.Generate<int, int> (0, i => i < 6, i => { Thread.Sleep (i * 50); return i + 1; }, i => i);
			var source = o1.Buffer (TimeSpan.FromMilliseconds (500), 3);
			bool done = false;
			DateTime start = DateTime.Now;
			int iter = 0;
			var dis = source.Subscribe (l => {
				if (iter == 0) {
					Assert.IsTrue (DateTime.Now - start <= TimeSpan.FromMilliseconds (500), "#1");
					Assert.AreEqual (3, l.Count, "#2");
				} else if (iter == 1) {
					Assert.IsTrue (l.Count < 3, "#3");
					Assert.IsTrue (DateTime.Now - start >= TimeSpan.FromMilliseconds (500), "#4");
				}
				else
					Assert.Fail ("Unexpected Generate() iteration");
				iter++;
				}, () => done = true);
			Assert.IsTrue (SpinWait.SpinUntil (() => done == true, 2000), "#5");
			dis.Dispose ();
		}
		
		[Test]
		public void Concat ()
		{
			int i = 0, j = 0;
			var source = Observable.Range (0, 5).Concat (Observable.Range (11, 3));
			source.Subscribe (v => i += v, () => j++);
			Assert.IsTrue (SpinWait.SpinUntil (() => j != 0, 1000), "#1");
			Assert.AreEqual (46, i, "#2");

			source = Observable.Range (0, 4).Concat (Observable.Throw<int> (new NotImplementedException ("failure")));
			try {
				source.ToEnumerable ().All (v => true);
				Assert.Fail ("should not complete");
			} catch (NotImplementedException) {
			}
		}
		
		[Test]
		public void Concat2 ()
		{
			var expected = new NotificationKind [] {
				NotificationKind.OnNext,
				NotificationKind.OnNext,
				NotificationKind.OnError };
			var source = Observable.Range (0, 2).Concat (Observable.Throw<int> (new Exception ("failure")));
			var arr = from n in source.Materialize ().ToEnumerable () select n.Kind;
			Assert.AreEqual (expected, arr.ToArray (), "#1");
		}
		
		[Test]
		public void Do ()
		{
			int i = 0, j = 0, k = 0;
			var source = Observable.Range (0, 5).Do (v => k += v);
			source.Subscribe (v => i += v, () => j++);
			Assert.IsTrue (SpinWait.SpinUntil (() => j != 0, 1000), "#1");
			Assert.AreEqual (10, i, "#2");
			Assert.AreEqual (10, k, "#3");
		}
		
		[Test]
		public void Generate ()
		{
			var source = Observable.Generate (-1, x => x < 5, x => x + 1, x => x);
			int i = 0;
			var dis = new CompositeDisposable ();
			int done = 0;
			// test multiple subscription
			foreach (var iter in Enumerable.Range (0, 5))
				dis.Add (source.Subscribe (v => i += v, () => done++));
			Assert.IsTrue (SpinWait.SpinUntil (() => done == 5, 1000), "#1");
			dis.Dispose ();
			Assert.AreEqual (45, i, "#2");
		}
		
		[Test]
		public void Retry ()
		{
			var source = Observable.Range (0, 4).Concat (Observable.Throw<int> (new Exception ("failure"))).Retry (2);
			var i = 0;
			bool done = false, error = false;
			var dis = source.Subscribe (
				v => i += v,
				ex => { error = true; done = true; Assert.AreEqual ("failure", ex.Message, "#1"); },
				() => Assert.Fail ("should not complete"));
			
			Assert.IsTrue (SpinWait.SpinUntil (() => done, 500), "#2");
			
			dis.Dispose ();
			Assert.IsTrue (error, "#3");
			Assert.AreEqual (12, i, "#4");
		}
		
		[Test]
		public void RetryZero ()
		{
			var source = Observable.Range (0, 4).Concat (Observable.Throw<int> (new Exception ("failure"))).Retry (0);
			bool done = false;
			var dis = source.Subscribe (
				v => Assert.Fail ("should not increment", "#1"),
				ex => Assert.Fail ("should not fail", "#2"),
				() => done = true
				);
			
			Assert.IsTrue (SpinWait.SpinUntil (() => done, 500), "#3");
			Assert.IsTrue (done, "#4");
		}
		
		class Resource : IDisposable
		{
			public bool Disposed;
			
			public Resource ()
			{
			}
			
			public void Dispose ()
			{
				Disposed = true;
			}
			
			public IObservable<int> GetObservable ()
			{
				return Observable.Range (0, 3);
			}
		}
		
		[Test]
		public void Start ()
		{
			bool next = false;
			try {
				Observable.Start (() => { Thread.Sleep (200); throw new NotImplementedException (); }); // run it in another thread.
				next = true;
			} catch (NotImplementedException) {
				Assert.IsTrue (next, "#1");
			}
		}
		
		[Test]
		public void Using ()
		{
			var res = new Resource ();
			var ro = Observable.Using<int,Resource> (() => res, r => r.GetObservable ());
			Assert.IsFalse (res.Disposed, "#1");
			int i = 0;
			bool done = false;
			var dis = ro.Subscribe (v => i += v, () => done = true);
			Assert.IsTrue (SpinWait.SpinUntil (() => done, TimeSpan.FromSeconds (1)), "#2");
			Assert.IsFalse (res.Disposed, "#2");
			dis.Dispose ();
			Assert.IsTrue (res.Disposed, "#3");
		}
	}
}
