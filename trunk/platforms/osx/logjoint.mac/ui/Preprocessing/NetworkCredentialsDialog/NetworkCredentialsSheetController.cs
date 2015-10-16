using System;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace LogJoint.UI
{
	public class NetworkCredentialsDialogController: NSObject
	{
		string site;
		bool confirmed;

		public static bool ShowSheet(NSWindow inWindow, string site) 
		{
			var dlg = new NetworkCredentialsDialogController(site);
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
		public void OnCancelled(NSObject sender)
		{
			confirmed = false;
			CloseSheet();
		}

		[Export ("OnConfirmed:")]
		public void OnConfirmed(NSObject sender)
		{
			confirmed = false;
			CloseSheet();
		}

		NetworkCredentialsDialogController(string site)
		{
			this.site = site;
			NSBundle.LoadNib ("NetworkCredentialsSheet", this);
		}

		public override void AwakeFromNib()
		{
			base.AwakeFromNib();

			captionLabel.StringValue = site; 
		}
			
		void CloseSheet() 
		{
			NSApplication.SharedApplication.EndSheet (Window);
			Window.Close();
		}
	}
}

