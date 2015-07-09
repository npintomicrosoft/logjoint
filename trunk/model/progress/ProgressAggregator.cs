﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LogJoint.Progress
{
	public class ProgressAggregator : IProgressAggregator
	{
		readonly HashSet<ProgressEventsSink> sinks = new HashSet<ProgressEventsSink>();
		readonly object sync = new object();
		bool isProgressActive;

		public ProgressAggregator(IHeartBeatTimer timer)
		{
			timer.OnTimer += (s, e) =>
			{
				if (e.IsNormalUpdate)
					Update();
			};
		}

		public IProgressEventsSink CreateProgressSink()
		{
			return new ProgressEventsSink(this);
		}

		public event EventHandler<EventArgs> ProgressStarted;

		public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

		public event EventHandler<EventArgs> ProgressEnded;

		void Add(ProgressEventsSink sink)
		{
			lock (sync)
				sinks.Add(sink);
		}

		void Remove(ProgressEventsSink sink)
		{
			lock (sync)
				sinks.Remove(sink);
		}

		void Update()
		{
			bool active;
			double progress;
			lock (sync)
			{
				active = sinks.Count != 0;
				if (active)
					progress = sinks.Sum(s => s.Value) / sinks.Count;
				else
					progress = 0;
			}
			EventHandler<EventArgs> startStop = null;
			if (active != isProgressActive)
				startStop = active ? ProgressStarted : ProgressEnded;
			isProgressActive = active;
			if (startStop != null)
				startStop(this, EventArgs.Empty);
			if (active && ProgressChanged != null)
				ProgressChanged(this, new ProgressChangedEventArgs((int)(progress * 100f), null));
		}

		class ProgressEventsSink : IProgressEventsSink
		{
			readonly ProgressAggregator owner;
			double value;

			public ProgressEventsSink(ProgressAggregator owner)
			{
				this.owner = owner;
				owner.Add(this);
			}

			public double Value { get { return value; } }

			void IProgressEventsSink.SetValue(double value)
			{
				this.value = value;
			}

			void IDisposable.Dispose()
			{
				owner.Remove(this);
			}
		};
	}
}