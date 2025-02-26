﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LogJoint.UI.Presenters.Postprocessing.TimelineVisualizer;
using LJD = LogJoint.Drawing;

namespace LogJoint.UI.Postprocessing.TimelineVisualizer
{
	public partial class TimelineVisualizerControl : UserControl, IView
	{
		IViewEvents eventsHandler;
		readonly ViewMetrics viewMetrics = new ViewMetrics();
		int activitesCount;
		int sequenceDiagramAreaWidth;
		CurrentNavigationOp currentNavigationOp;
		readonly Font activitesCaptionsFont;
		readonly UIUtils.ToolTipHelper activitiesPanelToolTipHelper;

		public TimelineVisualizerControl()
		{
			InitializeComponent();

			var toolboxIconsSize = UIUtils.Dpi.Scale(14, 120);
			prevUserActionButton.Image = nextUserActionButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.UserAction, toolboxIconsSize);
			prevBookmarkButton.Image = nextBookmarkButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.BigBookmark, toolboxIconsSize);
			findCurrentTimeButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.SelectCurrentTime, toolboxIconsSize);
			zoomInButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.ZoomIn, toolboxIconsSize);
			zoomOutButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.ZoomOut, toolboxIconsSize);
			notificationsButton.Image = UIUtils.DownscaleUIImage(TimelineVisualizerControlResources.Warning, toolboxIconsSize);

			var vm = viewMetrics;
			vm.LineHeight = UIUtils.Dpi.Scale(20, 120);
			vm.DPIScale = UIUtils.Dpi.Scale(1f);
			vm.ActivityBarRectPaddingY = UIUtils.Dpi.Scale(5, 120);
			vm.TriggerLinkWidth = UIUtils.Dpi.ScaleUp(5, 120);
			vm.DistnanceBetweenRulerMarks = UIUtils.Dpi.ScaleUp(40, 120);;
			vm.MeasurerTop = 25;
			vm.VisibleRangeResizerWidth = 8;
			vm.RulersPanelHeight = UIUtils.Dpi.Scale(53, 120);

			vm.ActivitesCaptionsFont = new LJD.Font(Font.FontFamily.Name, Font.Size);
			vm.ActivitesCaptionsBrush = new LJD.Brush(Color.Black);
			activitesCaptionsFont = Font;

			vm.ActionCaptionFont = new LJD.Font(this.Font.FontFamily.Name, 8f);
			vm.RulerMarkFont = new LJD.Font(this.Font.FontFamily.Name, 6f);
			vm.UserIcon = new LJD.Image(TimelineVisualizerControlResources.UserAction);
			vm.APIIcon = new LJD.Image(TimelineVisualizerControlResources.APICall);
			vm.BookmarkIcon = new LJD.Image(TimelineVisualizerControlResources.TimelineBookmark);
			vm.SelectedLineBrush = new LJD.Brush(Color.FromArgb(187, 196, 221));
			vm.RulerMarkBrush = new LJD.Brush (Color.Black);
			vm.RulerLinePen = new LJD.Pen (Color.LightGray, 1);

			vm.ProcedureBrush = new LJD.Brush(Color.LightBlue);
			vm.LifetimeBrush = new LJD.Brush(Color.LightGreen);
			vm.NetworkMessageBrush = new LJD.Brush(Color.LightSalmon);
			vm.UnknownActivityBrush = new LJD.Brush(Color.LightGray);
			vm.ActivitiesTopBoundPen = new LJD.Pen (Color.Gray, 1);
			vm.MilestonePen = new LJD.Pen(Color.FromArgb(180, Color.SteelBlue), UIUtils.Dpi.ScaleUp(3, 120));
			vm.ActivityBarBoundsPen = new LJD.Pen(Color.Gray, 1f);
			vm.ActivitiesConnectorPen = new LJD.Pen(Color.DarkGray, UIUtils.Dpi.ScaleUp(1, 120), new[] { 1f, 1f });
			vm.ActionLebelHeight = UIUtils.Dpi.Scale(20, 120);

			vm.UserEventPen = new LJD.Pen(Color.Salmon, UIUtils.Dpi.ScaleUp(2, 120));
			vm.EventRectBrush = new LJD.Brush(Color.Salmon);
			vm.EventRectPen = new LJD.Pen(Color.Gray, 1);
			vm.EventCaptionBrush = new LJD.Brush(Color.Black);
			vm.EventCaptionFont = vm.ActionCaptionFont;
			vm.EventCaptionStringFormat = new LJD.StringFormat(StringAlignment.Center, StringAlignment.Far);

			vm.BookmarkPen = new LJD.Pen(Color.FromArgb(0x5b, 0x87, 0xe0), UIUtils.Dpi.ScaleUp(1, 120));

			vm.FocusedMessagePen = new LJD.Pen(Color.Blue, UIUtils.Dpi.ScaleUp(1, 120));
			vm.FocusedMessageLineTop = new LJD.Image(TimelineVisualizerControlResources.FocusedMsgSlaveVert);

			vm.MeasurerPen = new LJD.Pen(Color.DarkGreen, UIUtils.Dpi.ScaleUp(1, 120), new[] { 4f, 2f });
			vm.MeasurerTextFont = new LJD.Font(this.Font.FontFamily.Name, 6f);
			vm.MeasurerTextBrush = new LJD.Brush(Color.Black);
			vm.MeasurerTextBoxBrush = new LJD.Brush(Color.White);
			vm.MeasurerTextBoxPen = new LJD.Pen(Color.DarkGreen, 1f);
			vm.MeasurerTextFormat = new LJD.StringFormat(StringAlignment.Center, StringAlignment.Center);

			vm.NavigationPanel_InvisibleBackground = new LJD.Brush(Color.FromArgb(235, 235, 235));
			vm.NavigationPanel_VisibleBackground = new LJD.Brush(Color.White);
			vm.SystemControlBrush = new LJD.Brush(SystemColors.Control);
			vm.VisibleRangePen = new LJD.Pen(Color.Gray, 1f);

			var rulersPanelHeight = vm.RulersPanelHeight;

			activitiesContainer.SplitterDistance = UIUtils.Dpi.Scale(260, 120);
			activitiesContainer.SplitterWidth = UIUtils.Dpi.ScaleUp(3, 120);

			activitiesScrollBar.SmallChange = vm.LineHeight;
			activitiesScrollBar.Top = rulersPanelHeight;
			activitiesScrollBar.Height = activitiesScrollBar.Parent.Height - activitiesScrollBar.Top;

			activitiesViewPanel.MouseWheel += activitiesViewPanel_MouseWheel;
			activitesCaptionsPanel.MouseWheel += activitesCaptionsPanel_MouseWheel;

			quickSearchEditBox.Top = rulersPanelHeight - quickSearchEditBox.Height;
			panel5.Height = rulersPanelHeight;
			this.quickSearchEditBox.InnerTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.quickSearchEditBox_KeyDown);

			currentActivityCaptionLabel.Font = new Font(currentActivityCaptionLabel.Font, FontStyle.Bold);

			activitiesPanelToolTipHelper = new UIUtils.ToolTipHelper(activitiesViewPanel, GetActivitiesToolTipInfo, 150);

			// link labels do not scale on DPI properly. scale them manually.
			currentActivityDescription.Height = UIUtils.Dpi.ScaleUp(18, 120);
			currentActivitySourceLinkLabel.Height = UIUtils.Dpi.ScaleUp(18, 120);
		}

		void IView.SetEventsHandler(IViewEvents eventsHandler)
		{
			this.eventsHandler = eventsHandler;
		}

		void IView.Invalidate(ViewAreaFlag flags)
		{
			if ((flags & ViewAreaFlag.NavigationPanelView) != 0)
				navigationPanel.Invalidate();
			if ((flags & ViewAreaFlag.ActivitiesCaptionsView) != 0)
				activitesCaptionsPanel.Invalidate();
			if ((flags & ViewAreaFlag.ActivitiesBarsView) != 0)
				activitiesViewPanel.Invalidate();
		}

		void IView.Refresh(ViewAreaFlag flags)
		{
			if ((flags & ViewAreaFlag.NavigationPanelView) != 0)
				navigationPanel.Refresh();
			if ((flags & ViewAreaFlag.ActivitiesCaptionsView) != 0)
				activitesCaptionsPanel.Refresh();
			if ((flags & ViewAreaFlag.ActivitiesBarsView) != 0)
				activitiesViewPanel.Refresh();
		}

		void IView.UpdateActivitiesScroller(int activitesCount)
		{
			this.activitesCount = activitesCount;
			UpdateActivitiesScroller();
		}

		void IView.UpdateSequenceDiagramAreaMetrics()
		{
			using (var g = new LJD.Graphics(CreateGraphics(), ownsGraphics: true))
			{
				sequenceDiagramAreaWidth = Metrics.GetSequenceDiagramAreaMetrics(g, GetUpToDateViewMetrics(), eventsHandler);
			}
		}

		void IView.UpdateCurrentActivityControls(string caption, 
			string descriptionText, IEnumerable<Tuple<object, int, int>> descriptionLinks, 
			string sourceText, Tuple<object, int, int> sourceLink)
		{
			currentActivityCaptionLabel.Text = caption;
			currentActivityDescription.Text = descriptionText;
			currentActivityDescription.Links.Clear();
			if (descriptionLinks != null)
			{
				foreach (var l in descriptionLinks)
					currentActivityDescription.Links.Add(new LinkLabel.Link()
					{
						LinkData = l.Item1,
						Start = l.Item2,
						Length = l.Item3
					});
			}
			currentActivitySourceLinkLabel.Visible = sourceText != null;
			if (sourceText != null)
			{
				currentActivitySourceLinkLabel.Text = sourceText;
				currentActivitySourceLinkLabel.Links.Clear();
				currentActivitySourceLinkLabel.Links.Add(new LinkLabel.Link()
					{
						LinkData = sourceLink.Item1,
						Start = sourceLink.Item2,
						Length = sourceLink.Item3
					});
			}
		}

		HitTestResult IView.HitTest(object hitTestToken)
		{
			var htToken = hitTestToken as HitTestToken;
			if (htToken == null)
				return new HitTestResult();
			return Metrics.HitTest(htToken.Pt, GetUpToDateViewMetrics(), eventsHandler, htToken.Control == activitesCaptionsPanel,
				() => new LJD.Graphics(CreateGraphics(), ownsGraphics: true));
		}

		void IView.EnsureActivityVisible(int activityIndex)
		{
			var vm = GetUpToDateViewMetrics();
			int y = Metrics.GetActivityY(vm, activityIndex);
			if (y > 0 && (y + vm.LineHeight) < activitiesViewPanel.Height)
				return;
			var scrollerPos = vm.RulersPanelHeight - activitiesViewPanel.Height / 2 + activityIndex * vm.LineHeight;
			scrollerPos = Math.Max(0, scrollerPos);
			activitiesScrollBar.Value = scrollerPos;
		}

		void IView.ReceiveInputFocus()
		{
			if (activitiesViewPanel.CanFocus)
				activitiesViewPanel.Focus();
		}

		LogJoint.UI.Presenters.QuickSearchTextBox.IView IView.QuickSearchTextBox
		{
			get { return quickSearchEditBox.InnerTextBox; }
		}

		LogJoint.UI.Presenters.TagsList.IView IView.TagsListView
		{
			get { return tagsListControl; }
		}

		Presenters.ToastNotificationPresenter.IView IView.ToastNotificationsView
		{
			get { return toastNotificationsListControl; }
		}

		void IView.SetNotificationsIconVisibility(bool value)
		{
			notificationsButton.Visible = value;
		}

		private void UpdateActivitiesScroller()
		{
			var vm = GetUpToDateViewMetrics();
			activitiesScrollBar.Maximum = activitesCount * vm.LineHeight;
			activitiesScrollBar.LargeChange = Math.Max(1, activitiesViewPanel.Height - vm.RulersPanelHeight);
			activitiesScrollBar.Enabled = activitiesScrollBar.Maximum > activitiesScrollBar.LargeChange;
			if (activitiesScrollBar.Enabled)
			{
				var maxValue = activitiesScrollBar.Maximum - activitiesScrollBar.LargeChange;
				if (activitiesScrollBar.Value > maxValue)
					activitiesScrollBar.Value = maxValue;
			}
			else
			{
				activitiesScrollBar.Value = 0;
			}
		}

		private void activitesCaptionsPanel_Paint(object sender, PaintEventArgs e)
		{
			if (eventsHandler == null)
				return;
			using (var g = new LJD.Graphics(e.Graphics))
			{
				Drawing.DrawCaptionsView(
					g,
					GetUpToDateViewMetrics(),
					eventsHandler,
					sequenceDiagramAreaWidth,
					(text, textRect, hlbegin, hllen) =>
					{
						if (hllen > 0 && hlbegin >= 0)
						{
							var bogusSz = new Size(10000, 10000);
							var highlightLeft = textRect.X + TextRenderer.MeasureText(
								e.Graphics,
								text.Substring(0, hlbegin),
								activitesCaptionsFont,
								bogusSz,
								TextFormatFlags.NoPadding).Width;
							var highlightWidth = TextRenderer.MeasureText(
								e.Graphics,
								text.Substring(hlbegin, hllen),
								activitesCaptionsFont,
								bogusSz,
								TextFormatFlags.NoPadding).Width;
							e.Graphics.FillRectangle(Brushes.Yellow, new RectangleF(highlightLeft, textRect.Y, highlightWidth, textRect.Height));
						}
						TextRenderer.DrawText(e.Graphics, text, activitesCaptionsFont, textRect, Color.Black,
							TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter |
							TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.NoPadding);
					}
				);
			}
		}

		static Brush MakeBrush(ModelColor c)
		{
			return new SolidBrush(Color.FromArgb(c.R, c.G, c.B));
		}

		private void activitiesViewPanel_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.FillRectangle(Brushes.White, e.ClipRectangle);
			if (eventsHandler == null)
				return;
			using (var g = new LJD.Graphics(e.Graphics))
				Drawing.DrawActivtiesView(g, GetUpToDateViewMetrics(), eventsHandler);
		}

		static KeyCode GetKeyModifiers()
		{
			var ret = KeyCode.None;
			var mk = Control.ModifierKeys;
			if ((mk & Keys.Shift) != 0)
				ret |= KeyCode.Shift;
			if ((mk & Keys.Control) != 0)
				ret |= KeyCode.Ctrl;
			return ret;
		}

		private void activitiesPanel_MouseDown(object sender, MouseEventArgs e)
		{
			if (eventsHandler == null)
				return;
			var ctrl = (Control)sender;
			ctrl.Focus();
			eventsHandler.OnMouseDown(new HitTestToken() { Control = ctrl, Pt = e.Location }, GetKeyModifiers(), e.Clicks == 2);
		}

		private void activitiesViewPanel_MouseMove(object sender, MouseEventArgs e)
		{
			if (eventsHandler != null)
				eventsHandler.OnMouseMove(new HitTestToken() { Control = (Control)sender, Pt = e.Location }, GetKeyModifiers());
		}

		private void activitiesViewPanel_MouseUp(object sender, MouseEventArgs e)
		{
			if (eventsHandler != null)
				eventsHandler.OnMouseUp(new HitTestToken() { Control = (Control)sender, Pt = e.Location });
		}

		private void activitiesPanel_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
			if (e.KeyCode == Keys.Up)
				eventsHandler.OnKeyDown(KeyCode.Up);
			else if (e.KeyCode == Keys.Down)
				eventsHandler.OnKeyDown(KeyCode.Down);
			else if (e.KeyCode == Keys.Left)
				eventsHandler.OnKeyDown(KeyCode.Left);
			else if (e.KeyCode == Keys.Right)
				eventsHandler.OnKeyDown(KeyCode.Right);
			else if (e.KeyValue == 187)
				eventsHandler.OnKeyDown(KeyCode.Plus | GetKeyModifiers());
			else if (e.KeyValue == 189)
				eventsHandler.OnKeyDown(KeyCode.Minus | GetKeyModifiers());
			else if (e.KeyCode == Keys.Enter)
				eventsHandler.OnKeyDown(KeyCode.Enter);
			else if (e.KeyCode == Keys.F && e.Control)
				eventsHandler.OnKeyDown(KeyCode.Find);
			else if (e.KeyCode == Keys.F6)
				eventsHandler.OnKeyDown(KeyCode.FindCurrentTimeShortcut);
			else if (e.KeyCode == Keys.F2 && !e.Shift)
				eventsHandler.OnKeyDown(KeyCode.NextBookmarkShortcut);
			else if (e.KeyCode == Keys.F2 && e.Shift)
				eventsHandler.OnKeyDown(KeyCode.PrevBookmarkShortcut);
		}

		private void activitiesViewPanel_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (Control.ModifierKeys == Keys.None)
			{
				eventsHandler.OnKeyPressed(e.KeyChar);
				e.Handled = true;
			}
		}

		void activitiesViewPanel_MouseWheel(object sender, MouseEventArgs e)
		{
			if (activitiesViewPanel.Width > 0 && Control.ModifierKeys == Keys.Control)
			{
				eventsHandler.OnMouseZoom((double)e.X / (double)activitiesViewPanel.Width, e.Delta);
			}
			else if (Control.ModifierKeys == Keys.None)
			{
				HandleActivitiesScroll(e);
			}
		}

		void activitesCaptionsPanel_MouseWheel(object sender, MouseEventArgs e)
		{
			if (Control.ModifierKeys == Keys.None)
			{
				HandleActivitiesScroll(e);
			}
		}

		void HandleActivitiesScroll(MouseEventArgs e)
		{
			var newValue = activitiesScrollBar.Value - Math.Sign(e.Delta) * 4 * viewMetrics.LineHeight;
			newValue = Math.Min(activitiesScrollBar.Maximum - activitiesScrollBar.LargeChange + 1, newValue);
			newValue = Math.Max(activitiesScrollBar.Minimum, newValue);
			activitiesScrollBar.Value = newValue;
			activitesCaptionsPanel.Invalidate();
			activitiesViewPanel.Invalidate();
		}

		private void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
		{
			activitesCaptionsPanel.Refresh();
			activitiesViewPanel.Refresh();
		}

		private void activitiesViewPanel_Resize(object sender, EventArgs e)
		{
			UpdateActivitiesScroller();
		}

		private void navigationPanel_Paint(object sender, PaintEventArgs e)
		{
			if (eventsHandler == null)
				return;
			using (var g = new LJD.Graphics(e.Graphics))
				Drawing.DrawNavigationPanel(g, GetUpToDateViewMetrics(), eventsHandler);
		}

		private void navigationPanel_SetCursor(object sender, HandledMouseEventArgs e)
		{
			if (eventsHandler == null)
				return;

			var vm = GetUpToDateViewMetrics();
			var m = Metrics.GetNavigationPanelMetrics(vm, eventsHandler);

			if (m.Resizer1.Contains(e.Location) || m.Resizer2.Contains(e.Location))
			{
				Cursor.Current = Cursors.SizeWE;
				e.Handled = true;
			}
			else if (m.VisibleRangeBox.Contains(e.Location))
			{
				Cursor.Current = Cursors.SizeAll;
				e.Handled = true;
			}
		}

		private void navigationPanel_MouseDown(object sender, MouseEventArgs e)
		{
			if (eventsHandler == null)
				return;
			var vm = GetUpToDateViewMetrics();
			var m = Metrics.GetNavigationPanelMetrics(vm, eventsHandler);
			if (m.Resizer1.Contains(e.Location))
			{
				currentNavigationOp = new CurrentNavigationOp() { beginDeltaX = m.VisibleRangeBox.X - e.X };
			}
			else if (m.Resizer2.Contains(e.Location))
			{
				currentNavigationOp = new CurrentNavigationOp() { endDeltaX = m.VisibleRangeBox.Right - e.X };
			}
			else if (m.VisibleRangeBox.Contains(e.Location))
			{
				currentNavigationOp = new CurrentNavigationOp()
				{
					beginDeltaX = m.VisibleRangeBox.X - e.X,
					endDeltaX = m.VisibleRangeBox.Right - e.X
				};
			}
			else
			{
				if (navigationPanel.Width != 0)
					eventsHandler.OnNavigation((double)e.X / (double)navigationPanel.Width);
			}
		}

		private void navigationPanel_MouseUp(object sender, MouseEventArgs e)
		{
			currentNavigationOp = null;
			activitiesViewPanel.Focus();
		}

		private void navigationPanel_MouseMove(object sender, MouseEventArgs e)
		{
			if (currentNavigationOp == null)
				return;
			var op = currentNavigationOp;
			Navigate(
				op.beginDeltaX != null ? (e.X + op.beginDeltaX.Value) : new int?(),
				op.endDeltaX != null ? (e.X + op.endDeltaX.Value) : new int?()
			);
		}

		void Navigate(int? newBeginX, int? newEndX)
		{
			if (navigationPanel.Width == 0)
				return;
			double width = (double)navigationPanel.Width;
			double? newBegin = null;
			if (newBeginX.HasValue)
				newBegin = (double)newBeginX.Value / width;
			double? newEnd = null;
			if (newEndX.HasValue)
				newEnd = (double)newEndX.Value / width;
			eventsHandler.OnNavigation(newBegin, newEnd);
		}

		private void activitiesViewPanel_SetCursor(object sender, HandledMouseEventArgs e)
		{
			if (eventsHandler == null)
				return;
			var cursor = Metrics.GetCursor(e.Location, GetUpToDateViewMetrics(), eventsHandler, 
				() => new LJD.Graphics(CreateGraphics(), ownsGraphics: true));
			if (cursor == CursorType.Hand)
			{
				Cursor.Current = Cursors.Hand;
				e.Handled = true;
			}
		}
		
		private void navigationPanel_DoubleClick(object sender, EventArgs e)
		{
			if (eventsHandler != null)
				eventsHandler.OnNavigationPanelDblClick();
		}

		private void activitiesContainer_DoubleClick(object sender, EventArgs e)
		{
			if (eventsHandler == null)
				return;
			var maxWidth = 0;
			using (var dc = CreateGraphics())
				foreach (var a in eventsHandler.OnDrawActivities())
					maxWidth = Math.Max(maxWidth, TextRenderer.MeasureText(dc, a.Caption, activitesCaptionsFont).Width);
			if (maxWidth != 0)
			{
				maxWidth = Math.Min(maxWidth, activitiesContainer.Width / 2); // protect from insanely big captions
				activitiesContainer.SplitterDistance = maxWidth;
			}
		}

		private void currentActivityDescription_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (e.Link.LinkData != null)
			{
				eventsHandler.OnActivityTriggerClicked(e.Link.LinkData);
			}
		}

		private void currentActivitySourceLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			if (e.Link.LinkData != null)
			{
				eventsHandler.OnActivitySourceLinkClicked(e.Link.LinkData);
			}
		}

		private void activitesCaptionsPanel_Resize(object sender, EventArgs e)
		{
			((IView)this).UpdateSequenceDiagramAreaMetrics();
		}

		LogJoint.UI.UIUtils.ToolTipInfo GetActivitiesToolTipInfo(Point pt)
		{
			if (eventsHandler == null)
				return null;
			var toolTip = eventsHandler.OnToolTip(new HitTestToken() { Pt = pt, Control = activitiesViewPanel }) ?? "";
			if (string.IsNullOrEmpty(toolTip))
				return null;
			var ret = new UIUtils.ToolTipInfo()
			{
				Text = toolTip,
				Title = null,
				Duration = 5000
			};
			return ret;
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				if (eventsHandler.OnEscapeCmdKey())
					return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void quickSearchEditBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Up)
			{
				e.Handled = true;
				eventsHandler.OnQuickSearchExitBoxKeyDown(KeyCode.Up);
			}
			else if (e.KeyCode == Keys.Down)
			{
				e.Handled = true;
				eventsHandler.OnQuickSearchExitBoxKeyDown(KeyCode.Down);
			}
		}

		private void toolPanelLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			HandleMouseClick(sender);
		}

		private void toolPanelLinkMouseDoubleClick(object sender, MouseEventArgs e)
		{
			HandleMouseClick(sender);
		}

		void notificationsButton_Click(object sender, System.EventArgs e)
		{
			eventsHandler.OnActiveNotificationButtonClicked();
		}

		void HandleMouseClick(object sender)
		{
			if (sender == prevUserActionButton)
				eventsHandler.OnPrevUserEventButtonClicked();
			else if (sender == nextUserActionButton)
				eventsHandler.OnNextUserEventButtonClicked();
			else if (sender == prevBookmarkButton)
				eventsHandler.OnPrevBookmarkButtonClicked();
			else if (sender == nextBookmarkButton)
				eventsHandler.OnNextBookmarkButtonClicked();
			else if (sender == findCurrentTimeButton)
				eventsHandler.OnFindCurrentTimeButtonClicked();
			else if (sender == zoomInButton)
				eventsHandler.OnZoomInButtonClicked();
			else if (sender == zoomOutButton)
				eventsHandler.OnZoomOutButtonClicked();
		}

		ViewMetrics GetUpToDateViewMetrics()
		{
			UpdateViewMetrics();
			return viewMetrics;
		}

		void UpdateViewMetrics()
		{
			var vm = viewMetrics;
			vm.ActivitiesViewWidth = activitiesViewPanel.Width;
			vm.ActivitiesViewHeight = activitiesViewPanel.Height;
			vm.ActivitesCaptionsViewWidth = activitesCaptionsPanel.Width;
			vm.ActivitesCaptionsViewHeight = activitesCaptionsPanel.Height;
			vm.NavigationPanelWidth = navigationPanel.Width;
			vm.NavigationPanelHeight = navigationPanel.Height;
			vm.VScrollBarValue = activitiesScrollBar.Value;
		}

		class CurrentNavigationOp
		{
			public int? beginDeltaX;
			public int? endDeltaX;
		};

		class HitTestToken
		{
			public Control Control;
			public Point Pt;
		};
	}
}
