/*
 * Copyright (C) 2015-2017 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */
 
/**
 * @summary Retrieves the count of elements matching the given locator.
 * @function getElementCount
 * @param {String} locator - Element locator.
 * @return {Integer} Element count or 0 if no elements were found.
 */
module.exports = function(locator) {
    var wdloc = this.helpers.getWdioLocator(locator); 
    return this.driver.elements(wdloc).value.length;
};
