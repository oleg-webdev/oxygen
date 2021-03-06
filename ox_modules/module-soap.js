/*
 * Copyright (C) 2015-2017 CloudBeat Limited
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

/**
 * Provides methods for working with SOAP based Web Services.
 */

const OxError = require('../errors/OxygenError');
var errHelper = require('../errors/helper');

module.exports = function(argv, context, rs, logger) {
    var deasync = require('deasync');
    var soap = require('soap');

    /**
     * @summary Initiates a SOAP request and returns the response.
     * @function get
     * @param {String} wsdlUrl - URL pointing to the WSDL XML.
     * @param {String} method - Method name (case sensitive).
     * @param {Object=} args - Arguments.
     * @return {Object} The response object.
     */
    module.get = function(wsdlUrl, method, args) {
        var resultClient = null;
        var result = null;

        soap.createClient(wsdlUrl, (err, client) => {
            if (client === undefined) {
                var msg =  'Error creating client';
                if (err.message) {
                    var soapMsg = err.message;
                    var firstBreakIndex = soapMsg.indexOf('\n');
                    if (firstBreakIndex > 0) {
                        soapMsg = err.message.substring(0, firstBreakIndex);
                    }
                    msg += ': ' + soapMsg;
                }
                resultClient = new OxError(errHelper.errorCode.SOAP, msg);
                return;
            }

            resultClient = client;

            if (typeof client[method] !== 'function') { 
                resultClient = new OxError(errHelper.errorCode.SOAP, 'No method named ' + method + ' was found.');
                return;
            }

            client[method](args, (err, res) => { 
                if (err !== null) {
                    result = new OxError(errHelper.errorCode.SOAP, err.root.Envelope.Body.Fault.faultstring);
                    return;
                }

                result = res;
            });
        });

        deasync.loopWhile(() => !resultClient);
        if (resultClient.type === errHelper.errorCode.SOAP) {
            throw resultClient;
        } 

        deasync.loopWhile(() => !result);
        if (result.type === errHelper.errorCode.SOAP) {
            throw result;
        }

        return result;
    };

    /**
     * @summary Returns SOAP service description.
     * @function describe
     * @param {String} wsdlUrl - URL pointing to the WSDL XML.
     * @return {String} JSON containing the service description.
     */
    module.describe = function(wsdlUrl) {
        var resultClient = null;

        soap.createClient(wsdlUrl, (err, client) => {
            if (client === undefined) {
                var msg =  'Error creating client';
                if (err.message) {
                    var soapMsg = err.message;
                    var firstBreakIndex = soapMsg.indexOf('\n');
                    if (firstBreakIndex > 0) {
                        soapMsg = err.message.substring(0, firstBreakIndex);
                    }
                    msg += ': ' + soapMsg;
                }
                resultClient = new OxError(errHelper.errorCode.SOAP, msg);
                return;
            }

            resultClient = JSON.stringify(client.describe());
        });

        deasync.loopWhile(() => !resultClient);
        if (resultClient.type === errHelper.errorCode.SOAP) {
            throw resultClient;
        } 

        return resultClient;
    };

    return module;
};
