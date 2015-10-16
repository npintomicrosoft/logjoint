using System;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace LogJoint.UI
{
	public class NetworkCredentialsDialogController: NSObject
	{
		bool confirmed;


		public static bool ShowSheet(NSWindow inWindow) 
		{
			var dlg = new NetworkCredentialsDialogController();
			NSApplication.SharedApplication.BeginSheet (dlg.Window, inWindow);
			return dlg.confirmed;				
		}


		[Export("window")]
		public NetworkCredentialsSheet Window { get; set;}

		[Outlet]
		public NSTextField captionLabel { get; set; }

		[Outlet]
		public NSSecureTextField passwordTextField { get; set; }

		[Outlet]
		public NSTextField userNameTextField { get; set; }

	
		[Export ("OnCancelled:")]
		void OnCancelled(NSObject sender)
		{
			confirmed = false;
			CloseSheet();
		}

		[Export ("OnConfirmed:")]
		void OnConfirmed(NSObject sender)
		{
			confirmed = false;
			CloseSheet();
		}

		NetworkCredentialsDialogController()
		{
			NSBundle.LoadNib ("FilesSelectionDialog", this);
		}
			
		void CloseSheet() 
		{
			NSApplication.SharedApplication.EndSheet (Window);
			Window.Close();
		}
	}
}

