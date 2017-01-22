/**
     * @summary Checks if the current context is of WebView type.
     * @function isWebViewContext
*/
module.exports = function() {
	var context = this._driver.context().value;
	return (context && (context.indexOf('WEBVIEW') > -1 || context.indexOf("CHROMIUM") > -1));
};
