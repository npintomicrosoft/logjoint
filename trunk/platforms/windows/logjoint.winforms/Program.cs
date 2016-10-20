using LogJoint.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace LogJoint
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(WireupDependenciesAndCreateMainForm());
		}

		static Form WireupDependenciesAndCreateMainForm()
		{
			var tracer = new LJTraceSource("App", "app");

			using (tracer.NewFrame)
			{
				ILogProviderFactoryRegistry logProviderFactoryRegistry = new LogProviderFactoryRegistry();
				IFormatDefinitionsRepository formatDefinitionsRepository = new DirectoryFormatsRepository(null);
				ITempFilesManager tempFilesManager = LogJoint.TempFilesManager.GetInstance();
				IUserDefinedFormatsManager userDefinedFormatsManager = new UserDefinedFormatsManager(
					formatDefinitionsRepository, logProviderFactoryRegistry, tempFilesManager);
				var appInitializer = new AppInitializer(tracer, userDefinedFormatsManager, logProviderFactoryRegistry);
				tracer.Info("app initializer created");
				var mainForm = new UI.MainForm();
				tracer.Info("main form created");
				IInvokeSynchronization invokingSynchronization = new InvokeSynchronization(mainForm);
				UI.HeartBeatTimer heartBeatTimer = new UI.HeartBeatTimer(mainForm);
				UI.Presenters.IViewUpdates viewUpdates = heartBeatTimer;
				IFiltersFactory filtersFactory = new FiltersFactory();
				IBookmarksFactory bookmarksFactory = new BookmarksFactory();
				var bookmarks = bookmarksFactory.CreateBookmarks();
				var persistentUserDataFileSystem = Persistence.Implementation.DesktopFileSystemAccess.CreatePersistentUserDataFileSystem();
				Persistence.Implementation.IStorageManagerImplementation userDataStorage = new Persistence.Implementation.StorageManagerImplementation();
				IShutdown shutdown = new Shutdown();
				Persistence.IStorageManager storageManager = new Persistence.PersistentUserDataManager(userDataStorage, shutdown);
				Settings.IGlobalSettingsAccessor globalSettingsAccessor = new Settings.GlobalSettingsAccessor(storageManager);
				userDataStorage.Init(
					 new Persistence.Implementation.RealTimingAndThreading(),
					 persistentUserDataFileSystem,
					 new Persistence.PersistentUserDataManager.ConfigAccess(globalSettingsAccessor)
				);
				Persistence.IFirstStartDetector firstStartDetector = persistentUserDataFileSystem;
				Persistence.Implementation.IStorageManagerImplementation contentCacheStorage = new Persistence.Implementation.StorageManagerImplementation();
				contentCacheStorage.Init(
					 new Persistence.Implementation.RealTimingAndThreading(),
					 Persistence.Implementation.DesktopFileSystemAccess.CreateCacheFileSystemAccess(),
					 new Persistence.ContentCacheManager.ConfigAccess(globalSettingsAccessor)
				);
				Persistence.IContentCache contentCache = new Persistence.ContentCacheManager(contentCacheStorage);
				Persistence.IWebContentCache webContentCache = new Persistence.WebContentCache(
					contentCache,
					new WebContentCacheConfig()
				);
				MultiInstance.IInstancesCounter instancesCounter = new MultiInstance.InstancesCounter(shutdown);
				Progress.IProgressAggregatorFactory progressAggregatorFactory = new Progress.ProgressAggregator.Factory(heartBeatTimer, invokingSynchronization);
				Progress.IProgressAggregator progressAggregator = progressAggregatorFactory.CreateProgressAggregator();

				IAdjustingColorsGenerator colorGenerator = new AdjustingColorsGenerator(
					new PastelColorsGenerator(),
					globalSettingsAccessor.Appearance.ColoringBrightness
				);

				IModelThreads modelThreads = new ModelThreads(colorGenerator);

				ILogSourcesManager logSourcesManager = new LogSourcesManager(
					heartBeatTimer,
					invokingSynchronization,
					modelThreads,
					tempFilesManager,
					storageManager,
					bookmarks,
					globalSettingsAccessor
				);

				Telemetry.ITelemetryCollector telemetryCollector = new Telemetry.TelemetryCollector(
					storageManager,
					new Telemetry.ConfiguredAzureTelemetryUploader(),
					invokingSynchronization,
					instancesCounter,
					shutdown,
					logSourcesManager
				);
				tracer.Info("telemetry created");

				MRU.IRecentlyUsedEntities recentlyUsedLogs = new MRU.RecentlyUsedEntities(
					storageManager,
					logProviderFactoryRegistry,
					telemetryCollector
				);

				IFormatAutodetect formatAutodetect = new FormatAutodetect(
					recentlyUsedLogs, 
					logProviderFactoryRegistry, 
					tempFilesManager
				);

				Workspaces.IWorkspacesManager workspacesManager = new Workspaces.WorkspacesManager(
					logSourcesManager,
					logProviderFactoryRegistry,
					storageManager,
					new Workspaces.Backend.AzureWorkspacesBackend(),
					tempFilesManager,
					recentlyUsedLogs,
					shutdown
				);

				AppLaunch.ILaunchUrlParser launchUrlParser = new AppLaunch.LaunchUrlParser();

				var pluggableProtocolManager = new PluggableProtocolManager(
					instancesCounter, 
					shutdown, 
					telemetryCollector,
					firstStartDetector,
					launchUrlParser
				);

				Preprocessing.IPreprocessingManagerExtensionsRegistry preprocessingManagerExtensionsRegistry = 
					new Preprocessing.PreprocessingManagerExtentionsRegistry();

				Preprocessing.ICredentialsCache preprocessingCredentialsCache = new UI.LogsPreprocessorCredentialsCache(
					invokingSynchronization,
					storageManager.GlobalSettingsEntry,
					mainForm
				);

				Preprocessing.IPreprocessingStepsFactory preprocessingStepsFactory = new Preprocessing.PreprocessingStepsFactory(
					workspacesManager,
					launchUrlParser,
					invokingSynchronization,
					preprocessingManagerExtensionsRegistry,
					progressAggregator,
					webContentCache,
					preprocessingCredentialsCache,
					logProviderFactoryRegistry
				);

				Preprocessing.ILogSourcesPreprocessingManager logSourcesPreprocessings = new Preprocessing.LogSourcesPreprocessingManager(
					invokingSynchronization,
					formatAutodetect,
					preprocessingManagerExtensionsRegistry,
					new Preprocessing.BuiltinStepsExtension(preprocessingStepsFactory),
					telemetryCollector,
					tempFilesManager
				);

				ISearchManager searchManager = new SearchManager(
					logSourcesManager,
					progressAggregatorFactory,
					invokingSynchronization,
					globalSettingsAccessor,
					telemetryCollector,
					heartBeatTimer
				);

				ISearchHistory searchHistory = new SearchHistory(storageManager.GlobalSettingsEntry);

				ILogSourcesController logSourcesController = new LogSourcesController(
					logSourcesManager,
					logSourcesPreprocessings,
					recentlyUsedLogs,
					shutdown
				);

				IBookmarksController bookmarksController = new BookmarkController(
					bookmarks,
					modelThreads,
					heartBeatTimer
				);

				IFiltersManager filtersManager = new FiltersManager(
					filtersFactory, 
					globalSettingsAccessor, 
					logSourcesManager, 
					colorGenerator, 
					shutdown
				);

				tracer.Info("model creation completed");


				var presentersFacade = new UI.Presenters.Facade();
				UI.Presenters.IPresentersFacade navHandler = presentersFacade;

				UI.Presenters.IClipboardAccess clipboardAccess = new ClipboardAccess(telemetryCollector);

				UI.Presenters.IShellOpen shellOpen = new ShellOpen();

				UI.Presenters.LogViewer.IPresenterFactory logViewerPresenterFactory = new UI.Presenters.LogViewer.PresenterFactory(
					heartBeatTimer,
					presentersFacade,
					clipboardAccess,
					bookmarksFactory,
					telemetryCollector,
					logSourcesManager,
					invokingSynchronization,
					modelThreads,
					filtersManager.HighlightFilters,
					bookmarks,
					globalSettingsAccessor,
					searchManager,
					filtersFactory
				);

				UI.Presenters.LoadedMessages.IView loadedMessagesView = mainForm.loadedMessagesControl;
				UI.Presenters.LoadedMessages.IPresenter loadedMessagesPresenter = new UI.Presenters.LoadedMessages.Presenter(
					logSourcesManager,
					bookmarks,
					loadedMessagesView,
					heartBeatTimer,
					logViewerPresenterFactory
				);

				UI.Presenters.LogViewer.IPresenter viewerPresenter = loadedMessagesPresenter.LogViewerPresenter;

				UI.Presenters.ITabUsageTracker tabUsageTracker = new UI.Presenters.TabUsageTracker();

				UI.Presenters.StatusReports.IPresenter statusReportsPresenter = new UI.Presenters.StatusReports.Presenter(
					new UI.StatusReportView(
						mainForm,
						mainForm.toolStripStatusLabel,
						mainForm.cancelLongRunningProcessDropDownButton,
						mainForm.cancelLongRunningProcessLabel
					),
					heartBeatTimer
				);
				UI.Presenters.StatusReports.IPresenter statusReportFactory = statusReportsPresenter;

				UI.Presenters.Timeline.IPresenter timelinePresenter = new UI.Presenters.Timeline.Presenter(
					logSourcesManager,
					logSourcesPreprocessings,
					searchManager,
					bookmarks,
					mainForm.timeLinePanel.TimelineControl,
					viewerPresenter,
					statusReportFactory,
					tabUsageTracker,
					heartBeatTimer);

				UI.Presenters.TimelinePanel.IPresenter timelinePanelPresenter = new UI.Presenters.TimelinePanel.Presenter(
					logSourcesManager,
					bookmarks,
					mainForm.timeLinePanel,
					timelinePresenter,
					heartBeatTimer);

				UI.Presenters.SearchResult.IPresenter searchResultPresenter = new UI.Presenters.SearchResult.Presenter(
					searchManager,
					bookmarks,
					filtersManager.HighlightFilters,
					mainForm.searchResultView,
					navHandler,
					loadedMessagesPresenter,
					heartBeatTimer,
					invokingSynchronization,
					statusReportFactory,
					logViewerPresenterFactory
				);

				UI.Presenters.ThreadsList.IPresenter threadsListPresenter = new UI.Presenters.ThreadsList.Presenter(
					modelThreads,
					logSourcesManager, 
					mainForm.threadsListView,
					viewerPresenter,
					navHandler,
					viewUpdates,
					heartBeatTimer);
				tracer.Info("threads list presenter created");

				UI.Presenters.SearchPanel.IPresenter searchPanelPresenter = new UI.Presenters.SearchPanel.Presenter(
					mainForm.searchPanelView,
					searchManager,
					searchHistory,
					logSourcesManager,
					new UI.SearchResultsPanelView() { container = mainForm.splitContainer_Log_SearchResults },
					loadedMessagesPresenter,
					searchResultPresenter,
					statusReportFactory);
				tracer.Info("search panel presenter created");

				UI.Presenters.IAlertPopup alertPopup = new Alerts();

				UI.Presenters.SourcePropertiesWindow.IPresenter sourcePropertiesWindowPresenter = 
					new UI.Presenters.SourcePropertiesWindow.Presenter(
						new UI.SourceDetailsWindowView(),
						logSourcesManager,
						logSourcesPreprocessings,
						navHandler,
						alertPopup,
						clipboardAccess,
						shellOpen
					);

				UI.Presenters.SourcesList.IPresenter sourcesListPresenter = new UI.Presenters.SourcesList.Presenter(
					logSourcesManager,
					mainForm.sourcesListView.SourcesListView,
					logSourcesPreprocessings,
					sourcePropertiesWindowPresenter,
					viewerPresenter,
					navHandler,
					alertPopup,
					clipboardAccess,
					shellOpen
				);


				UI.LogsPreprocessorUI logsPreprocessorUI = new UI.LogsPreprocessorUI(
					logSourcesPreprocessings,
					mainForm,
					statusReportsPresenter);

				UI.Presenters.Help.IPresenter helpPresenter = new UI.Presenters.Help.Presenter();

				AppLaunch.ICommandLineHandler commandLineHandler = new AppLaunch.CommandLineHandler(
					logSourcesPreprocessings,
					preprocessingStepsFactory);

				UI.Presenters.SharingDialog.IPresenter sharingDialogPresenter = new UI.Presenters.SharingDialog.Presenter(
					logSourcesManager,
					workspacesManager,
					logSourcesPreprocessings,
					alertPopup,
					clipboardAccess,
					new UI.ShareDialog()
				);

				UI.Presenters.HistoryDialog.IView historyDialogView = new UI.HistoryDialog();
				UI.Presenters.HistoryDialog.IPresenter historyDialogPresenter = new UI.Presenters.HistoryDialog.Presenter(
					logSourcesController,
					historyDialogView,
					logSourcesPreprocessings,
					preprocessingStepsFactory,
					recentlyUsedLogs,
					new UI.Presenters.QuickSearchTextBox.Presenter(historyDialogView.QuickSearchTextBox),
					alertPopup
				);

				UI.Presenters.NewLogSourceDialog.IPagePresentersRegistry newLogPagesPresentersRegistry =
					new UI.Presenters.NewLogSourceDialog.PagePresentersRegistry();

				UI.Presenters.NewLogSourceDialog.IPresenter newLogSourceDialogPresenter = new UI.Presenters.NewLogSourceDialog.Presenter(
					logProviderFactoryRegistry,
					newLogPagesPresentersRegistry,
					recentlyUsedLogs,
					new UI.NewLogSourceDialogView(),
					userDefinedFormatsManager,
					() => new UI.Presenters.NewLogSourceDialog.Pages.FormatDetection.Presenter(
						new UI.Presenters.NewLogSourceDialog.Pages.FormatDetection.AnyLogFormatUI(),
						logSourcesPreprocessings,
						preprocessingStepsFactory
					),
					new UI.Presenters.FormatsWizard.Presenter(() => // stub presenter implemenation. proper impl is to be done.
					{
						using (ManageFormatsWizard w = new ManageFormatsWizard(
								tempFilesManager, logProviderFactoryRegistry, userDefinedFormatsManager, logViewerPresenterFactory, helpPresenter))
						{
							w.ExecuteWizard();
						}
					})
				);

				newLogPagesPresentersRegistry.RegisterPagePresenterFactory(
					StdProviderFactoryUIs.FileBasedProviderUIKey,
					f => new UI.Presenters.NewLogSourceDialog.Pages.FileBasedFormat.Presenter(
						new UI.Presenters.NewLogSourceDialog.Pages.FileBasedFormat.FileLogFactoryUI(), 
						(IFileBasedLogProviderFactory)f,
						logSourcesController,
						alertPopup
					)
				);
				newLogPagesPresentersRegistry.RegisterPagePresenterFactory(
					StdProviderFactoryUIs.DebugOutputProviderUIKey, 
					f => new UI.Presenters.NewLogSourceDialog.Pages.DebugOutput.Presenter(
						new UI.Presenters.NewLogSourceDialog.Pages.DebugOutput.DebugOutputFactoryUI(),
						f,
						logSourcesController
					)
				);
				newLogPagesPresentersRegistry.RegisterPagePresenterFactory(
					StdProviderFactoryUIs.WindowsEventLogProviderUIKey,
					f => new UI.Presenters.NewLogSourceDialog.Pages.WindowsEventsLog.Presenter(
						new UI.Presenters.NewLogSourceDialog.Pages.WindowsEventsLog.EVTFactoryUI(),
						f,
						logSourcesController
					)
				);

				UI.Presenters.SourcesManager.IPresenter sourcesManagerPresenter = new UI.Presenters.SourcesManager.Presenter(
					logSourcesManager,
					userDefinedFormatsManager,
					recentlyUsedLogs,
					logSourcesPreprocessings,
					logSourcesController,
					mainForm.sourcesListView,
					preprocessingStepsFactory,
					workspacesManager,
					sourcesListPresenter,
					newLogSourceDialogPresenter,
					heartBeatTimer,
					sharingDialogPresenter,
					historyDialogPresenter,
					presentersFacade,
					sourcePropertiesWindowPresenter,
					alertPopup
				);


				UI.Presenters.MessagePropertiesDialog.IPresenter messagePropertiesDialogPresenter = new UI.Presenters.MessagePropertiesDialog.Presenter(
					bookmarks,
					filtersManager.HighlightFilters,
					new MessagePropertiesDialogView(mainForm),
					viewerPresenter,
					navHandler);


				Func<IFiltersList, UI.FiltersManagerView, UI.Presenters.FiltersManager.IPresenter> createFiltersManager = (filters, view) =>
				{
					var dialogPresenter = new UI.Presenters.FilterDialog.Presenter(logSourcesManager, filters, new UI.FilterDialogView(filtersFactory));
					UI.Presenters.FiltersListBox.IPresenter listPresenter = new UI.Presenters.FiltersListBox.Presenter(filters, view.FiltersListView, dialogPresenter);
					UI.Presenters.FiltersManager.IPresenter managerPresenter = new UI.Presenters.FiltersManager.Presenter(
						filters, view, listPresenter, dialogPresenter, viewerPresenter, viewUpdates, heartBeatTimer, filtersFactory);
					return managerPresenter;
				};

				UI.Presenters.FiltersManager.IPresenter hlFiltersManagerPresenter = createFiltersManager(
					filtersManager.HighlightFilters,
					mainForm.hlFiltersManagementView);

				UI.Presenters.BookmarksList.IPresenter bookmarksListPresenter = new UI.Presenters.BookmarksList.Presenter(
					bookmarks,
					logSourcesManager,
					mainForm.bookmarksManagerView.ListView,
					heartBeatTimer,
					loadedMessagesPresenter,
					clipboardAccess);

				UI.Presenters.BookmarksManager.IPresenter bookmarksManagerPresenter = new UI.Presenters.BookmarksManager.Presenter(
					bookmarks,
					mainForm.bookmarksManagerView,
					viewerPresenter,
					searchResultPresenter,
					bookmarksListPresenter,
					statusReportFactory,
					navHandler,
					viewUpdates,
					alertPopup
				);

				AutoUpdate.IAutoUpdater autoUpdater = new AutoUpdate.AutoUpdater(
					instancesCounter,
					new AutoUpdate.ConfiguredAzureUpdateDownloader(),
					tempFilesManager,
					shutdown,
					invokingSynchronization,
					firstStartDetector
				);


				var unhandledExceptionsReporter = new Telemetry.WinFormsUnhandledExceptionsReporter(
					telemetryCollector
				);

				UI.Presenters.Options.Dialog.IPresenter optionsDialogPresenter = new UI.Presenters.Options.Dialog.Presenter(
					new OptionsDialogView(),
					pageView => new UI.Presenters.Options.MemAndPerformancePage.Presenter(globalSettingsAccessor, recentlyUsedLogs, searchHistory, pageView),
					pageView => new UI.Presenters.Options.Appearance.Presenter(globalSettingsAccessor, pageView, logViewerPresenterFactory),
					pageView => new UI.Presenters.Options.UpdatesAndFeedback.Presenter(autoUpdater, globalSettingsAccessor, pageView)
				);

				DragDropHandler dragDropHandler = new DragDropHandler(
					logSourcesController,
					logSourcesPreprocessings, 
					preprocessingStepsFactory
				);

				UI.Presenters.About.IPresenter aboutDialogPresenter = new UI.Presenters.About.Presenter(
					new AboutBox(),
					new AboutDialogConfig(),
					clipboardAccess,
					autoUpdater
				);

				UI.Presenters.WebBrowserDownloader.IPresenter webBrowserDownloaderFormPresenter = new UI.Presenters.WebBrowserDownloader.Presenter(
					new LogJoint.Skype.WebBrowserDownloader.WebBrowserDownloaderForm(),
					invokingSynchronization,
					webContentCache
				);

				UI.Presenters.TimestampAnomalyNotification.IPresenter timestampAnomalyNotificationPresenter = new UI.Presenters.TimestampAnomalyNotification.Presenter(
					logSourcesManager,
					logSourcesPreprocessings,
					invokingSynchronization,
					heartBeatTimer,
					presentersFacade,
					statusReportsPresenter
				);

				UI.Presenters.MainForm.IPresenter mainFormPresenter = new UI.Presenters.MainForm.Presenter(
					logSourcesManager,
					mainForm,
					viewerPresenter,
					searchResultPresenter,
					searchPanelPresenter,
					sourcesListPresenter,
					sourcesManagerPresenter,
					messagePropertiesDialogPresenter,
					loadedMessagesPresenter,
					bookmarksManagerPresenter,
					heartBeatTimer,
					tabUsageTracker,
					statusReportFactory,
					dragDropHandler,
					navHandler,
					autoUpdater,
					progressAggregator,
					alertPopup,
					sharingDialogPresenter,
					shutdown
				);
				tracer.Info("main form presenter created");

				Extensibility.IApplication pluginEntryPoint = new Extensibility.Application(
					new Extensibility.Model(
						invokingSynchronization,
						telemetryCollector,
						webContentCache,
						contentCache,
						storageManager,
						bookmarks,
						logSourcesManager,
						modelThreads,
						tempFilesManager,
						preprocessingManagerExtensionsRegistry,
						logSourcesPreprocessings,
						preprocessingStepsFactory,
						progressAggregator,
						logProviderFactoryRegistry,
						userDefinedFormatsManager,
						recentlyUsedLogs,
						progressAggregatorFactory,
						heartBeatTimer,
						logSourcesController,
						shutdown
					),
					new Extensibility.Presentation(
						loadedMessagesPresenter,
						clipboardAccess,
						presentersFacade,
						sourcesManagerPresenter,
						webBrowserDownloaderFormPresenter,
						newLogSourceDialogPresenter,
						shellOpen,
						alertPopup
					),
					new Extensibility.View(
						mainForm
					)
				);

				var pluginsManager = new Extensibility.PluginsManager(
					pluginEntryPoint,
					mainFormPresenter,
					telemetryCollector,
					shutdown
				);
				tracer.Info("plugin manager created");

				appInitializer.WireUpCommandLineHandler(mainFormPresenter, commandLineHandler);

				presentersFacade.Init(
					messagePropertiesDialogPresenter,
					threadsListPresenter,
					sourcesListPresenter,
					bookmarksManagerPresenter,
					mainFormPresenter,
					aboutDialogPresenter,
					optionsDialogPresenter,
					historyDialogPresenter
				);

				return mainForm;
			}
		}
	}
}