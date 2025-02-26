﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LogJoint.Drawing;
using LogJoint.Postprocessing.Timeline;
using LogJoint.UI.Presenters.Postprocessing.TimelineVisualizer;
using LJD = LogJoint.Drawing;
using TLRulerMark = LogJoint.UI.Presenters.Postprocessing.TimelineVisualizer.RulerMark;

namespace LogJoint.UI.Postprocessing.TimelineVisualizer
{
	public class EventMetrics
	{
		public EventDrawInfo Event;
		public Point VertLineA, VertLineB;
		public Point[] VertLinePoints;
		public LJD.Image Icon;
		public RectangleF IconRect;
		public Rectangle CaptionRect;
		public Point CaptionDrawingOrigin;

		public bool IsOverLink(Point pt)
		{
			return Event.Trigger != null && CaptionRect.Contains(pt);
		}
	};

	public struct ActivityMilestoneMetrics
	{
		public int X;
		public Rectangle? Bounds;
		public object Trigger;
	};

	public class ActivityMetrics
	{
		public ActivityDrawInfo Activity;
		public Rectangle ActivityLineRect;
		public Rectangle ActivityBarRect;
		public Rectangle? BeginTriggerLinkRect, EndTriggerLinkRect;
		public IEnumerable<ActivityMilestoneMetrics> Milestones;
		public Rectangle? PairedActivityConnectorBounds;
	};

	public class BookmarkMetrics
	{
		public BookmarkDrawInfo Bookmark;
		public Point VertLineA, VertLineB;
		public LJD.Image Icon;
		public RectangleF IconRect;

		public bool IsOverLink(Point pt)
		{
			return Bookmark.Trigger != null && IconRect.Contains(pt);
		}
	};

	public class NavigationPanelMetrics
	{
		public Rectangle VisibleRangeBox;
		public Rectangle Resizer1;
		public Rectangle Resizer2;
	};

	public enum CursorType
	{
		Default,
		Hand
	};

	public class ViewMetrics
	{
		public int ActivitiesViewWidth; // activitiesViewPanel.Width
		public int ActivitiesViewHeight; // activitiesViewPanel.Height
		public int ActivitesCaptionsViewWidth;
		public int ActivitesCaptionsViewHeight;
		public int NavigationPanelWidth;
		public int NavigationPanelHeight;
		public int VScrollBarValue;

		public float DPIScale = 1f;

		public int LineHeight;

		public int RulersPanelHeight;
		public int ActionLebelHeight; // actionLebelHeight
		public int ActivityBarRectPaddingY;
		public int TriggerLinkWidth;
		public int DistnanceBetweenRulerMarks;
		public int MeasurerTop;
		public int VisibleRangeResizerWidth;

		public LJD.Font ActivitesCaptionsFont;
		public LJD.Brush ActivitesCaptionsBrush;

		public LJD.Font ActionCaptionFont, RulerMarkFont;
		public LJD.Image UserIcon, APIIcon, BookmarkIcon;
		public LJD.Brush SelectedLineBrush, RulerMarkBrush;
		public LJD.Pen RulerLinePen;

		public LJD.Brush ProcedureBrush;
		public LJD.Brush LifetimeBrush;
		public LJD.Brush NetworkMessageBrush;
		public LJD.Brush UnknownActivityBrush;
		public LJD.Pen ActivitiesTopBoundPen, MilestonePen, ActivityBarBoundsPen, ActivitiesConnectorPen;

		public LJD.Pen UserEventPen;
		public LJD.Brush EventRectBrush;
		public LJD.Pen EventRectPen;
		public LJD.Brush EventCaptionBrush;
		public LJD.Font EventCaptionFont;
		public LJD.StringFormat EventCaptionStringFormat;

		public LJD.Pen BookmarkPen;

		public LJD.Pen FocusedMessagePen;
		public LJD.Image FocusedMessageLineTop;

		public LJD.Pen MeasurerPen;
		public LJD.Font MeasurerTextFont;
		public LJD.Brush MeasurerTextBrush;
		public LJD.Brush MeasurerTextBoxBrush;
		public LJD.Pen MeasurerTextBoxPen;
		public LJD.StringFormat MeasurerTextFormat;

		public LJD.Brush NavigationPanel_InvisibleBackground;
		public LJD.Brush NavigationPanel_VisibleBackground;
		public LJD.Brush SystemControlBrush;
		public LJD.Pen VisibleRangePen;
	};

	public static class Metrics
	{
		public static IEnumerable<ActivityMetrics> GetActivitiesMetrics(ViewMetrics viewMetrics, IViewEvents eventsHandler)
		{
			double availableWidth = viewMetrics.ActivitiesViewWidth;
			int availableHeight = viewMetrics.ActivitiesViewHeight;

			foreach (var a in eventsHandler.OnDrawActivities())
			{
				int y = GetActivityY(viewMetrics, a.Index);
				if (y < 0 || y > availableHeight)
					continue;

				var activityLineRect = new Rectangle(0, y, (int)availableWidth, viewMetrics.LineHeight);

				var x1 = SafeGetScreenX(a.X1, availableWidth);
				var x2 = SafeGetScreenX(a.X2, availableWidth);
				var activityBarRect = new Rectangle(x1, y, Math.Max(1, x2 - x1), viewMetrics.LineHeight);
				activityBarRect.Inflate(0, -viewMetrics.ActivityBarRectPaddingY);

				var ret = new ActivityMetrics()
				{
					Activity = a,
					ActivityLineRect = activityLineRect,
					ActivityBarRect = activityBarRect
				};

				var limitedTriggerLinkWidth = Math.Min(viewMetrics.TriggerLinkWidth, ret.ActivityBarRect.Width);
				if (a.BeginTrigger != null)
					ret.BeginTriggerLinkRect = new Rectangle(activityBarRect.Location, new Size(limitedTriggerLinkWidth, activityBarRect.Height));
				if (a.EndTrigger != null)
					ret.EndTriggerLinkRect = new Rectangle(activityBarRect.Right - limitedTriggerLinkWidth, activityBarRect.Top, limitedTriggerLinkWidth, activityBarRect.Height);

				if (a.BeginTrigger != null && a.EndTrigger != null // if both links are to be displayed
					&& ret.ActivityBarRect.Width < viewMetrics.TriggerLinkWidth * 2) // but no room available for both
				{
					// show only link for 'begin' trigger
					ret.EndTriggerLinkRect = null;
				}

				bool milestonesWillFit = ret.ActivityBarRect.Width > viewMetrics.TriggerLinkWidth * a.MilestonesCount;
				ret.Milestones = a.Milestones.Select(ms =>
					{
						var msX = SafeGetScreenX(ms.X, availableWidth);
						Rectangle? bounds = null;
						if (ms.Trigger != null && milestonesWillFit)
						{
							var boundsX1 = Math.Max(msX - viewMetrics.TriggerLinkWidth / 2, activityBarRect.Left);
							var boundsX2 = Math.Min(msX + viewMetrics.TriggerLinkWidth / 2, activityBarRect.Right);
							bounds = new Rectangle(
								boundsX1, 
								activityBarRect.Y, 
								boundsX2 - boundsX1,
								activityBarRect.Height
							);
						}
						return new ActivityMilestoneMetrics() { X = msX, Bounds = bounds, Trigger = ms.Trigger };
					});

				if (a.PairedActivityIndex != null)
				{
					int pairedY = GetActivityY(viewMetrics, a.PairedActivityIndex.Value);
					if (y < pairedY)
					{
						ret.PairedActivityConnectorBounds = new Rectangle(
							activityBarRect.X, activityBarRect.Bottom,
							activityBarRect.Width, pairedY - activityBarRect.Bottom + viewMetrics.ActivityBarRectPaddingY
						);
					}
					else
					{
						int y2 = pairedY + viewMetrics.LineHeight - viewMetrics.ActivityBarRectPaddingY;
						ret.PairedActivityConnectorBounds = new Rectangle(
							activityBarRect.X, y2,
							activityBarRect.Width, activityBarRect.Y - y2
						);
					}
				}

				yield return ret;
			}
		}

		public static IEnumerable<EventMetrics> GetEventMetrics(LJD.Graphics g, IViewEvents eventsHandler, ViewMetrics viewMetrics)
		{
			double availableWidth = viewMetrics.ActivitiesViewWidth;
			int lastEventRight = int.MinValue;
			int overlappingEventsCount = 0;
			foreach (var evt in eventsHandler.OnDrawEvents(DrawScope.VisibleRange))
			{
				if (evt.X < 0 || evt.X > 1)
					continue;

				EventMetrics m = new EventMetrics() { Event = evt };

				int eventLineTop = viewMetrics.RulersPanelHeight - 2;
				var szF = g.MeasureString(evt.Caption, viewMetrics.ActionCaptionFont);
				var sz = new Size((int)szF.Width, (int)szF.Height);
				int x = SafeGetScreenX(evt.X, availableWidth);
				var bounds = new Rectangle(x - sz.Width / 2, eventLineTop - sz.Height, sz.Width, sz.Height);
				bounds.Inflate(2, 0);
				if (bounds.Left < lastEventRight)
				{
					++overlappingEventsCount;
					bounds.Offset(lastEventRight - bounds.Left + 2, 0);
					var mid = (bounds.Left + bounds.Right) / 2;
					var y2 = eventLineTop + 5 * overlappingEventsCount;
					m.VertLinePoints = new Point[] 
					{
						new Point(mid, eventLineTop),
						new Point(mid, y2),
						new Point(x, y2),
						new Point(x, viewMetrics.ActivitiesViewHeight)
					};
				}
				else
				{
					overlappingEventsCount = 0;
					m.VertLineA = new Point(x, eventLineTop);
					m.VertLineB = new Point(x, viewMetrics.ActivitiesViewHeight);
				}
				m.CaptionRect = bounds;
				m.CaptionDrawingOrigin = new Point((bounds.Left + bounds.Right) / 2, eventLineTop);

				if (evt.Type == EventType.UserAction)
					m.Icon = viewMetrics.UserIcon;
				else if (evt.Type == EventType.APICall)
					m.Icon = viewMetrics.APIIcon;
				if (m.Icon != null) 
				{
					var imgsz = m.Icon.GetSize(height: 14f).Scale(viewMetrics.DPIScale);
					m.IconRect = new RectangleF (
						m.CaptionDrawingOrigin.X - imgsz.Width / 2, 
						viewMetrics.RulersPanelHeight - imgsz.Height - viewMetrics.ActionLebelHeight, 
						imgsz.Width, imgsz.Height
					);
				}

				lastEventRight = bounds.Right;

				yield return m;
			}
		}

		public static IEnumerable<BookmarkMetrics> GetBookmarksMetrics(LJD.Graphics g, ViewMetrics viewMetrics, IViewEvents eventsHandler)
		{
			double availableWidth = viewMetrics.ActivitiesViewWidth;
			foreach (var bmk in eventsHandler.OnDrawBookmarks(DrawScope.VisibleRange))
			{
				if (bmk.X < 0 || bmk.X > 1)
					continue;

				BookmarkMetrics m = new BookmarkMetrics() { Bookmark = bmk };

				int x = SafeGetScreenX(bmk.X, availableWidth);
				int bmkLineTop = viewMetrics.RulersPanelHeight - 7;
				m.VertLineA = new Point(x, bmkLineTop);
				m.VertLineB = new Point(x, viewMetrics.ActivitiesViewHeight);

				m.Icon = viewMetrics.BookmarkIcon;
				var sz = m.Icon.GetSize(height: 5.1f).Scale(viewMetrics.DPIScale);
				m.IconRect = new RectangleF(
					x - sz.Width / 2, bmkLineTop - sz.Height, sz.Width, sz.Height);

				yield return m;
			}
		}

		public static int GetActivityY(ViewMetrics viewMetrics, int index)
		{
			return viewMetrics.RulersPanelHeight - viewMetrics.VScrollBarValue + index * viewMetrics.LineHeight;
		}

		public static IEnumerable<TLRulerMark> GetRulerMarks(
			ViewMetrics viewMetrics,
			IViewEvents eventsHandler,
			DrawScope scope
		)
		{
			return eventsHandler.OnDrawRulers(scope, viewMetrics.ActivitiesViewWidth, viewMetrics.DistnanceBetweenRulerMarks);
		}

		public static int GetSequenceDiagramAreaMetrics(
			LJD.Graphics g,
			ViewMetrics viewMetrics,
			IViewEvents eventsHandler
		)
		{
			float maxTextWidth = 0;
			if (eventsHandler != null)
			{
				foreach (var a in eventsHandler.OnDrawActivities())
				{
					if (!string.IsNullOrEmpty(a.SequenceDiagramText))
					{
						var w = g.MeasureString(a.SequenceDiagramText, viewMetrics.ActivitesCaptionsFont).Width;
						maxTextWidth = Math.Max(maxTextWidth, w);
					}
				}
			}
			return Math.Min((int)Math.Ceiling(maxTextWidth), viewMetrics.ActivitesCaptionsViewWidth / 2);
		}

		public static HitTestResult HitTest(
			Point pt, 
			ViewMetrics viewMetrics,
			IViewEvents eventsHandler,
			bool isCaptionsView,
			Func<LJD.Graphics> graphicsFactory
		)
		{
			HitTestResult ret = new HitTestResult ();

			double viewWidth = viewMetrics.ActivitiesViewWidth;
			ret.RelativeX = (double)pt.X / viewWidth;
			ret.ActivityIndex = GetActivityByPoint(pt, viewMetrics);

			if (isCaptionsView)
			{
				ret.Area = HitTestResult.AreaCode.CaptionsPanel;
				return ret;
			}

			using (var g = graphicsFactory())
			{
				foreach (var bmk in GetBookmarksMetrics(g, viewMetrics, eventsHandler))
				{
					if (bmk.IsOverLink(pt))
					{
						ret.Area = HitTestResult.AreaCode.BookmarkTrigger;
						ret.Trigger = bmk.Bookmark.Trigger;
						return ret;
					}
				}
				foreach (var evt in GetEventMetrics(g, eventsHandler, viewMetrics).Reverse())
				{
					if (evt.IsOverLink(pt))
					{
						ret.Area = HitTestResult.AreaCode.EventTrigger;
						ret.Trigger = evt.Event.Trigger;
						return ret;
					}
				}
			}

			if (pt.Y < viewMetrics.RulersPanelHeight)
			{
				ret.Area = HitTestResult.AreaCode.RulersPanel;
				return ret;
			}

			foreach (var a in GetActivitiesMetrics(viewMetrics, eventsHandler))
			{
				foreach (var ms in a.Milestones)
				{
					if (ms.Bounds.HasValue && ms.Bounds.Value.Contains(pt))
					{
						ret.Area = HitTestResult.AreaCode.ActivityTrigger;
						ret.Trigger = ms.Trigger;
						return ret;
					}
				}
				if (a.BeginTriggerLinkRect.HasValue && a.BeginTriggerLinkRect.Value.Contains(pt))
				{
					ret.Area = HitTestResult.AreaCode.ActivityTrigger;
					ret.Trigger = a.Activity.BeginTrigger;
					return ret;
				}
				if (a.EndTriggerLinkRect.HasValue && a.EndTriggerLinkRect.Value.Contains(pt))
				{
					ret.Area = HitTestResult.AreaCode.ActivityTrigger;
					ret.Trigger = a.Activity.EndTrigger;
					return ret;
				}
			}

			ret.Area = HitTestResult.AreaCode.ActivitiesPanel;
			return ret;
		}


		public static NavigationPanelMetrics GetNavigationPanelMetrics(
			ViewMetrics viewMetrics,
			IViewEvents eventsHandler)
		{
			NavigationPanelDrawInfo drawInfo = eventsHandler.OnDrawNavigationPanel();
			double width = (double)viewMetrics.NavigationPanelWidth;
			int x1 = (int)(width * drawInfo.VisibleRangeX1);
			int x2 = (int)(width * drawInfo.VisibleRangeX2);

			var visibleRangeBox = new Rectangle(x1, 1, x2 - x1, viewMetrics.NavigationPanelHeight - 4);
			var resizerWidth = Math.Min(viewMetrics.VisibleRangeResizerWidth, Math.Abs(x2 - x1));

			var resizer1 = new Rectangle(visibleRangeBox.Left + 1, visibleRangeBox.Top + 1,
				resizerWidth, visibleRangeBox.Height - 1);
			var resizer2 = new Rectangle(visibleRangeBox.Right - resizerWidth,
				visibleRangeBox.Top + 1, resizerWidth, visibleRangeBox.Height - 1);

			return new NavigationPanelMetrics()
			{
				VisibleRangeBox = visibleRangeBox,
				Resizer1 = resizer1,
				Resizer2 = resizer2
			};
		}

		static int GetActivityByPoint(Point pt, ViewMetrics vm)
		{
			if (pt.Y < vm.RulersPanelHeight)
				return -1;
			return (pt.Y + vm.VScrollBarValue - vm.RulersPanelHeight) / vm.LineHeight;
		}

		public static int SafeGetScreenX(double x, double viewWidth)
		{
			int maxX = 10000;
			if (x > maxX)
				return maxX;
			else if (x < -maxX)
				return -maxX;
			var ret = (int)(x * viewWidth);
			if (ret > maxX)
				return maxX;
			else if (ret < -maxX)
				return -maxX;
			return ret;
		}

		public static CursorType GetCursor(
			Point pt, 
			ViewMetrics vm,
			IViewEvents eventsHandler,
			Func<LJD.Graphics> graphicsFactory
		)
		{
			foreach (var a in Metrics.GetActivitiesMetrics(vm, eventsHandler))
			{
				bool overLink = false;
				overLink = overLink || (a.BeginTriggerLinkRect.HasValue && a.BeginTriggerLinkRect.Value.Contains(pt));
				overLink = overLink || (a.EndTriggerLinkRect.HasValue && a.EndTriggerLinkRect.Value.Contains(pt));
				overLink = overLink || a.Milestones.Any(ms => ms.Bounds.HasValue && ms.Bounds.Value.Contains(pt));
				if (overLink)
				{
					return CursorType.Hand;
				}
			}
			using (var g = graphicsFactory())
			{
				foreach (var bmk in Metrics.GetBookmarksMetrics(g, vm, eventsHandler))
				{
					if (bmk.IsOverLink(pt))
					{
						return CursorType.Hand;
					}
				}
				foreach (var evt in Metrics.GetEventMetrics(g, eventsHandler, vm))
				{
					if (evt.IsOverLink(pt))
					{
						return CursorType.Hand;
					}
				}
			}
			return CursorType.Default;
		}
	}
}

