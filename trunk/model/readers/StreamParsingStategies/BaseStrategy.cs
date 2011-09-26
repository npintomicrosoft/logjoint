﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LogJoint.RegularExpressions;

namespace LogJoint.StreamParsingStrategies
{
	public class BaseStrategy
	{
		public BaseStrategy(ILogMedia media, Encoding encoding, IRegex headerRe)
		{
			this.media = media;
			this.encoding = encoding;
			this.headerRe = headerRe;
		}

		public virtual void ParserCreated(CreateParserParams p) { }
		public virtual void ParserDestroyed() { }
		public virtual MessageBase ReadNext() { return null; }

		protected readonly ILogMedia media;
		protected readonly Encoding encoding;
		protected readonly IRegex headerRe;
	}
}