/*
 * Copyright (C) 2015-2017 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

/**
 * @summary Checks if the current context is of WebView type.
 * @function isWebViewContext
 * @return {Boolean} - true if the context name is WEBVIEW or CHROMIUM.
 * @for android, ios, hybrid, web
 */
module.exports = function() {
    var context = this.driver.context().value;
    return (context && (context.indexOf('WEBVIEW') > -1 || context.indexOf('CHROMIUM') > -1));
};
