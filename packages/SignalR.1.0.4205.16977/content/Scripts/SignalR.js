/// <reference path="jquery-1.6.2.js" />
(function ($, window) {
    /// <param name="$" type="jQuery" />
    "use strict";
    if (typeof (window.SignalR) === "function") {
        return;
    }

    function getTransport(connection) {
        /// <param name="instance" type="signalR">The signalR connection</param>
        return $.type(connection.method) === "object" ? connection.method : signalR.transports[connection.method];
    }

    var signalR = function (url) {
        /// <summary>Creates a new SignalR connection for the given url</summary>
        /// <param name="url" type="String">The URL of the long polling endpoint</param>
        /// <returns type="SignalR" />

        return new signalR.fn.init(url);
    };

    signalR.fn = signalR.prototype = {
        init: function (url) {
            this.url = url;
        },

        start: function (options, callback) {
            /// <summary>Starts the connection</summary>
            /// <param name="options" type="Object">Options map</param>
            /// <param name="callback" type="Function">A callback function to execute when the connection has started</param>
            /// <returns type="SignalR" />
            var that = this,
                config = {
                    transport: "auto"
                };

            if (that.method) {
                return that;
            }

            if ($.type(options) === "function") {
                // Support calling with single callback parameter
                callback = options;
            } else if ($.type(options) === "object") {
                $.extend(config, options);
                if ($.type(config.callback) === "function") {
                    callback = config.callback;
                }
            }

            if ($.type(callback) === "function") {
                $(that).bind("onStart", function (e, data) {
                    callback.call(that);
                });
            }

            var initialize = function (transports, index) {
                index = index || 0;
                if (index >= transports.length) {
                    if (!that.method) {
                        // No transport initialized successfully
                        throw "SignalR: No transport could be initialized successfully. Try specifying a different transport or none at all for auto initialization.";
                    }
                    return;
                }

                var method = transports[index];
                var transport = $.type(method) === "object" ? method : signalR.transports[method];

                transport.start(that, function () {
                    that.method = method;
                    $(that).trigger("onStart");
                },
                function () {
                    initialize(transports, index + 1);
                });
            };

            window.setTimeout(function () {
                $.post(that.url + '/negotiate', {}, function (res) {
                    that.appRelativeUrl = res.Url;
                    that.clientId = res.ClientId;

                    $(that).trigger("onStarting");

                    var transports = [],
                        supportedTransports = [];

                    $.each(signalR.transports, function (key) {
                        supportedTransports.push(key);
                    });

                    if ($.isArray(config.transport)) {
                        // ordered list provided
                        $.each(config.transport, function () {
                            var t = this;
                            if ($.type(t) === "object" || ($.type(t) === "string" && $.inArray("" + t, supportedTransports) >= 0)) {
                                transports.push($.type(t) === "string" ? "" + t : t);
                            }
                        });
                    } else if ($.type(config.transport) === "object" ||
                               $.inArray(config.transport, supportedTransports) >= 0) {
                        // specific transport provided, as object or a named transport, e.g. "longPolling"
                        transports.push(config.transport);
                    }
                    else { // default "auto"
                        transports = supportedTransports;
                    }

                    initialize(transports);
                });
            }, 0);

            return that;
        },

        starting: function (callback) {
            /// <summary>Adds a callback that will be invoked before the connection is started</summary>
            /// <param name="callback" type="Function">A callback function to execute when the connection is starting</param>
            /// <returns type="SignalR" />
            var that = this;

            $(that).bind("onStarting", function (e, data) {
                callback.call(that);
            });

            return that;
        },

        send: function (data) {
            /// <summary>Sends data over the connection</summary>
            /// <param name="data" type="String">The data to send over the connection</param>
            /// <returns type="SignalR" />
            var that = this;

            var transport = getTransport(that);
            if (!transport) {
                // Connection hasn't been started yet
                throw "SignalR: Connection must be started before data can be sent. Call .start() before .send()";
            }

            transport.send(that, data);

            return that;
        },

        sending: function (callback) {
            /// <summary>Adds a callback that will be invoked before anything is sent over the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute before each time data is sent on the connection</param>
            /// <returns type="SignalR" />
            var that = this;
            $(that).bind("onSending", function (e, data) {
                callback.call(that);
            });
            return that;
        },

        received: function (callback) {
            /// <summary>Adds a callback that will be invoked after anything is received over the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute when any data is received on the connection</param>
            /// <returns type="SignalR" />
            var that = this;
            $(that).bind("onReceived", function (e, data) {
                callback.call(that, data);
            });
            return that;
        },

        error: function (callback) {
            /// <summary>Adds a callback that will be invoked after an error occurs with the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute when an error occurs on the connection</param>
            /// <returns type="SignalR" />
            var that = this;
            $(that).bind("onError", function (e, data) {
                callback.call(that);
            });
            return that;
        },
        stop: function () {
            /// <summary>Stops listening</summary>
            /// <returns type="SignalR" />
            var that = this,
                transport = getTransport(that);
            transport.stop(that);
            that.method = null;
            return that;
        }
    };

    signalR.fn.init.prototype = signalR.fn;

    // Transports
    signalR.transports = {

        webSockets: {
            send: function (connection, data) {
                connection.socket.send(data);
            },
            start: function (connection, onSuccess, onFailed) {
                if (typeof window.WebSocket !== "function") {
                    onFailed();
                    return;
                }

                if (!connection.socket) {
                    // Build the url
                    var url = document.location.host + connection.appRelativeUrl;

                    $(connection).trigger("onSending");
                    if (connection.data) {
                        url += "?data=" + connection.data + "&clientId=" + connection.clientId;
                    }
                    else {
                        url += "?clientId=" + connection.clientId;
                    }

                    connection.socket = new WebSocket("ws://" + url);
                    var opened = false;
                    connection.socket.onopen = function () {
                        opened = true;
                        if (onSuccess) {
                            onSuccess();
                        }
                    };

                    connection.socket.onclose = function (event) {
                        if (!opened) {
                            if (onFailed) {
                                onFailed();
                            }
                        }
                    };

                    connection.socket.onmessage = function (event) {
                        var data = window.JSON.parse(event.data);
                        if (data) {
                            if (data.Messages) {
                                $.each(data.Messages, function () {
                                    $(connection).trigger("onReceived", [this]);
                                });
                            }
                            else {
                                $(connection).trigger("onReceived", [data]);
                            }
                        }
                    };
                }
            },
            stop: function (connection) {
                if (connection.socket != null) {
                    connection.socket.close();
                    connection.socket = null;
                }
            }
        },

        longPolling: {
            start: function (connection, onSuccess, onFailed) {
                /// <summary>Starts the long polling connection</summary>
                /// <param name="connection" type="SignalR">The SignalR connection to start</param>
                if (connection.pollXhr) {
                    connection.stop();
                }

                // Always supported
                onSuccess();

                connection.messageId = null;

                window.setTimeout(function () {
                    (function poll(instance) {
                        $(instance).trigger("onSending");

                        var messageId = instance.messageId,
                            connect = (messageId === null),
                            url = instance.url + (connect ? "/connect" : "");

                        instance.pollXhr = $.ajax(url, {
                            type: "POST",
                            data: {
                                clientId: instance.clientId,
                                messageId: messageId,
                                data: instance.data,
                                transport: "longPolling"
                            },
                            dataType: "json",
                            success: function (data) {
                                if (data) {
                                    if (data.Messages) {
                                        $.each(data.Messages, function () {
                                            $(instance).trigger("onReceived", [this]);
                                        });
                                    }
                                    instance.messageId = data.MessageId || null;
                                }
                                poll(instance);
                            },
                            error: function (data, textStatus) {
                                if (textStatus === "abort") {
                                    return;
                                }

                                $(instance).trigger("onError", [data]);

                                window.setTimeout(function () {
                                    poll(instance);
                                }, 2 * 1000);
                            }
                        });
                    })(connection);
                }, 250); // Have to delay initial poll so Chrome doesn't show loader spinner in tab
            },

            send: function (connection, data) {
                /// <summary>Sends data over this connection</summary>
                /// <param name="connection" type="SignalR">The SignalR connection to send data over</param>
                /// <param name="data" type="String">The data to send</param>
                /// <param name="callback" type="Function">A callback to be invoked when the send has completed</param>
                $.ajax(connection.url + '/send', {
                    type: "POST",
                    dataType: "json",
                    data: { 
                        data: data,
                        transport: "longPolling",
                        clientId: connection.clientId
                    },
                    success: function (result) {
                        if (result) {
                            $(connection).trigger("onReceived", [result]);
                        }
                    },
                    error: function (data, textStatus) {
                        if (textStatus === "abort") {
                            return;
                        }
                        $(connection).trigger("onError", [data]);
                    }
                });
            },

            stop: function (connection) {
                /// <summary>Stops the long polling connection</summary>
                /// <param name="connection" type="SignalR">The SignalR connection to stop</param>
                if (connection.pollXhr) {
                    connection.pollXhr.abort();
                    connection.pollXhr = null;
                }
            }
        }
    };

    window.signalR = signalR;

})(window.jQuery, window);