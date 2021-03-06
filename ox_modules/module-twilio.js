/*
 * Copyright (C) 2015-2018 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

/**
 * Provides methods for working with Twilio service.
 */

const OxError = require('../errors/OxygenError');
var errHelper = require('../errors/helper');

module.exports = function(argv, context, rs) {
    var deasync = require('deasync');

    var _client;

    module.init = function(accountSid, authToken) {
        _client = require('twilio')(accountSid, authToken);
    };

    /**
     * @summary Retrieves last SMS message.
     * @function getLastSms
     * @param {Boolean} removeOnRead - Specifies whether to delete the message after reading it.
     * @param {String} timeout - Timeout for waiting for the message to arrive.
     * @return {String} SMS text.
     */
    module.getLastSms = function(removeOnRead, timeout) {
        var msg;
        var now = (new Date()).getTime();
        
        while (!msg && ((new Date()).getTime() - now) < timeout) {
            _client.messages.each(messages => {
                if (messages.direction == 'inbound') {
                    if (msg && Date.parse(msg.dateCreated) < Date.parse(messages.dateCreated)) {
                        msg = messages;
                    } else if (!msg) {
                        msg = messages;
                    }
                }
            });
            deasync.loopWhile(() => !msg && ((new Date()).getTime() - now) < timeout);
            deasync.sleep(500);
        }

        if (!msg) {
            throw new OxError(errHelper.errorCode.TIMEOUT, "Couldn't get the SMS within " + timeout + 'ms.');
        }

        var removed;
        if (removeOnRead) {
            _client.messages(msg.sid).remove().then(() => { removed = true; });
            deasync.loopWhile(() => !removed);
        }

        return msg.body;
    };

    return module;
};
