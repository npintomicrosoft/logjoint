using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using LogJoint.RegularExpressions;
using System.Threading;
using LogFontSize = LogJoint.Settings.Appearance.LogFontSize;
using ColoringMode = LogJoint.Settings.Appearance.ColoringMode;
using System.Threading.Tasks;

namespace LogJoint.UI.Presenters.LogViewer
{
	public interface IPresenter
	{
		LogFontSize FontSize { get; set; }
		string FontName { get; set; }
		PreferredDblClickAction DblClickAction { get; set; }
		FocusedMessageDisplayModes FocusedMessageDisplayMode { get; set; }
		string DefaultFocusedMessageActionCaption { get; set; }
		bool ShowTime { get; set; }
		bool ShowMilliseconds { get; set; }
		bool ShowRawMessages { get; set; }
		bool RawViewAllowed { get; set; }
		bool ViewTailMode { get; set; }
		UserInteraction DisabledUserInteractions { get; set; }
		ColoringMode Coloring { get; set; }
		bool NavigationIsInProgress { get; }

		Task<bool> SelectMessageAt(IBookmark bmk);
		Task SelectMessageAt(DateTime date, ILogSource[] preferredSources);
		Task GoHome();
		Task GoToEnd();
		Task GoToNextMessageInThread();
		Task GoToPrevMessageInThread();
		Task GoToNextHighlightedMessage();
		Task GoToPrevHighlightedMessage();
		Task GoToNextMessage();
		Task GoToPrevMessage();
		Task<IMessage> Search(SearchOptions opts);
		void SelectFirstMessage();
		void SelectLastMessage();
		void MakeFirstLineFullyVisible();

		IMessage FocusedMessage { get; }
		IBookmark GetFocusedMessageBookmark();
		Task<Dictionary<IMessagesSource, long>> GetCurrentPositions(CancellationToken cancellation);
		IBookmark SlaveModeFocusedMessage { get; set; }
		Task SelectSlaveModeFocusedMessage();

		Task<string> GetSelectedText(); // func is async when selected text is not on the screen atm
		void ClearSelection();
		Task CopySelectionToClipboard();
		bool IsSinglelineNonEmptySelection { get; }

		bool HasInputFocus { get; }
		void ReceiveInputFocus();

		event EventHandler SelectionChanged;
		event EventHandler FocusedMessageChanged;
		event EventHandler FocusedMessageBookmarkChanged;
		event EventHandler DefaultFocusedMessageAction;
		event EventHandler ManualRefresh;
		event EventHandler RawViewModeChanged;
		event EventHandler ViewTailModeChanged;
		event EventHandler ColoringModeChanged;
		event EventHandler NavigationIsInProgressChanged;
		event EventHandler<ContextMenuEventArgs> ContextMenuOpening;
	};

	[Flags]
	public enum UserInteraction
	{
		None = 0,
		RawViewSwitching = 1,
		FontResizing = 2,
		FramesNavigationMenu = 4,
		CopyMenu = 8,
		CopyShortcut = 16,
	};

	[Flags]
	public enum Key
	{
		KeyCodeMask = 0xff,
		None = 0,
		Refresh,
		Up, Down, Left, Right,
		PageUp, PageDown,
		ContextMenu,
		Enter,
		Copy,
		BeginOfLine, EndOfLine,
		BeginOfDocument, EndOfDocument,
		BookmarkShortcut,
		NextHighlightedMessage,
		PrevHighlightedMessage,

		ModifySelectionModifier = 512,
		AlternativeModeModifier = 1024,
		JumpOverWordsModifier = 2048
	};

	[Flags]
	public enum ContextMenuItem
	{
		None = 0,
		Copy = 1,
		CollapseExpand = 2,
		RecursiveCollapseExpand = 4,
		GotoParentFrame = 8,
		GotoEndOfFrame = 16,
		ShowTime = 32,
		ShowRawMessages = 64,
		DefaultAction = 128,
		ToggleBmk = 256,
		GotoNextMessageInTheThread = 512,
		GotoPrevMessageInTheThread = 1024,
		CollapseAllFrames = 2048,
		ExpandAllFrames = 4096
	};

	public enum FocusedMessageDisplayModes
	{
		Master,
		Slave
	};

	public struct ViewLine
	{
		public int LineIndex;
		public IMessage Message;
		public int TextLineIndex;
		public bool IsBookmarked;
	};

	[Flags]
	public enum MessageMouseEventFlag
	{
		None = 0,

		SingleClick = 1,
		DblClick = 2,
		CapturedMouseMove = 4,

		RightMouseButton = 8,

		OulineBoxesArea = 16,

		ShiftIsHeld = 32,
		AltIsHeld = 64,
		CtrlIsHeld = 128
	};

	public enum PreferredDblClickAction
	{
		SelectWord,
		DoDefaultAction
	};

	public interface IViewFonts
	{
		string[] AvailablePreferredFamilies { get; }
		KeyValuePair<LogFontSize, int>[] FontSizes { get; }
	};

	public interface IView : IViewFonts
	{
		void SetViewEvents(IViewEvents viewEvents);
		void SetPresentationDataAccess(IPresentationDataAccess presentationDataAccess);
		float DisplayLinesPerPage { get; }
		void SetVScroll(double? value);
		void UpdateFontDependentData(string fontName, LogFontSize fontSize);
		void HScrollToSelectedText(SelectionInfo selection);
		void Invalidate();
		void InvalidateLine(ViewLine line);
		void DisplayNothingLoadedMessage(string messageToDisplayOrNull);
		void RestartCursorBlinking();
		void AnimateSlaveMessagePosition();
		void UpdateMillisecondsModeDependentData();
		bool HasInputFocus { get; }
		void ReceiveInputFocus();

		// todo: review if methods below are still valid
		object GetContextMenuPopupDataForCurrentSelection(SelectionInfo selection);
		void PopupContextMenu(object contextMenuPopupData);
	};

	public interface IViewEvents
	{
		void OnDisplayLinesPerPageChanged();
		void OnIncrementalVScroll(float nrOfDisplayLines);
		void OnVScroll(double value, bool isRealtimeScroll);
		void OnHScroll();
		void OnMouseWheelWithCtrl(int delta);
		void OnCursorTimerTick();
		void OnKeyPressed(Key k);
		MenuData OnMenuOpening();
		void OnMenuItemClicked(ContextMenuItem menuItem, bool? itemChecked = null);
		void OnMessageMouseEvent(
			ViewLine line,
			int charIndex,
			MessageMouseEventFlag flags,
			object preparedContextMenuPopupData);
		void OnDrawingError(Exception e);
	};

	public interface IPresentationDataAccess
	{
		int ViewLinesCount { get; }
		IEnumerable<ViewLine> GetViewLines(int beginIdx, int endIdx);
		double GetFirstDisplayMessageScrolledLines();
		bool ShowTime { get; }
		bool ShowMilliseconds { get; }
		bool ShowRawMessages { get; }
		SelectionInfo Selection { get; }
		ColoringMode Coloring { get; }
		FocusedMessageDisplayModes FocusedMessageDisplayMode { get; }
		Func<IMessage, IEnumerable<Tuple<int, int>>> InplaceHighlightHandler1 { get; }
		Func<IMessage, IEnumerable<Tuple<int, int>>> InplaceHighlightHandler2 { get; }
		Tuple<int, int> FindSlaveModeFocusedMessagePosition(int beginIdx, int endIdx);
	};

	public class MenuData
	{
		public ContextMenuItem VisibleItems;
		public ContextMenuItem CheckedItems;
		public string DefaultItemText;
		public class ExtendedItem
		{
			public string Text;
			public Action Click;
		};
		public List<ExtendedItem> ExtendededItems;
	};

	public interface IMessagesSource
	{
		Task<DateBoundPositionResponseData> GetDateBoundPosition(
			DateTime d,
			ListUtils.ValueBound bound,
			LogProviderCommandPriority priority,
			CancellationToken cancellation
		);
		Task EnumMessages(
			long fromPosition,
			Func<IMessage, bool> callback,
			EnumMessagesFlag flags,
			LogProviderCommandPriority priority,
			CancellationToken cancellation
		);
		FileRange.Range PositionsRange { get; }
		DateRange DatesRange { get; }

		FileRange.Range ScrollPositionsRange { get; }
		long MapPositionToScrollPosition(long pos);
		long MapScrollPositionToPosition(long pos);

		/// <summary>
		/// Returns log source if all messages from this messages source belong to it.
		/// Otherwise returns null.
		/// </summary>
		ILogSource LogSourceHint { get; }
	};

	public interface IModel
	{
		IEnumerable<IMessagesSource> Sources { get; }
		IModelThreads Threads { get; }
		IFiltersList HighlightFilters { get; }
		IBookmarks Bookmarks { get; }
		string MessageToDisplayWhenMessagesCollectionIsEmpty { get; }
		Settings.IGlobalSettingsAccessor GlobalSettings { get; }

		event EventHandler OnSourcesChanged;
		event EventHandler OnSourceMessagesChanged;
		event EventHandler OnLogSourceColorChanged;
	};

	public interface ISearchResultModel : IModel
	{
		IEnumerable<IFilter> SearchFilters { get; }
		void RaiseSourcesChanged();
	};

	public interface IPresenterFactory
	{
		IPresenter Create(IModel model, IView view, bool createIsolatedPresenter);
		IModel CreateLoadedMessagesModel();
		ISearchResultModel CreateSearchResultsModel();
	};

	public class ContextMenuEventArgs : EventArgs
	{
		internal List<MenuData.ExtendedItem> items;

		public void Add(MenuData.ExtendedItem item)
		{
			if (items == null)
				items = new List<MenuData.ExtendedItem>();
			items.Add(item);
		}
	};
};