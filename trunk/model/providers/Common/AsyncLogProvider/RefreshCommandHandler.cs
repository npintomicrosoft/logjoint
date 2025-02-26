using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

namespace LogJoint
{
	internal class RefreshCommandHandler : IAsyncLogProviderCommandHandler
	{
		public RefreshCommandHandler(IAsyncLogProvider owner, bool incremental)
		{
			this.owner = owner;
			this.incremental = incremental;
		}

		public Task Task { get { return task.Task; } }

		bool IAsyncLogProviderCommandHandler.RunSynchroniously(CommandContext ctx)
		{
			return false;
		}

		void IAsyncLogProviderCommandHandler.ContinueAsynchroniously(CommandContext ctx)
		{
			owner.UpdateAvailableTime(incremental);
		}

		void IAsyncLogProviderCommandHandler.Complete(Exception e)
		{
			task.SetResult(0);
		}

		readonly IAsyncLogProvider owner;
		readonly bool incremental;
		readonly TaskCompletionSource<int> task = new TaskCompletionSource<int>();
	};
}