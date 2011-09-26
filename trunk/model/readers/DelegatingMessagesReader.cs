﻿using System;
using System.Collections.Generic;
using System.Text;

namespace LogJoint
{
	class DelegatingMessagesReader: IPositionedMessagesReader
	{
		public DelegatingMessagesReader(IPositionedMessagesReader underliyingReader)
		{
			if (underliyingReader == null)
				throw new ArgumentNullException("underliyingReader");
			this.underliyingReader = underliyingReader;
		}

		#region IPositionedMessagesReader Members

		public long BeginPosition
		{
			get { return underliyingReader.BeginPosition; }
		}

		public long EndPosition
		{
			get { return underliyingReader.EndPosition; }
		}

		public UpdateBoundsStatus UpdateAvailableBounds(bool incrementalMode)
		{
			return underliyingReader.UpdateAvailableBounds(incrementalMode);
		}

		public long ActiveRangeRadius
		{
			get { return underliyingReader.ActiveRangeRadius; }
		}

		public long MaximumMessageSize
		{
			get { return underliyingReader.MaximumMessageSize; }
		}

		public long PositionRangeToBytes(LogJoint.FileRange.Range range)
		{
			return underliyingReader.PositionRangeToBytes(range);
		}

		public long SizeInBytes
		{
			get { return underliyingReader.SizeInBytes; }
		}

		public IPositionedMessagesParser CreateParser(CreateParserParams p)
		{
			return underliyingReader.CreateParser(p);
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			underliyingReader.Dispose();
		}

		#endregion

		protected readonly IPositionedMessagesReader underliyingReader;
	}
}