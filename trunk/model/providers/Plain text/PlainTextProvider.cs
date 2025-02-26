using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Threading;
using LogJoint.RegularExpressions;

namespace LogJoint.PlainText
{
	class LogProvider: LiveLogProvider
	{
		string fileName;

		public LogProvider(ILogProviderHost host, string fileName)
			:
			base(host, PlainText.Factory.Instance, 
				ConnectionParamsUtils.CreateConnectionParamsWithIdentity(ConnectionParamsUtils.CreateFileBasedConnectionIdentityFromFileName(fileName)))
		{
			this.fileName = fileName;
			StartLiveLogThread(string.Format("'{0}' listening thread", fileName));
		}

		public override string GetTaskbarLogName()
		{
			return ConnectionParamsUtils.GuessFileNameFromConnectionIdentity(fileName);
		}

		protected override void LiveLogListen(CancellationToken stopEvt, LiveLogXMLWriter output)
		{
			using (ILogMedia media = new SimpleFileMedia(
				LogMedia.FileSystemImpl.Instance, 
				SimpleFileMedia.CreateConnectionParamsFromFileName(fileName)))
			using (FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(fileName), 
				Path.GetFileName(fileName)))
			using (AutoResetEvent fileChangedEvt = new AutoResetEvent(true))
			{
				IMessagesSplitter splitter = new MessagesSplitter(
					new StreamTextAccess(media.DataStream, Encoding.ASCII, TextStreamPositioningParams.Default),
					RegexFactory.Instance.Create(@"^(?<body>.+)$", ReOptions.Multiline)
				);

				watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
				watcher.Changed += delegate(object sender, FileSystemEventArgs e)
				{
					fileChangedEvt.Set();
				};
				//watcher.EnableRaisingEvents = true;

				long lastLinePosition = 0;
				long lastStreamLength = 0;
				WaitHandle[] events = new WaitHandle[] { stopEvt.WaitHandle, fileChangedEvt };

				var capture = new TextMessageCapture();

				for (; ; )
				{
					if (WaitHandle.WaitAny(events, 250, false) == 0)
						break;

					media.Update();

					if (media.Size == lastStreamLength)
						continue;

					lastStreamLength = media.Size;

					DateTime lastModified = media.LastModified;

					splitter.BeginSplittingSession(new FileRange.Range(0, lastStreamLength), lastLinePosition, MessagesParserDirection.Forward);
					try
					{
						for (; ; )
						{
							if (!splitter.GetCurrentMessageAndMoveToNextOne(capture))
								break;
							lastLinePosition = capture.BeginPosition;

							XmlWriter writer = output.BeginWriteMessage(false);
							writer.WriteStartElement("m");
							writer.WriteAttributeString("d", Listener.FormatDate(lastModified));
							writer.WriteString(capture.MessageHeader);
							writer.WriteEndElement();
							output.EndWriteMessage();
						}
					}
					finally
					{
						splitter.EndSplittingSession();
					}
				}
			}
		}

	}

	class Factory : IFileBasedLogProviderFactory
	{
		public static readonly Factory Instance = new Factory();

		[RegistrationMethod]
		public static void Register(ILogProviderFactoryRegistry registry)
		{
			registry.Register(Instance);
		}

		#region IFileReaderFactory

		public IEnumerable<string> SupportedPatterns { get { yield break; } }

		public IConnectionParams CreateParams(string fileName)
		{
			return ConnectionParamsUtils.CreateFileBasedConnectionParamsFromFileName(fileName);
		}

		public IConnectionParams CreateRotatedLogParams(string folder)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region ILogReaderFactory Members

		public string CompanyName
		{
			get { return "LogJoint"; }
		}

		public string FormatName
		{
			get { return "Text file"; }
		}

		public string FormatDescription
		{
			get { return "Reads all the lines from any text file without any additional parsing. The messages get the timestamp equal to the file modification date. When tracking live file this timestamp may change."; }
		}

		string ILogProviderFactory.UITypeKey { get { return StdProviderFactoryUIs.FileBasedProviderUIKey; } }

		public string GetUserFriendlyConnectionName(IConnectionParams connectParams)
		{
			return ConnectionParamsUtils.GetFileOrFolderBasedUserFriendlyConnectionName(connectParams);
		}

		public string GetConnectionId(IConnectionParams connectParams)
		{
			return ConnectionParamsUtils.GetConnectionIdentity(connectParams);
		}

		public IConnectionParams GetConnectionParamsToBeStoredInMRUList(IConnectionParams originalConnectionParams)
		{
			return ConnectionParamsUtils.RemoveNonPersistentParams(originalConnectionParams.Clone(true), TempFilesManager.GetInstance());
		}

		public ILogProvider CreateFromConnectionParams(ILogProviderHost host, IConnectionParams connectParams)
		{
			return new LogProvider(host, connectParams[ConnectionParamsUtils.PathConnectionParam]);
		}

		public IFormatViewOptions ViewOptions { get { return FormatViewOptions.NoRawView; } }

		public LogProviderFactoryFlag Flags
		{
			get { return LogProviderFactoryFlag.None; }
		}

		#endregion
	};
}
