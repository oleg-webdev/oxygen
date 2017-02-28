﻿using CloudBeat.Oxygen.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Threading;

namespace CloudBeat.Oxygen.Modules
{
    public class ModuleWeb : Module, IModule
	{
        public SeleniumDriver driver { get; private set; }
        private Proxy proxy = null;
        private ScreenshotMode screenshotMode = ScreenshotMode.OnError;
        private bool fetchStats = false;
		private bool reopenBrowserOnIteration = false;
        private long prevNavigationStart = long.MinValue;
		private bool autoInitDriver = false;
		private string seleniumUrl;
        private bool proxyEnabled;
        private string proxyExe;
        private string proxyKey;
        private string proxyCer;
		private DesiredCapabilities capabilities;
        public string prevTransaction { get; private set; }

        private IDictionary<string, string> transactions = new Dictionary<string, string>();

		#region Defauts
		private const string DEFAULT_BROWSER_NAME = "internetexplorer"; // FIXME: default is not needed. browser should be mandatory?
		private const int PROXY_CONN_RETRY_COUNT = 10;
		private const int SELENIUM_CONN_RETRY_COUNT = 2;
		#endregion

		#region Argument Names
        const string ARG_PROXY_ENABLED = "proxyEnabled";
        const string ARG_PROXY_EXE = "proxyExe";
        const string ARG_PROXY_KEY = "proxyKey";
        const string ARG_PROXY_CER = "proxyCer";
        const string ARG_SELENIUM_URL = "seleniumUrl";
        const string ARG_INIT_DRIVER = "initDriver";
        const string ARG_BROWSER_NAME = "browserName";
		const string ARG_REOPEN_BROWSER = "reopenBrowser";
		const string ARG_SCREENSHOT_MODE = "screenshots";
        const string ARG_FETCH_STATS = "fetchStats";
		#endregion

		public ModuleWeb()
		{
		}

		#region General Public Functions

		public object IterationStarted()
		{
			// initialize selenium driver if auto init option is on and the driver is not initialized already
            if (!IsInitialized && autoInitDriver)
				InitializeSeleniumDriver();
			return null;
		}

        public object IterationEnded()
		{
			if (reopenBrowserOnIteration)
				Dispose();

            // har won't be fetched for last transaction, so we do it here
            if (proxy != null)
            {
                String har = proxy.HarGet();
                transactions.Remove(prevTransaction);
                transactions.Add(prevTransaction, har);
                proxy.HarReset();
            }

			return transactions;
		}

		public bool Initialize(Dictionary<string, string> args, ExecutionContext ctx)
		{
			this.ctx = ctx;

            proxyEnabled = args.ContainsKey(ARG_PROXY_ENABLED) && args[ARG_PROXY_ENABLED].Equals("true", StringComparison.InvariantCultureIgnoreCase);
            if (args.ContainsKey(ARG_PROXY_EXE) && !string.IsNullOrEmpty(args[ARG_PROXY_EXE]))
                proxyExe = args[ARG_PROXY_EXE];
            if (args.ContainsKey(ARG_PROXY_KEY) && !string.IsNullOrEmpty(args[ARG_PROXY_KEY]))
                proxyKey = args[ARG_PROXY_KEY];
            if (args.ContainsKey(ARG_PROXY_CER) && !string.IsNullOrEmpty(args[ARG_PROXY_CER]))
                proxyCer = args[ARG_PROXY_CER];

			if (args.ContainsKey(ARG_SELENIUM_URL) && !string.IsNullOrEmpty(args[ARG_SELENIUM_URL]))
				seleniumUrl = args[ARG_SELENIUM_URL];
			// screenshot mode
			if (args.ContainsKey(ARG_SCREENSHOT_MODE) && !string.IsNullOrEmpty(args[ARG_SCREENSHOT_MODE]))
			{
				var mode = args[ARG_SCREENSHOT_MODE];
				if (mode == "always")
					screenshotMode = ScreenshotMode.Always;
				else if (mode == "never")
					screenshotMode = ScreenshotMode.Never;
			}

            if (args.ContainsKey(ARG_FETCH_STATS) && args[ARG_FETCH_STATS].Equals("true", StringComparison.InvariantCultureIgnoreCase))
                fetchStats = true;

            autoInitDriver = args.ContainsKey(ARG_INIT_DRIVER) && args[ARG_INIT_DRIVER].Equals("true", StringComparison.InvariantCultureIgnoreCase);
            reopenBrowserOnIteration = args.ContainsKey(ARG_REOPEN_BROWSER) && args[ARG_REOPEN_BROWSER].Equals("true", StringComparison.InvariantCultureIgnoreCase);
			// initialize DesiredCapabilities with provided browser
			if (args.ContainsKey(ARG_BROWSER_NAME))
				capabilities = DCFactory.Get(args[ARG_BROWSER_NAME]);
			// add other capabilities if specified in arguments
			foreach (var key in args.Keys)
			{
				if (!key.StartsWith("web@cap:"))
					continue;
				if (capabilities == null)
					capabilities = new DesiredCapabilities();
				var capName = key.Replace("web@cap:", "");
				var capVal = args[key];
				capabilities.SetCapability(capName, capVal);
			}
			if (autoInitDriver)
				InitializeSeleniumDriver();
            IsInitialized = true;
			return true;
		}

		public void Quit()
		{
			Dispose();
		}

		protected void InitializeSeleniumDriver()
		{
            if (capabilities == null)
                capabilities = DCFactory.Get(DEFAULT_BROWSER_NAME);

            if (proxyEnabled)
            {
                proxy = Proxy.Create(proxyExe, proxyKey, proxyCer);

                OpenQA.Selenium.Proxy selProxy = new OpenQA.Selenium.Proxy
                {
                    HttpProxy = proxy.proxyAddr + ":" + proxy.proxyPort,
                    SslProxy = proxy.proxyAddr + ":" + proxy.proxyPort
                };
                capabilities.SetCapability(CapabilityType.Proxy, selProxy);
            }

			try
			{
				driver = ConnectToSelenium(capabilities, proxy, seleniumUrl, ctx);
                driver.SeCmdSetWindowSize(0, 0);
			}
			catch (Exception e)
			{
				throw new OxModuleInitializationException("Can't initialize web module", e);
			}
			if (driver == null)
				throw new OxModuleInitializationException("Can't initialize Selenium driver in web module");	
		}

		protected SeleniumDriver ConnectToSelenium(DesiredCapabilities dc, Proxy proxy, string seleniumUrl, CloudBeat.Oxygen.ExecutionContext context)
		{
			int connectAttempt = 0;
			while (true)
			{
				try
				{
                    return new SeleniumDriver(new Uri(seleniumUrl), dc, context);
				}
				catch (Exception e)
				{
					if (e is WebDriverException)
					{
						var we = e.InnerException as WebException;
						if (we != null && we.Status == WebExceptionStatus.Timeout)
						{
							connectAttempt++;
							if (connectAttempt >= SELENIUM_CONN_RETRY_COUNT)
								throw;

							Thread.Sleep(1000);	// in case the failure was due to resources overload - wait a bit...
							continue;
						}
						else if (e.Message.Contains("Unable to connect to the remote server"))
						{
							throw new Exception("Unable to connect to Selenium server: " + seleniumUrl);
						}
					}

					throw;
				}
			}
		}

        public bool Dispose()
        {
            try
            {
                if (driver != null)
					driver.Quit();
            } catch (Exception) {
            } // ignore exceptions

            try
            {
                if (proxy != null)
                    proxy.Dispose();
            }
            catch (Exception) {
            } // ignore exceptions
			driver = null;
            proxy = null;
            IsInitialized = false;
			return true;
        }

        public void SetBaseUrl(string url)
        {
			ExecuteCommand(url);
        }

		#endregion

		#region API

		public void Init(string seleniumUrl, Dictionary<string, string> caps, bool resetDefaultCaps = true)
		{
			if (driver != null)
				throw new Exception("Selenium driver has been already initialized");
			// override current selenium url if new is passed in this function
			if (!string.IsNullOrEmpty(seleniumUrl))
				this.seleniumUrl = seleniumUrl;
			if (resetDefaultCaps || this.capabilities == null)
				this.capabilities = new DesiredCapabilities();
			if (caps != null)
			{
				foreach (var cap in caps)
					this.capabilities.SetCapability(cap.Key, cap.Value);
			}
			InitializeSeleniumDriver();
		}

        public string GetSessionId()
        {
            if (driver == null)
                throw new OxModuleInitializationException("Selenium driver is not initialized in web module");
            var sessionIdProperty = typeof(RemoteWebDriver).GetProperty("SessionId", BindingFlags.Instance | BindingFlags.NonPublic);
            SessionId sessionId = sessionIdProperty.GetValue(driver, null) as SessionId;
            if (sessionId != null)
                return sessionId.ToString();
            return null;
        }

        public void transaction(string name)
        {
            // throw in case we hit a duplicate transaction                                 // FIXME: throw
            if (transactions.ContainsKey(name))
            {
                var e = new OxDuplicateTransactionException("Duplicate transaction found: \"" + name + "\". Transactions must be unique.");
                // TODO: thwo on duplicate trnsactions
            }

            transactions.Add(name, null);

            //  fetch har and save it under previous transaction
            if (proxy != null && prevTransaction != null)
            {
                String har = proxy.HarGet();
                transactions.Remove(prevTransaction);
                transactions.Add(prevTransaction, har);
                proxy.HarReset();
            }

            prevTransaction = name;
        }

		public CommandResult SetTimeout(int timeout)
        {
            return ExecuteCommand(timeout);
        }
		public CommandResult Open(string url)
        {
            return ExecuteCommand(url);
        }

		public CommandResult Point(string locator)
        {
            return ExecuteCommand(locator);
        }

		public CommandResult ScrollToElement(string locator, int yOffset)
        {
            return ExecuteCommand(locator, yOffset.ToString());
        }

		public CommandResult Click(string locator)
		{
            return ExecuteCommand(locator);
		}
		public CommandResult ClickHidden(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult AssertTitle(string pattern)
        {
            return ExecuteCommand(pattern);
        }
		public CommandResult Type(string locator, string value)
        {
            return ExecuteCommand(locator, value);
        }
		public CommandResult Clear(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult AssertText(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult SelectWindow(string windowLocator)
        {
            return ExecuteCommand(windowLocator);
        }
		public CommandResult GetText(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult GetAttribute(string locator, string attributeName)
        {
            return ExecuteCommand(locator, attributeName);
        }
		public CommandResult GetValue(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult DoubleClick(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult Select(string selectLocator, string optionLocator)
        {
            return ExecuteCommand(selectLocator, optionLocator);
        }
		public CommandResult Deselect(string selectLocator, string optionLocator)
        {
            return ExecuteCommand(selectLocator, optionLocator);
        }
		public CommandResult Pause(int waitTime)
        {
            return ExecuteCommand(waitTime);
        }
		public CommandResult WaitForWindow(string windowID, int timeout)
        {
            return ExecuteCommand(windowID, timeout);
        }
		public CommandResult SelectFrame(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult WaitForVisible(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult WaitForElementPresent(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult IsElementPresent(string locator, int timeout)
        {
            return ExecuteCommand(locator, timeout);
        }
		public CommandResult IsElementVisible(string locator, int timeout)
        {
            return ExecuteCommand(locator, timeout);
        }
		public CommandResult WaitForText(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult WaitForNotText(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult WaitForValue(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult WaitForNotValue(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult AssertValue(string locator, string pattern)
        {
            return ExecuteCommand(locator, pattern);
        }
		public CommandResult AssertTextPresent(string pattern)
        {
            return ExecuteCommand(pattern);
        }
		public CommandResult AssertElementPresent(string locator)
        {
            return ExecuteCommand(locator);
        }
		public CommandResult AssertAlert(string pattern)
        {
            return ExecuteCommand(pattern);
        }
		public CommandResult GetPageSource()
        {
            return ExecuteCommand();
        }
		public CommandResult GetXMLPageSource()
        {
            return ExecuteCommand();
        }
		public CommandResult GetXMLPageSourceAsJSON()
        {
            return ExecuteCommand();
        }
		public CommandResult GetWindowHandles()
        {
            return ExecuteCommand();
        }
        public CommandResult getElementCount(string xpath)
        {
            return ExecuteCommand(xpath);
        }
		public CommandResult CloseWindow()
        {
            return ExecuteCommand();
        }
		public CommandResult IsAlertPresent(string text, int timeout)
        {
            return ExecuteCommand(text, timeout);
        }
		public CommandResult AlertAccept()
        {
            return ExecuteCommand();
        }
		public CommandResult AlertDismiss()
        {
            return ExecuteCommand();
        }
		public CommandResult AssertSelectedLabel(string locator, string text)
        {
            return ExecuteCommand(locator, text);
        }
		public CommandResult AssertSelectedValue(string locator, string value)
        {
            return ExecuteCommand(locator, value);
        }
		public CommandResult GetAlertText()
        {
            return ExecuteCommand();
        }
		public CommandResult ExecuteScript(string script)
        {
            return ExecuteCommand(script);
        }
        public CommandResult FileBrowse(string filepath)
        {
            return ExecuteCommand(filepath);
        }
        public CommandResult MakeVisible(string locator)
        {
            return ExecuteCommand(locator);
        }
        public CommandResult SetWindowSize(int width, int height)
        {
            return ExecuteCommand(width, height);
        }
		#endregion

		#region Internal Methods Implementation
		private CommandResult ExecuteCommand(params object[] args)
        {
			if (driver == null)
				throw new OxModuleInitializationException("Selenium driver is not initialized in web module");

            var name = new StackTrace().GetFrame(1).GetMethod().Name;
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name", "Command name is null or empty");
			// lowercase the first letter
			name = Char.ToLowerInvariant(name[0]) + name.Substring(1);

            var cmd = new Command(name, args);
            var result = new CommandResult(cmd.ToJSCommand(Name));

            // substitute object repo locators
            object[] argsProcessed = null;
            if (cmd.Arguments != null)
            {
                argsProcessed = new object[cmd.Arguments.Length];
                for (int i = 0; i < cmd.Arguments.Length; i++)
                {
                    var arg = cmd.Arguments[i];
                    argsProcessed[i] = arg.GetType() == typeof(string) ? driver.SubstituteLocator(arg.ToString()) : arg;
                }
            }

            Exception exception = null;
            string screenShot = null;
            object retVal = null;
            try
            {
                Type dtype = driver.GetType();
                MethodInfo cmdMethod = dtype.GetMethod(SeleniumDriver.SE_CMD_METHOD_PREFIX + cmd.CommandName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                if (cmdMethod == null)
                    throw new OxCommandNotImplementedException();

                retVal = cmdMethod.Invoke(driver, argsProcessed);
                if ((screenshotMode == ScreenshotMode.OnAction && cmd.IsAction() == true) || screenshotMode == ScreenshotMode.Always)
                    screenShot = driver.TakeScreenshot();
            }
            catch (OxCommandNotImplementedException ame)
            {
                throw ame;
            }
            catch (TargetInvocationException tie)
            {
                if (screenshotMode != ScreenshotMode.Never)
                {
                    if (tie.InnerException != null &&
                        (tie.InnerException is OxAssertionException ||
                        tie.InnerException is OxVerificationException ||
                        tie.InnerException is OxWaitForException ||
                        tie.InnerException is OxElementNotFoundException ||
                        tie.InnerException is OxElementNotVisibleException ||
                        tie.InnerException is OxOperationException ||
                        tie.InnerException is NoAlertPresentException ||
                        tie.InnerException is WebDriverTimeoutException))
                    {
                        screenShot = driver.TakeScreenshot();
                    }
                    else if (tie.InnerException != null && tie.InnerException is UnhandledAlertException)
                    {
                        // can't take screenshots when alert is showing. so capture whole screen
                        // this works only localy
                        // TODO: linux
                        /*if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
                        {
                            Rectangle bounds = System.Windows.Forms.Screen.GetBounds(Point.Empty);
                            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                            {
                                using (Graphics g = Graphics.FromImage(bitmap))
                                {
                                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                                }

                                ImageConverter converter = new ImageConverter();
                                var sb = (byte[])converter.ConvertTo(bitmap, typeof(byte[]));
                                screenShot = Convert.ToBase64String(sb);
                            }
                        }*/
                    }
                }

                // wrap Selenium exceptions. generaly SeCmds throw Oxygen exceptions, however in certain 
                // cases like with ElementNotVisibleException it's simplier to just wrap it out here so we don't need to do it
                // for each FindElement().doSomething
                if (tie.InnerException is ElementNotVisibleException)
                    exception = new OxElementNotVisibleException();
                else if (tie.InnerException is WebDriverTimeoutException)
                    exception = new OxTimeoutException();
                else
                    exception = tie.InnerException;
            }

			result.IsAction = cmd.IsAction();
			result.Screenshot = screenShot;
			result.ReturnValue = retVal;
			result.IsSuccess = exception == null;
			if (exception != null)
			{
                string statusData = null;
                var status = GetStatusByException(exception, out statusData);
                result = result.ErrorBase(status, statusData);

                if (status == CheckResultStatus.UNKNOWN_ERROR)
                {
                    result.ErrorType = exception.GetType().ToString();
                    result.ErrorMessage = exception.Message;
                    result.ErrorDetails = exception.StackTrace;
                }
            }

            if (fetchStats && cmd.IsAction())
            {
                long navigationStart = 0;
                int domContentLoaded = 0;
                int load = 0;
                if (driver.GetPerformanceTimings(out domContentLoaded, out load, out navigationStart))
                {
                    // if navigateStart equals to the one we got from previous attempt
                    // it means we are still on the same page and don't need to record load/domContentLoaded times
                    if (prevNavigationStart == navigationStart)
                        load = domContentLoaded = 0;
                    else
                        prevNavigationStart = navigationStart;
                }
				result.DomContentLoadedEvent = domContentLoaded;
				result.LoadEvent = load;
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = (result.EndTime - result.StartTime).TotalSeconds;

            return result;
        }

		private CheckResultStatus GetStatusByException(Exception e, out string moreInfo)
		{
			var type = e.GetType();
			moreInfo = null;

			if (type == typeof(OxAssertionException))
				return CheckResultStatus.ASSERT;
			else if (type == typeof(OxVerificationException))
				return CheckResultStatus.VERIFICATION;
			else if (type == typeof(NoSuchElementException))
				return CheckResultStatus.NO_ELEMENT;
			else if (type == typeof(OxElementNotFoundException))
				return CheckResultStatus.NO_ELEMENT;
			else if (type == typeof(OxElementNotVisibleException))
				return CheckResultStatus.ELEMENT_NOT_VISIBLE;
			else if (type == typeof(NoSuchFrameException))
				return CheckResultStatus.FRAME_NOT_FOUND;
			else if (type == typeof(StaleElementReferenceException))
				return CheckResultStatus.STALE_ELEMENT;
			else if (type == typeof(UnhandledAlertException))
			{
				moreInfo = "Alert text: " + driver.SeCmdGetAlertText();
				return CheckResultStatus.UNHANDLED_ALERT;
			}
			else if (type == typeof(OxWaitForException))
				// This is thrown by any WaitFor* commands (e.g. SeCmdWaitForVisible)
				// and essentially implies a script level timeout.
				// By default the timeout is set to SeCommandProcessor.DEFAULT_WAIT_FOR_TIMEOUT but
				// can be overriden in the script using SetTimeout command.
				return CheckResultStatus.SCRIPT_TIMEOUT;
			else if (type == typeof(OxTimeoutException))
				// This is thrown by any commands which rely on PageLoadTimeout (Open, Click, etc.)
				// and essentially implies a script level timeout.
				// By default the timeout is set to SeCommandProcessor.DEFAULT_PAGE_LOAD_TIMEOUT but
				// can be overriden in the script using SetTimeout command.
				return CheckResultStatus.SCRIPT_TIMEOUT;
			else if (type == typeof(WebDriverException))
			{
				var wde = e as WebDriverException;

				// This is thrown when WebDriver does not respond within the command timeout period defined by SeCommandProcessor.TIMEOUT_COMMAND
				// and may occur due to multiple reasons: network errors, webdriver locking due to browser/internal bugs, etc.
				if (wde.InnerException != null && wde.InnerException is WebException)
				{
					var wex = wde.InnerException as WebException;
					if (wex.Status == WebExceptionStatus.Timeout)
						// there seems to be chromedriver bug where open/click will end in command timeout if 'load' event did not fire.
                        return CheckResultStatus.NAVIGATE_TIMEOUT;
				}

				return CheckResultStatus.UNKNOWN_ERROR;
			}
			else if (type == typeof(OxVariableUndefined))
			{
				moreInfo = e.Message;
				return CheckResultStatus.VARIABLE_NOT_DEFINED;
			}
			else if (type == typeof(OxLocatorUndefined))
			{
				moreInfo = e.Message;
				return CheckResultStatus.UNKNOWN_PAGE_OBJECT;
			}
			else if (type == typeof(OxCommandNotImplementedException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.COMMAND_NOT_IMPLEMENTED;
			}
			else if (type == typeof(OxOperationException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.INVALID_OPERATION;
			}
			else if (type == typeof(OxXMLExtractException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.XML_ERROR;
			}
			else if (type == typeof(OxXMLtoJSONConvertException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.XML_ERROR;
			}
			else if (type == typeof(NoAlertPresentException))
				return CheckResultStatus.NO_ALERT_PRESENT;
			else if (type == typeof(OxBrowserJSExecutionException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.BROWSER_JS_EXECUTE_ERROR;
			}
			else if (type == typeof(OxDuplicateTransactionException))
			{
				moreInfo = e.Message;
				return CheckResultStatus.DUPLICATE_TRANSACTION;
			}
			else
			{
				moreInfo = e.Message;
				return CheckResultStatus.UNKNOWN_ERROR;
			}
		}

		#endregion
	}
}
