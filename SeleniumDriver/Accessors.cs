﻿using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace CloudBeat.Oxygen
{
    public partial class SeleniumDriver
    {
        // not exposed through web module
        public void SeCmdStore(string expression, string variableName)
        {
            SeCmdStoreExpression(variableName.Trim(), expression);
        }

        // not exposed through web module
        public void SeCmdStoreExpression(string expression, string variableName)
        {
            variables.Add(variableName.Trim(), expression);
        }

        // not exposed through web module
        public void SeCmdStoreAttribute(string locator, string variableName)
        {
            string attributeName;
            var loc = ResolveAttributeLocator(locator, out attributeName);

            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    var attribute = this.FindElement(loc).GetAttribute(attributeName);
                    variables.Add(variableName.Trim(), attribute);
                    return;
                }
                catch (StaleElementReferenceException) { }
            }
        }

        // not exposed through web module
        public void SeCmdStoreText(string locator, string variableName)
        {
            var loc = ResolveLocator(locator);

            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    var text = this.FindElement(loc).Text;
                    variables.Add(variableName.Trim(), text);
                    return;
                }
                catch (StaleElementReferenceException) { }
            }
        }

        // not exposed through web module
        public void SeCmdStoreValue(string locator, string variableName)
        {
            var loc = ResolveLocator(locator);

            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    var el = this.FindElement(loc);

                    var type = el.GetAttribute("type");
                    if (type == null)
                        throw new OxElementHasNoValueException(locator);
                    
                    type = type.Trim().ToLower();

                    if (type == "radio" || type == "checkbox")
                    {
                        variables.Add(variableName.Trim(), el.Selected ? "on" : "off");
                    }
                    else
                    {
                        var elValue = el.GetAttribute("value");
                        if (elValue == null)
                            throw new OxElementHasNoValueException(locator);

                        variables.Add(variableName.Trim(), elValue);
                    }

                    return;
                }
                catch (StaleElementReferenceException) { }
            }
        }

        public string SeCmdGetAttribute(string locator, string attributeName)
        {
            var loc = ResolveLocator(locator);
            string attr = null;
            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    new WebDriverWait(this, TimeSpan.FromMilliseconds(waitForTimeout)).Until((d) =>
                    {
                        try
                        {
                            var el = this.FindElement(loc);
                            attr = el.GetAttribute(attributeName);
                            return true;
                        }
                        catch (NoSuchElementException)
                        {
                        }
                        return false;
                    });
                }
                catch (StaleElementReferenceException) 
                { 
                }
                catch (WebDriverTimeoutException)
                {
                    throw new OxElementNotFoundException();
                }
            }
            return attr;
        }

        public string SeCmdGetText(string locator)
        {
            var loc = ResolveLocator(locator);
            string text = null;
            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    new WebDriverWait(this, TimeSpan.FromMilliseconds(waitForTimeout)).Until((d) =>
                    {
                        try
                        {
                            var el = this.FindElement(loc);
                            text = el.Text;
                            return true;
                        }
                        catch (NoSuchElementException)
                        {
                        }
                        return false;
                    });
                }
                catch (StaleElementReferenceException)
                {
                }
                catch (WebDriverTimeoutException)
                {
                    throw new OxElementNotFoundException();
                }
            }
            return text;
        }

        public string SeCmdGetValue(string locator)
        {
            var loc = ResolveLocator(locator);
            string val = null;
            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    new WebDriverWait(this, TimeSpan.FromMilliseconds(waitForTimeout)).Until((d) =>
                    {
                        try
                        {
                            var el = this.FindElement(loc);

                            var type = el.GetAttribute("type");
                            if (type == null)
                                throw new OxElementHasNoValueException(locator);

                            type = type.Trim().ToLower();

                            if (type == "radio" || type == "checkbox")
                            {
                                val = el.Selected ? "on" : "off";
                            }
                            else
                            {
                                var elValue = el.GetAttribute("value");
                                if (elValue == null)
                                    throw new OxElementHasNoValueException(locator);
                                val = elValue;
                            }
                            return true;
                        }
                        catch (NoSuchElementException)
                        {
                        }
                        return false;
                    });
                }
                catch (StaleElementReferenceException)
                {
                }
                catch (WebDriverTimeoutException)
                {
                    throw new OxElementNotFoundException();
                }
            }
            return val;
        }

        public string SeCmdGetPageSource()
        {
            return this.PageSource;
        }

        public string SeCmdGetXMLPageSource()
        {
            switch (this.Capabilities.BrowserName)
            {
                case "chrome":
                    try
                    {
                        return this.ExecuteScript("return document.getElementById(\"webkit-xml-viewer-source-xml\").innerHTML;") as string;
                    }
                    catch (Exception)
                    {
                        throw new OxXMLExtractException("Unable to extract XML from: " + this.PageSource);
                    }
                case "internet explorer":
                    // TODO: optimize
                    var src = Regex.Replace(this.PageSource, @"<a\s*.*?>&lt;.*?<\/a>", "", RegexOptions.Multiline);
                    src = Regex.Replace(src, @"<style\s*.*?>.*?<\/style>", "", RegexOptions.Multiline);
                    src = Regex.Replace(src, @"<div\s*.*?>.*?<\/div>", "", RegexOptions.Multiline);
                    return Regex.Replace(src, @"<span\s*.*?>.*?<span\s*.*?>.*?<\/span>.*?<\/span>", "", RegexOptions.Multiline);
                case "firefox":
                default:
                    throw new OxXMLExtractException("This command is not supported on " + this.Capabilities.BrowserName + " yet.");
            }
        }

        public string SeCmdGetXMLPageSourceAsJSON()
        {
            var src = SeCmdGetXMLPageSource();
            try 
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(src);
                return JsonConvert.SerializeXmlNode(doc);
            }
            catch (Exception)
            {
                throw new OxXMLtoJSONConvertException("Unable to convert XML to JSON: " + this.PageSource);
            }
        }

        public int SeCmdGetElementCount(string xpath)
        {
            int count = 0;
            for (int i = 0; i < STALE_ELEMENT_ATTEMPTS; i++)
            {
                try
                {
                    new WebDriverWait(this, TimeSpan.FromMilliseconds(waitForTimeout)).Until((d) =>
                    {
                        var els = this.FindElementsByXPath(xpath);
                        if (els == null || els.Count == 0)
                            return false;
                        count = els.Count;
                        return true;
                    });
                }
                catch (StaleElementReferenceException)
                {
                }
                catch (WebDriverTimeoutException)
                {
                    return 0;
                }
            }
            return count;
        }

        public string SeCmdGetWindowHandles()
        {
            var originalWin = this.CurrentWindowHandle;

            IList<object> windows = new List<object>();
            foreach (var handle in this.WindowHandles)
            {
                this.SwitchTo().Window(handle);
                windows.Add(new
                {
                    handle = handle,
                    title = this.Title,
                    url = this.Url
                });
            }
            this.SwitchTo().Window(originalWin);

            return JsonConvert.SerializeObject(windows);
        }

        public string SeCmdGetAlertText()
        {
            return base.SwitchTo().Alert().Text;
        }
    }
}
