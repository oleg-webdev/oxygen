/*
 * Copyright (C) 2015-2017 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

/**
 * @summary Perform tap at the specified coordinate.
 * @function tap
 * @param {Integer} x - x offset.
 * @param {Integer} y - y offset.
 * @for android, ios
 */
module.exports = function(x, y) {
    this.helpers._assertArgumentNumberNonNegative(x);
    this.helpers._assertArgumentNumberNonNegative(y);

    this.driver.touchPerform([{
        action: 'tap',
        options: {
            x: x,
            y: y
        }
    }]);
};
