/*
 * Copyright (C) 2015-2017 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
 
/**
 * @summary Double clicks on an element.
 * @function doubleClick
 * @param {String} locator - An element locator.
 */
module.exports = function(locator) {
    var wdloc = this.helpers.getWdioLocator(locator); 
    this.waitForVisible(locator);
    return this.driver.doubleClick(wdloc);
};
