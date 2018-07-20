using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;

namespace LogJoint.UI.Presenters.SearchPanel
{
	public interface IPresenter
	{
		event EventHandler InputFocusAbandoned;
		void ReceiveInputFocusByShortcut(bool forceSearchAllOccurencesMode = false);
		void PerformSearch();
		void PerformReversedSearch();
		void CollapseSearchResultPanel();
	};

	public interface IView
	{
		void SetPresenter(IViewEvents presenter);
		ViewCheckableControl GetCheckableControlsState();
		void SetCheckableControlsState(ViewCheckableControl affectedControls, ViewCheckableControl checkedControls);
		void EnableCheckableControls(ViewCheckableControl affectedControls, ViewCheckableControl enabledControls);
		void SetFiltersLink(bool isVisible, string text);

		QuickSearchTextBox.IView SearchTextBox { get; }
	};

	public interface ISearchResultsPanelView
	{
		bool Collapsed { get; set; }
	};

	[Flags]
	public enum ViewCheckableControl
	{
		None = 0,
		MatchCase = 1,
		WholeWord = 2,
		RegExp = 4,
		SearchWithinThisThread = 8,
		Errors = 16,
		Warnings = 32,
		Infos = 64,
		Frames = 128,
		QuickSearch = 256,
		SearchUp = 512,
		SearchFromCurrentPosition = 1024,
		SearchInSearchResult = 2048,
		SearchAllOccurences = 4096,
		SearchWithinCurrentLog = 8192,
	};

	public interface IViewEvents
	{
		void OnSearchButtonClicked();
		void OnSearchModeControlChecked(ViewCheckableControl ctrl);
		void OnFiltersLinkClicked();
	};
};