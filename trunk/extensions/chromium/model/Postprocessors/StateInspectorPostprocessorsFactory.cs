﻿using System.Threading.Tasks;
using System.Threading;
using System.Xml.Linq;
using System.Linq;
using LogJoint.Postprocessing;
using LogJoint.Analytics;
using LogJoint.Postprocessing.StateInspector;
using CDL = LogJoint.Chromium.ChromeDebugLog;
using WRD = LogJoint.Chromium.WebrtcInternalsDump;
using Sym = LogJoint.Symphony.Rtc;
using LogJoint.Analytics.StateInspector;

namespace LogJoint.Chromium.StateInspector
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreateChromeDebugPostprocessor();
		ILogSourcePostprocessor CreateWebRtcInternalsDumpPostprocessor();
		ILogSourcePostprocessor CreateSymphontRtcPostprocessor();
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		readonly static string typeId = PostprocessorIds.StateInspector;
		readonly static string caption = PostprocessorIds.StateInspector;

		public PostprocessorsFactory()
		{
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateChromeDebugPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption, 
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForChromeDebug(new CDL.Reader(i.CancellationToken).Read(i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), i.OutputFileName, i.CancellationToken, i.TemplatesTracker, i.InputContentsEtagAttr)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateWebRtcInternalsDumpPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption,
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForWebRTCDump(new WRD.Reader(i.CancellationToken).Read(i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), i.OutputFileName, i.CancellationToken, i.TemplatesTracker, i.InputContentsEtagAttr)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateSymphontRtcPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption,
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForSymRTC(new Sym.Reader(i.CancellationToken).Read(i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), i.OutputFileName, i.CancellationToken, i.TemplatesTracker, i.InputContentsEtagAttr)
			);
		}

		IStateInspectorOutput DeserializeOutput(XDocument data, ILogSource forSource)
		{
			return new StateInspectorOutput(data, forSource);
		}

		async static Task RunForChromeDebug(
			IEnumerableAsync<CDL.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			XAttribute contentsEtagAttr
		)
		{
			var inputMultiplexed = input.Multiplex();

			IPrefixMatcher matcher = new PrefixMatcher();
			var logMessages = CDL.Helpers.MatchPrefixes(inputMultiplexed, matcher).Multiplex();

			CDL.IWebRtcStateInspector webRtcStateInspector = new CDL.WebRtcStateInspector(matcher);

			var webRtcEvts = webRtcStateInspector.GetEvents(logMessages);

			Sym.IMeetingsStateInspector symMeetingsStateInspector = new Sym.MeetingsStateInspector(matcher);
			var symMessages = Sym.Helpers.MatchPrefixes((new Sym.Reader()).FromChromeDebugLog(inputMultiplexed), matcher).Multiplex();

			var symMeetingEvents = symMeetingsStateInspector.GetEvents(symMessages);

			matcher.Freeze();

			var events = EnumerableAsync.Merge(
				webRtcEvts.Select(ConvertTriggers<CDL.Message>),
				symMeetingEvents.Select(ConvertTriggers<Sym.Message>)
			)
			.ToFlatList();

			await Task.WhenAll(events, symMessages.Open(), logMessages.Open(), inputMultiplexed.Open());

			if (cancellation.IsCancellationRequested)
				return;

			if (templatesTracker != null)
				(await events).ForEach(e => templatesTracker.RegisterUsage(e.TemplateId));

			StateInspectorOutput.SerializePostprocessorOutput(await events, null, contentsEtagAttr).Save(outputFileName);
		}

		async static Task RunForWebRTCDump(
			IEnumerableAsync<WRD.Message[]> input,
			string outputFileName,
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			XAttribute contentsEtagAttr
		)
		{
			IPrefixMatcher matcher = new PrefixMatcher();
			var logMessages = WRD.Helpers.MatchPrefixes(input, matcher).Multiplex();

			WRD.IWebRtcStateInspector webRtcStateInspector = new WRD.WebRtcStateInspector(matcher);

			var webRtcEvts = webRtcStateInspector.GetEvents(logMessages);

			matcher.Freeze();

			var events = EnumerableAsync.Merge(
				webRtcEvts
			)
			.Select(ConvertTriggers<WRD.Message>)
			.ToFlatList();

			await Task.WhenAll(events, logMessages.Open());

			if (cancellation.IsCancellationRequested)
				return;

			if (templatesTracker != null)
				(await events).ForEach(e => templatesTracker.RegisterUsage(e.TemplateId));

			StateInspectorOutput.SerializePostprocessorOutput(await events, null, contentsEtagAttr).Save(outputFileName);
		}

		async static Task RunForSymRTC(
			IEnumerableAsync<Sym.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			XAttribute contentsEtagAttr
		)
		{
			IPrefixMatcher matcher = new PrefixMatcher();
			var logMessages = Sym.Helpers.MatchPrefixes(input, matcher).Multiplex();

			Sym.IMeetingsStateInspector symMeetingsStateInspector = new Sym.MeetingsStateInspector(matcher);

			var symMeetingEvents = symMeetingsStateInspector.GetEvents(logMessages);

			matcher.Freeze();

			var events = EnumerableAsync.Merge(
				symMeetingEvents.Select(ConvertTriggers<Sym.Message>)
			).ToFlatList();

			await Task.WhenAll(events, logMessages.Open());

			if (cancellation.IsCancellationRequested)
				return;

			if (templatesTracker != null)
				(await events).ForEach(e => templatesTracker.RegisterUsage(e.TemplateId));

			StateInspectorOutput.SerializePostprocessorOutput(await events, null, contentsEtagAttr).Save(outputFileName);
		}

		static Event[] ConvertTriggers<T>(Event[] batch) where T : class, ITriggerStreamPosition, ITriggerTime
		{
			T m;
			foreach (var e in batch)
				if ((m = e.Trigger as T) != null)
					e.Trigger = TextLogEventTrigger.Make(m);
			return batch;
		}
	};

}
