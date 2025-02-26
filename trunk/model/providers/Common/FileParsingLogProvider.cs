using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace LogJoint
{
	public class StreamBasedFormatInfo
	{
		public readonly MessagesReaderExtensions.XmlInitializationParams ExtensionsInitData;

		public StreamBasedFormatInfo(MessagesReaderExtensions.XmlInitializationParams extensionsInitData)
		{
			this.ExtensionsInitData = extensionsInitData;
		}
	};

	public class StreamLogProvider : AsyncLogProvider, ISaveAs
	{
		ILogMedia media;
		readonly IPositionedMessagesReader reader;
		bool isSavableAs;
		string suggestedSaveAsFileName;
		string taskbarFileName;

		public StreamLogProvider(
			ILogProviderHost host, 
			ILogProviderFactory factory,
			IConnectionParams connectParams,
			StreamBasedFormatInfo formatInfo,
			Type readerType
		):
			base (host, factory, connectParams)
		{
			using (host.Trace.NewFrame)
			{
				host.Trace.Info("readerType={0}", readerType);

				if (connectionParams[ConnectionParamsUtils.RotatedLogFolderPathConnectionParam] != null)
					media = new RollingFilesMedia(
						LogMedia.FileSystemImpl.Instance,
						readerType, 
						formatInfo,
						host.Trace,
						new GenericRollingMediaStrategy(connectionParams[ConnectionParamsUtils.RotatedLogFolderPathConnectionParam]),
						host.TempFilesManager
					);
				else
					media = new SimpleFileMedia(connectParams);

				reader = (IPositionedMessagesReader)Activator.CreateInstance(
					readerType, new MediaBasedReaderParams(this.threads, media, host.TempFilesManager, settingsAccessor: host.GlobalSettings), formatInfo);

				ITimeOffsets initialTimeOffset;
				if (LogJoint.TimeOffsets.TryParse(
					connectionParams[ConnectionParamsUtils.TimeOffsetConnectionParam] ?? "", out initialTimeOffset))
				{
					reader.TimeOffsets = initialTimeOffset;
				}

				StartAsyncReader("Reader thread: " + connectParams.ToString(), reader);

				InitPathDependentMembers(connectParams);
			}
		}

		public override async Task Dispose()
		{
			if (IsDisposed)
				return;
			string tmpFileName = connectionParamsReadonlyView[ConnectionParamsUtils.PathConnectionParam];
			if (tmpFileName != null && !host.TempFilesManager.IsTemporaryFile(tmpFileName))
				tmpFileName = null;
			await base.Dispose();
			media?.Dispose();
			reader?.Dispose();
			if (tmpFileName != null)
			{
				File.Delete(tmpFileName);
			}
		}

		bool ISaveAs.IsSavableAs
		{
			get { return isSavableAs; }
		}

		string ISaveAs.SuggestedFileName
		{
			get { return suggestedSaveAsFileName; }
		}

		void ISaveAs.SaveAs(string fileName)
		{
			CheckDisposed();
			string srcFileName = connectionParamsReadonlyView[ConnectionParamsUtils.PathConnectionParam];
			if (srcFileName == null)
				return;
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(fileName));
			System.IO.File.Copy(srcFileName, fileName, true);
		}

		void InitPathDependentMembers(IConnectionParams connectParams)
		{
			isSavableAs = false;
			taskbarFileName = null;
			bool isTempFile = false;
			string guessedFileName = null;

			string fname = connectParams[ConnectionParamsUtils.PathConnectionParam];
			if (fname != null)
			{
				isTempFile = host.TempFilesManager.IsTemporaryFile(fname);
				isSavableAs = isTempFile;
			}
			string connectionIdentity = connectParams[ConnectionParamsUtils.IdentityConnectionParam];
			if (connectionIdentity != null)
				guessedFileName = ConnectionParamsUtils.GuessFileNameFromConnectionIdentity(connectionIdentity);
			if (isSavableAs)
			{
				suggestedSaveAsFileName = SanitizeSuggestedFileName(guessedFileName);
			}
			taskbarFileName = guessedFileName;
		}

		public override string GetTaskbarLogName()
		{
			return taskbarFileName;
		}

		static string SanitizeSuggestedFileName(string str)
		{
			var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
			return new string(str.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
		}
	};
}
