﻿// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LogJoint.UI
{
	[Register ("TimelinePanelControl")]
	partial class TimelinePanelControl
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}

	[Register ("TimelinePanelControlAdapter")]
	partial class TimelinePanelControlAdapter
	{
		[Outlet]
		AppKit.NSView timelineControlPlaceholder { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (timelineControlPlaceholder != null) {
				timelineControlPlaceholder.Dispose ();
				timelineControlPlaceholder = null;
			}
		}
	}
}
