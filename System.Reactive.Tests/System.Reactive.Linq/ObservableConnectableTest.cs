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
	public class ObservableConnectableTest
	{
		[Test]
		public void Publish ()
		{
			var source = Observable.Range (2, 3);

			int result = 1;
			bool started = false;
			var published = source.Publish ();
			var pdis1 = published.Subscribe (i => { started = true; result *= i; });
			Assert.IsFalse (started, "#1");
			var cdis = published.Connect ();
			Assert.IsTrue (started, "#2");
			Thread.Sleep (100); // should be enough to finish publishing source.
			Assert.AreEqual (24, result, "#3");
			var pdis2 = published.Subscribe (i => { started = true; result *= i; });
			Thread.Sleep (50); // should be enough to make some change (if it were to happen).
			Assert.AreEqual (24, result, "#3"); // but it should not happen.
			cdis.Dispose (); // disconnect
			pdis1.Dispose ();
			pdis2.Dispose ();
		}

		[Test]
		public void PublishConnectTwice ()
		{
			var source = Observable.Range (2, 3);

			int result = 1;
			bool started = false;
			var published = source.Publish ();
			var pdis1 = published.Subscribe (i => { started = true; result *= i; });
			Assert.IsFalse (started, "#1");
			var cdis1 = published.Connect ();
			var cdis2 = published.Connect (); // no error
			Assert.AreEqual (cdis1, cdis2, "#2");
			cdis1.Dispose ();
		}
		[Test] // FIXME: this test is somewhat processing-speed dependent. Sleep() is not enough very often.
		public void PublishReconnect ()
		{
			var source = Observable.Interval (TimeSpan.FromMilliseconds (50));

			int result = 0;
			bool started = false;
			var published = source.Publish ();
			var pdis1 = published.Subscribe (i => result++);
			var cdis1 = published.Connect ();
			Thread.Sleep (200); // should be enough to receive some events
			Assert.IsTrue (result > 0, "#1");
			cdis1.Dispose (); // disconnect
			int oldResult = result;
			Thread.Sleep (200); // should be enough to raise interval event if it were active (which should *not*)
			Assert.AreEqual (oldResult, result, "#2");
			var cdis2 = published.Connect ();
			Thread.Sleep (400); // should be enough to receive some events
			Assert.IsTrue (result > oldResult, "#3");
			cdis2.Dispose ();
		}
	}
}
