using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using LogJoint;
using System.Reflection;
using System.IO;
using EM = LogJoint.Tests.ExpectedMessage;
using NUnit.Framework;

namespace LogJoint.Tests
{
	[Flags]
	public enum TestOptions
	{
	};

	public class ExpectedMessage
	{
		public long? Position;
		public string Text;
		public string Thread;
		public MessageTimestamp? Date;
		public MessageFlag? Type;
		public MessageFlag? ContentType;
		public int? FrameLevel;
		public Func<MessageTimestamp, bool> DateVerifier;
		public Func<string, bool> TextVerifier;
		internal bool Verified;
		public bool TextNeedsNormalization;

		public ExpectedMessage()
		{
		}

		public ExpectedMessage(string text, string thread = null, DateTime? date = null) 
		{
			Text = text;
			Thread = thread;
			Date = null;
			if (date != null)
				Date = new MessageTimestamp(date.Value);
		}
	};

	public class ExpectedLog
	{
		public ExpectedLog Add(int expectedLine, params ExpectedMessage[] expectedMessages)
		{
			foreach (var m in expectedMessages)
			{
				Assert.IsNotNull(m);
				Assert.IsFalse(this.expectedMessages.ContainsKey(expectedLine));
				this.expectedMessages.Add(expectedLine, m);
				++expectedLine;
			}
			return this;
		}

		public void StartVerification()
		{
			foreach (var m in expectedMessages.Values)
				m.Verified = false;
		}

		public void FinishVerification()
		{
			foreach (var m in expectedMessages)
				Assert.IsTrue(m.Value.Verified, string.Format("Message {0} left unverified", m.Key));
		}

		public void Verify(int actualLine, IMessage actualMessage, int actualFrameLevel)
		{
			ExpectedMessage expectedMessage;
			if (expectedMessages.TryGetValue(actualLine, out expectedMessage))
			{
				expectedMessage.Verified = true;
				Assert.IsNotNull(actualMessage);
				if (expectedMessage.Date != null)
					Assert.IsTrue(MessageTimestamp.EqualStrict(expectedMessage.Date.Value, actualMessage.Time),
						string.Format("Expected message timestamp: {0}, actual: {1}", expectedMessage.Date.Value, actualMessage.Time));
				else if (expectedMessage.DateVerifier != null)
					Assert.IsTrue(expectedMessage.DateVerifier(actualMessage.Time));
				if (expectedMessage.Thread != null)
					Assert.AreEqual(expectedMessage.Thread, actualMessage.Thread.ID);
				if (expectedMessage.Type != null)
					Assert.AreEqual(expectedMessage.Type.Value, actualMessage.Flags & MessageFlag.TypeMask);
				if (expectedMessage.ContentType != null)
					Assert.AreEqual(expectedMessage.ContentType.Value, actualMessage.Flags & MessageFlag.ContentTypeMask);
				if (expectedMessage.Text != null)
					if (expectedMessage.TextNeedsNormalization)
						Assert.AreEqual(StringUtils.NormalizeLinebreakes(expectedMessage.Text), StringUtils.NormalizeLinebreakes(actualMessage.Text.Value));
					else
						Assert.AreEqual(expectedMessage.Text, actualMessage.Text.Value);
				else if (expectedMessage.TextVerifier != null)
					Assert.IsTrue(expectedMessage.TextVerifier(actualMessage.Text.Value));
				if (expectedMessage.FrameLevel != null)
					Assert.AreEqual(expectedMessage.FrameLevel.Value, actualFrameLevel);
			}
		}

		public int Count { get { return expectedMessages.Count; } }

		Dictionary<int, ExpectedMessage> expectedMessages = new Dictionary<int, ExpectedMessage>();
	};

	public static class ReaderIntegrationTest
	{
		static ITempFilesManager tempFilesManager = new TempFilesManager();

		public static IMediaBasedReaderFactory CreateFactoryFromAssemblyResource(Assembly asm, string companyName, string formatName)
		{
			var repo = new ResourcesFormatsRepository(asm);
			ILogProviderFactoryRegistry reg = new LogProviderFactoryRegistry();
			IUserDefinedFormatsManager formatsManager = new UserDefinedFormatsManager(repo, reg, tempFilesManager);
			LogJoint.RegularGrammar.UserDefinedFormatFactory.Register(formatsManager);
			LogJoint.XmlFormat.UserDefinedFormatFactory.Register(formatsManager);
			formatsManager.ReloadFactories();
			var factory = reg.Find(companyName, formatName);
			Assert.IsNotNull(factory);
			return factory as IMediaBasedReaderFactory;
		}

		public static void Test(IMediaBasedReaderFactory factory, ILogMedia media, ExpectedLog expectation)
		{
			using (ILogSourceThreads threads = new LogSourceThreads())
			using (IPositionedMessagesReader reader = factory.CreateMessagesReader(new MediaBasedReaderParams(threads, media, tempFilesManager)))
			{
				reader.UpdateAvailableBounds(false);

				List<IMessage> msgs = new List<IMessage>();

				using (var parser = reader.CreateParser(new CreateParserParams(reader.BeginPosition)))
				{
					for (; ; )
					{
						var msg = parser.ReadNext();
						if (msg == null)
							break;
						msgs.Add(msg);
					}
				}

				expectation.StartVerification();
				int frameLevel = 0;
				for (int i = 0; i < msgs.Count; ++i)
				{
					switch (msgs[i].Flags & MessageFlag.TypeMask)
					{
						case MessageFlag.StartFrame:
							++frameLevel;
							break;
						case MessageFlag.EndFrame:
							--frameLevel;
							break;
					}

					expectation.Verify(i, msgs[i], frameLevel);
				}
				expectation.FinishVerification();
			}
		}

		public static void Test(IMediaBasedReaderFactory factory, string testLog, ExpectedLog expectation)
		{
			Test(factory, testLog, expectation, Encoding.ASCII);
		}

		public static void Test(IMediaBasedReaderFactory factory, string testLog, ExpectedLog expectation, Encoding encoding)
		{
			using (StringStreamMedia media = new StringStreamMedia(testLog, encoding))
			{
				Test(factory, media, expectation);
			}
		}

		public static void Test(IMediaBasedReaderFactory factory, System.IO.Stream testLogStream, ExpectedLog expectation)
		{
			using (StringStreamMedia media = new StringStreamMedia())
			{
				media.SetData(testLogStream);

				Test(factory, media, expectation);
			}
		}
	}

	[TestFixture]
	public class TextWriterTraceListenerIntegrationTests
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(), "Microsoft", "TextWriterTraceListener");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			ExpectedLog expectedLog = new ExpectedLog();
			expectedLog.Add(0, expectedMessages);
			DoTest(testLog, expectedLog);
		}

		[Test]
		public void TextWriterTraceListenerSmokeTest()
		{
			DoTest(
				@"
SampleApp Information: 0 : No free data file found. Going sleep.
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:10:34.3694222Z
SampleApp Information: 0 : Searching for data files
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:10:34.4294223Z
SampleApp Information: 0 : No free data file found. Going sleep.
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:10:34.4294223Z
SampleApp Information: 0 : File cannot be open which means that it was handled
  ProcessId=4756
  ThreadId=6
  DateTime=2011-07-12T12:10:35.3294235Z
SampleApp Start: 0 : Test frame 
  ProcessId=4756
  ThreadId=6
  DateTime=2011-07-12T12:10:35.3294260Z
SampleApp Stop: 0 : 
  ProcessId=4756
  ThreadId=6
  DateTime=2011-07-12T12:10:35.3294260Z
SampleApp Information: 0 : Timestamp parsed and ignored
  ProcessId=4756
  ThreadId=6
  DateTime=2011-07-12T12:10:35.3294235Z
  Timestamp=232398
				",
				new EM("No free data file found. Going sleep.", "4756 - 7"),
				new EM("Searching for data files", "4756 - 7", null),
				new EM("No free data file found. Going sleep.", "4756 - 7", null),
				new EM("File cannot be open which means that it was handled", "4756 - 6", null),
				new EM("Test frame", "4756 - 6", null) { Type = MessageFlag.Content },
				new EM("", "4756 - 6", null) { Type = MessageFlag.Content },
				new EM("Timestamp parsed and ignored", "4756 - 6", null)
			);
		}
		
		[Test]
		public void TextWriterTraceListener_FindPrevMessagePositionTest()
		{
			var testLog = 
@"SampleApp Information: 0 : No free data file found. Going sleep.
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:10:00.0000000Z
SampleApp Information: 0 : Searching for data files
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:12:00.0000000Z
SampleApp Information: 0 : No free data file found. Going sleep.
  ProcessId=4756
  ThreadId=7
  DateTime=2011-07-12T12:14:00.0000000Z
";
			using (StringStreamMedia media = new StringStreamMedia(testLog, Encoding.ASCII))
			using (ILogSourceThreads threads = new LogSourceThreads())
			using (IPositionedMessagesReader reader = CreateFactory().CreateMessagesReader(new MediaBasedReaderParams(threads, media, new TempFilesManager())))
			{
				reader.UpdateAvailableBounds(false);
				long? prevMessagePos = PositionedMessagesUtils.FindPrevMessagePosition(reader, 0x0000004A);
				Assert.IsTrue(prevMessagePos.HasValue);
				Assert.AreEqual(0, prevMessagePos.Value);
			}
		}


	}

	[TestFixture]
	public class XmlWriterTraceListenerIntegrationTests
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(),
				"Microsoft", "XmlWriterTraceListener");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			DoTest(testLog, new ExpectedLog().Add(0, expectedMessages));
		}

		[Test]
		public void XmlWriterTraceListenerSmokeTest()
		{
			DoTest(
				@"
<E2ETraceEvent xmlns='http://schemas.microsoft.com/2004/06/E2ETraceEvent'>
 <System xmlns='http://schemas.microsoft.com/2004/06/windows/eventlog/system'>
  <EventID>1</EventID>
  <Type>3</Type>
  <SubType Name='Error'>0</SubType>
  <Level>2</Level>
  <TimeCreated SystemTime='2007-01-16T15:20:07.0781250Z' />
  <Source Name='TestApp' />
  <Correlation ActivityID='{00000000-0000-0000-0000-000000000000}' />
  <Execution ProcessName='trace_cs.vshost' ProcessID='5620' ThreadID='10' />
  <Channel/>
  <Computer>TEST</Computer>
 </System>
 <ApplicationData>Error message.</ApplicationData>
</E2ETraceEvent>
<E2ETraceEvent xmlns='http://schemas.microsoft.com/2004/06/E2ETraceEvent'>
 <System xmlns='http://schemas.microsoft.com/2004/06/windows/eventlog/system'>
  <EventID>1</EventID>
  <Type>3</Type>
  <SubType Name='Information'>0</SubType>
  <Level>2</Level>
  <TimeCreated SystemTime='2007-01-16T15:20:07.0781250Z' />
  <Source Name='TestApp' />
  <Correlation ActivityID='{00000000-0000-0000-0000-000000000000}' />
  <Execution ProcessName='trace_cs.vshost' ProcessID='5620' ThreadID='20' />
  <Channel/>
  <Computer>TEST</Computer>
 </System>
 <ApplicationData>message 2</ApplicationData>
</E2ETraceEvent>
				",
				new EM("Error message.", "trace_cs.vshost(5620), 10") { ContentType = MessageFlag.Error },
				new EM("message 2", "trace_cs.vshost(5620), 20") { ContentType = MessageFlag.Info }
			);
		}

		[Test]
		public void RealLogTest()
		{
			ReaderIntegrationTest.Test(
				CreateFactory(),
				Assembly.GetExecutingAssembly().GetManifestResourceStream(
					Assembly.GetExecutingAssembly().GetManifestResourceNames().SingleOrDefault(n => n.Contains("XmlWriterTraceListener1.xml"))),
				new ExpectedLog()
				.Add(0, 
					new EM("Void Main(System.String[])", "SampleLoggingApp(1956), 1") { Type = MessageFlag.StartFrame },
					new EM("----- Sample application started 07/24/2011 12:37:26 ----", "SampleLoggingApp(1956), 1")
				)
				.Add(8,
					new EM("Void Producer()", "SampleLoggingApp(1956), 6") { Type = MessageFlag.StartFrame }
				)
				.Add(11,
					new EM("", "SampleLoggingApp(1956), 1") { Type = MessageFlag.EndFrame }
				)
			);			
		}
	}

	[TestFixture]
	public class HTTPERRIntegrationTests
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(), "Microsoft", "HTTPERR");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			ExpectedLog expectedLog = new ExpectedLog();
			expectedLog.Add(0, expectedMessages);
			DoTest(testLog, expectedLog);
		}

		[Test]
		public void HTTPERR_SmokeTest()
		{
			DoTest(
				@"
#Software: Microsoft HTTP API 2.0
#Version: 1.0
#Date: 2011-12-08 06:06:19
#Fields: date time c-ip c-port s-ip s-port cs-version cs-method cs-uri sc-status s-siteid s-reason s-queuename
2011-12-08 06:06:19 192.168.150.1 2774 192.168.150.122 2869 HTTP/1.1 NOTIFY /upnp/eventing/gerpeyxyas - - Connection_Abandoned_By_ReqQueue -
#Software: Microsoft HTTP API 2.0
#Version: 1.0
#Date: 2012-02-06 13:31:17
#Fields: date time c-ip c-port s-ip s-port cs-version cs-method cs-uri sc-status s-siteid s-reason s-queuename
2012-02-06 13:31:17 2001:4898:0:fff:0:5efe:10.164.167.30%0 54697 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
2012-02-06 13:31:17 2001:4898:0:fff:0:5efe:10.164.167.30%0 54699 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
2012-02-06 13:45:43 2001:4898:0:fff:0:5efe:10.164.167.30%0 54856 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
2012-02-06 13:51:58 2001:4898:0:fff:0:5efe:10.164.167.30%0 54863 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
2012-02-06 13:59:18 2001:4898:0:fff:0:5efe:10.164.167.30%0 54865 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
2012-02-06 14:11:23 2001:4898:0:fff:0:5efe:10.164.167.30%0 54875 2001:4898:0:fff:0:5efe:10.85.220.4%0 80 - - - - - Timer_ConnectionIdle -
#Software: Microsoft HTTP API 2.0
#Version: 1.0
#Date: 2012-02-29 12:48:58
#Fields: date time c-ip c-port s-ip s-port cs-version cs-method cs-uri sc-status s-siteid s-reason s-queuename
2012-02-29 12:48:58 10.36.206.59 50228 10.85.220.5 80 HTTP/1.0 GET / 404 - NotFound -
2012-02-29 12:49:34 10.36.206.59 50330 10.85.220.5 80 HTTP/1.1 GET / 404 - NotFound -
2012-02-29 12:49:50 10.36.206.59 50422 10.85.220.5 80 HTTP/1.1 GET / 404 - NotFound -
				",
				new EM("Client: 192.168.150.1:2774, Server: 192.168.150.122:2869, Protocol: HTTP/1.1, Verb: NOTIFY, URL: /upnp/eventing/gerpeyxyas, Status: -, SideID: -, Reason: Connection_Abandoned_By_ReqQueue", null, new DateTime(2011, 12, 8, 6, 6, 19)),
				new EM("Client: 2001:4898:0:fff:0:5efe:10.164.167.30%0:54697, Server: 2001:4898:0:fff:0:5efe:10.85.220.4%0:80, Protocol: -, Verb: -, URL: -, Status: -, SideID: -, Reason: Timer_ConnectionIdle", null, new DateTime(2012, 2, 6, 13, 31, 17))
			);
		}
	}

	[TestFixture]
	public class IISIntegrationTests
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(), "Microsoft", "IIS");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			ExpectedLog expectedLog = new ExpectedLog();
			expectedLog.Add(0, expectedMessages);
			DoTest(testLog, expectedLog);
		}

		[Test]
		public void IIS_SmokeTest()
		{
			DoTest(
@"192.168.114.201, -, 03/20/01, 7:55:20, W3SVC2, SERVER, 172.21.13.45, 4502, 163, 3223, 200, 0, GET, /DeptLogo.gif, -,
192.168.110.54, -, 03/20/01, 7:57:20, W3SVC2, SERVER, 172.21.13.45, 411, 221, 1967, 200, 0, GET, /style.css, -,
192.168.1.109, -, 6/10/2009, 10:11:59, W3SVC1893743816, SPUTNIK01, 192.168.1.109, 0, 261, 1913, 401, 2148074254, GET, /, -, 
192.168.1.109, -, 6/10/2009, 10:11:59, W3SVC1893743816, SPUTNIK01, 192.168.1.109, 15, 363, 2113, 401, 0, GET, /, -, 
192.168.1.109, NT AUTHORITY\LOCAL SERVICE, 6/10/2009, 10:11:59, W3SVC1893743816, SPUTNIK01, 192.168.1.109, 46, 379, 336, 200, 0, GET, /, -, 
192.168.1.109, -, 6/10/2009, 10:11:59, W3SVC1893743816, SPUTNIK01, 192.168.1.109, 0, 336, 1889, 401, 2148074254, POST, /_vti_bin/sitedata.asmx, -,
				",
				new EM("ClientIP=192.168.114.201, Service=W3SVC2, Server=SERVER, ServerIP=172.21.13.45, TimeTaken=4502, ClientBytes=163, ServerBytes=3223, ServiceStatus=200, WindowsStatus=0, Request=GET, Target=/DeptLogo.gif, body=", null, new DateTime(2001, 3, 20, 7, 55, 20)),
				new EM("ClientIP=192.168.110.54, Service=W3SVC2, Server=SERVER, ServerIP=172.21.13.45, TimeTaken=411, ClientBytes=221, ServerBytes=1967, ServiceStatus=200, WindowsStatus=0, Request=GET, Target=/style.css, body=", null, new DateTime(2001, 3, 20, 7, 57, 20)) { ContentType = MessageFlag.Info },
				new EM("ClientIP=192.168.1.109, Service=W3SVC1893743816, Server=SPUTNIK01, ServerIP=192.168.1.109, TimeTaken=0, ClientBytes=261, ServerBytes=1913, ServiceStatus=401, WindowsStatus=2148074254, Request=GET, Target=/, body=", null, new DateTime(2009, 6, 10, 10, 11, 59)) { ContentType = MessageFlag.Warning }
			);
		}

		[Test]
		public void IIS7_Test()
		{
			DoTest(
@"::1, -, 2/23/2013, 12:12:46, W3SVC1, MSA3644463, ::1, 324, 285, 935, 200, 0, GET, /, -,
::1, -, 2/23/2013, 12:12:46, W3SVC1, MSA3644463, ::1, 5, 337, 185196, 200, 0, GET, /welcome.png, -,
::1, -, 2/23/2013, 12:12:46, W3SVC1, MSA3644463, ::1, 3, 238, 5375, 404, 2, GET, /favicon.ico, -,
::1, -, 2/23/2013, 12:12:50, W3SVC1, MSA3644463, ::1, 1, 238, 5375, 404, 2, GET, /favicon.ico, -,
",
				new EM("ClientIP=::1, Service=W3SVC1, Server=MSA3644463, ServerIP=::1, TimeTaken=324, ClientBytes=285, ServerBytes=935, ServiceStatus=200, WindowsStatus=0, Request=GET, Target=/, body=", null, new DateTime(2013, 2, 23, 12, 12, 46)),
				new EM("ClientIP=::1, Service=W3SVC1, Server=MSA3644463, ServerIP=::1, TimeTaken=5, ClientBytes=337, ServerBytes=185196, ServiceStatus=200, WindowsStatus=0, Request=GET, Target=/welcome.png, body=", null, new DateTime(2013, 2, 23, 12, 12, 46)),
				new EM("ClientIP=::1, Service=W3SVC1, Server=MSA3644463, ServerIP=::1, TimeTaken=3, ClientBytes=238, ServerBytes=5375, ServiceStatus=404, WindowsStatus=2, Request=GET, Target=/favicon.ico, body=", null, new DateTime(2013, 2, 23, 12, 12, 46)) { ContentType = MessageFlag.Warning },
				new EM("ClientIP=::1, Service=W3SVC1, Server=MSA3644463, ServerIP=::1, TimeTaken=1, ClientBytes=238, ServerBytes=5375, ServiceStatus=404, WindowsStatus=2, Request=GET, Target=/favicon.ico, body=", null, new DateTime(2013, 2, 23, 12, 12, 50)) { ContentType = MessageFlag.Warning }
			);
		}

	}

	[TestFixture]
	public class WindowsUpdateIntegrationTests
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(), "Microsoft", "WindowsUpdate.log");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			ExpectedLog expectedLog = new ExpectedLog();
			expectedLog.Add(0, expectedMessages);
			DoTest(testLog, expectedLog);
		}

		[Test]
		public void WindowsUpdate_SmokeTest()
		{
			DoTest(
				@"
2013-01-27	10:55:33:204	1160	3ca0	DnldMgr	  * BITS job initialized, JobId = {082DB2AF-902B-4457-810C-62B6E2D3A034}
2013-01-27	10:55:33:207	1160	3ca0	DnldMgr	  * Downloading from http://sup-eu1-nlb.europe.corp.microsoft.com/Content/E7/BA6933C31C37166A9CAAC87AA635AB5A5BFDF7E7.exe to C:\windows\SoftwareDistribution\Download\29e9d7b4b531db72a29aea5b8094b5cd\ba6933c31c37166a9caac87aa635ab5a5bfdf7e7 (full file).
2013-01-27	10:55:33:210	1160	3ca0	Agent	*********
2013-01-27	10:55:33:210	1160	3ca0	Agent	**  END  **  Agent: Downloading updates [CallerId = AutomaticUpdates]
2013-01-27	10:55:33:210	1160	3ca0	Agent	*************
2013-01-27	10:55:33:210	1160	2320	AU	Successfully wrote event for AU health state:0
2013-01-27	10:55:38:171	1160	3ca0	Report	REPORT EVENT: {023764A7-9115-43D9-966E-18496EE41A09}	2013-01-27 10:55:33:171+0100	1	147	101	{00000000-0000-0000-0000-000000000000}	0	0	AutomaticUpdates	Success	Software Synchronization	Windows Update Client successfully detected 1 updates.
2013-01-27	10:55:38:171	1160	3ca0	Report	REPORT EVENT: {96655A05-A1D9-450B-8A1A-FBFE75A860C3}	2013-01-27 10:55:33:172+0100	1	156	101	{00000000-0000-0000-0000-000000000000}	0	0	AutomaticUpdates	Success	Pre-Deployment Check	Reporting client status.
2013-01-27	10:55:38:171	1160	3ca0	Report	CWERReporter finishing event handling. (00000000)
2013-01-27	10:55:44:276	1160	4348	DnldMgr	BITS job {082DB2AF-902B-4457-810C-62B6E2D3A034} completed successfully
				",
				new EM(@"DnldMgr   * BITS job initialized, JobId = {082DB2AF-902B-4457-810C-62B6E2D3A034}", "Process: 1160; Thread: 3ca0", new DateTime(2013, 1, 27, 10, 55, 33, 204)),
				new EM(@"DnldMgr   * Downloading from http://sup-eu1-nlb.europe.corp.microsoft.com/Content/E7/BA6933C31C37166A9CAAC87AA635AB5A5BFDF7E7.exe to C:\windows\SoftwareDistribution\Download\29e9d7b4b531db72a29aea5b8094b5cd\ba6933c31c37166a9caac87aa635ab5a5bfdf7e7 (full file).", "Process: 1160; Thread: 3ca0", new DateTime(2013, 1, 27, 10, 55, 33, 207)),
				new EM(@"Agent *********", "Process: 1160; Thread: 3ca0", new DateTime(2013, 1, 27, 10, 55, 33, 210)),
				new EM(@"Agent **  END  **  Agent: Downloading updates [CallerId = AutomaticUpdates]", "Process: 1160; Thread: 3ca0", new DateTime(2013, 1, 27, 10, 55, 33, 210)),
				new EM(@"Agent *************", "Process: 1160; Thread: 3ca0", new DateTime(2013, 1, 27, 10, 55, 33, 210)),
				new EM(@"AU Successfully wrote event for AU health state:0", "Process: 1160; Thread: 2320", new DateTime(2013, 1, 27, 10, 55, 33, 210))
			);
		}
	}

	[TestFixture]
	public class W3CExtendedLogFormatTest
	{
		IMediaBasedReaderFactory CreateFactory()
		{
			return ReaderIntegrationTest.CreateFactoryFromAssemblyResource(Assembly.GetExecutingAssembly(), "W3C", "Extended Log Format");
		}

		void DoTest(string testLog, ExpectedLog expectedLog)
		{
			ReaderIntegrationTest.Test(CreateFactory(), testLog, expectedLog);
		}

		void DoTest(string testLog, params ExpectedMessage[] expectedMessages)
		{
			ExpectedLog expectedLog = new ExpectedLog();
			expectedLog.Add(0, expectedMessages);
			DoTest(testLog, expectedLog);
		}

		[Test]
		public void W3CExtendedLogFormat_SmokeTest()
		{
			DoTest(
@"#Software: Microsoft Internet Information Services 7.5
#Version: 1.0
#Date: 2013-02-07 08:35:37
#Fields: date time s-ip cs-method cs-uri-stem cs-uri-query s-port cs-username c-ip cs(User-Agent) sc-status sc-substatus sc-win32-status time-taken
2013-02-07 08:35:37 fe80::5d3d:c591:3026:46ee%14 OPTIONS /System32/TPHDEXLG64.exe - 80 - fe80::5d3d:c591:3026:46ee%14 Microsoft-WebDAV-MiniRedir/6.1.7601 200 0 0 340
2013-02-07 08:35:37 fe80::5d3d:c591:3026:46ee%14 PROPFIND /System32/TPHDEXLG64.exe - 80 - fe80::5d3d:c591:3026:46ee%14 Microsoft-WebDAV-MiniRedir/6.1.7601 404 0 2 4
2013-02-07 08:35:37 fe80::5d3d:c591:3026:46ee%14 PROPFIND /System32 - 80 - fe80::5d3d:c591:3026:46ee%14 Microsoft-WebDAV-MiniRedir/6.1.7601 404 0 2 1",
				new EM("fe80::5d3d:c591:3026:46ee%14 OPTIONS /System32/TPHDEXLG64.exe - 80 - fe80::5d3d:c591:3026:46ee%14 Microsoft-WebDAV-MiniRedir/6.1.7601 200 0 0 340", null, new DateTime(2013, 02, 07, 8, 35, 37)),
				new EM("fe80::5d3d:c591:3026:46ee%14 PROPFIND /System32/TPHDEXLG64.exe - 80 - fe80::5d3d:c591:3026:46ee%14 Microsoft-WebDAV-MiniRedir/6.1.7601 404 0 2 4", null, new DateTime(2013, 02, 07, 8, 35, 37))
			);
		}

	}

}
