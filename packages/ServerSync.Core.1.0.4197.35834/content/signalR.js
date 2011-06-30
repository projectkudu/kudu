/// <reference path="jquery-1.6.1.js" />
(function ($, window) {
    /// <param name="$" type="jQuery" />
    "use strict";
    
    if (typeof (window.signalR) === "function") {
        return;
    }

    var signalR = function (url) {
        /// <summary>Creates a new signalR connection for the given url</summary>
        /// <param name="url" type="String">The URL of the long polling endpoint</param>
        /// <returns type="signalR" />

        return new signalR.fn.init(url);
    };

    signalR.fn = signalR.prototype = {
        init: function (url) {
            this.url = url;
        },
        
        start: function (callback) {
            /// <summary>Starts the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute when the connection has started</param>
            /// <returns type="signalR" />
            var that = this;

            if (that.method) {
                return that;
            }

            if (typeof (callback) === "function") {
                $(that).bind("onStart", function (e, data) {
                    callback.call(that);
                });
            }
            
            var initialize = function (transports, index) {
                index = index || 0;
                if (index >= transports.length) {
                    return;
                }

                var method = transports[index];
                var transport = signalR.transports[method];

                transport.start(that, function () {
                    that.method = method;
                }, 
                function () {
                    initialize(transports, index + 1);
                });                
            };

            $.post(this.url + '/negotiate', {}, function (res) {                
                that.appRelativeUrl = res.url;

                $(that).trigger("onStarting");
                var transports = [];
                $.each(signalR.transports, function (key) {
                    transports.push(key);    
                });

                initialize(transports);
            });

            return that;
        },

        starting: function (callback) {
            /// <summary>Adds a callback that will be invoked before the connection is started</summary>
            /// <param name="callback" type="Function">A callback function to execute when the connection is starting</param>
            /// <returns type="signalR" />
            var that = this;

            $(that).bind("onStarting", function (e, data) {
                callback.call(that);
            });

            return that;
        },

        send: function (data) {
            /// <summary>Sends data over the connection</summary>
            /// <param name="data" type="String">The data to send over the connection</param>
            /// <returns type="signalR" />
            var that = this;

            var transport = signalR.transports[that.method];
            if (!transport) {
                // Connection hasn't been started yet
                throw "signalR: Connection must be started before data can be sent. Call .start() before .send()";
            }

            var args = $.makeArray([that]).concat($.makeArray(arguments));
            transport.send.apply(transport, args);

            return that;
        },

        sending: function (callback) {
            /// <summary>Adds a callback that will be invoked before anything is sent over the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute before each time data is sent on the connection</param>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onSending", function (e, data) {
                callback.call(that);
            });
            return that;
        },

        received: function (callback) {
            /// <summary>Adds a callback that will be invoked after anything is received over the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute when any data is received on the connection</param>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onReceived", function (e, data) {
                callback.call(that, data);
            });
            return that;
        },

        error: function (callback) {
            /// <summary>Adds a callback that will be invoked after an error occurs with the connection</summary>
            /// <param name="callback" type="Function">A callback function to execute when an error occurs on the connection</param>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onError", function (e, data) {
                callback.call(that);
            });
            return that;
        },
        stop: function () {
            /// <summary>Stops listening</summary>
            /// <returns type="signalR" />
            signalR.transports[this.method].stop(this);
            this.method = null;
            return this;
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
                        url += "?data=" + connection.data;
                    }

                    connection.socket = new WebSocket("ws://" + url);
                    var opened = false;
                    connection.socket.onopen = function () {
                        opened = true;
                        if (onSuccess) { 
                            onSuccess();
                        }
                        $(connection).trigger("onStart");
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
                        if (data.Messages) {
                            $.each(data.Messages, function () {
                                $(connection).trigger("onReceived", [this]);
                            });
                        }
                        else {
                            $(connection).trigger("onReceived", [data]);
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
                /// <param name="connection" type="signalR">The signalR connection to start</param>
                if (connection.pollXhr) {
                    connection.stop();
                }
                
                // Always supported
                onSuccess();
                $(connection).trigger("onStart");

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
                                    else {
                                        $(instance).trigger("onReceived", [data]);
                                    }
                                    instance.messageId = data.MessageId;
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
                }, 0); // Have to delay initial poll so Chrome doesn't show loader spinner in tab
            },

            send: function (connection, data, callback) {
                /// <summary>Sends data over this connection</summary>
                /// <param name="connection" type="signalR">The signalR connection to send data over</param>
                /// <param name="data" type="String">The data to send</param>
                /// <param name="callback" type="Function">A callback to be invoked when the send has completed</param>
                $.ajax(connection.url + '/send', {
                    type: "POST",
                    data: { data: data, transport: "longPolling" },
                    dataType: "json",
                    success: callback,
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
                /// <param name="connection" type="signalR">The signalR connection to stop</param>
                if (connection.pollXhr) {
                    connection.pollXhr.abort();
                    connection.pollXhr = null;
                }
            }
        }
    };

    window.signalR = signalR;

})(window.jQuery, window);